
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class GestorEscenariosPartida : MonoBehaviour
{
    public static GestorEscenariosPartida Instance { get; private set; }

    [Header("Escenarios disponibles")]
    [SerializeField] private ConfiguracionEscenarioJugable escenarioLaboratorio;
    [SerializeField] private ConfiguracionEscenarioJugable escenarioCiudad;
    [SerializeField] private ConfiguracionEscenarioJugable escenarioCabana;

    [Header("Zonas seguras")]
    [SerializeField] private GameObject safeZone1;
    [SerializeField] private GameObject safeZone2;

    [Header("Puntos de aparición")]
    [SerializeField] private Transform spawnSafeZone1;
    [SerializeField] private Transform spawnSafeZone2;

    private ConfiguracionEscenarioJugable escenarioActivo;

    private class EstadoEscenario
    {
        public GameObject raiz;
        public Renderer[] renderers;
        public bool[] renderersActivos;
        public Collider[] colliders;
        public bool[] collidersActivos;
        public Light[] luces;
        public bool[] lucesActivas;
        public Behaviour[] behaviours;
        public bool[] behavioursActivos;
    }

    private readonly Dictionary<GameObject, EstadoEscenario> estados =
        new Dictionary<GameObject, EstadoEscenario>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "[Escenarios] Hay más de un GestorEscenariosPartida."
            );
            return;
        }

        Instance = this;

        PrepararEscenario(escenarioLaboratorio);
        PrepararEscenario(escenarioCiudad);
        PrepararEscenario(escenarioCabana);

        ConfigurarEscenarioInicial();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private GameObject ObtenerRaiz(
        ConfiguracionEscenarioJugable escenario
    )
    {
        if (escenario == null)
            return null;

        return escenario.transform.parent != null
            ? escenario.transform.parent.gameObject
            : escenario.gameObject;
    }

    private void PrepararEscenario(
        ConfiguracionEscenarioJugable escenario
    )
    {
        GameObject raiz = ObtenerRaiz(escenario);

        if (raiz == null || estados.ContainsKey(raiz))
            return;

        // La raíz debe estar activa desde que carga la escena para que
        // Netcode pueda registrar los NetworkObject colocados en ella.
        if (!raiz.activeSelf)
        {
            Debug.LogError(
                $"[Escenarios] El objeto raíz '{raiz.name}' está " +
                "desactivado en la jerarquía. Activálo en el Inspector " +
                "antes de iniciar la partida."
            );

            raiz.SetActive(true);
        }

        EstadoEscenario estado = new EstadoEscenario();
        estado.raiz = raiz;

        estado.renderers = raiz.GetComponentsInChildren<Renderer>(true);
        estado.renderersActivos = new bool[estado.renderers.Length];

        for (int i = 0; i < estado.renderers.Length; i++)
            estado.renderersActivos[i] = estado.renderers[i].enabled;

        estado.colliders = raiz.GetComponentsInChildren<Collider>(true);
        estado.collidersActivos = new bool[estado.colliders.Length];

        for (int i = 0; i < estado.colliders.Length; i++)
            estado.collidersActivos[i] = estado.colliders[i].enabled;

        estado.luces = raiz.GetComponentsInChildren<Light>(true);
        estado.lucesActivas = new bool[estado.luces.Length];

        for (int i = 0; i < estado.luces.Length; i++)
            estado.lucesActivas[i] = estado.luces[i].enabled;

        // Los NetworkBehaviour deben permanecer habilitados para que
        // los NetworkObject de escena puedan inicializarse.
        Behaviour[] todosLosBehaviours =
            raiz.GetComponentsInChildren<Behaviour>(true);

        List<Behaviour> behavioursNormales = new List<Behaviour>();

        foreach (Behaviour behaviour in todosLosBehaviours)
        {
            if (behaviour == null)
                continue;

            if (behaviour is NetworkBehaviour)
                continue;

            behavioursNormales.Add(behaviour);
        }

        estado.behaviours = behavioursNormales.ToArray();
        estado.behavioursActivos =
            new bool[estado.behaviours.Length];

        for (int i = 0; i < estado.behaviours.Length; i++)
            estado.behavioursActivos[i] = estado.behaviours[i].enabled;

        estados.Add(raiz, estado);

        OcultarEscenario(raiz);

        Debug.Log(
            $"[Escenarios] Escenario preparado para Netcode: {raiz.name}."
        );
    }

    private void OcultarEscenario(GameObject raiz)
    {
        if (raiz == null || !estados.TryGetValue(raiz, out EstadoEscenario estado))
            return;

        foreach (Renderer renderer in estado.renderers)
        {
            if (renderer != null)
                renderer.enabled = false;
        }

        foreach (Collider collider in estado.colliders)
        {
            if (collider != null)
                collider.enabled = false;
        }

        foreach (Light luz in estado.luces)
        {
            if (luz != null)
                luz.enabled = false;
        }

        foreach (Behaviour behaviour in estado.behaviours)
        {
            if (behaviour != null)
                behaviour.enabled = false;
        }

        // Importante: no desactivar la raíz.
        raiz.SetActive(true);
    }

    private void MostrarEscenario(GameObject raiz)
    {
        if (raiz == null || !estados.TryGetValue(raiz, out EstadoEscenario estado))
            return;

        raiz.SetActive(true);

        for (int i = 0; i < estado.renderers.Length; i++)
        {
            if (estado.renderers[i] != null)
                estado.renderers[i].enabled = estado.renderersActivos[i];
        }

        for (int i = 0; i < estado.colliders.Length; i++)
        {
            if (estado.colliders[i] != null)
                estado.colliders[i].enabled = estado.collidersActivos[i];
        }

        for (int i = 0; i < estado.luces.Length; i++)
        {
            if (estado.luces[i] != null)
                estado.luces[i].enabled = estado.lucesActivas[i];
        }

        for (int i = 0; i < estado.behaviours.Length; i++)
        {
            if (estado.behaviours[i] != null)
                estado.behaviours[i].enabled = estado.behavioursActivos[i];
        }

        int r = 0, c = 0;
        foreach (var x in estado.renderers) if (x != null && x.enabled) r++;
        foreach (var x in estado.colliders) if (x != null && x.enabled) c++;
        Debug.Log($"[Escenarios] Mostrar '{raiz.name}': renderers {r}/{estado.renderers.Length}, colliders {c}/{estado.colliders.Length}, activeInHierarchy={raiz.activeInHierarchy}");
    }

    private void ConfigurarEscenarioInicial()
    {
        if (ConfiguracionPartidaSeleccionada.Instancia == null)
        {
            Debug.LogError(
                "[Escenarios] No existe ConfiguracionPartidaSeleccionada."
            );
            return;
        }

        ActivarEscenario(
            ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado
        );
    }

    private void DesactivarTodasLasUbicaciones()
    {
        Debug.Log("[Escenarios] DesactivarTodasLasUbicaciones llamado.");
        OcultarEscenario(ObtenerRaiz(escenarioLaboratorio));
        OcultarEscenario(ObtenerRaiz(escenarioCiudad));
        OcultarEscenario(ObtenerRaiz(escenarioCabana));

        if (safeZone1 != null)
            safeZone1.SetActive(false);

        if (safeZone2 != null)
            safeZone2.SetActive(false);

        escenarioActivo = null;
    }

    public void ActivarEscenario(
        ConfiguracionPartidaSeleccionada.Escenario escenario
    )
    {
        DesactivarTodasLasUbicaciones();

        switch (escenario)
        {
            case ConfiguracionPartidaSeleccionada.Escenario.Laboratorio:
                escenarioActivo = escenarioLaboratorio;
                break;

            case ConfiguracionPartidaSeleccionada.Escenario.Ciudad:
                escenarioActivo = escenarioCiudad;
                break;

            case ConfiguracionPartidaSeleccionada.Escenario.Cabana:
                escenarioActivo = escenarioCabana;
                break;

            default:
                Debug.LogError(
                    $"[Escenarios] Escenario no válido: {escenario}."
                );
                return;
        }

        if (escenarioActivo == null)
        {
            Debug.LogError(
                $"[Escenarios] No asignaste el escenario {escenario}."
            );
            return;
        }

        MostrarEscenario(ObtenerRaiz(escenarioActivo));

        if (ConfiguracionPartidaSeleccionada.Instancia != null)
        {
            ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado =
                escenario;
        }

        Debug.Log($"[Escenarios] Escenario activo: {escenario}.");
    }

    public void ActivarSafeZone1()
    {
        DesactivarTodasLasUbicaciones();

        if (safeZone1 == null)
        {
            Debug.LogError(
                "[Escenarios] No asignaste SafeZone1 en el Inspector."
            );
            return;
        }

        safeZone1.SetActive(true);
        Debug.Log("[Escenarios] SafeZone1 activada.");
    }

    public void ActivarSafeZone2()
    {
        DesactivarTodasLasUbicaciones();

        if (safeZone2 == null)
        {
            Debug.LogError(
                "[Escenarios] No asignaste SafeZone2 en el Inspector."
            );
            return;
        }

        safeZone2.SetActive(true);
        Debug.Log("[Escenarios] SafeZone2 activada.");
    }

    public void MostrarSafeZone1()
    {
        if (safeZone1 == null)
        {
            Debug.LogError(
                "[Escenarios] No asignaste SafeZone1 en el Inspector."
            );
            return;
        }

        if (safeZone1.activeSelf)
            return;

        safeZone1.SetActive(true);
        Debug.Log("[Escenarios] SafeZone1 visible tras la puerta.");
    }

    public void EntrarSafeZone1(Transform jugador)
    {
        StoryIntroController intro =
            jugador.GetComponentInChildren<StoryIntroController>(true);

        if (intro != null)
            intro.MostrarAvisoContinuarSafeZone();
        else
            Debug.LogWarning(
                "[Escenarios] No se encontró StoryIntroController en el jugador."
            );
    }

    public void SalirSafeZone1(Transform jugador)
    {
        StoryIntroController intro =
            jugador.GetComponentInChildren<StoryIntroController>(true);

        if (intro != null)
            intro.OcultarAvisoContinuarSafeZone();
    }

    public bool EstaEnZonaSeguraActiva(Transform objeto)
    {
        if (objeto == null)
            return false;

        return EstaDentroDeZonaActiva(objeto, safeZone1)
            || EstaDentroDeZonaActiva(objeto, safeZone2);
    }

    private static bool EstaDentroDeZonaActiva(
        Transform objeto,
        GameObject zona
    )
    {
        return zona != null
            && zona.activeInHierarchy
            && objeto.IsChildOf(zona.transform);
    }

    public void TransicionarASafeZone1(Transform jugador)
    {
        if (safeZone1 == null || spawnSafeZone1 == null)
        {
            Debug.LogError(
                "[Escenarios] Asigná SafeZone1 y su punto de aparición."
            );
            return;
        }

        ActivarSafeZone1();
        TrasladarJugador(jugador, spawnSafeZone1);

        StoryIntroController intro =
            jugador.GetComponentInChildren<StoryIntroController>(true);

        if (intro != null)
            intro.MostrarAvisoContinuarSafeZone();
        else
            Debug.LogWarning(
                "[Escenarios] No se encontró StoryIntroController en el jugador."
            );
    }

    public void TransicionarASafeZone2(Transform jugador)
    {
        if (safeZone2 == null || spawnSafeZone2 == null)
        {
            Debug.LogError(
                "[Escenarios] Asigná SafeZone2 y su punto de aparición."
            );
            return;
        }

        ActivarSafeZone2();
        TrasladarJugador(jugador, spawnSafeZone2);

        StoryIntroController intro =
            jugador.GetComponentInChildren<StoryIntroController>(true);

        if (intro == null)
        {
            Debug.LogError(
                "[Escenarios] No se encontró StoryIntroController en el jugador."
            );
            return;
        }

        Debug.Log(
            "[Escenarios] Avisando al jugador que puede continuar a la Mansión."
        );

        intro.MostrarAvisoContinuarAMansion();
    }

   
