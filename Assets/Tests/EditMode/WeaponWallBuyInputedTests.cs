using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
//using UnityEngine.InputSystem.Testing;
using UnityEngine.TestTools;

public class WeaponWallBuyInputedTests : InputTestFixture
{
    private GameObject controllerGO;
    private WeaponWallBuy controller;
    private PlayerControls controls;
 
    [SetUp]
    public override void Setup()
    {
        base.Setup(); // inicializa el Input System de test, aislado del real
 
        controllerGO = new GameObject("WeaponWallBuy_AR");
        controllerGO.AddComponent<BoxCollider>();
        controller = controllerGO.AddComponent<WeaponWallBuy>();
 
        InvokePrivate(controller, "Awake");
        InvokePrivate(controller, "OnEnable");
        controls = (PlayerControls)GetPrivateField(controller, "controls");
    }
 
    [TearDown]
    public override void TearDown()
    {
        InvokePrivate(controller, "OnDisable");
        Object.DestroyImmediate(controllerGO);
        base.TearDown();
    }
 
    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(fi, $"No se encontró el campo privado '{fieldName}'.");
        return fi.GetValue(target);
    }
 
    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo mi = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mi, $"No se encontró el método privado '{methodName}'.");
        mi.Invoke(target, args);
    }
 
    private static InputDevice AddDeviceForEffectivePath(string effectivePath)
    {
        int start = effectivePath.IndexOf('<');
        int end = effectivePath.IndexOf('>');
        Assert.IsTrue(start >= 0 && end > start,
            $"No se pudo interpretar el layout de dispositivo en el binding '{effectivePath}'.");
        string layout = effectivePath.Substring(start + 1, end - start - 1);
        return InputSystem.AddDevice(layout);
    }
 
    [Test]
    public void Update_ConInteractDisparadoYArmaEnLaMira_EjecutaElCaminoDeCompraSinExcepcion()
    {
        // Cámara + collider propio en la trayectoria del rayo, para que
        // EstaMirandoElArma() dé true y Update() llegue hasta TryPurchase().
        var camGO = new GameObject("Cam");
        camGO.tag = "MainCamera";
        camGO.transform.position = Vector3.zero;
        camGO.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        camGO.AddComponent<Camera>();
        controllerGO.transform.position = Vector3.forward * 2f;
        Physics.SyncTransforms();
 
        InputAction interactAction = controls.Player.Interact;
        Assert.Greater(interactAction.bindings.Count, 0, "La acción Interact no tiene bindings configurados.");
 
        string path = interactAction.bindings[0].effectivePath;
        AddDeviceForEffectivePath(path);
 
        InputControl resolved = InputSystem.FindControl(path);
        Assert.IsNotNull(resolved, $"No se pudo resolver un control para el binding '{path}'.");
        var button = resolved as ButtonControl;
        Assert.IsNotNull(button, "El binding de Interact no resolvió a un control de tipo botón simple.");
 
        Press(button);
 
        // No hay WeaponSwitcher/PlayerScore en la escena de prueba, así que
        // TryPurchase() cortará temprano con este warning esperado.
        LogAssert.Expect(LogType.Warning, "[WeaponWallBuy] No se encontró WeaponSwitcher en el jugador.");
 
        Assert.DoesNotThrow(() => InvokePrivate(controller, "Update"));
    }
 
    [Test]
    public void Update_SinPresionarInteract_NoIntentaComprar()
    {
        var camGO = new GameObject("Cam");
        camGO.tag = "MainCamera";
        camGO.transform.position = Vector3.zero;
        camGO.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        camGO.AddComponent<Camera>();
        controllerGO.transform.position = Vector3.forward * 2f;
        Physics.SyncTransforms();
 
        // Sin Press(): Interact.triggered debe ser false, así que TryPurchase()
        // no debería ejecutarse ni loguear el warning de "falta WeaponSwitcher".
        Assert.DoesNotThrow(() => InvokePrivate(controller, "Update"));
        LogAssert.NoUnexpectedReceived();
    }
}
