using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Unity.Services.Multiplayer;

public class MultiplayerLobbyController : MonoBehaviour
{
    public static bool AbrirDirectoEnSalaEspera = false;
    public static bool AbrirDirectoEnIngresarCodigo = false; //
    public static string CodigoGeneradoParaMostrar = "";

    [Header("--- PANELES ---")]
    [SerializeField] private GameObject modoMultiplayerPanel;
    [SerializeField] private GameObject ingresarCodPanel;
    [SerializeField] private GameObject salaEsperaPanel;

    [Header("--- NETWORK ---")]
    [SerializeField] private NetworkBootstrap network;

    [Header("--- AVISO SIN INTERNET ---")]
    [SerializeField] private GameObject panelSinInternet;

    [Header("--- AVISO CÓDIGO INVÁLIDO ---")]
    [SerializeField] private GameObject panelCodigoInvalido;


    [Header("--- INGRESAR CÓDIGO ---")]
    [SerializeField] private TMP_InputField codigoInputField;

    [Header("--- CÓDIGO DE SALA ---")]
    [SerializeField] private Button botonCopiarCodigo;
    [SerializeField] private TMP_Text labelCodigoACopiar;
    [SerializeField] private TMP_Text feedbackCopiarText;

    [Header("--- JUGADOR 1 ---")]
    [SerializeField] private TMP_Text p1ConexionText;
    [SerializeField] private TMP_Text p1EstadoText;
    [SerializeField] private Image p1ConexionIcon;
    [SerializeField] private Image p1EstadoIcon;

    [Header("--- JUGADOR 2 ---")]
    [SerializeField] private TMP_Text p2ConexionText;
    [SerializeField] private TMP_Text p2EstadoText;
    [SerializeField] private Image p2ConexionIcon;
    [SerializeField] private Image p2EstadoIcon;

    [Header("--- SLOTS CON PERSONAJE 3D (reemplazan a JUGADOR 1/2 de arriba si se asignan) ---")]
    [SerializeField] private LobbyPlayerSlot[] slots;
    [SerializeField] private CharacterCatalog catalogoPersonajes;
    [SerializeField] private CharacterPreviewStage escenarioPersonajes;

    [Tooltip("Botón PERSONAJE: se desactiva al marcar LISTO (la elección queda fija).")]
    [SerializeField] private Button btnPersonaje;

    [Header("--- MENSAJE EMERGENTE (sala llena, sala cerrada...) ---")]
    [SerializeField] private GameObject popupMensaje;
    [SerializeField] private TMP_Text popupMensajeTitulo;
    [SerializeField] private TMP_Text popupMensajeTexto;

    [Header("--- BOTÓN LISTO ---")]
    [SerializeField] private Button btnPlayMultiplayer;
    [SerializeField] private TMP_Text btnPlayMultiplayerText;

