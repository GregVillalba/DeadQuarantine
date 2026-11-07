using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Recursos del mapa del tutorial. En las partidas los pone GestorRecursosEscenario según el escenario activo;
/// el tutorial no tiene escenarios, así que este componente hace aparecer un recurso en cada PuntoRecursoEscenario
/// hijo (activo) y lo repone segundosReaparicion después de que lo junten, para poder practicar sin trabarse.
/// Trabaja solo en el servidor.
/// </summary>
public class TutorialRecursosMapa : MonoBehaviour
{
    [Tooltip("Prefab del recurso del mapa (RecursoEscenario). Tiene que estar en DefaultNetworkPrefabs.")]
    [SerializeField] private RecursoEscenario prefabRecurso;

    [Tooltip("Segundos que tarda un punto en volver a tener su recurso después de que lo junten.")]
    [SerializeField, Min(0f)] private float segundosReaparicion = 15f;

    [Tooltip("Desmarcado = los recursos no aparecen hasta que alguien llame a Activar (ej: TutorialSecciones al " +
             "empezar la sección de recursos).")]
    [SerializeField] private bool activoAlIniciar = true;

    private readonly List<GestorRecursosEscenario.PuntoEnUso> puntosEnUso = new List<GestorRecursosEscenario.PuntoEnUso>();
    private bool colocados;
    private bool activo;

    public IReadOnlyList<GestorRecursosEscenario.PuntoEnUso> PuntosEnUso => puntosEnUso;
    public float SegundosReaparicion => segundosReaparicion;
    public bool Activo => activo;

    private readonly HashSet<PuntoRecursoEscenario> puntosJuntados = new HashSet<PuntoRecursoEscenario>();

    /// <summary>Cuántos recursos del mapa se juntaron desde que se activó (solo servidor).</summary>
    public int Recolectados { get; private set; }

    /// <summary>De cuántos puntos distintos se juntó al menos una vez el recurso desde que se activó (solo servidor).</summary>
    public int PuntosJuntados => puntosJuntados.Count;

    /// <summary>Cantidad de puntos (activos) donde aparecen recursos.</summary>
    public int CantidadPuntos => GetComponentsInChildren<PuntoRecursoEscenario>().Length;

    private void Awake()
    {
        activo = activoAlIniciar;
    }

    /// <summary>Hace aparecer los recursos (si todavía no estaban) y empieza a contar los que se juntan.</summary>
    public void Activar()
    {
        if (activo)
            return;

        activo = true;
        Recolectados = 0;
        puntosJuntados.Clear();
    }

    private void Update()
    {
        NetworkManager red = NetworkManager.Singleton;

        if (red == null || !red.IsServer || !red.IsListening)
        {
            // Sin red los recursos ya no existen: solo se olvidan.
            puntosEnUso.Clear();
            colocados = false;
            return;
        }

        if (!activo)
            return;

        if (!colocados)
            ColocarRecursos();

        ReponerRecursos(Time.time);
    }

    private void ColocarRecursos()
    {
        // Se marca aunque falle, para no volver a intentarlo cada frame.
        colocados = true;

        if (prefabRecurso == null)
        {
            Debug.LogWarning("[TutorialRecursosMapa] Falta el prefab del recurso.", this);
            return;
        }

        foreach (PuntoRecursoEscenario punto in GetComponentsInChildren<PuntoRecursoEscenario>())
        {
            GestorRecursosEscenario.PuntoEnUso enUso = new GestorRecursosEscenario.PuntoEnUso(punto);
            enUso.Reaparecio(Aparecer(punto));
            puntosEnUso.Add(enUso);
        }
    }

    // Un recurso que ya no está spawneado lo juntó un jugador (RecursoEscenario.IntentarRecolectar lo despawnea).
    private void ReponerRecursos(float ahora)
    {
        foreach (GestorRecursosEscenario.PuntoEnUso enUso in puntosEnUso)
        {
            if (enUso.Esperando)
            {
                if (enUso.TerminoLaEspera(ahora))
                    enUso.Reaparecio(Aparecer(enUso.punto));

                continue;
            }

            if (enUso.recurso == null || !enUso.recurso.IsSpawned)
            {
                enUso.EmpezarEspera(ahora, segundosReaparicion);
                Recolectados++;
                puntosJuntados.Add(enUso.punto);
            }
        }
    }

    private RecursoEscenario Aparecer(PuntoRecursoEscenario punto)
    {
        RecursoEscenario recurso = Instantiate(prefabRecurso, punto.transform.position, punto.transform.rotation);
        recurso.Configurar(punto.Tipo);
        recurso.NetworkObject.Spawn(true);
        return recurso;
    }
}
