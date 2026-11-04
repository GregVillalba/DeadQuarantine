
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class LoadGameMenuController : MonoBehaviour
{
    [Header("Panel de carga")]
    [SerializeField] private GameObject panelCarga;

    [Header("Escena de juego")]
    [SerializeField] private string escenaJuego =
        "MainSceneSinglePlayer";

    [Header("Botones")]
    [SerializeField] private Button botonHistoria;
    [SerializeField] private Button botonCargar;
    [SerializeField] private Button botonVolver;

    [Header("Textos")]
    [SerializeField] private TMP_Text informacion;
    [SerializeField] private TMP_Text subtitulo;

    public static bool CargandoPartidaDesdeMenu { get; private set; }

    private HistoriaSaveData datosCheckpoint;

    private bool historiaSeleccionada;
    private bool eventosConectados;

    // =====================================================
    // INICIALIZACIÓN
    // =====================================================

    private void Awake()
    {
        ConectarEventos();

        if (panelCarga != null)
            panelCarga.SetActive(false);
    }

    private void Start()
    {
        ComprobarPartidaGuardada();

        if (botonCargar != null)
            botonCargar.interactable = false;
    }

    private void ConectarEventos()
    {
        if (eventosConectados)
            return;

        if (botonHistoria == null ||
            botonCargar == null ||
            botonVolver == null)
        {
            Debug.LogError(
                "[LoadGameMenuController] Faltan referencias de botones."
            );
            return;
        }

        botonHistoria.onClick.AddListener(SeleccionarHistoria);
        botonCargar.onClick.AddListener(CargarPartida);
        botonVolver.onClick.AddListener(Volver);

        eventosConectados = true;
    }

    // =====================================================
    // ABRIR Y CERRAR MENÚ
    // =====================================================

    public void AbrirMenu()
    {
        if (CargandoPartidaDesdeMenu)
            return;

        if (panelCarga == null)
        {
            Debug.LogError(
                "[LoadGameMenuController] No se asignó PanelCarga."
            );
            return;
        }

        historiaSeleccionada = false;

        ComprobarPartidaGuardada();

        if (botonCargar != null)
            botonCargar.interactable = false;

        if (subtitulo != null)
            subtitulo.text = "Seleccioná el modo de juego";

        if (informacion != null)
        {
            informacion.text =
                "Seleccioná Historia para consultar tu partida.";
        }

        panelCarga.SetActive(true);
    }

    public void Volver()
    {
        if (CargandoPartidaDesdeMenu)
            return;

        if (panelCarga != null)
            panelCarga.SetActive(false);

        historiaSeleccionada = false;

        Debug.Log(
            "[LoadGameMenuController] Se cerró el menú de carga."
        );
    }

    // =====================================================
    // COMPROBAR PARTIDA
    // =====================================================

    private void ComprobarPartidaGuardada()
    {
        datosCheckpoint = SaveSystem.CargarHistoria();

        if (datosCheckpoint == null)
            return;

        bool valido =
            datosCheckpoint.checkpoint ==
                (int)CheckpointManager.EscenarioCheckpoint.SafeZone1 ||
            datosCheckpoint.checkpoint ==
                (int)CheckpointManager.EscenarioCheckpoint.SafeZone2;

        if (!valido)
            datosCheckpoint = null;
    }

    // =====================================================
    // SELECCIONAR HISTORIA
    // =====================================================

    private void SeleccionarHistoria()
    {
        historiaSeleccionada = true;

        ComprobarPartidaGuardada();

        if (datosCheckpoint == null)
        {
            informacion.text =
                "NO HAY PARTIDAS GUARDADAS\n\n" +
                "Primero tenés que alcanzar una zona segura.";

            botonCargar.interactable = false;
            subtitulo.text = "Modo Historia";

            return;
        }

        informacion.text =
            "PARTIDA ENCONTRADA\n\n" +
            "Checkpoint: " +
            ObtenerNombreCheckpoint(datosCheckpoint.checkpoint) +
            "\nDificultad: " +
            ObtenerNombreDificultad(datosCheckpoint.dificultad);

        subtitulo.text = "Continuar modo Historia";

        botonCargar.interactable = true;
    }

    // =====================================================
    // CARGAR PARTIDA
    // =====================================================

    private void CargarPartida()
    {
        if (CargandoPartidaDesdeMenu || !historiaSeleccionada)
            return;

        ComprobarPartidaGuardada();

        if (datosCheckpoint == null)
        {
            SeleccionarHistoria();
            return;
        }

        if (CheckpointManager.Instance == null)
        {
            Debug.LogError(
                "[LoadGameMenuController] No existe CheckpointManager."
            );
            return;
        }

        ConfiguracionPartidaSeleccionada configuracion =
            ConfiguracionPartidaSeleccionada.Instancia;

        if (configuracion == null)
        {
            Debug.LogError(
                "[LoadGameMenuController] No existe ConfiguracionPartidaSeleccionada."
            );
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager != null &&
            networkManager.IsListening &&
            !networkManager.IsServer)
        {
            Debug.LogError(
                "[LoadGameMenuController] Solo el servidor puede cargar."
            );
            return;
        }

        configuracion.modoSeleccionado =
            ConfiguracionPartidaSeleccionada.ModoJuego.Historia;

        configuracion.dificultadSeleccionada =
            (ConfiguracionPartidaSeleccionada.Dificultad)
            datosCheckpoint.dificultad;

        switch ((CheckpointManager.EscenarioCheckpoint)
            datosCheckpoint.checkpoint)
        {
            case CheckpointManager.EscenarioCheckpoint.SafeZone1:

                configuracion.escenarioSeleccionado =
                    ConfiguracionPartidaSeleccionada.Escenario.Ciudad;

                break;

            case CheckpointManager.EscenarioCheckpoint.SafeZone2:

                configuracion.escenarioSeleccionado =
                    ConfiguracionPartidaSeleccionada.Escenario.Cabana;

                break;

            default:
                Debug.LogError(
                    "[LoadGameMenuController] Checkpoint inválido."
                );
                return;
        }

        CargandoPartidaDesdeMenu = true;

        botonHistoria.interactable = false;
        botonCargar.interactable = false;
        botonVolver.interactable = false;

        informacion.text =
            "CARGANDO PARTIDA...\n\n" +
            "Preparando escenario y restaurando datos.";

        SceneManager.sceneLoaded += AlCargarEscena;

        if (networkManager != null &&
            networkManager.IsListening)
        {
            networkManager.SceneManager.LoadScene(
                escenaJuego,
                LoadSceneMode.Single
            );
        }
        else
        {
            SceneManager.LoadScene(
                escenaJuego,
                LoadSceneMode.Single
            );
        }
    }

    // =====================================================
    // ESCENA CARGADA
    // =====================================================

    private void AlCargarEscena(
        Scene escena,
        LoadSceneMode modo)
    {
        if (escena.name != escenaJuego)
            return;

        SceneManager.sceneLoaded -= AlCargarEscena;

        CheckpointManager manager =
            CheckpointManager.Instance;

        if (manager == null)
        {
            Debug.LogError(
                "[LoadGameMenuController] No se encontró CheckpointManager."
            );
            return;
        }

        Debug.Log(
            "[LoadGameMenuController] Escena cargada. Restaurando checkpoint."
        );

        manager.CargarCheckpoint();

        Destroy(gameObject);
    }

    // =====================================================
    // UTILIDADES
    // =====================================================

    private string ObtenerNombreCheckpoint(int checkpoint)
    {
        switch ((CheckpointManager.EscenarioCheckpoint)checkpoint)
        {
            case CheckpointManager.EscenarioCheckpoint.SafeZone1:
                return "Zona segura 1 - Afueras de la ciudad";

            case CheckpointManager.EscenarioCheckpoint.SafeZone2:
                return "Zona segura 2 - Campamento del bosque";

            default:
                return "Desconocido";
        }
    }

    private string ObtenerNombreDificultad(int dificultad)
    {
        return ((ConfiguracionPartidaSeleccionada.Dificultad)
            dificultad).ToString();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;

        if (botonHistoria != null)
            botonHistoria.onClick.RemoveListener(SeleccionarHistoria);

        if (botonCargar != null)
            botonCargar.onClick.RemoveListener(CargarPartida);

        if (botonVolver != null)
            botonVolver.onClick.RemoveListener(Volver);
    }
}
