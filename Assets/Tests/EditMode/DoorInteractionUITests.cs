using System.Reflection;
using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class DoorInteractionUITests
{
    private const string DoorIsOpenMemberName = "IsOpen";
    private const float TestInteractRange = 5f;
 
    private GameObject root;
    private GameObject uiGO;
    private GameObject interactPromptGO;
    private TextMeshProUGUI interactText;
    private DoorInteractionUI controller;
 
    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Player");
 
        uiGO = new GameObject("DoorInteractionUI");
        uiGO.transform.SetParent(root.transform);
        controller = uiGO.AddComponent<DoorInteractionUI>();
 
        interactPromptGO = new GameObject("InteractPrompt");
        var textGO = new GameObject("InteractText");
        textGO.transform.SetParent(interactPromptGO.transform);
        interactText = textGO.AddComponent<TextMeshProUGUI>();
 
        SetPrivateField(controller, "interactPrompt", interactPromptGO);
        SetPrivateField(controller, "interactText", interactText);
        SetPrivateField(controller, "interactRange", TestInteractRange);
    }
 
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(interactPromptGO);
    }
 
    // ---------- Helpers de reflexión ----------
 
    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(fi, $"No se encontró el campo privado '{fieldName}'.");
        fi.SetValue(target, value);
    }
 
    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(fi, $"No se encontró el campo privado '{fieldName}'.");
        return fi.GetValue(target);
    }
 
    private static void InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo mi = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mi, $"No se encontró el método privado '{methodName}'.");
        mi.Invoke(target, null);
    }
 
    private static void SetMember(object target, string memberName, object value)
    {
        System.Type type = target.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
 
        PropertyInfo pi = type.GetProperty(memberName, flags);
        if (pi != null && pi.CanWrite)
        {
            pi.SetValue(target, value);
            return;
        }
 
        FieldInfo fi = type.GetField(memberName, flags);
        Assert.IsNotNull(fi, $"No se encontró propiedad ni campo '{memberName}' en {type.Name}.");
        fi.SetValue(target, value);
    }
 
    private static Camera CreateCamera(Transform parent, Vector3 localPosition)
    {
        var camGO = new GameObject("Camera");
        camGO.transform.SetParent(parent);
        camGO.transform.localPosition = localPosition;
        camGO.transform.localRotation = Quaternion.identity;
        return camGO.AddComponent<Camera>();
    }
 
    private static Door CreateDoor(Vector3 position, bool isOpen)
    {
        var doorGO = new GameObject("Door");
        doorGO.transform.position = position;
        var collider = doorGO.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        Door door = doorGO.AddComponent<Door>();
        SetMember(door, DoorIsOpenMemberName, isOpen);
        return door;
    }
 
    // ---------- Awake ----------
 
    [Test]
    public void Awake_SinNetworkObjectPadre_DejaPlayerNetworkObjectEnNull()
    {
        InvokePrivateMethod(controller, "Awake");
 
        Assert.IsNull(GetPrivateField(controller, "playerNetworkObject"));
    }
 
    [Test]
    public void Awake_OcultaElPromptAlIniciar()
    {
        interactPromptGO.SetActive(true);
 
        InvokePrivateMethod(controller, "Awake");
 
        Assert.IsFalse(interactPromptGO.activeSelf);
    }
 
    // ---------- BuscarCamara ----------
 
    [Test]
    public void BuscarCamara_SinCamarasEnLaJerarquia_DejaPlayerCameraEnNull()
    {
        InvokePrivateMethod(controller, "BuscarCamara");
 
        Assert.IsNull(GetPrivateField(controller, "playerCamera"));
    }
 
    [Test]
    public void BuscarCamara_ConCamaraMainCameraSinNetworkObject_LaAsignaComoPlayerCamera()
    {
        Camera cam = CreateCamera(root.transform, Vector3.zero);
        cam.tag = "MainCamera";
 
        InvokePrivateMethod(controller, "BuscarCamara");
 
        Assert.AreSame(cam, GetPrivateField(controller, "playerCamera"));
    }
 
    // ---------- BuscarPuerta ----------
 
    [Test]
    public void BuscarPuerta_ConPuertaCerradaEnRango_MuestraTextoParaAbrir()
    {
        Camera cam = CreateCamera(root.transform, Vector3.zero);
        cam.transform.forward = Vector3.forward;
        SetPrivateField(controller, "playerCamera", cam);
 
        Door door = CreateDoor(cam.transform.position + Vector3.forward * 2f, isOpen: false);
        Physics.SyncTransforms();
 
        InvokePrivateMethod(controller, "BuscarPuerta");
 
        Assert.IsTrue(interactPromptGO.activeSelf);
        Assert.AreEqual("E para abrir", interactText.text);
 
        Object.DestroyImmediate(door.gameObject);
    }
 
    [Test]
    public void BuscarPuerta_ConPuertaAbiertaEnRango_MuestraTextoParaCerrar()
    {
        Camera cam = CreateCamera(root.transform, Vector3.zero);
        cam.transform.forward = Vector3.forward;
        SetPrivateField(controller, "playerCamera", cam);
 
        Door door = CreateDoor(cam.transform.position + Vector3.forward * 2f, isOpen: true);
        Physics.SyncTransforms();
 
        InvokePrivateMethod(controller, "BuscarPuerta");
 
        Assert.IsTrue(interactPromptGO.activeSelf);
        Assert.AreEqual("E para cerrar", interactText.text);
 
        Object.DestroyImmediate(door.gameObject);
    }
 
    [Test]
    public void BuscarPuerta_ConPuertaFueraDeRango_OcultaElPrompt()
    {
        Camera cam = CreateCamera(root.transform, Vector3.zero);
        cam.transform.forward = Vector3.forward;
        SetPrivateField(controller, "playerCamera", cam);
        interactPromptGO.SetActive(true);
 
        Door door = CreateDoor(cam.transform.position + Vector3.forward * (TestInteractRange + 10f), isOpen: false);
        Physics.SyncTransforms();
 
        InvokePrivateMethod(controller, "BuscarPuerta");
 
        Assert.IsFalse(interactPromptGO.activeSelf);
 
        Object.DestroyImmediate(door.gameObject);
    }
 
    [Test]
    public void BuscarPuerta_SinNadaEnElRayo_OcultaElPrompt()
    {
        Camera cam = CreateCamera(root.transform, Vector3.zero);
        cam.transform.forward = Vector3.forward;
        SetPrivateField(controller, "playerCamera", cam);
        interactPromptGO.SetActive(true);
 
        InvokePrivateMethod(controller, "BuscarPuerta");
 
        Assert.IsFalse(interactPromptGO.activeSelf);
    }
 
    // ---------- Mostrar ----------
 
    [Test]
    public void Mostrar_ConPuertaAbierta_ActivaPromptYSeteaTextoCerrar()
    {
        InvokePrivateMethod2(controller, "Mostrar", true);
 
        Assert.IsTrue(interactPromptGO.activeSelf);
        Assert.AreEqual("E para cerrar", interactText.text);
    }
 
    [Test]
    public void Mostrar_ConPuertaCerrada_ActivaPromptYSeteaTextoAbrir()
    {
        InvokePrivateMethod2(controller, "Mostrar", false);
 
        Assert.IsTrue(interactPromptGO.activeSelf);
        Assert.AreEqual("E para abrir", interactText.text);
    }
 
    [Test]
    public void Mostrar_ConInteractPromptNulo_NoLanzaExcepcionYNoModificaTexto()
    {
        SetPrivateField(controller, "interactPrompt", null);
        interactText.text = "sin cambios";
 
        Assert.DoesNotThrow(() => InvokePrivateMethod2(controller, "Mostrar", true));
        Assert.AreEqual("sin cambios", interactText.text);
    }
 
    [Test]
    public void Mostrar_ConInteractTextNulo_ActivaPromptSinLanzarExcepcion()
    {
        SetPrivateField(controller, "interactText", null);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod2(controller, "Mostrar", true));
        Assert.IsTrue(interactPromptGO.activeSelf);
    }
 
    // ---------- Ocultar ----------
 
    [Test]
    public void Ocultar_DesactivaElPrompt()
    {
        interactPromptGO.SetActive(true);
 
        InvokePrivateMethod(controller, "Ocultar");
 
        Assert.IsFalse(interactPromptGO.activeSelf);
    }
 
    [Test]
    public void Ocultar_ConInteractPromptNulo_NoLanzaExcepcion()
    {
        SetPrivateField(controller, "interactPrompt", null);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod(controller, "Ocultar"));
    }
 
    // ---------- Update (sin NetworkObject, ver nota sobre IsOwner) ----------
 
    [Test]
    public void Update_SinCamaraDisponible_OcultaElPrompt()
    {
        interactPromptGO.SetActive(true);
 
        InvokePrivateMethod(controller, "Update");
 
        Assert.IsFalse(interactPromptGO.activeSelf);
    }
 
    [Test]
    public void Update_ConCamaraYPuertaEnRango_MuestraElPrompt()
    {
        Camera cam = CreateCamera(root.transform, Vector3.zero);
        cam.tag = "MainCamera";
        cam.transform.forward = Vector3.forward;
 
        Door door = CreateDoor(cam.transform.position + Vector3.forward * 2f, isOpen: false);
        Physics.SyncTransforms();
 
        InvokePrivateMethod(controller, "Update");
 
        Assert.IsTrue(interactPromptGO.activeSelf);
        Assert.AreEqual("E para abrir", interactText.text);
 
        Object.DestroyImmediate(door.gameObject);
    }
 
    // Helper adicional para invocar métodos privados con un parámetro bool (Mostrar).
    private static void InvokePrivateMethod2(object target, string methodName, bool arg)
    {
        MethodInfo mi = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mi, $"No se encontró el método privado '{methodName}'.");
        mi.Invoke(target, new object[] { arg });
    }
}