public void TransicionarAScenarioCabana(Transform jugador)
{
    if (escenarioCabana == null)
    {
        Debug.LogError("[Escenarios] No asignaste el escenario Cabana.");
        return;
    }

    if (jugador == null)
    {
        Debug.LogError("[Escenarios] No se recibió el jugador.");
        return;
    }

    ActivarEscenario(
        ConfiguracionPartidaSeleccionada.Escenario.Cabana
    );

    TrasladarJugadorAInicioDeEscenario(jugador);

    bool esServidor =
        NetworkManager.Singleton == null ||
        NetworkManager.Singleton.IsServer;

    bool esModoHistoria =
        ConfiguracionPartidaSeleccionada.Instancia != null &&
        ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado ==
        ConfiguracionPartidaSeleccionada.ModoJuego.Historia;

    if (!esServidor || !esModoHistoria)
        return;

    GestorMansionHistoria gestor = GestorMansionHistoria.Instance;

    if (gestor == null)
        gestor = FindAnyObjectByType<GestorMansionHistoria>();

    if (gestor != null)
    {
        gestor.IniciarMision();
    }
    else
    {
        Debug.LogError(
            "[Escenarios] No se encontró GestorMansionHistoria después de activar la mansión."
        );
    }
}


    public void TrasladarJugadorAInicioDeEscenario(Transform jugador)
    {
        Transform spawn = ObtenerSpawnJugador();

        if (spawn == null)
            return;

        TrasladarJugador(jugador, spawn);
    }

    private void TrasladarJugador(
        Transform jugador,
        Transform destino
    )
    {
        if (jugador == null)
        {
            Debug.LogError("[Escenarios] No se recibió el jugador.");
            return;
        }

        if (destino == null)
        {
            Debug.LogError("[Escenarios] El destino es nulo.");
            return;
        }

        CharacterController controlador =
            jugador.GetComponent<CharacterController>();

        if (controlador != null)
            controlador.enabled = false;

        jugador.SetPositionAndRotation(
            destino.position,
            destino.rotation
        );

        if (controlador != null)
            controlador.enabled = true;

        Debug.Log(
            $"[Escenarios] Jugador trasladado a {destino.name}."
        );
    }

    public ConfiguracionEscenarioJugable ObtenerEscenarioActivo()
    {
        return escenarioActivo;
    }

    public Transform ObtenerSpawnJugador()
    {
        if (escenarioActivo == null)
        {
            Debug.LogError(
                "[Escenarios] No hay un escenario jugable activo."
            );
            return null;
        }

        return escenarioActivo.SpawnJugador;
    }

    public Transform ObtenerSpawnSafeZone1()
    {
        return spawnSafeZone1;
    }

    public Transform ObtenerSpawnSafeZone2()
    {
        return spawnSafeZone2;
    }

    public Transform[] ObtenerSpawnsZombies()
    {
        if (escenarioActivo == null)
        {
            Debug.LogError(
                "[Escenarios] No hay un escenario jugable activo."
            );
            return null;
        }

        return escenarioActivo.SpawnsZombies;
    }
}
