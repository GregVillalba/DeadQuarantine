
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class GestorEscenariosPartida : MonoBehaviour
{
    public static GestorEscenariosPartida Instance { get; private set; }

    [Header("Escenas multiplayer aditivas")]
    [SerializeField] private bool usarEscenasAditivasMultiplayer;
    public bool UsaEscenasAditivasMultiplayer => usarEscenasAditivasMultiplayer;
    public bool MapaMultiplayerListo { get; private set; }
    public bool PresentacionLocalPreparada { get; private set; }
    public event System.Action EventoMapaMultiplayerListo;


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
    private Transform[] spawnsSafeZone1 = new Transform[4];
    private Transform[] spawnsSafeZone2 = new Transform[4];

    private ConfiguracionEscenarioJugable escenarioActivo;
    private Scene escenaAditivaActiva;
    private string escenaAditivaAnterior;
    private bool cargaInicialAditivaIniciada;

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

        if (usarEscenasAditivasMultiplayer)
            return;

        PrepararEscenario(escenarioLaboratorio);
        PrepararEscenario(escenarioCiudad);
        PrepararEscenario(escenarioCabana);

        ConfigurarEscenarioInicial();
    }

    private void Start()
    {
        if (!usarEscenasAditivasMultiplayer || NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening)
            return;

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadAditivoCompletado;
        if (NetworkManager.Singleton.IsServer && !cargaInicialAditivaIniciada)
            StartCoroutine(CargarEscenaInicialAditiva());
    }

    private System.Collections.IEnumerator CargarEscenaInicialAditiva()
    {
        cargaInicialAditivaIniciada = true;
        yield return null;
        yield return null;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            yield break;

        var escenario = ConfiguracionPartidaSeleccionada.Instancia != null
            ? ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado
            : ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;
        SolicitarMapaServidor(NombreEscenaMP(escenario));
    }

    private void OnLoadAditivoCompletado(string sceneName, LoadSceneMode mode,
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!usarEscenasAditivasMultiplayer || !sceneName.StartsWith("MP_"))
            return;

        Scene nueva = SceneManager.GetSceneByName(sceneName);
        if (!nueva.IsValid() || !nueva.isLoaded)
            return;

        PresentacionLocalPreparada = false;
        string anterior = escenaAditivaActiva.IsValid()
            ? escenaAditivaActiva.name
            : escenaAditivaAnterior;
        escenaAditivaActiva = nueva;
        BindearMapaAditivo(nueva);
        SceneManager.SetActiveScene(nueva);
        MapaMultiplayerListo = true;
        EventoMapaMultiplayerListo?.Invoke();
        if (sceneName == "MP_Ciudad" && NetworkManager.Singleton != null &&
            !NetworkManager.Singleton.IsServer && escenarioActivo != null &&
            NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            MultiplayerPlayerSpawnAssigner local =
                NetworkManager.Singleton.LocalClient.PlayerObject
                    .GetComponent<MultiplayerPlayerSpawnAssigner>();
            if (local != null)
                local.ColocarJugadorLocalEnEscenario(
                    escenarioActivo.ObtenerSpawnJugador(
                        Mathf.Max(0, local.AssignedSpawnIndex.Value)));
        }
        StartCoroutine(PrepararPresentacionMapaLocal(sceneName));

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        AplicarMapaALosJugadores(sceneName);
        if (!string.IsNullOrEmpty(anterior) && anterior != sceneName)
        {
            Scene escenaVieja = SceneManager.GetSceneByName(anterior);
            if (escenaVieja.IsValid() && escenaVieja.isLoaded)
                NetworkManager.Singleton.SceneManager.UnloadScene(escenaVieja);
        }
    }

    private void BindearMapaAditivo(Scene scene)
    {
        escenarioActivo = null;
        safeZone1 = null;
        safeZone2 = null;
        spawnSafeZone1 = null;
        spawnSafeZone2 = null;

        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            if (raiz == null) continue;
            if (raiz.name == "ZonaSegura1") safeZone1 = raiz;
            if (raiz.name == "ZonaSegura2") safeZone2 = raiz;
            if (raiz.name == "ZonaSegura1")
            {
                spawnsSafeZone1 = BuscarSpawnsZonaSegura(raiz.transform, "spawnZonaSegura1");
                spawnSafeZone1 = spawnsSafeZone1[0];
            }
            if (raiz.name == "ZonaSegura2")
            {
                spawnsSafeZone2 = BuscarSpawnsZonaSegura(raiz.transform, "spawnZonaSegura2");
                spawnSafeZone2 = spawnsSafeZone2[0];
            }

            ConfiguracionEscenarioJugable config =
                raiz.GetComponentInChildren<ConfiguracionEscenarioJugable>(true);
            if (config != null) escenarioActivo = config;
        }

        if (escenarioActivo != null && ConfiguracionPartidaSeleccionada.Instancia != null)
            ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado = escenarioActivo.TipoEscenario;
    }

    private IEnumerator PrepararPresentacionMapaLocal(string sceneName)
    {
        yield return null;

        // El jugador local puede terminar de aparecer después de cargar el mapa.
        StoryIntroController intro = null;
        while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkObject jugador = NetworkManager.Singleton.LocalClient != null
                ? NetworkManager.Singleton.LocalClient.PlayerObject : null;
            intro = jugador != null
                ? jugador.GetComponentInChildren<StoryIntroController>(true) : null;
            if (intro != null && intro.IsSpawned && intro.IsOwner)
                break;
            yield return null;
        }

        if (intro == null || !intro.IsSpawned || !intro.IsOwner)
            yield break;

        AvisarAlJugadorLocalSobreZonaSegura(sceneName);
        PresentacionLocalPreparada = true;
    }

    private static Transform BuscarHijo(Transform raiz, string nombre)
    {
        foreach (Transform hijo in raiz.GetComponentsInChildren<Transform>(true))
            if (hijo.name == nombre) return hijo;
        return null;
    }

    private static Transform[] BuscarSpawnsZonaSegura(Transform raiz, string nombreBase)
    {
        Transform[] puntos = new Transform[4];
        puntos[0] = BuscarHijo(raiz, nombreBase);

        for (int i = 1; i < puntos.Length; i++)
            puntos[i] = BuscarHijo(raiz, nombreBase + "_" + (i + 1));

        for (int i = 1; i < puntos.Length; i++)
            if (puntos[i] == null)
                puntos[i] = puntos[0];

        return puntos;
    }

    private static void AvisarAlJugadorLocalSobreZonaSegura(string sceneName)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null)
            return;
        NetworkObject jugador = NetworkManager.Singleton.LocalClient.PlayerObject;
        if (jugador == null) return;
        StoryIntroController intro = jugador.GetComponentInChildren<StoryIntroController>(true);
        if (intro == null) return;
        if (sceneName == "MP_ZonaSegura1") intro.MostrarAvisoContinuarSafeZone();
        else if (sceneName == "MP_ZonaSegura2") intro.MostrarAvisoContinuarAMansion();
        else if (sceneName == "MP_Laboratorio")
            intro.MostrarIntroduccionMultiplayer(ConfiguracionPartidaSeleccionada.Escenario.Laboratorio);
        else if (sceneName == "MP_Ciudad")
            intro.MostrarIntroduccionMultiplayer(ConfiguracionPartidaSeleccionada.Escenario.Ciudad);
        else if (sceneName == "MP_Cabana")
            intro.MostrarIntroduccionMultiplayer(ConfiguracionPartidaSeleccionada.Escenario.Cabana);
    }
    private void AplicarMapaALosJugadores(string sceneName)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        bool zona1 = sceneName == "MP_ZonaSegura1";
        bool zona2 = sceneName == "MP_ZonaSegura2";
        foreach (NetworkClient cliente in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (cliente.PlayerObject == null) continue;
            MultiplayerPlayerSpawnAssigner assigner =
                cliente.PlayerObject.GetComponent<MultiplayerPlayerSpawnAssigner>();
            if (assigner == null) continue;

            if (zona1 || zona2)
            {
                Transform spawn = ObtenerSpawnZonaSegura(zona1, assigner.AssignedSpawnIndex.Value);
                if (spawn != null)
                    assigner.AplicarSpawnCheckpoint(spawn.position, spawn.rotation);
            }
            else if (escenarioActivo != null)
            {
                Transform spawn = escenarioActivo.ObtenerSpawnJugador(assigner.AssignedSpawnIndex.Value);
                if (spawn != null) assigner.AplicarSpawnDeEscenario(spawn.position, spawn.rotation);
            }
        }
    }

    private Transform ObtenerSpawnZonaSegura(bool zona1, int indice)
    {
        Transform[] puntos = zona1 ? spawnsSafeZone1 : spawnsSafeZone2;
        if (puntos == null || puntos.Length == 0)
            return zona1 ? spawnSafeZone1 : spawnSafeZone2;

        int indiceSeguro = Mathf.Clamp(indice, 0, puntos.Length - 1);
        Transform spawn = puntos[indiceSeguro];
        return spawn != null ? spawn : (zona1 ? spawnSafeZone1 : spawnSafeZone2);
    }

    private static string NombreEscenaMP(ConfiguracionPartidaSeleccionada.Escenario escenario)
    {
        switch (escenario)
        {
            case ConfiguracionPartidaSeleccionada.Escenario.Ciudad: return "MP_Ciudad";
            case ConfiguracionPartidaSeleccionada.Escenario.Cabana: return "MP_Cabana";
            default: return "MP_Laboratorio";
        }
    }

    private void SolicitarMapaAditivo(string sceneName)
    {
        if (!usarEscenasAditivasMultiplayer || NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening)
            return;

        if (NetworkManager.Singleton.IsServer)
        {
            SolicitarMapaServidor(sceneName);
            return;
        }

        NetworkObject jugador = NetworkManager.Singleton.LocalClient != null
            ? NetworkManager.Singleton.LocalClient.PlayerObject : null;
        MultiplayerPlayerSpawnAssigner assigner = jugador != null
            ? jugador.GetComponent<MultiplayerPlayerSpawnAssigner>() : null;
        if (assigner != null) assigner.SolicitarMapaMultiplayer(sceneName);
    }

    public void SolicitarMapaDesdeRpc(string sceneName)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            SolicitarMapaServidor(sceneName);
    }

    private void SolicitarMapaServidor(string sceneName)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;
        Scene existente = SceneManager.GetSceneByName(sceneName);
        if (existente.IsValid() && existente.isLoaded)
        {
            PresentacionLocalPreparada = false;
            escenaAditivaAnterior = escenaAditivaActiva.IsValid()
                ? escenaAditivaActiva.name : string.Empty;
            BindearMapaAditivo(existente);
            SceneManager.SetActiveScene(existente);
            AplicarMapaALosJugadores(sceneName);
            MapaMultiplayerListo = true;
            EventoMapaMultiplayerListo?.Invoke();
            StartCoroutine(PrepararPresentacionMapaLocal(sceneName));
            return;
        }

        escenaAditivaAnterior = escenaAditivaActiva.IsValid()
            ? escenaAditivaActiva.name : string.Empty;
        var resultado = NetworkManager.Singleton.SceneManager.LoadScene(
            sceneName, LoadSceneMode.Additive);
        if (resultado != SceneEventProgressStatus.Started)
            Debug.LogError("[Escenarios MP] No se pudo cargar " + sceneName + ": " + resultado);
    }
    private void OnDestroy()
    {
        if (usarEscenasAditivasMultiplayer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadAditivoCompletado;
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
        if (usarEscenasAditivasMultiplayer)
        {
            if (ConfiguracionPartidaSeleccionada.Instancia != null)
                ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado = escenario;
            SolicitarMapaAditivo(NombreEscenaMP(escenario));
            return;
        }
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
        if (usarEscenasAditivasMultiplayer) { SolicitarMapaAditivo("MP_ZonaSegura1"); return; }
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
        if (usarEscenasAditivasMultiplayer) { SolicitarMapaAditivo("MP_ZonaSegura2"); return; }
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
public bool PrepararCheckpoint(
    CheckpointManager.EscenarioCheckpoint checkpoint,
    Transform jugador
)
{
    if (usarEscenasAditivasMultiplayer)
    {
        SolicitarMapaAditivo(checkpoint == CheckpointManager.EscenarioCheckpoint.SafeZone1
            ? "MP_ZonaSegura1" : "MP_ZonaSegura2");
        return true;
    }
    if (jugador == null)
    {
        Debug.LogError(
            "[Escenarios] No se puede preparar el checkpoint: jugador nulo."
        );
        return false;
    }

    switch (checkpoint)
    {
        case CheckpointManager.EscenarioCheckpoint.SafeZone1:

            if (safeZone1 == null || spawnSafeZone1 == null)
            {
                Debug.LogError(
                    "[Escenarios] SafeZone1 o su punto de aparición no están asignados."
                );
                return false;
            }

            if (ConfiguracionPartidaSeleccionada.Instancia != null)
            {
                ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado =
                    ConfiguracionPartidaSeleccionada.Escenario.Ciudad;
            }

            ActivarSafeZone1();
            TrasladarJugadorACheckpoint(jugador, spawnSafeZone1);

            StoryIntroController introCiudad =
                jugador.GetComponentInChildren<StoryIntroController>(true);

            if (introCiudad != null)
                introCiudad.MostrarAvisoContinuarSafeZone();
            else
                Debug.LogWarning(
                    "[Escenarios] No se encontró StoryIntroController para SafeZone1."
                );

            break;

        case CheckpointManager.EscenarioCheckpoint.SafeZone2:

            if (safeZone2 == null || spawnSafeZone2 == null)
            {
                Debug.LogError(
                    "[Escenarios] SafeZone2 o su punto de aparición no están asignados."
                );
                return false;
            }

            if (ConfiguracionPartidaSeleccionada.Instancia != null)
            {
                ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado =
                    ConfiguracionPartidaSeleccionada.Escenario.Cabana;
            }

            ActivarSafeZone2();
            TrasladarJugadorACheckpoint(jugador, spawnSafeZone2);

            StoryIntroController introMansion =
                jugador.GetComponentInChildren<StoryIntroController>(true);

            if (introMansion != null)
                introMansion.MostrarAvisoContinuarAMansion();
            else
                Debug.LogWarning(
                    "[Escenarios] No se encontró StoryIntroController para SafeZone2."
                );

            break;

        default:

            Debug.LogError(
                "[Escenarios] Checkpoint inválido: " + checkpoint
            );
            return false;
    }

    Debug.Log(
        "[Escenarios] Checkpoint preparado correctamente: " + checkpoint
    );

    return true;
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
        if (usarEscenasAditivasMultiplayer) { SolicitarMapaAditivo("MP_ZonaSegura1"); return; }
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
        if (usarEscenasAditivasMultiplayer) { SolicitarMapaAditivo("MP_ZonaSegura2"); return; }
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
    if (usarEscenasAditivasMultiplayer) { SolicitarMapaAditivo("MP_Cabana"); return; }
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
    private void TrasladarJugadorACheckpoint(
    Transform jugador,
    Transform destino
)
{
    if (jugador == null || destino == null)
        return;

    MultiplayerPlayerSpawnAssigner assigner =
        jugador.GetComponent<MultiplayerPlayerSpawnAssigner>();

    if (
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsServer &&
        assigner != null &&
        assigner.IsSpawned
    )
    {
        assigner.AplicarSpawnCheckpoint(
            destino.position,
            destino.rotation
        );

        return;
    }

    TrasladarJugador(jugador, destino);
}
}




