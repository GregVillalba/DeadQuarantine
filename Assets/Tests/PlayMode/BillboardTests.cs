using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class BillboardTests
{
    private GameObject billboardObject;
    private Billboard billboard;
    private GameObject cameraObject;
    private Camera testCamera;

    [SetUp]
    public void SetUp()
    {
        // 1. Arrange: Crear la cámara de prueba en la escena
        cameraObject = new GameObject("TestCamera");
        testCamera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.position = new Vector3(0f, 0f, -10f); // Cámara enfrente del origen

        // Crear el objeto Billboard
        billboardObject = new GameObject("TestBillboard");
        billboardObject.transform.position = Vector3.zero;
        billboard = billboardObject.AddComponent<Billboard>();
    }

    [TearDown]
    public void TearDown()
    {
        if (billboardObject != null)
            Object.DestroyImmediate(billboardObject);
        if (cameraObject != null)
            Object.DestroyImmediate(cameraObject);
    }


    [UnityTest]
    public IEnumerator LateUpdate_OrientaHaciaLaCamara_ConEjeVerticalBloqueado()
    {
        // Arrange: Asegurar que el bloqueo de eje vertical está activo (por defecto es true)
        SetPrivateField("bloquearEjeVertical", true);

        // Posicionamos el objeto Billboard y la cámara con desalineación en altura (eje Y)
        billboardObject.transform.position = new Vector3(0f, 0f, 0f);
        cameraObject.transform.position = new Vector3(5f, 10f, -5f); // Con componente Y considerable

        // Act: Invocamos LateUpdate mediante reflexión
        InvokePrivateMethod("LateUpdate");
        yield return null;

        // Assert: La rotación final no debería haber inclinado el objeto en el eje X o Z 
        // debido a que bloquearEjeVertical = true fija direccion.y = 0f.
        Vector3 forwardDireccion = billboardObject.transform.forward;
        
        // El vector forward horizontalizado debe apuntar hacia la cámara en el plano XZ
        Assert.AreEqual(0f, forwardDireccion.y, 0.01f, "El eje Y del forward debería estar bloqueado a cero.");
    }

    [UnityTest]
    public IEnumerator LateUpdate_OrientaLibremente_SinBloqueoVertical()
    {
        // Arrange: Desactivamos el bloqueo del eje vertical
        SetPrivateField("bloquearEjeVertical", false);

        billboardObject.transform.position = new Vector3(0f, 0f, 0f);
        cameraObject.transform.position = new Vector3(0f, 5f, -5f); // Cámara arriba

        // Act
        InvokePrivateMethod("LateUpdate");
        yield return null;

        // Assert: Al estar libre el eje vertical, el Billboard debe inclinarse hacia arriba apuntando a la cámara
        Camera camaraDetectada = (Camera)GetPrivateField("camaraObjetivo");
        Assert.AreEqual(testCamera, camaraDetectada, "Debería haber encontrado y asignado la cámara activa correctamente.");
        
        // Verificamos que la rotación se haya aplicado
        Assert.AreNotEqual(Quaternion.identity, billboardObject.transform.rotation, "El Billboard debería haber rotado hacia la cámara.");
    }

    [UnityTest]
    public IEnumerator BuscarCamaraActiva_EncuentraLaCamaraValida()
    {
        // Arrange: Desactivamos la cámara actual para probar la búsqueda
        testCamera.enabled = false;

        GameObject segundaCamaraObj = new GameObject("ActiveCamera");
        Camera segundaCamara = segundaCamaraObj.AddComponent<Camera>();

        // Act: Invocamos el método privado de búsqueda de cámara
        Camera camaraEncontrada = (Camera)InvokePrivateMethod("BuscarCamaraActiva");

        // Assert
        Assert.AreEqual(segundaCamara, camaraEncontrada, "Debería encontrar la única cámara activa y habilitada en la escena.");

        Object.DestroyImmediate(segundaCamaraObj);
        yield return null;
    }


    private void SetPrivateField(string fieldName, object value)
    {
        typeof(Billboard)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(billboard, value);
    }

    private object GetPrivateField(string fieldName)
    {
        return typeof(Billboard)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(billboard);
    }

    private object InvokePrivateMethod(string methodName, params object[] args)
    {
        MethodInfo method = typeof(Billboard)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        return method?.Invoke(billboard, args);
    }


}
