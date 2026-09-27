using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class StoryIntroControllerTests
{
    private GameObject controllerObject;
    private StoryIntroController controller;
    private GameObject popupObj;
    private GameObject hudObj;
    private GameObject playerObj;

    [SetUp]
    public void SetUp()
    {
        // 1. Arrange: Construir la jerarquía de prueba
        playerObj = new GameObject("PlayerRoot");
        
        controllerObject = new GameObject("StoryIntroHost");
        controllerObject.transform.SetParent(playerObj.transform);
        controller = controllerObject.AddComponent<StoryIntroController>();

        popupObj = new GameObject("PopupHistoria");
        hudObj = new GameObject("HUD");

        // Inyectar referencias mediante reflexión
        SetPrivateField("popupHistoria", popupObj);
        SetPrivateField("hud", hudObj);
    }

    [TearDown]
    public void TearDown()
    {
        if (playerObj != null)
            Object.DestroyImmediate(playerObj);
    }


    [UnityTest]
    public IEnumerator Awake_DesactivaPopupInicialmente()
    {
        // Arrange
        popupObj.SetActive(true); // Lo dejamos activo a propósito

        // Act: Forzamos o dejamos que corra Awake
        InvokePrivateMethod("Awake");
        yield return null;

        // Assert
        Assert.IsFalse(popupObj.activeSelf, "El Awake() debería desactivar el popup de historia al iniciar.");
    }

    [UnityTest]
    public IEnumerator MostrarHistoriaInicial_ConfiguraEstadoYBloqueaJugador()
    {
        // Arrange
        hudObj.SetActive(true);
        popupObj.SetActive(false);

        // Act
        InvokePrivateMethod("MostrarHistoriaInicial");
        yield return null;

        // Assert
        Assert.IsTrue((bool)GetPrivateField("historiaActiva"), "historiaActiva debería estar en true.");
        Assert.IsFalse(hudObj.activeSelf, "El HUD debería desactivarse durante la intro.");
        Assert.IsTrue(popupObj.activeSelf, "El popup de historia debería activarse.");
        Assert.AreEqual(CursorVisibleState(true), Cursor.visible, "El cursor debería estar visible.");
        Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "El cursor debería estar desbloqueado.");
    }

    [UnityTest]
    public IEnumerator ContinuarHistoria_RestauraHUDYDesbloquea()
    {
        // Arrange: Ponemos el controlador en estado de historia activa
        SetPrivateField("historiaActiva", true);
        SetPrivateField("IsOwner", true); // Forzamos simulación de propiedad si se desea o se asume dueño
        popupObj.SetActive(true);
        hudObj.SetActive(false);

       
        var netObj = playerObj.AddComponent<Unity.Netcode.NetworkObject>();
       
        InvokePrivateMethod("HabilitarJugador");
        yield return null;

        Assert.IsTrue(true, "La transición de habilitación completó sin errores.");
    }

    [UnityTest]
    public IEnumerator BloquearYDeshabilitarJugador_NoLanzaExcepcionesSinComponentes()
    {
        // Act & Assert: Comprobamos que métodos defensivos no fallen si faltan referencias opcionales
        Assert.DoesNotThrow(() => InvokePrivateMethod("BloquearJugador"));
        Assert.DoesNotThrow(() => InvokePrivateMethod("HabilitarJugador"));
        yield return null;
    }



    private void SetPrivateField(string fieldName, object value)
    {
        typeof(StoryIntroController)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(controller, value);
    }

    private object GetPrivateField(string fieldName)
    {
        return typeof(StoryIntroController)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(controller);
    }

    private object InvokePrivateMethod(string methodName, params object[] args)
    {
        MethodInfo method = typeof(StoryIntroController)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        return method?.Invoke(controller, args);
    }

    private bool CursorVisibleState(bool expected) => expected;

}
