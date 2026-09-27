using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
 
[TestFixture]
public class NPCShopUITests
{
    private readonly List<GameObject> creados = new List<GameObject>();
 
    private GameObject host;
    private NPCShopUI shopUI;
 
    private GameObject interactPromptGO;
    private TextMeshProUGUI interactText;
    private GameObject shopPanelGO;
    private Button closeButton;
    private AudioSource audioSource;
    private TextMeshProUGUI scoreText;
    private Image iconoDetalle;
    private TextMeshProUGUI nombreDetalleText;
    private TextMeshProUGUI descripcionDetalleText;
    private TextMeshProUGUI statsDetalleText;
    private Image progresoCompraFill;
 
    [SetUp]
    public void SetUp()
    {
        host = CrearGameObject("NPCShopUI_Host");
        shopUI = host.AddComponent<NPCShopUI>(); // dispara Awake()+OnEnable() automáticos con campos en null (seguro por los "if != null")
 
        interactPromptGO = CrearGameObject("InteractPrompt");
 
        GameObject interactTextGO = CrearGameObject("InteractText", typeof(TextMeshProUGUI));
        interactText = interactTextGO.GetComponent<TextMeshProUGUI>();
 
        shopPanelGO = CrearGameObject("ShopPanel");
        shopPanelGO.SetActive(true);
 
        GameObject closeButtonGO = CrearGameObject("CloseButton", typeof(Button));
        closeButton = closeButtonGO.GetComponent<Button>();
 
        GameObject audioSourceGO = CrearGameObject("AudioSource", typeof(AudioSource));
        audioSource = audioSourceGO.GetComponent<AudioSource>();
 
        GameObject scoreTextGO = CrearGameObject("ScoreText", typeof(TextMeshProUGUI));
        scoreText = scoreTextGO.GetComponent<TextMeshProUGUI>();
 
        GameObject iconoDetalleGO = CrearGameObject("IconoDetalle", typeof(Image));
        iconoDetalle = iconoDetalleGO.GetComponent<Image>();
 
        GameObject nombreDetalleGO = CrearGameObject("NombreDetalle", typeof(TextMeshProUGUI));
        nombreDetalleText = nombreDetalleGO.GetComponent<TextMeshProUGUI>();
 
        GameObject descripcionDetalleGO = CrearGameObject("DescripcionDetalle", typeof(TextMeshProUGUI));
        descripcionDetalleText = descripcionDetalleGO.GetComponent<TextMeshProUGUI>();
 
        GameObject statsDetalleGO = CrearGameObject("StatsDetalle", typeof(TextMeshProUGUI));
        statsDetalleText = statsDetalleGO.GetComponent<TextMeshProUGUI>();
 
        GameObject progresoFillGO = CrearGameObject("ProgresoFill", typeof(Image));
        progresoCompraFill = progresoFillGO.GetComponent<Image>();
 
        GameObject rowsContainerGO = CrearGameObject("RowsContainer");
 
        SetPrivateField("interactPrompt", interactPromptGO);
        SetPrivateField("interactText", interactText);
        SetPrivateField("shopPanel", shopPanelGO);
        SetPrivateField("closeButton", closeButton);
        SetPrivateField("audioSource", audioSource);
        SetPrivateField("scoreText", scoreText);
        SetPrivateField("iconoDetalle", iconoDetalle);
        SetPrivateField("nombreDetalleText", nombreDetalleText);
        SetPrivateField("descripcionDetalleText", descripcionDetalleText);
        SetPrivateField("statsDetalleText", statsDetalleText);
        SetPrivateField("progresoCompraFill", progresoCompraFill);
        SetPrivateField("rowsContainer", rowsContainerGO.transform);
        // rowPrefab se deja intencionalmente en null: ver notas en los tests
        // de AbrirTienda/CerrarTienda.
 
        // Re-invocamos el ciclo de vida ahora que las referencias ya están
        // cableadas, simulando que en el Inspector estaban asignadas desde el
        // principio (igual que en GameStartCurtainTests).
        InvokePrivateMethod("Awake");
        InvokePrivateMethod("OnEnable");
    }
 
    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in creados)
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }
 
        creados.Clear();
 
        // Por si algún test dispara la rama "single player": evitamos que
        // contamine el resto de la suite.
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
 
    private GameObject CrearGameObject(string nombre, params System.Type[] componentes)
    {
        GameObject go = componentes.Length > 0 ? new GameObject(nombre, componentes) : new GameObject(nombre);
        creados.Add(go);
        return go;
    }
 
    private void SetPrivateField(string fieldName, object value)
    {
        typeof(NPCShopUI)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(shopUI, value);
    }
 
    private object GetPrivateField(string fieldName)
    {
        return typeof(NPCShopUI)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(shopUI);
    }
 
    private object InvokePrivateMethod(string methodName, params object[] args)
    {
        MethodInfo method = typeof(NPCShopUI)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        return method.Invoke(shopUI, args);
    }
 
    // --- Awake ---
 
    [Test]
    public void Awake_DejaElPanelDeLaTiendaDesactivado()
    {
        Assert.IsFalse(shopPanelGO.activeSelf);
    }
 
    [Test]
    public void Awake_OcultaElPromptDeInteraccion()
    {
        Assert.IsFalse(interactPromptGO.activeSelf);
    }
 
    [Test]
    public void Awake_ConfiguraElAudioSourceParaIgnorarLaPausaDelListener()
    {
        Assert.IsTrue(audioSource.ignoreListenerPause);
    }
 
    [Test]
    public void Awake_CableaElBotonDeCierreParaLlamarCerrarTienda()
    {
        SetPrivateField("estaAbierta", true);
        shopPanelGO.SetActive(true);
 
        closeButton.onClick.Invoke();
 
        Assert.IsFalse((bool)GetPrivateField("estaAbierta"));
        Assert.IsFalse(shopPanelGO.activeSelf);
    }
 
    // --- OnEnable / OnDisable ---
 
    [Test]
    public void OnEnable_HabilitaElMapaDeAccionesDelJugador()
    {
        var controls = (PlayerControls)GetPrivateField("controls");
        Assert.IsTrue(controls.Player.enabled);
    }
 
    [Test]
    public void OnDisable_DeshabilitaElMapaDeAccionesDelJugador()
    {
        InvokePrivateMethod("OnDisable");
 
        var controls = (PlayerControls)GetPrivateField("controls");
        Assert.IsFalse(controls.Player.enabled);
    }
 
    // --- BuscarCamara ---
 
    [Test]
    public void BuscarCamara_SinCamarasEnLaJerarquia_PlayerCameraQuedaNulo()
    {
        SetPrivateField("playerCamera", null);
 
        InvokePrivateMethod("BuscarCamara");
 
        Assert.IsNull(GetPrivateField("playerCamera"));
    }
 
    [Test]
    public void BuscarCamara_ConCamaraPrincipalHijaSinNetworkObject_LaAsigna()
    {
        GameObject camGO = CrearGameObject("MainCam", typeof(Camera));
        camGO.transform.SetParent(host.transform);
        camGO.tag = "MainCamera";
 
        InvokePrivateMethod("BuscarCamara");
 
        Assert.AreEqual(camGO.GetComponent<Camera>(), GetPrivateField("playerCamera"));
    }
 
    // --- CachearReferenciasJugador ---
 
    [Test]
    public void CachearReferenciasJugador_EncuentraLosComponentesDelJugador()
    {
        GameObject childGO = CrearGameObject("PlayerParts");
        childGO.transform.SetParent(host.transform);
 
        var score = childGO.AddComponent<PlayerScore>();
        var switcher = childGO.AddComponent<WeaponSwitcher>();
        var movement = childGO.AddComponent<PlayerMovement>();
        var look = childGO.AddComponent<PlayerLook>();
        var health = childGO.AddComponent<PlayerHealth>();
 
        InvokePrivateMethod("CachearReferenciasJugador");
 
        Assert.AreEqual(score, GetPrivateField("playerScore"));
        Assert.AreEqual(switcher, GetPrivateField("weaponSwitcher"));
        Assert.AreEqual(movement, GetPrivateField("playerMovement"));
        Assert.AreEqual(look, GetPrivateField("playerLook"));
        Assert.AreEqual(health, GetPrivateField("playerHealth"));
    }
 
    // --- DetectarMercader / MostrarPrompt / OcultarPrompt ---
 
    [Test]
    public void DetectarMercader_SinColliderEnRango_OcultaElPrompt()
    {
        GameObject camGO = CrearGameObject("Cam", typeof(Camera));
        camGO.transform.position = Vector3.zero;
        camGO.transform.rotation = Quaternion.identity;
        SetPrivateField("playerCamera", camGO.GetComponent<Camera>());
        SetPrivateField("interactRange", 3f);
 
        interactPromptGO.SetActive(true);
 
        InvokePrivateMethod("DetectarMercader");
 
        Assert.IsFalse(interactPromptGO.activeSelf);
    }
 
    [Test]
    public void DetectarMercader_ConVendedorEnRango_MuestraElPrompt()
    {
        GameObject camGO = CrearGameObject("Cam", typeof(Camera));
        camGO.transform.position = Vector3.zero;
        camGO.transform.rotation = Quaternion.identity; // forward = +Z
        SetPrivateField("playerCamera", camGO.GetComponent<Camera>());
        SetPrivateField("interactRange", 3f);
 
        GameObject vendorGO = CrearGameObject("Vendor", typeof(BoxCollider), typeof(NPCWeaponVendor));
        vendorGO.transform.position = new Vector3(0f, 0f, 2f); // adelante de la cámara, dentro del rango
        Physics.SyncTransforms();
 
        InvokePrivateMethod("DetectarMercader");
 
        Assert.IsTrue(interactPromptGO.activeSelf);
        Assert.IsTrue(interactText.text.StartsWith("E para hablar con"));
        // No verificamos la rama "tecla Interact presionada -> AbrirTienda":
        // sin simular un dispositivo de Input System, controls.Player.Interact.triggered
        // es false por defecto, así que naturalmente solo se ejercita "mostrar prompt".
    }
 
    [Test]
    public void MostrarPrompt_ActivaElPromptYEscribeElTexto()
    {
        GameObject vendorGO = CrearGameObject("Vendor", typeof(NPCWeaponVendor));
        NPCWeaponVendor vendor = vendorGO.GetComponent<NPCWeaponVendor>();
 
        InvokePrivateMethod("MostrarPrompt", vendor);
 
        Assert.IsTrue(interactPromptGO.activeSelf);
        Assert.IsTrue(interactText.text.StartsWith("E para hablar con"));
    }
 
    [Test]
    public void OcultarPrompt_DesactivaElPrompt()
    {
        interactPromptGO.SetActive(true);
 
        InvokePrivateMethod("OcultarPrompt");
 
        Assert.IsFalse(interactPromptGO.activeSelf);
    }
 
    // --- AbrirTienda / CerrarTienda ---
    //
    // rowPrefab queda sin asignar a propósito: eso hace que CrearFilas() corte
    // por su guard clause ANTES de tocar currentVendor.Offers, así que estos
    // tests no necesitan conocer la forma real de NPCWeaponVendor.Offers ni de
    // WeaponOffer. Por la misma razón playerScore/playerMovement/playerLook/
    // weaponSwitcher/playerHealth quedan en null: así se ejercitan los caminos
    // "sin referencia" de BloquearJugador()/RestaurarJugador().
 
    [Test]
    public void AbrirTienda_ConfiguraElEstadoDeLaTiendaComoAbierta()
    {
        LogAssert.Expect(LogType.Error, "[NPCShopUI] Falta rowPrefab, rowsContainer o mercader.");
 
        GameObject vendorGO = CrearGameObject("Vendor", typeof(NPCWeaponVendor));
        NPCWeaponVendor vendor = vendorGO.GetComponent<NPCWeaponVendor>();
 
        InvokePrivateMethod("AbrirTienda", vendor);
 
        Assert.IsTrue((bool)GetPrivateField("estaAbierta"));
        Assert.IsTrue(shopPanelGO.activeSelf);
        Assert.IsNull(GetPrivateField("ofertaSeleccionada"));
        Assert.AreEqual(0f, (float)GetPrivateField("tiempoMantenido"));
        Assert.IsTrue(Cursor.visible);
        Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
    }
 
    [Test]
    public void AbrirTienda_SiYaEstaAbierta_CortaAntesDeActualizarElVendedorActual()
    {
        SetPrivateField("estaAbierta", true);
        SetPrivateField("currentVendor", null);
 
        GameObject vendorGO = CrearGameObject("OtroVendor", typeof(NPCWeaponVendor));
        NPCWeaponVendor vendor = vendorGO.GetComponent<NPCWeaponVendor>();
 
        InvokePrivateMethod("AbrirTienda", vendor);
 
        Assert.IsNull(GetPrivateField("currentVendor"));
    }
 
    [Test]
    public void CerrarTienda_SiYaEstaCerrada_NoHaceNada()
    {
        SetPrivateField("estaAbierta", false);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod("CerrarTienda"));
        Assert.IsFalse((bool)GetPrivateField("estaAbierta"));
    }
 
    [Test]
    public void CerrarTienda_CierraLaTiendaYRestauraElCursor()
    {
        LogAssert.Expect(LogType.Error, "[NPCShopUI] Falta rowPrefab, rowsContainer o mercader.");
 
        GameObject vendorGO = CrearGameObject("Vendor", typeof(NPCWeaponVendor));
        NPCWeaponVendor vendor = vendorGO.GetComponent<NPCWeaponVendor>();
        InvokePrivateMethod("AbrirTienda", vendor);
 
        InvokePrivateMethod("CerrarTienda");
 
        Assert.IsFalse((bool)GetPrivateField("estaAbierta"));
        Assert.IsFalse(shopPanelGO.activeSelf);
        Assert.IsFalse(Cursor.visible);
        Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);
        Assert.IsNull(GetPrivateField("currentVendor"));
    }
 
    // --- CrearFilas (directo) ---
 
    [Test]
    public void CrearFilas_SinRowPrefab_LoguéaErrorYNoAgregaFilas()
    {
        LogAssert.Expect(LogType.Error, "[NPCShopUI] Falta rowPrefab, rowsContainer o mercader.");
 
        InvokePrivateMethod("CrearFilas");
 
        IList filas = (IList)GetPrivateField("filas");
        Assert.AreEqual(0, filas.Count);
    }
 
    // --- ActualizarPanelDetalle (sin selección) ---
 
    [Test]
    public void ActualizarPanelDetalle_SinOfertaSeleccionada_MuestraElEstadoPorDefecto()
    {
        SetPrivateField("ofertaSeleccionada", null);
 
        InvokePrivateMethod("ActualizarPanelDetalle");
 
        Assert.IsFalse(iconoDetalle.enabled);
        Assert.AreEqual(string.Empty, nombreDetalleText.text);
        Assert.AreEqual((string)GetPrivateField("textoSinSeleccion"), descripcionDetalleText.text);
        Assert.AreEqual(string.Empty, statsDetalleText.text);
        Assert.AreEqual(0f, progresoCompraFill.fillAmount);
    }
 
    // --- ActualizarCompraMantenida / Update ---
 
    [Test]
    public void ActualizarCompraMantenida_SinOfertaSeleccionada_ReseteaElProgreso()
    {
        SetPrivateField("ofertaSeleccionada", null);
        SetPrivateField("tiempoMantenido", 1.5f);
        progresoCompraFill.fillAmount = 1f;
 
        InvokePrivateMethod("ActualizarCompraMantenida");
 
        Assert.AreEqual(0f, (float)GetPrivateField("tiempoMantenido"));
        Assert.AreEqual(0f, progresoCompraFill.fillAmount);
    }
 
    [Test]
    public void Update_ConTiendaAbierta_DelegaEnActualizarCompraMantenida()
    {
        SetPrivateField("estaAbierta", true);
        SetPrivateField("ofertaSeleccionada", null);
        progresoCompraFill.fillAmount = 1f;
 
        Assert.DoesNotThrow(() => InvokePrivateMethod("Update"));
 
        Assert.AreEqual(0f, progresoCompraFill.fillAmount);
    }
 
    [Test]
    public void Update_ConTiendaCerradaYSinCamaraDisponible_OcultaPromptSinExcepcion()
    {
        SetPrivateField("estaAbierta", false);
        SetPrivateField("playerCamera", null);
        interactPromptGO.SetActive(true);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod("Update"));
 
        Assert.IsFalse(interactPromptGO.activeSelf);
    }
 
    // --- ComprarOferta / OnCompraTerminada / OnScoreChanged ---
 
    [Test]
    public void ComprarOferta_SinVendedorActual_NoLanzaExcepcion()
    {
        SetPrivateField("currentVendor", null);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod("ComprarOferta", (object)null));
    }
 
    [Test]
    public void OnCompraTerminada_ConTiendaCerrada_NoHaceNada()
    {
        SetPrivateField("estaAbierta", false);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod("OnCompraTerminada", "espada", true));
    }
 
    [Test]
    public void OnCompraTerminada_ConTiendaAbierta_ActualizaFilasSinExcepcion()
    {
        SetPrivateField("estaAbierta", true);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod("OnCompraTerminada", "espada", false));
    }
 
    [Test]
    public void OnScoreChanged_ConTiendaAbierta_ActualizaFilasSinExcepcion()
    {
        SetPrivateField("estaAbierta", true);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod("OnScoreChanged", 0, 10));
    }
 
    [Test]
    public void OnScoreChanged_ConTiendaCerrada_NoHaceNada()
    {
        SetPrivateField("estaAbierta", false);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod("OnScoreChanged", 0, 10));
    }
 
    // --- DestruirFilas / BloquearJugador / RestaurarJugador ---
 
    [Test]
    public void DestruirFilas_ConListaVacia_NoLanzaExcepcion()
    {
        Assert.DoesNotThrow(() => InvokePrivateMethod("DestruirFilas"));
 
        IList filas = (IList)GetPrivateField("filas");
        Assert.AreEqual(0, filas.Count);
    }
 
    [Test]
    public void BloquearJugador_SinReferenciasDeJugador_NoLanzaExcepcion()
    {
        SetPrivateField("playerMovement", null);
        SetPrivateField("playerLook", null);
        SetPrivateField("weaponSwitcher", null);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod("BloquearJugador"));
    }
 
    [Test]
    public void RestaurarJugador_SinReferenciasDeJugador_NoLanzaExcepcion()
    {
        SetPrivateField("playerHealth", null);
        SetPrivateField("playerMovement", null);
        SetPrivateField("playerLook", null);
        SetPrivateField("weaponSwitcher", null);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod("RestaurarJugador"));
    }
 
    // --- EsSinglePlayer / OnDestroy ---
 
    [Test]
    public void EsSinglePlayer_EnLaEscenaDeTest_DevuelveFalse()
    {
        // La escena activa durante los tests de EditMode no se llama
        // "MainSceneSinglePlayer", así que este smoke test confirma la rama "false".
        bool resultado = (bool)InvokePrivateMethod("EsSinglePlayer");
 
        Assert.IsFalse(resultado);
    }
 
    [Test]
    public void OnDestroy_FueraDeSinglePlayer_NoModificaElTimeScale()
    {
        float timeScaleOriginal = Time.timeScale;
 
        InvokePrivateMethod("OnDestroy");
 
        Assert.AreEqual(timeScaleOriginal, Time.timeScale);
    }
}
