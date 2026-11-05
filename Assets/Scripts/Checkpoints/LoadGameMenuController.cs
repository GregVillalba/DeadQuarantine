
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
    [SerializeField] private Button botonSupervivencia;
    [SerializeField] private Button botonCargar;
    [SerializeField] private Button botonVolver;

    [Header("Textos")]
    [SerializeField] private TMP_Text informacion;
    [SerializeField] private TMP_Text subtitulo;

    public static bool CargandoPartidaDesdeMenu { get; private set; }
    public static bool CargandoSupervivenciaDesdeMenu { get; private set; }

    private HistoriaSaveData datosCheckpoint;
    private SupervivenciaSaveData datosSupervivencia;

    private bool historiaSeleccionada;
    private bool supervivenciaSeleccionada;
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
            botonSupervivencia == null ||
            botonCargar == null ||
            botonVolver == null)
        {
            Debug.LogError(
                "[LoadGameMenuController] Faltan referencias de botones."
            );
            return;
        }

        botonHistoria.onClick.AddListener(SeleccionarHistoria);
        botonSupervivencia.onClick.AddListener(SeleccionarSupervivencia);
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
        supervivenciaSeleccionada = false;

        ComprobarPartidaGuardada();

        if (botonCargar != null)
            botonCargar.interactable = false;

        if (subtitulo != null)
            subtitulo.text = "Seleccioná el modo de juego";

        if (informacion != null)
        {
            informacion.text =
                "Seleccioná un Modo para restaurar una partida guardada.";
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
        supervivenciaSeleccionada = false;

        if (botonCargar != null)
            botonCargar.interactable = false;

        Debug.Log(
            "[LoadGameMenuController] Se cerró el menú de carga."
        );
    }

    // =====================================================
    // COMPROBAR PARTIDAS GUARDADAS
    // =====================================================


