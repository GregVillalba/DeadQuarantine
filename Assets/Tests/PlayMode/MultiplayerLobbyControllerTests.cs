using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;


public class MultiplayerLobbyControllerTests
{
    private GameObject controllerGO;
    private MultiplayerLobbyController controller;

    private GameObject modoMultiplayerPanel;
    private GameObject ingresarCodPanel;
    private GameObject salaEsperaPanel;
    private GameObject countdownPanel;

    private TMP_InputField codigoInputField;
    private Button botonCopiarCodigo;
    private TMP_Text labelCodigoACopiar;
    private TMP_Text feedbackCopiarText;

    private TMP_Text p1ConexionText;
    private TMP_Text p1EstadoText;
    private Image p1ConexionIcon;
    private Image p1EstadoIcon;

    private TMP_Text p2ConexionText;
    private TMP_Text p2EstadoText;
    private Image p2ConexionIcon;
    private Image p2EstadoIcon;

    private Button btnPlayMultiplayer;
    private TMP_Text btnPlayMultiplayerText;
    private TMP_Text countdownText;

    private Sprite iconoVerde;
    private Sprite iconoRojo;
    private Color colorRojoTest = Color.red;

    private bool teniaVolverAElegirModo;
    private bool volverAElegirModoOriginal;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Backup del estático de PantallasUIController para no dejar
        // basura entre corridas de test.
        teniaVolverAElegirModo = PantallasUIController.VolverAElegirModo;
        volverAElegirModoOriginal = PantallasUIController.VolverAElegirModo;

        controllerGO = new GameObject("MultiplayerLobbyController");

        modoMultiplayerPanel = new GameObject("ModoMultiplayerPanel");
        ingresarCodPanel = new GameObject("IngresarCodPanel");
        salaEsperaPanel = new GameObject("SalaEsperaPanel");
        countdownPanel = new GameObject("CountdownPanel");

        codigoInputField = CrearUI<TMP_InputField>("CodigoInputField");
        botonCopiarCodigo = CrearUI<Button>("BotonCopiarCodigo");
        labelCodigoACopiar = CrearUI<TextMeshProUGUI>("LabelCodigoACopiar");
        feedbackCopiarText = CrearUI<TextMeshProUGUI>("FeedbackCopiarText");

        p1ConexionText = CrearUI<TextMeshProUGUI>("P1ConexionText");
        p1EstadoText = CrearUI<TextMeshProUGUI>("P1EstadoText");
        p1ConexionIcon = CrearUI<Image>("P1ConexionIcon");
        p1EstadoIcon = CrearUI<Image>("P1EstadoIcon");

        p2ConexionText = CrearUI<TextMeshProUGUI>("P2ConexionText");
        p2EstadoText = CrearUI<TextMeshProUGUI>("P2EstadoText");
        p2ConexionIcon = CrearUI<Image>("P2ConexionIcon");
        p2EstadoIcon = CrearUI<Image>("P2EstadoIcon");

        btnPlayMultiplayer = CrearUI<Button>("BtnPlayMultiplayer");
        btnPlayMultiplayerText = CrearUI<TextMeshProUGUI>("BtnPlayMultiplayerText");
        countdownText = CrearUI<TextMeshProUGUI>("CountdownText");

