using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using TMPro;
 
public class ButtonHoverMenuPrincipalTests
{
    private GameObject root;
    private GameObject childGO;
    private TextMeshProUGUI tmp;
 
    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Root");
        childGO = new GameObject("Child", typeof(RectTransform));
        childGO.transform.SetParent(root.transform);
        tmp = childGO.AddComponent<TextMeshProUGUI>();
    }
 
    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.DestroyImmediate(root);
    }
 
    // ---------------------- Helpers de reflexión ----------------------
 
    private static object GetPrivateField(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"No se encontró el campo '{fieldName}'.");
        return field.GetValue(target);
    }
 
    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"No se encontró el campo '{fieldName}'.");
        field.SetValue(target, value);
    }
 
    // ============================= Awake() =============================
 
    [UnityTest]
    public IEnumerator Awake_CuandoTextoTMPEsNull_LoAsignaDesdeElHijo()
    {
        var controller = root.AddComponent<ButtonHoverMenuPrincipal>();
        yield return null;
 
        var asignado = GetPrivateField(controller, "textoTMP") as TextMeshProUGUI;
        Assert.IsNotNull(asignado, "textoTMP debería asignarse automáticamente en Awake().");
        Assert.AreSame(tmp, asignado, "Debería tomar el TMP encontrado en los hijos.");
    }
 
    [UnityTest]
    public IEnumerator Awake_CuandoTextoTMPYaEstaAsignado_NoLoSobrescribe()
    {
        var otroGO = new GameObject("Otro", typeof(RectTransform));
        var tmpPreAsignado = otroGO.AddComponent<TextMeshProUGUI>();
 
        // Agregamos el componente con el GO inactivo para inyectar el valor
        // antes de que Awake() se dispare.
        root.SetActive(false);
        var controller = root.AddComponent<ButtonHoverMenuPrincipal>();
        SetPrivateField(controller, "textoTMP", tmpPreAsignado);
        root.SetActive(true);
 
        yield return null;
 
        var final = GetPrivateField(controller, "textoTMP") as TextMeshProUGUI;
        Assert.AreSame(tmpPreAsignado, final,
            "Si textoTMP ya tenía valor, Awake() no debería reemplazarlo por el del hijo.");
 
        Object.DestroyImmediate(otroGO);
    }
 
    [UnityTest]
    public IEnumerator Awake_SinTMPEnHijos_TextoTMPQuedaNull_NoLanzaExcepcion()
    {
        Object.DestroyImmediate(childGO); // sin hijos con TMP
 
        ButtonHoverMenuPrincipal controller = null;
        Assert.DoesNotThrow(() => controller = root.AddComponent<ButtonHoverMenuPrincipal>());
        yield return null;
 
        Assert.IsNull(GetPrivateField(controller, "textoTMP"));
    }
 
    [UnityTest]
    public IEnumerator Awake_AlIniciar_AplicaColorNormalAlTexto()
    {
        tmp.color = Color.black; // color "sucio" antes de Awake
 
        var controller = root.AddComponent<ButtonHoverMenuPrincipal>();
        yield return null;
 
        Assert.AreEqual(controller.colorNormal, tmp.color,
            "Tras Awake(), el texto debería quedar en colorNormal (vía RestaurarColor()).");
    }
 
    // ========================= OnPointerEnter() =========================
 
    [UnityTest]
    public IEnumerator OnPointerEnter_ConTextoTMPAsignado_CambiaAColorHover()
    {
        var controller = root.AddComponent<ButtonHoverMenuPrincipal>();
        yield return null;
        tmp.color = controller.colorNormal; // estado esperado antes del hover
 
        controller.OnPointerEnter(null);
 
        Assert.AreEqual(controller.colorHover, tmp.color,
            "OnPointerEnter debería pintar el texto con colorHover.");
    }
 
    [UnityTest]
    public IEnumerator OnPointerEnter_RespetaElColorHoverConfiguradoEnInspector()
    {
        var controller = root.AddComponent<ButtonHoverMenuPrincipal>();
        controller.colorHover = new Color32(255, 0, 0, 255); // override manual
        yield return null;
 
        controller.OnPointerEnter(null);
 
        Assert.AreEqual(new Color32(255, 0, 0, 255), (Color32)tmp.color,
            "Debería usar el colorHover asignado, no un valor fijo.");
    }
 
    [UnityTest]
    public IEnumerator OnPointerEnter_ConTextoTMPNull_NoLanzaExcepcion()
    {
        Object.DestroyImmediate(childGO); // sin TMP => queda null tras Awake
 
        var controller = root.AddComponent<ButtonHoverMenuPrincipal>();
        yield return null;
 
        Assert.DoesNotThrow(() => controller.OnPointerEnter(null));
    }
 
    // ========================= OnPointerExit() =========================
 
    [UnityTest]
    public IEnumerator OnPointerExit_RestauraElColorNormal()
    {
        var controller = root.AddComponent<ButtonHoverMenuPrincipal>();
        yield return null;
 
        // Simulamos que el mouse estuvo encima (color hover aplicado)
        controller.OnPointerEnter(null);
        Assert.AreEqual(controller.colorHover, tmp.color); // precondición
 
        controller.OnPointerExit(null);
 
        Assert.AreEqual(controller.colorNormal, tmp.color,
            "OnPointerExit debería restaurar colorNormal en el texto.");
    }
 
    [UnityTest]
    public IEnumerator OnPointerExit_ConTextoTMPNull_NoLanzaExcepcion()
    {
        Object.DestroyImmediate(childGO);
 
        var controller = root.AddComponent<ButtonHoverMenuPrincipal>();
        yield return null;
 
        Assert.DoesNotThrow(() => controller.OnPointerExit(null));
    }
 
    // ===================== Secuencia / integración =====================
 
    [UnityTest]
    public IEnumerator SecuenciaCompletaHover_EntraYSale_TerminaEnColorNormal()
    {
        var controller = root.AddComponent<ButtonHoverMenuPrincipal>();
        yield return null;
 
        Assert.AreEqual(controller.colorNormal, tmp.color, "Estado inicial: colorNormal.");
 
        controller.OnPointerEnter(null);
        Assert.AreEqual(controller.colorHover, tmp.color, "Tras entrar: colorHover.");
 
        controller.OnPointerExit(null);
        Assert.AreEqual(controller.colorNormal, tmp.color, "Tras salir: colorNormal de nuevo.");
    }
 
    [UnityTest]
    public IEnumerator MultiplesEnterSeguidos_MantieneColorHover()
    {
        var controller = root.AddComponent<ButtonHoverMenuPrincipal>();
        yield return null;
 
        controller.OnPointerEnter(null);
        controller.OnPointerEnter(null); // llamada redundante, no debería romper nada
 
        Assert.AreEqual(controller.colorHover, tmp.color);
    }
 
    // ===================== Valores por defecto del Inspector =====================
 
    [UnityTest]
    public IEnumerator ValoresPorDefecto_ColorNormalYHover_CoincidenConLoDefinido()
    {
        var controller = root.AddComponent<ButtonHoverMenuPrincipal>();
        yield return null;
 
        Assert.AreEqual(new Color32(120, 145, 165, 255), (Color32)controller.colorNormal,
            "colorNormal por defecto debería ser el gris azulado definido en el script.");
        Assert.AreEqual(new Color32(0, 255, 102, 255), (Color32)controller.colorHover,
            "colorHover por defecto debería ser el verde neón definido en el script.");
    }
}