private void ComprobarPartidaGuardada()
{
    // Historia
    datosCheckpoint = SaveSystem.CargarHistoria();

    if (datosCheckpoint != null)
    {
        bool valido =
            datosCheckpoint.checkpoint ==
                (int)CheckpointManager.EscenarioCheckpoint.SafeZone1 ||
            datosCheckpoint.checkpoint ==
                (int)CheckpointManager.EscenarioCheckpoint.SafeZone2;

        if (!valido)
            datosCheckpoint = null;
    }

    // Supervivencia
    datosSupervivencia = null;

    if (!SaveSystem.ExisteGuardadoSupervivencia())
        return;

    SupervivenciaSaveData datos =
        SaveSystem.CargarSupervivencia();

    if (datos == null)
        return;

    // Validar que realmente exista un checkpoint válido.
    bool rondaValida =
        datos.rondaActual >= 5 &&
        datos.rondaActual % 5 == 0;

    bool escenarioValido =
        datos.escenario !=
        (int)ConfiguracionPartidaSeleccionada.Escenario.Ninguno;

    bool jugadorValido = datos.jugador != null;

    if (!rondaValida || !escenarioValido || !jugadorValido)
    {
        Debug.LogWarning(
            "[LoadGameMenuController] El guardado de Supervivencia está vacío o es inválido."
        );

        return;
    }

    datosSupervivencia = datos;
}


    // =====================================================
    // SELECCIONAR HISTORIA
    // =====================================================

    private void SeleccionarHistoria()
    {
        historiaSeleccionada = true;
        supervivenciaSeleccionada = false;

        ComprobarPartidaGuardada();

        if (datosCheckpoint == null)
        {
            if (informacion != null)
            {
                informacion.text =
                    "NO HAY PARTIDAS GUARDADAS\n\n" +
                    "Primero tenés que alcanzar una zona segura.";
            }

            if (botonCargar != null)
                botonCargar.interactable = false;

            if (subtitulo != null)
                subtitulo.text = "Modo Historia";

            return;
        }

        if (informacion != null)
        {
            informacion.text =
                "PARTIDA ENCONTRADA\n\n" +
                "Checkpoint: " +
                ObtenerNombreCheckpoint(datosCheckpoint.checkpoint) +
                "\nDificultad: " +
                ObtenerNombreDificultad(datosCheckpoint.dificultad);
        }

        if (subtitulo != null)
            subtitulo.text = "Continuar modo Historia";

        if (botonCargar != null)
            botonCargar.interactable = true;
    }

    // =====================================================
    // SELECCIONAR SUPERVIVENCIA
    // =====================================================

    private void SeleccionarSupervivencia()
    {
        historiaSeleccionada = false;
        supervivenciaSeleccionada = true;

        ComprobarPartidaGuardada();

        if (datosSupervivencia == null)
        {
            if (informacion != null)
            {
                informacion.text =
                    "NO HAY PARTIDAS GUARDADAS\n\n" +
                    "El modo Supervivencia todavía no tiene un checkpoint disponible.";
            }

            if (botonCargar != null)
                botonCargar.interactable = false;

            if (subtitulo != null)
                subtitulo.text = "Modo Supervivencia";

            return;
        }

        if (informacion != null)
        {
            informacion.text =
                "PARTIDA ENCONTRADA\n\n" +
                "Escenario: " +
                ObtenerNombreEscenario(datosSupervivencia.escenario) +
                "\nDificultad: " +
                ObtenerNombreDificultad(datosSupervivencia.dificultad) +
                "\nÚltima ronda guardada: " +
                datosSupervivencia.rondaActual;
        }

        if (subtitulo != null)
            subtitulo.text = "Continuar modo Supervivencia";

        if (botonCargar != null)
            botonCargar.interactable = true;
    }

    // =====================================================
    // CARGAR PARTIDA
    // =====================================================

    private void CargarPartida()
    {
        if (CargandoPartidaDesdeMenu)
            return;

        if (historiaSeleccionada)
        {
            CargarHistoria();
            return;
        }

        if (supervivenciaSeleccionada)
        {
            CargarSupervivencia();
            return;
        }
    }

    // =====================================================
    // CARGAR HISTORIA
    // =====================================================

    private void CargarHistoria()
    {
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

        if (!ValidarServidor())
            return;

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
        CargandoSupervivenciaDesdeMenu = false;

        BloquearBotonesDuranteCarga();

        if (informacion != null)
        {
            informacion.text =
                "CARGANDO PARTIDA...\n\n" +
                "Preparando escenario y restaurando datos.";
        }

        CheckpointManager.Instance.PrepararCargaDesdeMenu(
            escenaJuego
        );

        CargarEscena();
    }

    // =====================================================
    // CARGAR SUPERVIVENCIA
    // =====================================================

    private void CargarSupervivencia()
    {
        ComprobarPartidaGuardada();

        if (datosSupervivencia == null)
        {
            SeleccionarSupervivencia();
            return;
        }

        if (datosSupervivencia.rondaActual < 5 ||
            datosSupervivencia.rondaActual % 5 != 0)
        {
            Debug.LogError(
                "[LoadGameMenuController] La ronda guardada de Supervivencia es inválida."
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

        if (!ValidarServidor())
            return;

        configuracion.modoSeleccionado =
            ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;

        configuracion.escenarioSeleccionado =
            (ConfiguracionPartidaSeleccionada.Escenario)
            datosSupervivencia.escenario;

        configuracion.dificultadSeleccionada =
            (ConfiguracionPartidaSeleccionada.Dificultad)
            datosSupervivencia.dificultad;

        PrepararCargaSupervivencia();

        BloquearBotonesDuranteCarga();

        if (informacion != null)
        {
            informacion.text =
                "CARGANDO SUPERVIVENCIA...\n\n" +
                "Restaurando la ronda " +
                datosSupervivencia.rondaActual +
                " y los datos del jugador.";
        }

        Debug.Log(
            "[LoadGameMenuController] Cargando Supervivencia. Ronda: " +
            datosSupervivencia.rondaActual
        );

        CargarEscena();
    }

    // =====================================================
    // UTILIDADES DE CARGA
    // =====================================================

    private bool ValidarServidor()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager != null &&
            networkManager.IsListening &&
            !networkManager.IsServer)
        {
            Debug.LogError(
                "[LoadGameMenuController] Solo el servidor puede cargar."
            );

            return false;
        }

        return true;
    }

    private void BloquearBotonesDuranteCarga()
    {
        if (botonHistoria != null)
            botonHistoria.interactable = false;

        if (botonSupervivencia != null)
            botonSupervivencia.interactable = false;

        if (botonCargar != null)
            botonCargar.interactable = false;

        if (botonVolver != null)
            botonVolver.interactable = false;
    }

    private void CargarEscena()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

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
    // UTILIDADES DE INFORMACIÓN
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

    private string ObtenerNombreEscenario(int escenario)
    {
        return ((ConfiguracionPartidaSeleccionada.Escenario)
            escenario).ToString();
    }

    private string ObtenerNombreDificultad(int dificultad)
    {
        return ((ConfiguracionPartidaSeleccionada.Dificultad)
            dificultad).ToString();
    }

    // =====================================================
    // ESTADO DE CARGA
    // =====================================================

    public static void PrepararCargaSupervivencia()
    {
        CargandoPartidaDesdeMenu = true;
        CargandoSupervivenciaDesdeMenu = true;
    }

    public static void FinalizarCargaDesdeMenu()
    {
        CargandoPartidaDesdeMenu = false;
        CargandoSupervivenciaDesdeMenu = false;

        Debug.Log(
            "[LoadGameMenu] Estado de carga desde menú restablecido."
        );
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarEstadoDeCarga()
    {
        CargandoPartidaDesdeMenu = false;
        CargandoSupervivenciaDesdeMenu = false;
    }

    // =====================================================
    // DESTRUCCIÓN
    // =====================================================

    private void OnDestroy()
    {
        if (botonHistoria != null)
            botonHistoria.onClick.RemoveListener(SeleccionarHistoria);

        if (botonSupervivencia != null)
            botonSupervivencia.onClick.RemoveListener(SeleccionarSupervivencia);

        if (botonCargar != null)
            botonCargar.onClick.RemoveListener(CargarPartida);

        if (botonVolver != null)
            botonVolver.onClick.RemoveListener(Volver);
    }
}
