
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
        SinglePlayerNetworkStarter.SceneName;

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
    private GameObject fichaGuardado;
    private TMP_Text tituloGuardado, etiquetaUbicacion, ubicacionGuardada, dificultadGuardada;

    // =====================================================
    // INICIALIZACIÓN
    // =====================================================

    private void Awake()
    {
        if (panelCarga != null)
            ConstruirVista();
        ConectarEventos();

        if (panelCarga != null)
            panelCarga.SetActive(false);
    }

    private void ConstruirVista()
    {
        foreach (Transform child in panelCarga.transform)
            child.gameObject.SetActive(false);
        if (panelCarga.TryGetComponent<Image>(out var fondo))
            fondo.color = new Color(.025f, .045f, .065f, .74f);

        var panel = PartidaMenuVisual.Panel(panelCarga.transform, "CargarPartidaActualizado", 746);
        PartidaMenuVisual.Texto(panel, "Modo", "PARTIDA EN SOLITARIO", 48, 40, 664, 28, 17);
        PartidaMenuVisual.Texto(panel, "Titulo", "CARGAR PARTIDA", 48, 72, 664, 90, 80, true);
        subtitulo = PartidaMenuVisual.Texto(panel, "Subtitulo", "Seleccioná el modo que querés continuar.", 48, 170, 664, 35, 20);
        botonHistoria = PartidaMenuVisual.Boton(panel, "BotonHistoria", "HISTORIA", 48, 238);
        botonSupervivencia = PartidaMenuVisual.Boton(panel, "BotonSupervivencia", "SUPERVIVENCIA", 386, 238);

        var ficha = PartidaMenuVisual.Caja(panel, "Ficha", 48, 336, 664, 250, new Color32(13, 23, 29, 255));
        PartidaMenuVisual.Borde(ficha, new Color32(41, 57, 66, 255));
        informacion = PartidaMenuVisual.Texto(ficha, "InformacionCheckpoint", "", 26, 24, 612, 200, 20);
        var datos = PartidaMenuVisual.Caja(ficha, "DatosGuardados", 0, 0, 664, 250, Color.clear);
        fichaGuardado = datos.gameObject;
        PartidaMenuVisual.Texto(datos, "Estado", "PARTIDA GUARDADA", 26, 22, 612, 28, 16).color = PartidaMenuVisual.Verde;
        tituloGuardado = PartidaMenuVisual.Texto(datos, "Checkpoint", "", 26, 53, 612, 63, 48, true);
        PartidaMenuVisual.Linea(datos, 26, 128, 612);
        etiquetaUbicacion = PartidaMenuVisual.Texto(datos, "EtiquetaUbicacion", "Escenario", 26, 139, 195, 40, 19);
        ubicacionGuardada = PartidaMenuVisual.Texto(datos, "Ubicacion", "", 226, 139, 412, 40, 19);
        PartidaMenuVisual.Linea(datos, 26, 184, 612);
        PartidaMenuVisual.Texto(datos, "EtiquetaDificultad", "Dificultad", 26, 193, 195, 40, 19);
        dificultadGuardada = PartidaMenuVisual.Texto(datos, "Dificultad", "", 226, 193, 412, 40, 19);
        foreach (var valor in new[] { ubicacionGuardada, dificultadGuardada })
        {
            valor.alignment = TextAlignmentOptions.MidlineRight;
            valor.color = Color.white;
        }

        botonVolver = PartidaMenuVisual.Boton(panel, "BotonVolver", "VOLVER", 48, 620);
        botonCargar = PartidaMenuVisual.Boton(panel, "BotonCargar", "CARGAR PARTIDA", 386, 620, true);
        MostrarFicha(false);
    }

    private void MostrarFicha(bool visible)
    {
        if (fichaGuardado == null)
            return;
        fichaGuardado.SetActive(visible);
        informacion.gameObject.SetActive(!visible);
        PartidaMenuVisual.Marcar(botonHistoria, historiaSeleccionada);
        PartidaMenuVisual.Marcar(botonSupervivencia, supervivenciaSeleccionada);
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
        MostrarFicha(false);

        ComprobarPartidaGuardada();

        if (botonCargar != null)
            botonCargar.interactable = false;

        if (subtitulo != null)
            subtitulo.text = "Seleccioná el modo que querés continuar.";

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

        if (!valido || datosCheckpoint.jugador == null)
            datosCheckpoint = null;
    }

    // Supervivencia
    datosSupervivencia = null;

    SupervivenciaSaveData datos =
        SaveSystem.CargarSupervivencia();

    if (datos == null)
        return;

    // Validar que realmente exista un checkpoint válido.
    bool rondaValida = SurvivalSaveManager.EsRondaGuardable(datos.rondaActual);

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
            MostrarFicha(false);
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

        MostrarFicha(true);
        tituloGuardado.text = $"Zona segura {datosCheckpoint.checkpoint}";
        etiquetaUbicacion.text = "Checkpoint";
        ubicacionGuardada.text = ObtenerNombreCheckpoint(datosCheckpoint.checkpoint);
        dificultadGuardada.text = ObtenerNombreDificultad(datosCheckpoint.dificultad);

        if (subtitulo != null)
            subtitulo.text = "Seleccioná el modo que querés continuar.";

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
            MostrarFicha(false);
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

        MostrarFicha(true);
        tituloGuardado.text = $"Ronda {datosSupervivencia.rondaActual}";
        etiquetaUbicacion.text = "Escenario";
        ubicacionGuardada.text = ObtenerNombreEscenario(datosSupervivencia.escenario);
        dificultadGuardada.text = ObtenerNombreDificultad(datosSupervivencia.dificultad);

        if (subtitulo != null)
            subtitulo.text = "Seleccioná el modo que querés continuar.";

        if (botonCargar != null)
            botonCargar.interactable = true;
    }

    // =====================================================
    // CARGAR PARTIDA
    // =====================================================

    private void CargarPartida()
    {
        if (CargandoPartidaDesdeMenu || SceneTransition.IsLoading)
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
        MostrarFicha(false);
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
            DeadQuarantine.PvPvE.PvPvELoadingScreen.BeginOriginal(escenaJuego);
            var resultado = networkManager.SceneManager.LoadScene(escenaJuego, LoadSceneMode.Single);
            if (resultado != SceneEventProgressStatus.Started)
            {
                DeadQuarantine.PvPvE.PvPvELoadingScreen.CancelOriginal();
                Debug.LogError($"[Cargar partida] No se pudo cargar {escenaJuego}: {resultado}.");
                MostrarErrorCarga();
            }
        }
        else if (!SceneTransition.TryLoad(escenaJuego, showMap: true,
            onFailure: MostrarErrorCarga, onExit: LimpiarCargaPendiente))
        {
            MostrarErrorCarga();
        }
    }

    private void MostrarErrorCarga()
    {
        LimpiarCargaPendiente();

        if (this == null)
            return;

        if (botonHistoria != null) botonHistoria.interactable = true;
        if (botonSupervivencia != null) botonSupervivencia.interactable = true;
        if (botonCargar != null) botonCargar.interactable = true;
        if (botonVolver != null) botonVolver.interactable = true;
        if (informacion != null) informacion.text = "No se pudo abrir la partida guardada.";
    }

    private static void LimpiarCargaPendiente()
    {
        if (CheckpointManager.Instance != null && !CargandoSupervivenciaDesdeMenu)
            CheckpointManager.Instance.FinalizarCarga();
        else
            FinalizarCargaDesdeMenu();
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