    [Header("--- COUNTDOWN ---")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TMP_Text countdownText;

    [Header("--- ESCENA ---")]
    [SerializeField] private string nombreEscenaJuego =
        "MainSceneMultiPlayer";

    [Header("--- COLOR ESTADO NEGATIVO ---")]
    [SerializeField] private Color colorRojo =
        new Color(1f, 0.1f, 0.1f, 1f);

    [Header("--- ICONOS DE ESTADO ---")]
    [SerializeField] private Sprite iconoVerde;
    [SerializeField] private Sprite iconoRojo;

    private bool estoyEnSala = false;
    private bool countdownIniciado = false;
    private bool miReady = false;
    private float tiempoCountdown = -1f;

    private const string READY_PROPERTY = "Ready";

    // Guarda una fecha/hora final, no el número 5, 4, 3...
    private const string COUNTDOWN_PROPERTY =
        "CountdownEndUtcTicks";

    private const string ESCENARIO_PROPERTY = "Escenario";
    private const string MODO_PROPERTY = "Modo";
    private const string DIFICULTAD_PROPERTY = "Dificultad";

    // Personaje elegido por cada jugador (número del catálogo). Viaja junto al Ready.
    private const string CHARACTER_PROPERTY = "Character";

    // Identidad pública del jugador. NO es el nombre del personaje.
    private const string USERNAME_PROPERTY = "Username";

    private int ultimoPersonajePublicado = -1;
    private float proximoPublicarPersonaje = 0f;

    // Presencia: detecta a los que cerraron el juego sin salir de la sala.
    private readonly LobbyPresence presence = new LobbyPresence();
    private readonly HashSet<string> removiendo = new HashSet<string>();
    private bool presenciaActiva;
    private bool saliendoPorCierre;

    private void Awake()
    {
        if (network == null)
            network = NetworkBootstrap.Instance;

        if (botonCopiarCodigo != null)
        {
            botonCopiarCodigo.onClick.AddListener(
                OnClick_CopiarCodigo
            );
        }

        if (codigoInputField != null)
        {
            codigoInputField.onValidateInput =
                (texto, indice, caracter) => char.ToUpperInvariant(caracter);

            codigoInputField.onValueChanged.AddListener(OcultarCodigoInvalidoSiVacio);
        }
    }

    private void Start()
    {
        ConfigurarSlots();

        if (AbrirDirectoEnSalaEspera)
        {
            AbrirDirectoEnSalaEspera = false;
            estoyEnSala = true;

            MostrarPanel(salaEsperaPanel);

            if (labelCodigoACopiar != null)
            {
                if (!string.IsNullOrEmpty(CodigoGeneradoParaMostrar))
                {
                    labelCodigoACopiar.text = CodigoGeneradoParaMostrar;
                }
                else if (network != null && !string.IsNullOrEmpty(network.CurrentJoinCode))
                {
                    labelCodigoACopiar.text = network.CurrentJoinCode;
                }
            }
        }
        // SI VENIMOS DESDE PANTALLAS UI PIDIENDO INGRESAR CÓDIGO:
        else if (AbrirDirectoEnIngresarCodigo)
        {
            AbrirDirectoEnIngresarCodigo = false; // Reset de la bandera

            if (codigoInputField != null)
            {
                codigoInputField.text = "";
            }

            MostrarPanel(ingresarCodPanel); // Activa el panel de ingresar código
        }
        else
        {
            MostrarPanel(modoMultiplayerPanel);
        }

        if (feedbackCopiarText != null)
        {
            feedbackCopiarText.gameObject.SetActive(false);
        }

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }
        if (panelSinInternet != null)
            panelSinInternet.SetActive(false);

        if (panelCodigoInvalido != null)
            panelCodigoInvalido.SetActive(false);

        ActualizarUI(
            jugador1Conectado: false,
            jugador2Conectado: false,
            jugador1Listo: false,
            jugador2Listo: false
        );
    }


    private void Update()
    {
        if (!estoyEnSala)
        {
            presenciaActiva = false;
            return;
        }

        if (!presenciaActiva)
        {
            presenciaActiva = true;
            presence.Reset();
            removiendo.Clear();
        }

        ActualizarEstadoSala();
        PublicarPersonajeSiCambio();
        AplicarConfiguracionDeSesion();
        LeerCountdownDeSesion();
        ActualizarCountdownVisual();
    }

    // =========================================================
    // CREAR SALA
    // =========================================================

