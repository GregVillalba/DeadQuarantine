using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class MultiplayerLobbyController : MonoBehaviour
{
    [Header("--- PANELES ---")]
    [SerializeField] private GameObject menuPrincipalPanel;
    [SerializeField] private GameObject elegirModoPanel;
    [SerializeField] private GameObject modoMultiplayerPanel;
    [SerializeField] private GameObject ingresarCodPanel;
    [SerializeField] private GameObject salaEsperaPanel;

    [Header("--- INPUT Y VALIDACIÓN DE CÓDIGO ---")]
    [SerializeField] private TMP_InputField codigoInputField;
    [SerializeField] private string codigoCorrecto = "1234";

    [Header("--- COPIAR CÓDIGO (BOTÓN + LABEL) ---")]
    [SerializeField] private Button botonCopiarCodigo;
    [SerializeField] private TMP_Text labelCodigoACopiar;
    [SerializeField] private TMP_Text feedbackCopiarText;

    [Header("--- BORDES / OUTLINES DE LAS TARJETAS ---")]
    [Tooltip("Arrastra aquí el componente Outline de la tarjeta Jugador 1")]
    [SerializeField] private Outline p1Outline;
    [Tooltip("Arrastra aquí el componente Outline de la tarjeta Jugador 2")]
    [SerializeField] private Outline p2Outline;

    [Header("--- CONJUNTOS DE TEXTO E IMAGEN (ESTADOS) ---")]
    [Tooltip("Conjunto verde de Jugador 1 (Icono + Texto 'ESTADO: LISTO')")]
    [SerializeField] private GameObject p1ConjuntoConectado;

    [Tooltip("Conjunto rojo de Jugador 2 (Icono + Texto 'ESTADO: NO LISTO')")]
    [SerializeField] private GameObject p2ConjuntoEsperando;
    [Tooltip("Conjunto verde de Jugador 2 (Icono + Texto 'ESTADO: LISTO')")]
    [SerializeField] private GameObject p2ConjuntoConectado;

    [Header("--- COLORES DE CONTORNO ---")]
    [SerializeField] private Color outlineVerde = new Color(0.2f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color outlineRojo = new Color(0.85f, 0.2f, 0.2f, 1f);

    [Header("--- BOTÓN COMENZAR PARTIDA ---")]
    [SerializeField] private Button btnPlayMultiplayer;
    [SerializeField] private string nombreEscenaJuego = "GameScene";

    private bool isPlayer1Active = false;
    private bool isPlayer2Active = false;

    private void Awake()
    {
        if (botonCopiarCodigo != null)
        {
            botonCopiarCodigo.onClick.AddListener(OnClick_CopiarCodigo);
        }
    }

    private void Start()
    {
        if (menuPrincipalPanel != null)
        {
            AbrirSoloPanel(menuPrincipalPanel);
        }
        if (feedbackCopiarText != null) feedbackCopiarText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (botonCopiarCodigo != null)
        {
            botonCopiarCodigo.onClick.RemoveListener(OnClick_CopiarCodigo);
        }
    }

    #region LÓGICA DE SALA DE ESPERA (CAMBIO DE OUTLINE Y CONJUNTOS)

    private void ConfigurarSalaEspera(bool jugador1Listo, bool jugador2Listo)
    {
        isPlayer1Active = jugador1Listo;
        isPlayer2Active = jugador2Listo;

        // Jugador 1: Borde verde y conjunto listo activo
        if (p1Outline != null) p1Outline.effectColor = outlineVerde;
        if (p1ConjuntoConectado != null) p1ConjuntoConectado.SetActive(true);

        // Jugador 2: Cambia solo el outline y conmuta los conjuntos de UI
        if (isPlayer2Active)
        {
            // El 2do usuario llegó -> Borde cambia a VERDE
            if (p2Outline != null) p2Outline.effectColor = outlineVerde;
            if (p2ConjuntoEsperando != null) p2ConjuntoEsperando.SetActive(false);
            if (p2ConjuntoConectado != null) p2ConjuntoConectado.SetActive(true);
        }
        else
        {
            // El 2do usuario aún no llegó -> Borde se mantiene en ROJO
            if (p2Outline != null) p2Outline.effectColor = outlineRojo;
            if (p2ConjuntoEsperando != null) p2ConjuntoEsperando.SetActive(true);
            if (p2ConjuntoConectado != null) p2ConjuntoConectado.SetActive(false);
        }

        ActualizarEstadoBotonPlay();
    }

    // Método para simular o recibir por red que el Jugador 2 se conectó
    public void SetPlayer2Conectado(bool conectado)
    {
        ConfigurarSalaEspera(isPlayer1Active, conectado);
    }

    private void ActualizarEstadoBotonPlay()
    {
        if (btnPlayMultiplayer != null)
        {
            btnPlayMultiplayer.interactable = (isPlayer1Active && isPlayer2Active);
        }
    }

    #endregion

    #region ACCIONES DE BOTONES Y NAVEGACIÓN

    public void OnClick_CopiarCodigo()
    {
        string textoACopiar = (labelCodigoACopiar != null && !string.IsNullOrEmpty(labelCodigoACopiar.text))
            ? labelCodigoACopiar.text.Trim()
            : codigoCorrecto;

        GUIUtility.systemCopyBuffer = textoACopiar;

        if (feedbackCopiarText != null)
        {
            feedbackCopiarText.text = "¡Código copiado!";
            feedbackCopiarText.gameObject.SetActive(true);
            CancelInvoke(nameof(OcultarFeedbackCopiado));
            Invoke(nameof(OcultarFeedbackCopiado), 2.0f);
        }
    }

    private void OcultarFeedbackCopiado()
    {
        if (feedbackCopiarText != null) feedbackCopiarText.gameObject.SetActive(false);
    }

    public void OnClick_AbrirModoMultiplayer()
    {
        AbrirSoloPanel(modoMultiplayerPanel);
    }

    public void OnClick_CrearSala()
    {
        // Host crea sala: P1 listo, P2 esperando (borde rojo)
        ConfigurarSalaEspera(jugador1Listo: true, jugador2Listo: false);
        AbrirSoloPanel(salaEsperaPanel);
    }

    public void OnClick_IrAingresarCodigo()
    {
        if (codigoInputField != null) codigoInputField.text = "";
        AbrirSoloPanel(ingresarCodPanel);
    }

    public void OnClick_ConfirmarUnirseConCodigo()
    {
        if (codigoInputField == null) return;

        string codigoIngresado = codigoInputField.text.Trim();

        if (codigoIngresado == codigoCorrecto)
        {
            // Código validado: ambos listos (borde verde)
            ConfigurarSalaEspera(jugador1Listo: true, jugador2Listo: true);
            AbrirSoloPanel(salaEsperaPanel);
        }
    }

    public void OnClick_VolverAModoMultiplayer()
    {
        isPlayer1Active = false;
        isPlayer2Active = false;
        AbrirSoloPanel(modoMultiplayerPanel);
    }

    public void OnClick_VolverAIngresarCodigo()
    {
        isPlayer2Active = false;
        if (codigoInputField != null) codigoInputField.text = "";
        AbrirSoloPanel(ingresarCodPanel);
    }

    public void OnClick_VolverAElegirModo()
    {
        isPlayer1Active = false;
        isPlayer2Active = false;
        AbrirSoloPanel(elegirModoPanel);
    }

    public void OnClick_VolverAMenuPrincipal()
    {
        isPlayer1Active = false;
        isPlayer2Active = false;
        AbrirSoloPanel(menuPrincipalPanel != null ? menuPrincipalPanel : elegirModoPanel);
    }

    public void OnClick_ComenzarPartida()
    {
        if (isPlayer1Active && isPlayer2Active)
        {
            SceneManager.LoadScene(nombreEscenaJuego);
        }
    }

    private void AbrirSoloPanel(GameObject panelActivo)
    {
        if (menuPrincipalPanel != null) menuPrincipalPanel.SetActive(menuPrincipalPanel == panelActivo);
        if (elegirModoPanel != null) elegirModoPanel.SetActive(elegirModoPanel == panelActivo);
        if (modoMultiplayerPanel != null) modoMultiplayerPanel.SetActive(modoMultiplayerPanel == panelActivo);
        if (ingresarCodPanel != null) ingresarCodPanel.SetActive(ingresarCodPanel == panelActivo);
        if (salaEsperaPanel != null) salaEsperaPanel.SetActive(salaEsperaPanel == panelActivo);
    }

    #endregion
}