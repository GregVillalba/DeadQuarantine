using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class LinkedinProfileTests
{
    private GameObject canvasGO;
    private GameObject textGO;
    private TextMeshProUGUI tmp;
    private LinkOpener linkOpener;
 
    private const string HoverColor = "#58A6FF";
 
    [TearDown]
    public void TearDown()
    {
        if (canvasGO != null)
        {
            Object.DestroyImmediate(canvasGO);
        }
    }
 
    // ---------- Helpers de reflexión ----------
 
    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(fi, $"No se encontró el campo privado '{fieldName}'.");
        return fi.GetValue(target);
    }
 
    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(fi, $"No se encontró el campo privado '{fieldName}'.");
        fi.SetValue(target, value);
    }
 
    private static object InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo mi = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mi, $"No se encontró el método privado '{methodName}'.");
        return mi.Invoke(target, args);
    }
 
    /// <summary>
    /// Crea Canvas + TextMeshProUGUI (con texto ya asignado antes de agregar
    /// LinkOpener, para que Awake capture originalText correctamente) y agrega LinkOpener.
    /// </summary>
    private void BuildScene(string initialText, RenderMode renderMode = RenderMode.ScreenSpaceOverlay, Camera worldCamera = null)
    {
        canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = renderMode;
        canvas.worldCamera = worldCamera;
 
        textGO = new GameObject("Text");
        textGO.transform.SetParent(canvasGO.transform, false);
        tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = initialText;
        tmp.ForceMeshUpdate();
 
        // Awake se ejecuta acá, ya con tmp.text seteado.
        linkOpener = textGO.AddComponent<LinkOpener>();
    }
 
    // ---------- Awake ----------
 
    [Test]
    public void Awake_CapturaElTextoOriginal()
    {
        BuildScene("Hola mundo");
 
        string originalText = (string)GetPrivateField(linkOpener, "originalText");
 
        Assert.AreEqual("Hola mundo", originalText);
    }
 
    [Test]
    public void Awake_ConCanvasScreenSpaceOverlay_NoAsignaCamara()
    {
        BuildScene("Texto", RenderMode.ScreenSpaceOverlay);
 
        object uiCamera = GetPrivateField(linkOpener, "uiCamera");
 
        Assert.IsNull(uiCamera);
    }
 
    [Test]
    public void Awake_ConCanvasScreenSpaceCamera_AsignaLaCamaraDelCanvas()
    {
        var camGO = new GameObject("Cam");
        Camera cam = camGO.AddComponent<Camera>();
 
        BuildScene("Texto", RenderMode.ScreenSpaceCamera, cam);
 
        object uiCamera = GetPrivateField(linkOpener, "uiCamera");
 
        Assert.AreSame(cam, uiCamera);
 
        Object.DestroyImmediate(camGO);
    }
 
    // ---------- OnLinkEnter (privado, invocado por reflexión) ----------
 
    [Test]
    public void OnLinkEnter_EnvuelveElTextoDelLinkConColorYSubrayado()
    {
        BuildScene("Visitá <link=\"id1\">nuestro sitio</link> ahora");
        tmp.ForceMeshUpdate();
        Assert.Greater(tmp.textInfo.linkInfo.Length, 0, "El texto de prueba debe generar al menos un link.");
 
        InvokePrivateMethod(linkOpener, "OnLinkEnter", 0);
 
        string expectedFragment = $"<color={HoverColor}><u>nuestro sitio</u></color>";
        StringAssert.Contains(expectedFragment, tmp.text);
    }
 
    [Test]
    public void OnLinkEnter_SiYaEstaHovering_NoModificaElTexto()
    {
        BuildScene("Ir a <link=\"id1\">enlace</link> ya");
        tmp.ForceMeshUpdate();
        SetPrivateField(linkOpener, "isHovering", true);
 
        InvokePrivateMethod(linkOpener, "OnLinkEnter", 0);
 
        Assert.AreEqual("Ir a <link=\"id1\">enlace</link> ya", tmp.text);
    }
 
    // ---------- OnLinkExit / OnPointerExit ----------
 
    [Test]
    public void OnPointerExit_RestauraElTextoOriginalYLimpiaEstado()
    {
        BuildScene("Ir a <link=\"id1\">enlace</link> ya");
        tmp.ForceMeshUpdate();
        InvokePrivateMethod(linkOpener, "OnLinkEnter", 0);
        SetPrivateField(linkOpener, "currentLinkIndex", 0);
 
        linkOpener.OnPointerExit(null);
 
        Assert.AreEqual("Ir a <link=\"id1\">enlace</link> ya", tmp.text);
        Assert.AreEqual(-1, (int)GetPrivateField(linkOpener, "currentLinkIndex"));
        Assert.IsFalse((bool)GetPrivateField(linkOpener, "isHovering"));
    }
 
    [Test]
    public void OnLinkExit_SiNoEstabaHovering_NoLanzaExcepcion()
    {
        BuildScene("Texto sin cambios");
 
        Assert.DoesNotThrow(() => InvokePrivateMethod(linkOpener, "OnLinkExit"));
        Assert.AreEqual("Texto sin cambios", tmp.text);
    }
 
    // ---------- OnDisable ----------
 
    [Test]
    public void OnDisable_RestauraElTextoOriginal()
    {
        BuildScene("Ir a <link=\"id1\">enlace</link> ya");
        tmp.ForceMeshUpdate();
        InvokePrivateMethod(linkOpener, "OnLinkEnter", 0);
 
        textGO.SetActive(false); // dispara OnDisable
 
        Assert.AreEqual("Ir a <link=\"id1\">enlace</link> ya", tmp.text);
    }
 
    // ---------- OnPointerMove (solo camino negativo, ver nota) ----------
 
    [Test]
    public void OnPointerMove_SinLinkEnLaPosicion_DejaCurrentLinkIndexEnMenosUno()
    {
        BuildScene("Ir a <link=\"id1\">enlace</link> ya");
        tmp.ForceMeshUpdate();
 
        var eventData = new PointerEventData(null)
        {
            position = new Vector2(-99999f, -99999f) // fuera de cualquier geometría posible
        };
 
        linkOpener.OnPointerMove(eventData);
 
        Assert.AreEqual(-1, (int)GetPrivateField(linkOpener, "currentLinkIndex"));
        Assert.AreEqual("Ir a <link=\"id1\">enlace</link> ya", tmp.text);
    }
}
