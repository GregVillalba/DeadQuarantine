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
    [Header("--- PANELES ---")]
    [SerializeField] private GameObject modoMultiplayerPanel;
    [SerializeField] private GameObject ingresarCodPanel;
    [SerializeField] private GameObject salaEsperaPanel;

    [Header("--- NETWORK ---")]
    [SerializeField] private NetworkBootstrap network;

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
    }

    private void Start()
    {
        MostrarPanel(modoMultiplayerPanel);

        if (feedbackCopiarText != null)
        {
            feedbackCopiarText.gameObject.SetActive(
                false
            );
        }

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }

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
            return;

        ActualizarEstadoSala();
        LeerCountdownDeSesion();
        ActualizarCountdownVisual();
    }

    // =========================================================
    // CREAR SALA
    // =========================================================

    public async void OnClick_CrearSala()
    {
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
            string codigo =
                await network.StartHost();

            if (string.IsNullOrEmpty(codigo))
            {
                Debug.LogError(
                    "[Lobby] No se pudo crear la sala."
                );

                return;
            }

            estoyEnSala = true;
            tiempoCountdown = -1f;

            if (labelCodigoACopiar != null)
            {
                labelCodigoACopiar.text = codigo;
            }

            miReady = false;

            await CambiarReady(false);

            ActualizarBotonReady(false);

            MostrarPanel(salaEsperaPanel);

            Debug.Log(
                "[Lobby] Sala creada. Código: " +
                codigo
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Lobby] Error creando sala: " +
                e.Message
            );
        }
    }

    // =========================================================
    // UNIRSE A SALA
    // =========================================================

    public void OnClick_IrAIngresarCodigo()
    {
        if (codigoInputField != null)
        {
            codigoInputField.text = "";
        }

        MostrarPanel(ingresarCodPanel);
    }

    public async void OnClick_ConfirmarUnirseConCodigo()
    {
        if (codigoInputField == null)
            return;

        string codigo =
            codigoInputField.text.Trim();

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

        var jugadores =
            network.CurrentSession.Players;

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

        ActualizarUI(
            jugador1Conectado,
            jugador2Conectado,
            jugador1Listo,
            jugador2Listo
        );

        // Solo el host inicia la cuenta.
        if (network.CurrentSession.IsHost &&
            jugador1Conectado &&
            jugador2Conectado &&
            jugador1Listo &&
            jugador2Listo &&
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

        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }
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
    }
}