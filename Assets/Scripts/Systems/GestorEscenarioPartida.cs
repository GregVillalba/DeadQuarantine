
using UnityEngine;

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
        ConfigurarEscenarioInicial();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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
        DesactivarEscenario(escenarioLaboratorio);
        DesactivarEscenario(escenarioCiudad);
        DesactivarEscenario(escenarioCabana);

        if (safeZone1 != null)
            safeZone1.SetActive(false);

        if (safeZone2 != null)
            safeZone2.SetActive(false);

        escenarioActivo = null;
    }

    private void DesactivarEscenario(
        ConfiguracionEscenarioJugable escenario
    )
    {
        if (escenario == null)
            return;

        Transform padre = escenario.transform.parent;

        if (padre != null)
            padre.gameObject.SetActive(false);
        else
            escenario.gameObject.SetActive(false);
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

        Transform padreEscenario = escenarioActivo.transform.parent;

        if (padreEscenario != null)
            padreEscenario.gameObject.SetActive(true);
        else
            escenarioActivo.gameObject.SetActive(true);

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

    // La zona segura queda pegada a la salida del laboratorio: se muestra sin apagar
    // el laboratorio, así el jugador la cruza caminando al abrirse la puerta.
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

    // La llama EntradaZonaSegura cuando el jugador local cruza el trigger de la zona.
    public void EntrarSafeZone1(Transform jugador)
    {
        StoryIntroController intro =
            jugador.GetComponentInChildren<StoryIntroController>(true);

        if (intro != null)
        {
            intro.MostrarAvisoContinuarSafeZone();
        }
        else
        {
            Debug.LogWarning(
                "[Escenarios] No se encontró StoryIntroController en el jugador."
            );
        }
    }

    // Volvió al laboratorio: ENTER deja de llevarlo a la Ciudad hasta que vuelva a entrar.
    public void SalirSafeZone1(Transform jugador)
    {
        StoryIntroController intro =
            jugador.GetComponentInChildren<StoryIntroController>(true);

        if (intro != null)
            intro.OcultarAvisoContinuarSafeZone();
    }

    // true si el objeto cuelga de una zona segura que está activa (ej. el mercader).
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
    {
        intro.MostrarAvisoContinuarSafeZone();
    }
    else
    {
        Debug.LogWarning(
            "[Escenarios] No se encontró StoryIntroController en el jugador."
        );
    }
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
