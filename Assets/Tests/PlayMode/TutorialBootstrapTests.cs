using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class TutorialBootstrapTests
{
    private GameObject bootstrapObject;
    private TutorialBootstrap bootstrap;
    
    private GameObject hudObject;
    private GameObject playerObject;
    private PlayerMovement playerMovement;
    private PlayerLook playerLook;
    private Weapon testWeapon;

    [SetUp]
    public void SetUp()
    {
        // 1. Arrange: Crear la jerarquía de objetos y componentes necesarios
        playerObject = new GameObject("PlayerRoot");
        
        hudObject = new GameObject("HUD");
        hudObject.SetActive(false); // Inicia desactivado como en el juego

        playerMovement = playerObject.AddComponent<PlayerMovement>();
        playerMovement.MovementLocked = true; // Inicia bloqueado

        playerLook = playerObject.AddComponent<PlayerLook>();
        playerLook.enabled = false; // Inicia deshabilitado

        // Creamos un arma hija para probar el desbloqueo por colección
        GameObject weaponObj = new GameObject("TestWeapon");
        weaponObj.transform.SetParent(playerObject.transform);
        testWeapon = weaponObj.AddComponent<Weapon>();
        testWeapon.InputLocked = true; // Inicia bloqueada

        // Creamos el componente principal a testear
        bootstrapObject = new GameObject("TutorialBootstrapHost");
        bootstrapObject.transform.SetParent(playerObject.transform);
        bootstrap = bootstrapObject.AddComponent<TutorialBootstrap>();

        // Inyectar referencias privadas mediante reflexión
        SetPrivateField("hud", hudObject);
        SetPrivateField("playerMovement", playerMovement);
        SetPrivateField("playerLook", playerLook);
    }

    [TearDown]
    public void TearDown()
    {
        if (playerObject != null)
            Object.DestroyImmediate(playerObject);
    }


    [UnityTest]
    public IEnumerator HabilitarConDelay_DesbloqueaComponentesActivaHUDYConfiguraCursor()
    {
        // Act: Invocamos la corrutina privada usando reflexión
        Coroutine coroutine = (Coroutine)InvokePrivateMethod("HabilitarConDelay");
        
        yield return null; 

        yield return null;

        // Assert: Validar que todos los elementos se hayan habilitado/desbloqueado correctamente
        Assert.IsTrue(hudObject.activeSelf, "El HUD debería haberse activado.");
        Assert.IsFalse(playerMovement.MovementLocked, "El movimiento del jugador debería estar desbloqueado.");
        Assert.IsTrue(playerLook.enabled, "El componente PlayerLook debería estar habilitado.");
        Assert.IsFalse(testWeapon.InputLocked, "El input de las armas debería estar desbloqueado.");
        Assert.IsFalse(Cursor.visible, "El cursor debería estar oculto.");
        Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState, "El cursor debería estar bloqueado en el centro de la pantalla.");
    }


    private void SetPrivateField(string fieldName, object value)
    {
        typeof(TutorialBootstrap)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(bootstrap, value);
    }

    private object InvokePrivateMethod(string methodName, params object[] args)
    {
        MethodInfo method = typeof(TutorialBootstrap)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        return method?.Invoke(bootstrap, args);
    }

}