        var tex = new Texture2D(1, 1);
        iconoVerde = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        iconoRojo = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));

        // El componente se agrega al final para setear todos los campos
        // ANTES de que Awake()/Start() se disparen automáticamente.
        SetFieldsAntesDeAgregarComponente();

        yield return null;
    }

    private void SetFieldsAntesDeAgregarComponente()
    {
        controller = controllerGO.AddComponent<MultiplayerLobbyController>();

        SetField("network", null); // forzamos: sin NetworkBootstrap real
        SetField("modoMultiplayerPanel", modoMultiplayerPanel);
        SetField("ingresarCodPanel", ingresarCodPanel);
        SetField("salaEsperaPanel", salaEsperaPanel);
        SetField("countdownPanel", countdownPanel);
        SetField("codigoInputField", codigoInputField);
        SetField("botonCopiarCodigo", botonCopiarCodigo);
        SetField("labelCodigoACopiar", labelCodigoACopiar);
        SetField("feedbackCopiarText", feedbackCopiarText);
        SetField("p1ConexionText", p1ConexionText);
        SetField("p1EstadoText", p1EstadoText);
        SetField("p1ConexionIcon", p1ConexionIcon);
        SetField("p1EstadoIcon", p1EstadoIcon);
        SetField("p2ConexionText", p2ConexionText);
        SetField("p2EstadoText", p2EstadoText);
        SetField("p2ConexionIcon", p2ConexionIcon);
        SetField("p2EstadoIcon", p2EstadoIcon);
        SetField("btnPlayMultiplayer", btnPlayMultiplayer);
        SetField("btnPlayMultiplayerText", btnPlayMultiplayerText);
        SetField("countdownText", countdownText);
        SetField("iconoVerde", iconoVerde);
        SetField("iconoRojo", iconoRojo);
        SetField("colorRojo", colorRojoTest);

        // Re-disparamos Awake/Start manualmente para que tomen los
        // campos recién asignados (AddComponent ya los ejecutó antes).
        Invoke("Awake");
        Invoke("Start");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        PantallasUIController.VolverAElegirModo = volverAElegirModoOriginal;

        if (controllerGO != null) Object.Destroy(controllerGO);
        foreach (var go in new[]
        {
            modoMultiplayerPanel, ingresarCodPanel, salaEsperaPanel, countdownPanel,
            codigoInputField?.gameObject, botonCopiarCodigo?.gameObject,
            labelCodigoACopiar?.gameObject, feedbackCopiarText?.gameObject,
            p1ConexionText?.gameObject, p1EstadoText?.gameObject,
            p1ConexionIcon?.gameObject, p1EstadoIcon?.gameObject,
            p2ConexionText?.gameObject, p2EstadoText?.gameObject,
            p2ConexionIcon?.gameObject, p2EstadoIcon?.gameObject,
            btnPlayMultiplayer?.gameObject, btnPlayMultiplayerText?.gameObject,
            countdownText?.gameObject
        })
        {
            if (go != null) Object.Destroy(go);
        }

        yield return null;
    }

    // ---------------------------------------------------------------
    // Awake (registro del listener) + OnClick_CopiarCodigo
    // ---------------------------------------------------------------

    [Test]
    public void Awake_ConectaElBotonCopiarCodigoConOnClickCopiarCodigo()
    {
        labelCodigoACopiar.text = "ABC123";

        botonCopiarCodigo.onClick.Invoke();

        Assert.AreEqual("ABC123", GUIUtility.systemCopyBuffer);
        Assert.IsTrue(feedbackCopiarText.gameObject.activeSelf);
    }

    // ---------------------------------------------------------------
    // Start
    // ---------------------------------------------------------------

    [Test]
    public void Start_MuestraSoloElPanelDeModoMultiplayer()
    {
        Assert.IsTrue(modoMultiplayerPanel.activeSelf);
        Assert.IsFalse(ingresarCodPanel.activeSelf);
        Assert.IsFalse(salaEsperaPanel.activeSelf);
    }

    [Test]
    public void Start_OcultaFeedbackYCountdownPanel()
    {
        Assert.IsFalse(feedbackCopiarText.gameObject.activeSelf);
        Assert.IsFalse(countdownPanel.activeSelf);
    }

    [Test]
    public void Start_InicializaLaUIComoDesconectadoYNoListo()
    {
        Assert.AreEqual("DESCONECTADO", p1ConexionText.text);
        Assert.AreEqual("ESTADO: NO LISTO", p1EstadoText.text);
        Assert.AreEqual("DESCONECTADO", p2ConexionText.text);
        Assert.AreEqual("ESTADO: NO LISTO", p2EstadoText.text);
    }

    // ---------------------------------------------------------------
    // Update (caso por defecto: estoyEnSala == false)
    // ---------------------------------------------------------------

    [Test]
    public void Update_SinEstarEnSala_NoRompeYNoActivaElCountdown()
    {
        Assert.DoesNotThrow(() => Invoke("Update"));
        Assert.IsFalse(countdownPanel.activeSelf);
    }

    // ---------------------------------------------------------------
    // OnClick_IrAIngresarCodigo
    // ---------------------------------------------------------------

    [Test]
    public void OnClickIrAIngresarCodigo_LimpiaElInputYMuestraElPanel()
    {
        codigoInputField.text = "algo";

        controller.OnClick_IrAIngresarCodigo();

        Assert.AreEqual("", codigoInputField.text);
        Assert.IsTrue(ingresarCodPanel.activeSelf);
        Assert.IsFalse(modoMultiplayerPanel.activeSelf);
    }

    // ---------------------------------------------------------------
    // OnClick_CrearSala (solo guard: sin NetworkBootstrap)
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator OnClickCrearSala_SinNetworkBootstrap_NoCambiaDeEstado()
    {
        controller.OnClick_CrearSala();
        yield return null;

        Assert.IsFalse((bool)GetField("estoyEnSala"));
        Assert.IsFalse(salaEsperaPanel.activeSelf);
    }

    // ---------------------------------------------------------------
    // OnClick_ConfirmarUnirseConCodigo (solo guards)
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator OnClickConfirmarUnirse_ConCodigoVacio_NoHaceNada()
    {
        codigoInputField.text = "   ";

        controller.OnClick_ConfirmarUnirseConCodigo();
        yield return null;

        Assert.IsFalse((bool)GetField("estoyEnSala"));
    }

    [UnityTest]
    public IEnumerator OnClickConfirmarUnirse_SinNetworkBootstrap_NoCambiaDeEstado()
    {
        codigoInputField.text = "ABCDE";

        controller.OnClick_ConfirmarUnirseConCodigo();
        yield return null;

        Assert.IsFalse((bool)GetField("estoyEnSala"));
    }

    // ---------------------------------------------------------------
    // OnClick_Listo (solo guard: sin NetworkBootstrap)
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator OnClickListo_SinNetworkBootstrap_NoCambiaMiReady()
    {
        controller.OnClick_Listo();
        yield return null;

        Assert.IsFalse((bool)GetField("miReady"));
    }

    // ---------------------------------------------------------------
    // CambiarReady (privado, solo guard)
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator CambiarReady_SinNetworkBootstrap_TerminaSinRomper()
    {
        var task = (Task)InvokeReturning("CambiarReady", true);
        yield return new WaitUntil(() => task.IsCompleted);

        Assert.IsFalse(task.IsFaulted);
    }

    // ---------------------------------------------------------------
    // ActualizarEstadoSala (privado, solo guard)
    // ---------------------------------------------------------------

    [Test]
    public void ActualizarEstadoSala_SinNetworkBootstrap_NoRompe()
    {
        Assert.DoesNotThrow(() => Invoke("ActualizarEstadoSala"));
    }

    // ---------------------------------------------------------------
    // ObtenerReadyJugador (solo rama player == null)
    // ---------------------------------------------------------------

    [Test]
    public void ObtenerReadyJugador_ConJugadorNull_DevuelveFalse()
    {
        bool resultado = (bool)InvokeReturning("ObtenerReadyJugador", new object[] { null });

        Assert.IsFalse(resultado);
    }

    // ---------------------------------------------------------------
    // ActualizarUI
    // ---------------------------------------------------------------

    [Test]
    public void ActualizarUI_AmbosConectadosYListos_MuestraEstadoPositivo()
    {
        Invoke("ActualizarUI", true, true, true, true);

        Assert.AreEqual("CONECTADO", p1ConexionText.text);
        Assert.AreEqual(Color.green, p1ConexionText.color);
        Assert.AreSame(iconoVerde, p1ConexionIcon.sprite);

        Assert.AreEqual("ESTADO: LISTO", p1EstadoText.text);
        Assert.AreEqual(Color.green, p1EstadoText.color);
        Assert.AreSame(iconoVerde, p1EstadoIcon.sprite);

        Assert.AreEqual("CONECTADO", p2ConexionText.text);
        Assert.AreEqual("ESTADO: LISTO", p2EstadoText.text);
    }

    [Test]
    public void ActualizarUI_NingunoConectado_MuestraEstadoNegativo()
    {
        Invoke("ActualizarUI", false, false, false, false);

        Assert.AreEqual("DESCONECTADO", p1ConexionText.text);
        Assert.AreEqual(colorRojoTest, p1ConexionText.color);
        Assert.AreSame(iconoRojo, p1ConexionIcon.sprite);

        Assert.AreEqual("ESTADO: NO LISTO", p1EstadoText.text);
        Assert.AreEqual(colorRojoTest, p1EstadoText.color);
        Assert.AreSame(iconoRojo, p1EstadoIcon.sprite);

        Assert.AreEqual("DESCONECTADO", p2ConexionText.text);
        Assert.AreEqual("ESTADO: NO LISTO", p2EstadoText.text);
    }

    // ---------------------------------------------------------------
    // ActualizarBotonReady
    // ---------------------------------------------------------------

    [Test]
    public void ActualizarBotonReady_ConListoTrue_MuestraTextoNoListo()
    {
        Invoke("ActualizarBotonReady", true);

        Assert.AreEqual("NO LISTO", btnPlayMultiplayerText.text);
        Assert.IsTrue(btnPlayMultiplayer.interactable);
    }

    [Test]
    public void ActualizarBotonReady_ConListoFalse_MuestraTextoListo()
    {
        Invoke("ActualizarBotonReady", false);

        Assert.AreEqual("LISTO", btnPlayMultiplayerText.text);
        Assert.IsTrue(btnPlayMultiplayer.interactable);
    }

    // ---------------------------------------------------------------
    // IniciarCountdown / EscribirCountdown / LeerCountdownDeSesion
    // (privados, solo guard)
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator IniciarCountdown_SinNetworkBootstrap_ReseteaCountdownIniciado()
    {
        SetField("countdownIniciado", true);

        var task = (Task)InvokeReturning("IniciarCountdown");
        yield return new WaitUntil(() => task.IsCompleted);

        Assert.IsFalse((bool)GetField("countdownIniciado"));
    }

    [UnityTest]
    public IEnumerator EscribirCountdown_SinNetworkBootstrap_TerminaSinRomper()
    {
        var task = (Task)InvokeReturning("EscribirCountdown", 12345L);
        yield return new WaitUntil(() => task.IsCompleted);

        Assert.IsFalse(task.IsFaulted);
    }

    [Test]
    public void LeerCountdownDeSesion_SinNetworkBootstrap_NoRompe()
    {
        Assert.DoesNotThrow(() => Invoke("LeerCountdownDeSesion"));
    }

    // ---------------------------------------------------------------
    // ActualizarCountdownVisual
    // ---------------------------------------------------------------

    [Test]
    public void ActualizarCountdownVisual_ConTiempoNegativo_NoActivaElPanel()
    {
        SetField("tiempoCountdown", -1f);

        Invoke("ActualizarCountdownVisual");

        Assert.IsFalse(countdownPanel.activeSelf);
    }

    [Test]
    public void ActualizarCountdownVisual_ConTiempoPositivo_MuestraElPanelYElTexto()
    {
        SetField("tiempoCountdown", 3.2f);

        Invoke("ActualizarCountdownVisual");

        Assert.IsTrue(countdownPanel.activeSelf);
        Assert.AreEqual("LA PARTIDA COMIENZA EN: 4", countdownText.text);
    }

    // ---------------------------------------------------------------
    // OnClick_CopiarCodigo
    // ---------------------------------------------------------------

    [Test]
    public void OnClickCopiarCodigo_ConTextoValido_CopiaYMuestraFeedback()
    {
        labelCodigoACopiar.text = "  XYZ99  ";

        controller.OnClick_CopiarCodigo();

        Assert.AreEqual("XYZ99", GUIUtility.systemCopyBuffer);
        Assert.AreEqual("Código copiado!", feedbackCopiarText.text);
        Assert.IsTrue(feedbackCopiarText.gameObject.activeSelf);
    }

    [Test]
    public void OnClickCopiarCodigo_SinTexto_NoHaceNada()
    {
        labelCodigoACopiar.text = "";
        feedbackCopiarText.gameObject.SetActive(false);
        GUIUtility.systemCopyBuffer = "valor_previo";

        controller.OnClick_CopiarCodigo();

        Assert.AreEqual("valor_previo", GUIUtility.systemCopyBuffer);
        Assert.IsFalse(feedbackCopiarText.gameObject.activeSelf);
    }

    // ---------------------------------------------------------------
    // OcultarFeedbackCopiado
    // ---------------------------------------------------------------

    [Test]
    public void OcultarFeedbackCopiado_OcultaElTextoDeFeedback()
    {
        feedbackCopiarText.gameObject.SetActive(true);

        Invoke("OcultarFeedbackCopiado");

        Assert.IsFalse(feedbackCopiarText.gameObject.activeSelf);
    }

    // ---------------------------------------------------------------
    // OnClick_AtrasDesdeIngresarCodigo
    // ---------------------------------------------------------------

    [Test]
    public void OnClickAtrasDesdeIngresarCodigo_LimpiaInputYVuelveAModoMultiplayer()
    {
        codigoInputField.text = "abc";
        ingresarCodPanel.SetActive(true);
        modoMultiplayerPanel.SetActive(false);

        controller.OnClick_AtrasDesdeIngresarCodigo();

        Assert.AreEqual("", codigoInputField.text);
        Assert.IsTrue(modoMultiplayerPanel.activeSelf);
        Assert.IsFalse(ingresarCodPanel.activeSelf);
    }

    // ---------------------------------------------------------------
    // SalirDeSala (privado, solo guard) + OnClick_AtrasDesdeSalaEspera
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator SalirDeSala_SinNetworkBootstrap_ReseteaEstadoYOcultaCountdown()
    {
        SetField("estoyEnSala", true);
        SetField("countdownIniciado", true);
        SetField("tiempoCountdown", 3f);
        countdownPanel.SetActive(true);

        var task = (Task)InvokeReturning("SalirDeSala");
        yield return new WaitUntil(() => task.IsCompleted);

        Assert.IsFalse((bool)GetField("estoyEnSala"));
        Assert.IsFalse((bool)GetField("countdownIniciado"));
        Assert.AreEqual(-1f, (float)GetField("tiempoCountdown"));
        Assert.IsFalse(countdownPanel.activeSelf);
    }

    [UnityTest]
    public IEnumerator OnClickAtrasDesdeSalaEspera_VuelveAModoMultiplayer()
    {
        salaEsperaPanel.SetActive(true);
        modoMultiplayerPanel.SetActive(false);

        controller.OnClick_AtrasDesdeSalaEspera();
        yield return null;

        Assert.IsTrue(modoMultiplayerPanel.activeSelf);
        Assert.IsFalse(salaEsperaPanel.activeSelf);
    }

    // ---------------------------------------------------------------
    // OnClick_AtrasAlMenuPrincipal
    // (requiere que "PantallasUI" esté en Build Settings)
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator OnClickAtrasAlMenuPrincipal_MarcaVolverAElegirModoYCargaPantallasUI()
    {
        controller.OnClick_AtrasAlMenuPrincipal();
        yield return null;

        Assert.IsTrue(PantallasUIController.VolverAElegirModo);
        Assert.AreEqual(
            "PantallasUI",
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    // ---------------------------------------------------------------
    // MostrarPanel
    // ---------------------------------------------------------------

    [Test]
    public void MostrarPanel_ActivaSoloElPanelIndicado()
    {
        Invoke("MostrarPanel", salaEsperaPanel);

        Assert.IsFalse(modoMultiplayerPanel.activeSelf);
        Assert.IsFalse(ingresarCodPanel.activeSelf);
        Assert.IsTrue(salaEsperaPanel.activeSelf);
    }

    // ---------------------------------------------------------------
    // OnDestroy
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator OnDestroy_QuitaElListenerDelBotonCopiarCodigo()
    {
        Invoke("OnDestroy");
        yield return null;

        GUIUtility.systemCopyBuffer = "sin_cambios";
        labelCodigoACopiar.text = "NUEVOCODIGO";

        botonCopiarCodigo.onClick.Invoke();

        Assert.AreEqual("sin_cambios", GUIUtility.systemCopyBuffer,
            "Tras OnDestroy, el click ya no debería ejecutar OnClick_CopiarCodigo");
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private T CrearUI<T>(string nombre) where T : Component
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        return go.AddComponent<T>();
    }

    private const BindingFlags Flags =
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    private void SetField(string name, object value)
    {
        var field = typeof(MultiplayerLobbyController).GetField(name, Flags);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}'");
        field.SetValue(controller, value);
    }

    private object GetField(string name)
    {
        var field = typeof(MultiplayerLobbyController).GetField(name, Flags);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}'");
        return field.GetValue(controller);
    }

    private void Invoke(string methodName, params object[] args)
    {
        var method = typeof(MultiplayerLobbyController).GetMethod(methodName, Flags);
        Assert.IsNotNull(method, $"No se encontró el método '{methodName}'");
        method.Invoke(controller, args);
    }

    private object InvokeReturning(string methodName, params object[] args)
    {
        var method = typeof(MultiplayerLobbyController).GetMethod(methodName, Flags);
        Assert.IsNotNull(method, $"No se encontró el método '{methodName}'");
        return method.Invoke(controller, args);
    }
}