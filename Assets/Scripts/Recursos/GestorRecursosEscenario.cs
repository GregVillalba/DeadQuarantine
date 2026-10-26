using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Recursos del mapa (US 19.1). Va al lado de GestorEscenariosPartida y trabaja solo en el servidor:
/// cuando se activa un escenario elige al azar una de sus opciones de puntos y hace aparecer un recurso en cada
/// punto. Cuando alguien junta uno, ese punto vuelve a tener su recurso segundosReaparicion después (cada punto
/// cuenta su tiempo, también en la fase de compras). Al cambiar de escenario (Historia) saca los del anterior.
/// Funciona en los 3 modos: el escenario activo lo decide GestorEscenariosPartida.
/// </summary>
public class GestorRecursosEscenario : MonoBehaviour
{
    [Tooltip("Prefab del recurso del mapa (RecursoEscenario). Tiene que estar en DefaultNetworkPrefabs.")]
    [SerializeField] private RecursoEscenario prefabRecurso;

    [Tooltip("Segundos que tarda un punto en volver a tener su recurso después de que lo junten.")]
    [SerializeField] private float segundosReaparicion = 240f;

    /// <summary>Un punto de la opción elegida: su recurso en el mapa o la cuenta para que vuelva.</summary>
    public class PuntoEnUso
    {
        public readonly PuntoRecursoEscenario punto;
        public RecursoEscenario recurso;

        public bool Esperando { get; private set; }
        public float MomentoReaparicion { get; private set; }

        public PuntoEnUso(PuntoRecursoEscenario punto)
        {
            this.punto = punto;
        }

        /// <summary>El recurso se juntó: vuelve a aparecer en segundos.</summary>
        public void EmpezarEspera(float ahora, float segundos)
        {
            recurso = null;
            Esperando = true;
            MomentoReaparicion = ahora + segundos;
        }

        public bool TerminoLaEspera(float ahora)
        {
            return Esperando && ahora >= MomentoReaparicion;
        }

        public void Reaparecio(RecursoEscenario nuevo)
        {
            recurso = nuevo;
            Esperando = false;
        }
    }

    private readonly List<PuntoEnUso> puntosEnUso = new List<PuntoEnUso>();
    private ConfiguracionEscenarioJugable escenarioConRecursos;

    public IReadOnlyList<PuntoEnUso> PuntosEnUso => puntosEnUso;

    /// <summary>Opción usada en el escenario activo (0, 1 o 2). -1 si no hay.</summary>
    public int OpcionElegida { get; private set; } = -1;

    private void Update()
    {
        NetworkManager red = NetworkManager.Singleton;

        if (red == null || !red.IsServer || !red.IsListening)
        {
            // Sin red los recursos ya no existen: solo se olvidan.
            puntosEnUso.Clear();
            escenarioConRecursos = null;
            OpcionElegida = -1;
            return;
        }

        ConfiguracionEscenarioJugable escenario = GestorEscenariosPartida.Instance != null
            ? GestorEscenariosPartida.Instance.ObtenerEscenarioActivo()
            : null;

        if (escenario != escenarioConRecursos)
        {
            QuitarRecursos();

            if (escenario != null)
                ColocarRecursos(escenario);
        }

        ReponerRecursos(Time.time);
    }

    /// <summary>Índice de la opción según una tirada entre 0 y 1 (Random.value). -1 si no hay opciones.</summary>
    public static int ElegirOpcion(int cantidadOpciones, float tirada)
    {
        if (cantidadOpciones <= 0)
            return -1;

        return Mathf.Clamp(Mathf.FloorToInt(tirada * cantidadOpciones), 0, cantidadOpciones - 1);
    }

    private void ColocarRecursos(ConfiguracionEscenarioJugable escenario)
    {
        // Se marca aunque no tenga opciones, para no volver a intentarlo cada frame.
        escenarioConRecursos = escenario;

        OpcionRecursosEscenario[] opciones = escenario.OpcionesRecursos;
        OpcionElegida = ElegirOpcion(opciones != null ? opciones.Length : 0, Random.value);

        if (OpcionElegida < 0 || opciones[OpcionElegida] == null || prefabRecurso == null)
        {
            Debug.LogWarning("[Recursos] " + escenario.TipoEscenario + " no tiene opciones de recursos o falta el prefab.", this);
            OpcionElegida = -1;
            return;
        }

        OpcionRecursosEscenario opcion = opciones[OpcionElegida];

        if (!opcion.EstaCompleta)
            Debug.LogWarning("[Recursos] " + escenario.TipoEscenario + ", " + opcion.name + " incompleta: " + opcion.Resumen() + ".", opcion);

        foreach (PuntoRecursoEscenario punto in opcion.Puntos)
        {
            PuntoEnUso enUso = new PuntoEnUso(punto);
            enUso.Reaparecio(Aparecer(punto));
            puntosEnUso.Add(enUso);
        }

        Debug.Log("[Recursos] " + escenario.TipoEscenario + ": opción " + (OpcionElegida + 1) + " (" + opcion.name + ").");
    }

    // Un recurso que ya no está spawneado lo juntó un jugador (RecursoEscenario.IntentarRecolectar lo despawnea).
    private void ReponerRecursos(float ahora)
    {
        foreach (PuntoEnUso enUso in puntosEnUso)
        {
            if (enUso.Esperando)
            {
                if (enUso.TerminoLaEspera(ahora))
                    enUso.Reaparecio(Aparecer(enUso.punto));

                continue;
            }

            if (enUso.recurso == null || !enUso.recurso.IsSpawned)
                enUso.EmpezarEspera(ahora, segundosReaparicion);
        }
    }

    private RecursoEscenario Aparecer(PuntoRecursoEscenario punto)
    {
        RecursoEscenario recurso = Instantiate(prefabRecurso, punto.transform.position, punto.transform.rotation);
        recurso.Configurar(punto.Tipo);
        recurso.NetworkObject.Spawn(true);
        return recurso;
    }

    private void QuitarRecursos()
    {
        foreach (PuntoEnUso enUso in puntosEnUso)
        {
            if (enUso.recurso != null && enUso.recurso.IsSpawned)
                enUso.recurso.NetworkObject.Despawn(true);
        }

        puntosEnUso.Clear();
        escenarioConRecursos = null;
        OpcionElegida = -1;
    }
}
