using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

public class RatingPopupTests
{
    private GameObject playerGO;
    private GameObject popupGO;
    private RatingPopup popup;
    private PauseController pauseController;
    private Button[] estrellas;
    private TextMeshProUGUI texto;

    [SetUp]
    public void SetUp()
    {
        // Player "padre" con el PauseController, tal como en la jerarquía real
        playerGO = new GameObject("Player");
        pauseController = playerGO.AddComponent<PauseController>();

        // Panel de rating, hijo del Player
        popupGO = new GameObject("RatingPopup");
        popupGO.transform.SetParent(playerGO.transform);
        popup = popupGO.AddComponent<RatingPopup>();

        // 3 botones de estrellas
        estrellas = new Button[3];
        for (int i = 0; i < estrellas.Length; i++)
        {
            var go = new GameObject("Estrella" + i);
            go.transform.SetParent(popupGO.transform);
            estrellas[i] = go.AddComponent<Button>();
        }

        // Texto de estado
        var textoGO = new GameObject("TextoEstado");
        textoGO.transform.SetParent(popupGO.transform);
        texto = textoGO.AddComponent<TextMeshProUGUI>();

        SetField(popup, "estrellas", estrellas);
        SetField(popup, "textoEstado", texto);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(playerGO);
    }

    // ---------------------------------------------------------------
    // Awake
    // ---------------------------------------------------------------

    [Test]
    public void Awake_EncuentraPauseControllerEnElPadre()
    {
        Invoke(popup, "Awake");

        var encontrado = GetField(popup, "pauseController");
        Assert.AreSame(pauseController, encontrado);
    }

    [Test]
    public void Awake_SiNoHayPauseControllerEnElPadre_QuedaNull()
    {
        // popup "huérfano", sin padre con PauseController
        var solo = new GameObject("Solo").AddComponent<RatingPopup>();

        Invoke(solo, "Awake");

        Assert.IsNull(GetField(solo, "pauseController"));

        Object.DestroyImmediate(solo.gameObject);
    }

    // ---------------------------------------------------------------
    // OnEnable
    // ---------------------------------------------------------------

    [Test]
    public void OnEnable_ReiniciaProcesandoYHabilitaBotones()
    {
        SetField(popup, "procesando", true);
        foreach (var b in estrellas) b.interactable = false;
        texto.text = "texto previo";

        Invoke(popup, "OnEnable");

        Assert.IsFalse((bool)GetField(popup, "procesando"));
        foreach (var b in estrellas)
            Assert.IsTrue(b.interactable, "Cada estrella debería quedar habilitada al reabrir el panel");
        Assert.AreEqual("", texto.text);
    }

    [Test]
    public void OnEnable_NoRompeSiTextoEstadoEsNull()
    {
        SetField(popup, "textoEstado", null);
        Assert.DoesNotThrow(() => Invoke(popup, "OnEnable"));
    }

    // ---------------------------------------------------------------
    // Start (asignación de listeners con la calificación correcta)
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator Start_ClickEnEstrellaDispara_SeleccionarConElIndiceCorrecto()
    {
        Invoke(popup, "Start");
        yield return null;

        // clic en la 3ra estrella (índice 2) -> calificación esperada = 3
        estrellas[2].onClick.Invoke();

        // Efecto sincrónico inmediato de Seleccionar(3): pasa a "procesando"
        // y bloquea los botones. No verificamos el resultado de la corutina
        // de red porque depende de servicios externos reales.
        Assert.IsTrue((bool)GetField(popup, "procesando"));
        foreach (var b in estrellas)
            Assert.IsFalse(b.interactable);
    }

    // ---------------------------------------------------------------
    // Seleccionar
    // ---------------------------------------------------------------

    [Test]
    public void Seleccionar_MarcaProcesandoYBloqueaBotones()
    {
        Invoke(popup, "Seleccionar", 5);

        Assert.IsTrue((bool)GetField(popup, "procesando"));
        foreach (var b in estrellas)
            Assert.IsFalse(b.interactable);
    }

    [Test]
    public void Seleccionar_SegundoClickMientrasProcesa_NoRompeNiCambiaEstado()
    {
        // Simula que ya hay una calificación en curso
        SetField(popup, "procesando", true);
        foreach (var b in estrellas) b.interactable = false;

        Assert.DoesNotThrow(() => Invoke(popup, "Seleccionar", 3));

        // Sigue procesando, sin efectos secundarios nuevos
        Assert.IsTrue((bool)GetField(popup, "procesando"));
        foreach (var b in estrellas)
            Assert.IsFalse(b.interactable);
    }

    // ---------------------------------------------------------------
    // SetEstado
    // ---------------------------------------------------------------

    [Test]
    public void SetEstado_ActualizaElTextoDeEstado()
    {
        Invoke(popup, "SetEstado", "Cargando resultado...");
        Assert.AreEqual("Cargando resultado...", texto.text);
    }

    [Test]
    public void SetEstado_NoRompeSiTextoEstadoEsNull()
    {
        SetField(popup, "textoEstado", null);
        Assert.DoesNotThrow(() => Invoke(popup, "SetEstado", "algo"));
    }

    // ---------------------------------------------------------------
    // BloquearBotones / HabilitarBotones
    // ---------------------------------------------------------------

    [Test]
    public void BloquearBotones_DejaTodosLosBotonesNoInteractuables()
    {
        Invoke(popup, "BloquearBotones");

        foreach (var b in estrellas)
            Assert.IsFalse(b.interactable);
    }

    [Test]
    public void HabilitarBotones_DejaTodosLosBotonesInteractuables()
    {
        foreach (var b in estrellas) b.interactable = false;

        Invoke(popup, "HabilitarBotones");

        foreach (var b in estrellas)
            Assert.IsTrue(b.interactable);
    }

    // ---------------------------------------------------------------
    // Helpers de reflexión (para acceder a miembros privados)
    // ---------------------------------------------------------------

    private const BindingFlags Flags =
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    private static void SetField(object obj, string name, object value)
    {
        var field = obj.GetType().GetField(name, Flags);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}' en {obj.GetType().Name}");
        field.SetValue(obj, value);
    }

    private static object GetField(object obj, string name)
    {
        var field = obj.GetType().GetField(name, Flags);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}' en {obj.GetType().Name}");
        return field.GetValue(obj);
    }

    private static void Invoke(object obj, string methodName, params object[] args)
    {
        var method = obj.GetType().GetMethod(methodName, Flags);
        Assert.IsNotNull(method, $"No se encontró el método '{methodName}' en {obj.GetType().Name}");
        method.Invoke(obj, args);
    }
}