    public async void OnClick_CrearSala()
    {
        if (!VerificarInternet())
            return;

        if (network == null)
            network = NetworkBootstrap.Instance;

        if (network == null)
        {
            Debug.LogError("[Lobby] No se encontró NetworkBootstrap.");
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            MostrarSinInternet();
            return;
        }

        try
        {
            string codigo = await network.StartHost();

            if (string.IsNullOrEmpty(codigo))
            {
                Debug.LogError("[Lobby] No se pudo crear la sala.");
                MostrarSinInternet();
                return;
            }

            estoyEnSala = true;
            tiempoCountdown = -1f;

            // Guardamos el código de forma persistente
            CodigoGeneradoParaMostrar = codigo;

            await EscribirConfiguracionPartida();

            if (labelCodigoACopiar != null)
            {
                labelCodigoACopiar.text = codigo;
            }

            miReady = false;
            await CambiarReady(false);
            ActualizarBotonReady(false);

            Debug.Log("[Lobby] Sala creada. Código: " + codigo);

            if (SceneManager.GetActiveScene().name == "PantallasUI")
            {
                AbrirDirectoEnSalaEspera = true;
                SceneManager.LoadScene("NetworkLobby");
            }
            else
            {
                MostrarPanel(salaEsperaPanel);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[Lobby] Error creando sala: " + e.Message);
            MostrarSinInternet();
        }
    }

    // =========================================================
    // UNIRSE A SALA
    // =========================================================

    public void OnClick_IrAIngresarCodigo()
    {
        if (!VerificarInternet())
            return;

        // Si estamos en la escena "PantallasUI", cargamos "NetworkLobby"
        if (SceneManager.GetActiveScene().name == "PantallasUI")
        {
            AbrirDirectoEnIngresarCodigo = true;
            SceneManager.LoadScene("NetworkLobby");
        }
        else
        {
            // Si ya estábamos dentro de NetworkLobby:
            if (codigoInputField != null)
            {
                codigoInputField.text = "";
            }
            MostrarPanel(ingresarCodPanel);
        }
    }

    public async void OnClick_ConfirmarUnirseConCodigo()
    {
        if (!VerificarInternet())
            return;
            
        if (codigoInputField == null)
            return;

       /* string codigo =
            codigoInputField.text.Trim();*/

        string codigo = codigoInputField.text.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(codigo))
        {
            Debug.LogWarning(
                "[Lobby] Ingresá un código."
            );

            return;
        }

        if (network == null)
            network = NetworkBootstrap.Instance;

        if (network == null)
        {
            Debug.LogError(
                "[Lobby] No se encontró NetworkBootstrap."
            );

            return;
        }

        try
        {
            bool conectado =
                await network.StartClient(codigo);

            if (!conectado)
            {
                Debug.LogError(
                    "[Lobby] No se pudo conectar."
                );
                MostrarCodigoInvalido();

                switch (network.LastJoinFailReason)
                {
                    case NetworkBootstrap.JoinFailReason.SessionFull:
                        MostrarMensaje(
                            "SALA LLENA",
                            "La sala ya tiene el máximo de " + NetworkBootstrap.MaxPlayersPerRoom + " jugadores."
                        );
                        break;

                    case NetworkBootstrap.JoinFailReason.NotFound:
                        MostrarMensaje(
                            "SALA NO ENCONTRADA",
                            "No existe una sala con ese código. Revisalo e intentá de nuevo."
                        );
                        break;

                    default:
                        MostrarMensaje(
                            "NO SE PUDO ENTRAR",
                            "No se pudo conectar a la sala. Intentá de nuevo."
                        );
                        break;
                }

                return;
            }

            estoyEnSala = true;
            tiempoCountdown = -1f;

            miReady = false;

            await CambiarReady(false);

            ActualizarBotonReady(false);

            if (labelCodigoACopiar != null)
            {
                labelCodigoACopiar.text =
                    network.CurrentJoinCode;
            }

            MostrarPanel(salaEsperaPanel);

            Debug.Log(
                "[Lobby] Unido a la sala: " +
                codigo
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Lobby] Error uniéndose: " +
                e.Message
            );

            MostrarCodigoInvalido();
        }
    }

    // =========================================================
    // BOTÓN LISTO
    // =========================================================

    public async void OnClick_Listo()
    {
        if (network == null)
            network = NetworkBootstrap.Instance;

        if (network == null)
            return;

        if (network.CurrentSession == null)
        {
            Debug.LogError(
                "[Lobby] No existe una sesión."
            );

            return;
        }

        miReady = !miReady;

        await CambiarReady(miReady);

        ActualizarBotonReady(miReady);

        Debug.Log(
            "[Lobby] Mi estado Ready: " +
            (miReady
                ? "LISTO"
                : "NO LISTO")
        );
    }

    private async Task CambiarReady(bool listo)
    {
        if (network == null ||
            network.CurrentSession == null)
        {
            return;
        }

        var player =
            network.CurrentSession.CurrentPlayer;

        player.SetProperty(
            READY_PROPERTY,
            new PlayerProperty(
                listo
                    ? "true"
                    : "false"
            )
        );

        // El personaje viaja en el mismo guardado que el Ready.
        ultimoPersonajePublicado = CharacterSelection.SelectedIndex;

        player.SetProperty(
            CHARACTER_PROPERTY,
            new PlayerProperty(
                ultimoPersonajePublicado.ToString()
            )
        );

        // Publica la identidad del usuario junto con Ready/Character.
        string username = PlayerIdentity.GetUsername();

        if (!string.IsNullOrWhiteSpace(username))
        {
            player.SetProperty(
                USERNAME_PROPERTY,
                new PlayerProperty(username)
            );
        }

        await network.CurrentSession
            .SaveCurrentPlayerDataAsync();
    }

    // =========================================================
    // ESTADO DE LA SALA
    // =========================================================

    private void ActualizarEstadoSala()
    {
        if (network == null)
            network = NetworkBootstrap.Instance;

        if (network == null ||
            network.CurrentSession == null)
        {
            return;
        }

        // Presencia: latido propio + detectar jugadores que ya no dan señales (cerraron el juego sin salir).
        presence.Tick(network.CurrentSession);

        if (RevisarCierreDeSala())
            return;

        List<IReadOnlyPlayer> jugadores =
            FiltrarJugadoresVivos(network.CurrentSession.Players);

        bool jugador1Conectado =
            jugadores.Count >= 1;

        bool jugador2Conectado =
            jugadores.Count >= 2;

        bool jugador1Listo = false;
        bool jugador2Listo = false;

        if (jugador1Conectado)
        {
            jugador1Listo =
                ObtenerReadyJugador(
                    jugadores[0]
                );
        }

        if (jugador2Conectado)
        {
            jugador2Listo =
                ObtenerReadyJugador(
                    jugadores[1]
                );
        }

        if (slots != null && slots.Length > 0)
        {
            ActualizarSlots(jugadores);
        }
        else
        {
            ActualizarUI(
                jugador1Conectado,
                jugador2Conectado,
                jugador1Listo,
                jugador2Listo
            );
        }

        // Empieza cuando hay al menos 2 jugadores y TODOS los conectados están listos.
        bool todosListos = jugadores.Count >= 2;

        for (int i = 0; todosListos && i < jugadores.Count; i++)
        {
            if (!ObtenerReadyJugador(jugadores[i]))
                todosListos = false;
        }

        // Solo el host inicia la cuenta.
        if (network.CurrentSession.IsHost &&
            todosListos &&
            !countdownIniciado)
        {
            countdownIniciado = true;

            _ = IniciarCountdown();
        }
    }

    private bool ObtenerReadyJugador(
        IReadOnlyPlayer player
    )
    {
        if (player == null)
            return false;

        if (!player.Properties.TryGetValue(
                READY_PROPERTY,
                out var propiedad))
        {
            return false;
        }

        return propiedad.Value == "true";
    }

    // =========================================================
    // UI DE JUGADORES
    // =========================================================

    private void ActualizarUI(
        bool jugador1Conectado,
        bool jugador2Conectado,
        bool jugador1Listo,
        bool jugador2Listo
    )
    {
        if (p1ConexionText != null)
        {
            p1ConexionText.text =
                jugador1Conectado
                    ? "CONECTADO"
                    : "DESCONECTADO";

            p1ConexionText.color =
                jugador1Conectado
                    ? Color.green
                    : colorRojo;
        }

        if (p1ConexionIcon != null)
        {
            p1ConexionIcon.sprite =
                jugador1Conectado
                    ? iconoVerde
                    : iconoRojo;
        }

        if (p1EstadoText != null)
        {
            p1EstadoText.text =
                jugador1Listo
                    ? "ESTADO: LISTO"
                    : "ESTADO: NO LISTO";

            p1EstadoText.color =
                jugador1Listo
                    ? Color.green
                    : colorRojo;
        }

        if (p1EstadoIcon != null)
        {
            p1EstadoIcon.sprite =
                jugador1Listo
                    ? iconoVerde
                    : iconoRojo;
        }

        if (p2ConexionText != null)
        {
            p2ConexionText.text =
                jugador2Conectado
                    ? "CONECTADO"
                    : "DESCONECTADO";

            p2ConexionText.color =
                jugador2Conectado
                    ? Color.green
                    : colorRojo;
        }

        if (p2ConexionIcon != null)
        {
            p2ConexionIcon.sprite =
                jugador2Conectado
                    ? iconoVerde
                    : iconoRojo;
        }

        if (p2EstadoText != null)
        {
            p2EstadoText.text =
                jugador2Listo
                    ? "ESTADO: LISTO"
                    : "ESTADO: NO LISTO";

            p2EstadoText.color =
                jugador2Listo
                    ? Color.green
                    : colorRojo;
        }

        if (p2EstadoIcon != null)
        {
            p2EstadoIcon.sprite =
                jugador2Listo
                    ? iconoVerde
                    : iconoRojo;
        }
    }

    // =========================================================
    // SLOTS CON PERSONAJE
    // =========================================================

    private void ConfigurarSlots()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            slots[i].Configure(
                catalogoPersonajes,
                escenarioPersonajes,
                colorRojo,
                iconoVerde,
                iconoRojo
            );

            slots[i].SetEmpty();
        }
    }

    private void ActualizarSlots(IReadOnlyList<IReadOnlyPlayer> jugadores)
    {
        string miId = network.CurrentSession.CurrentPlayer.Id;
        string hostId = network.CurrentSession.Host;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            if (i >= jugadores.Count)
            {
                slots[i].SetEmpty();
                continue;
            }

            IReadOnlyPlayer p = jugadores[i];

            slots[i].SetPlayer(
                p.Id == miId,
                p.Id == hostId,
                ObtenerReadyJugador(p),
                ObtenerPersonajeJugador(p),
                ObtenerUsernameJugador(p)
            );
        }
    }

    private int ObtenerPersonajeJugador(IReadOnlyPlayer player)
    {
        if (player != null &&
            player.Properties.TryGetValue(CHARACTER_PROPERTY, out var propiedad) &&
            int.TryParse(propiedad.Value, out int indice))
        {
            return indice;
        }

        return 0;
    }

    private string ObtenerUsernameJugador(IReadOnlyPlayer player)
    {
        if (player != null &&
            player.Properties.TryGetValue(USERNAME_PROPERTY, out var propiedad) &&
            !string.IsNullOrWhiteSpace(propiedad.Value))
        {
            return propiedad.Value.Trim();
        }

        return string.Empty;
    }

    // Si el jugador local cambió de personaje en el selector, lo publica (máx. 1 vez cada 0.5 s para no saturar el servicio).
    private void PublicarPersonajeSiCambio()
    {
        if (CharacterSelection.SelectedIndex == ultimoPersonajePublicado)
            return;

        if (Time.unscaledTime < proximoPublicarPersonaje)
            return;

        proximoPublicarPersonaje = Time.unscaledTime + 0.5f;

        _ = PublicarPersonaje();
    }

    private async Task PublicarPersonaje()
    {
        if (network == null ||
            network.CurrentSession == null)
        {
            return;
        }

        ultimoPersonajePublicado = CharacterSelection.SelectedIndex;

        network.CurrentSession.CurrentPlayer.SetProperty(
            CHARACTER_PROPERTY,
            new PlayerProperty(
                ultimoPersonajePublicado.ToString()
            )
        );

        string username = PlayerIdentity.GetUsername();

        if (!string.IsNullOrWhiteSpace(username))
        {
            network.CurrentSession.CurrentPlayer.SetProperty(
                USERNAME_PROPERTY,
                new PlayerProperty(username)
            );
        }

        await network.CurrentSession
            .SaveCurrentPlayerDataAsync();
    }

    private void ActualizarBotonReady(bool listo)
    {
        if (btnPlayMultiplayerText != null)
        {
            btnPlayMultiplayerText.text =
                listo
                    ? "NO LISTO"
                    : "LISTO";
        }

        if (btnPlayMultiplayer != null)
        {
            btnPlayMultiplayer.interactable = true;
        }

        if (btnPersonaje != null)
        {
            btnPersonaje.interactable = !listo;
        }
    }

    // =========================================================
    // COUNTDOWN
    // =========================================================

    private async Task IniciarCountdown()
    {
        if (network == null ||
            network.CurrentSession == null ||
            !network.CurrentSession.IsHost)
        {
            countdownIniciado = false;
            return;
        }

        // El host escribe una única vez
        // cuándo debe terminar la cuenta.
        DateTime countdownEndTime =
            DateTime.UtcNow.AddSeconds(5);

        await EscribirConfiguracionPartida();

        await EscribirCountdown(
            countdownEndTime.Ticks
        );

        while (true)
        {
            double remainingSeconds =
                (countdownEndTime - DateTime.UtcNow)
                    .TotalSeconds;

            tiempoCountdown =
                Mathf.Max(
                    0f,
                    (float)remainingSeconds
                );

            ActualizarCountdownVisual();

            if (remainingSeconds <= 0d)
                break;

            // Solo espera localmente; no escribe
            // propiedades online cada segundo.
            await Task.Delay(50);
        }

        if (network != null &&
            network.CurrentSession != null &&
            network.CurrentSession.IsHost)
        {
            await network.StartMultiplayerGame();
        }
    }

    private async Task EscribirCountdown(
        long endUtcTicks
    )
    {
        if (network == null ||
            network.CurrentSession == null ||
            !network.CurrentSession.IsHost)
        {
            return;
        }

        var hostSession =
            network.CurrentSession.AsHost();

        hostSession.SetProperty(
            COUNTDOWN_PROPERTY,
            new SessionProperty(
                endUtcTicks.ToString()
            )
        );

        await hostSession.SavePropertiesAsync();
    }

    // HOST: publica en la sesion lo que eligio (escenario, modo y dificultad).
    private async Task EscribirConfiguracionPartida()
    {
        if (network == null ||
            network.CurrentSession == null ||
            !network.CurrentSession.IsHost)
        {
            return;
        }

        ConfiguracionPartidaSeleccionada config =
            ConfiguracionPartidaSeleccionada.Instancia;

        if (config == null)
            return;

        var hostSession = network.CurrentSession.AsHost();

        hostSession.SetProperty(
            ESCENARIO_PROPERTY,
            new SessionProperty(((int)config.escenarioSeleccionado).ToString())
        );

        hostSession.SetProperty(
            MODO_PROPERTY,
            new SessionProperty(((int)config.modoSeleccionado).ToString())
        );

        hostSession.SetProperty(
            DIFICULTAD_PROPERTY,
            new SessionProperty(((int)config.dificultadSeleccionada).ToString())
        );

        await hostSession.SavePropertiesAsync();
    }

    // INVITADO: copia a su configuracion local lo que eligio el host.
    private void AplicarConfiguracionDeSesion()
    {
        if (network == null ||
            network.CurrentSession == null ||
            network.CurrentSession.IsHost)
        {
            return;
        }

        ConfiguracionPartidaSeleccionada config =
            ConfiguracionPartidaSeleccionada.Instancia;

        if (config == null)
            return;

        var props = network.CurrentSession.Properties;

        if (props.TryGetValue(ESCENARIO_PROPERTY, out var escProp) &&
            int.TryParse(escProp.Value, out int escValor))
        {
            config.escenarioSeleccionado =
                (ConfiguracionPartidaSeleccionada.Escenario)escValor;
        }

        if (props.TryGetValue(MODO_PROPERTY, out var modoProp) &&
            int.TryParse(modoProp.Value, out int modoValor))
        {
            config.modoSeleccionado =
                (ConfiguracionPartidaSeleccionada.ModoJuego)modoValor;
        }

        if (props.TryGetValue(DIFICULTAD_PROPERTY, out var difProp) &&
            int.TryParse(difProp.Value, out int difValor))
        {
            config.dificultadSeleccionada =
                (ConfiguracionPartidaSeleccionada.Dificultad)difValor;
        }

        if (DifficultyManager.Instance != null)
            DifficultyManager.Instance.SincronizarConSeleccion();
    }

    private void LeerCountdownDeSesion()
    {
        if (network == null ||
            network.CurrentSession == null)
        {
            return;
        }

        if (!network.CurrentSession.Properties.TryGetValue(
                COUNTDOWN_PROPERTY,
                out var countdownProperty))
        {
            return;
        }

        if (!long.TryParse(
                countdownProperty.Value,
                out long endUtcTicks))
        {
            return;
        }

        DateTime endTime =
            new DateTime(
                endUtcTicks,
                DateTimeKind.Utc
            );

        double remainingSeconds =
            (endTime - DateTime.UtcNow)
                .TotalSeconds;

        if (remainingSeconds <= 0d)
        {
            // La escena va a cambiar enseguida;
            // evita que el cliente quede mostrando 0.
            tiempoCountdown = -1f;

            if (countdownPanel != null)
            {
                countdownPanel.SetActive(false);
            }

            return;
        }

        tiempoCountdown =
            (float)remainingSeconds;
    }

    private void ActualizarCountdownVisual()
    {
        if (tiempoCountdown < 0f)
            return;

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(true);
        }

        if (countdownText != null)
        {
            countdownText.text =
                "LA PARTIDA COMIENZA EN: " +
                Mathf.CeilToInt(
                    tiempoCountdown
                );
        }
    }

    // =========================================================
    // COPIAR CÓDIGO
    // =========================================================

    public void OnClick_CopiarCodigo()
    {
        string textoACopiar =
            labelCodigoACopiar != null &&
            !string.IsNullOrEmpty(
                labelCodigoACopiar.text
            )
                ? labelCodigoACopiar.text.Trim()
                : "";

        if (string.IsNullOrEmpty(textoACopiar))
            return;

        GUIUtility.systemCopyBuffer =
            textoACopiar;

        if (feedbackCopiarText != null)
        {
            feedbackCopiarText.text =
                "Código copiado!";

            feedbackCopiarText.gameObject.SetActive(
                true
            );

            CancelInvoke(
                nameof(OcultarFeedbackCopiado)
            );

            Invoke(
                nameof(OcultarFeedbackCopiado),
                2f
            );
        }
    }

    private void OcultarFeedbackCopiado()
    {
        if (feedbackCopiarText != null)
        {
            feedbackCopiarText.gameObject.SetActive(
                false
            );
        }
    }

    // =========================================================
    // SALIR / VOLVER
    // =========================================================

    public void OnClick_AtrasDesdeIngresarCodigo()
    {
        if (codigoInputField != null)
        {
            codigoInputField.text = "";
        }

        MostrarPanel(modoMultiplayerPanel);
    }

    public async void OnClick_AtrasDesdeSalaEspera()
    {
        await SalirDeSala();

        MostrarPanel(modoMultiplayerPanel);
    }

    public async void OnClick_AtrasAlMenuPrincipal()
    {
        await SalirDeSala();

        PantallasUIController.VolverAElegirModo = true;

        SceneManager.LoadScene(
            "PantallasUI"
        );
    }

    private async Task SalirDeSala()
    {
        estoyEnSala = false;
        countdownIniciado = false;
        tiempoCountdown = -1f;

        if (network != null &&
            network.CurrentSession != null)
        {
            try
            {
                // El anfitrión que sale cierra la sala (avisa a los demás); un invitado solo sale.
                if (network.CurrentSession.IsHost)
                    await network.CerrarSalaComoHost();
                else
                    await network.CurrentSession.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "[Lobby] Error saliendo de sala: " +
                    e.Message
                );
            }
        }

        LimpiarSlots();

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }
    }

    // =========================================================
    // PRESENCIA, SALA CERRADA Y MENSAJES
    // =========================================================

    // Deja solo a los jugadores que siguen en la sala. Si soy el anfitrión, libero el lugar de los que no dan señales.
    private List<IReadOnlyPlayer> FiltrarJugadoresVivos(IReadOnlyList<IReadOnlyPlayer> todos)
    {
        ISession sesion = network.CurrentSession;
        List<IReadOnlyPlayer> vivos = new List<IReadOnlyPlayer>(todos.Count);

        for (int i = 0; i < todos.Count; i++)
        {
            IReadOnlyPlayer p = todos[i];

            if (presence.IsAlive(p, sesion))
            {
                vivos.Add(p);
            }
            else if (sesion.IsHost && removiendo.Add(p.Id))
            {
                _ = QuitarJugador(p.Id);
            }
        }

        return vivos;
    }

    private async Task QuitarJugador(string playerId)
    {
        try
        {
            await network.CurrentSession.AsHost().RemovePlayerAsync(playerId);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Lobby] No pude quitar al jugador desconectado: " + e.Message);

            // Se reintenta más adelante.
            await Task.Delay(5000);
            removiendo.Remove(playerId);
        }
    }

    // INVITADO: ¿el anfitrión cerró la sala o dejó de dar señales? Devuelve true si ya se está saliendo.
    private bool RevisarCierreDeSala()
    {
        ISession sesion = network.CurrentSession;

        if (sesion.IsHost || saliendoPorCierre)
            return saliendoPorCierre;

        // 1) El anfitrión avisó que cierra.
        if (sesion.Properties.TryGetValue(NetworkBootstrap.CerradaProperty, out var cerrada) &&
            cerrada.Value == "1")
        {
            ManejarSalaCerrada("SALA CERRADA", "El anfitrión cerró la sala.");
            return true;
        }

        // 2) El anfitrión cerró el juego sin avisar: dejó de latir.
        string hostId = sesion.Host;
        IReadOnlyList<IReadOnlyPlayer> todos = sesion.Players;

        for (int i = 0; i < todos.Count; i++)
        {
            if (todos[i].Id == hostId && !presence.IsAlive(todos[i], sesion))
            {
                ManejarSalaCerrada("SALA CERRADA", "Se perdió la conexión con el anfitrión.");
                return true;
            }
        }

        return false;
    }

    private async void ManejarSalaCerrada(string titulo, string texto)
    {
        if (saliendoPorCierre)
            return;

        saliendoPorCierre = true;

        await SalirDeSala();

        MostrarPanel(modoMultiplayerPanel);
        MostrarMensaje(titulo, texto);

        saliendoPorCierre = false;
    }

    private void LimpiarSlots()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].SetEmpty();
        }
    }

    // Ventana emergente genérica con un botón ACEPTAR (que cierra la ventana).
    private void MostrarMensaje(string titulo, string texto)
    {
        if (popupMensaje == null)
        {
            Debug.LogWarning("[Lobby] " + titulo + ": " + texto + " (asigná 'Popup Mensaje' en el controlador para mostrarlo en pantalla)");
            return;
        }

        if (popupMensajeTitulo != null)
            popupMensajeTitulo.text = titulo;

        if (popupMensajeTexto != null)
            popupMensajeTexto.text = texto;

        popupMensaje.SetActive(true);
    }

    // =========================================================
    // PANELES
    // =========================================================

    private void MostrarPanel(GameObject panelActivo)
    {
        if (modoMultiplayerPanel != null)
        {
            modoMultiplayerPanel.SetActive(
                modoMultiplayerPanel == panelActivo
            );
        }

        if (ingresarCodPanel != null)
        {
            ingresarCodPanel.SetActive(
                ingresarCodPanel == panelActivo
            );
        }

        if (salaEsperaPanel != null)
        {
            salaEsperaPanel.SetActive(
                salaEsperaPanel == panelActivo
            );
        }
    }

    private void OnDestroy()
    {
        if (botonCopiarCodigo != null)
        {
            botonCopiarCodigo.onClick.RemoveListener(
                OnClick_CopiarCodigo
            );
        }

        if (codigoInputField != null)
            codigoInputField.onValueChanged.RemoveListener(OcultarCodigoInvalidoSiVacio);
    }

    private void MostrarSinInternet()
    {
        if (panelSinInternet != null)
            panelSinInternet.SetActive(true);
    }

    public bool VerificarInternet()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
            return true;

        MostrarSinInternet();
        return false;
    }

// Botón "Aceptar" del cartel
    public void OnClick_CerrarSinInternet()
    {
        if (panelSinInternet != null)
            panelSinInternet.SetActive(false);

        
    }

    private void MostrarCodigoInvalido()
    {
        if (panelCodigoInvalido != null)
            panelCodigoInvalido.SetActive(true);
    }

    private void OcultarCodigoInvalidoSiVacio(string texto)
{
    if (panelCodigoInvalido != null)
        panelCodigoInvalido.SetActive(false);
}
    
}