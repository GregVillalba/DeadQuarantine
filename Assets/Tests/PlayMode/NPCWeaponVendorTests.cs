using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;

[TestFixture]
public class NPCWeaponVendorTests
{
    private GameObject vendorObject;
    private NPCWeaponVendor vendor;

    [SetUp]
    public void SetUp()
    {
        vendorObject = new GameObject("TestMerchant");
        vendor = vendorObject.AddComponent<NPCWeaponVendor>();
    }

    [TearDown]
    public void TearDown()
    {
        if (vendorObject != null)
            Object.DestroyImmediate(vendorObject);
    }

    #region Métodos Auxiliares de Reflexión

    private void SetPrivateField(string fieldName, object value)
    {
        typeof(NPCWeaponVendor)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(vendor, value);
    }

    private object GetPrivateField(string fieldName)
    {
        return typeof(NPCWeaponVendor)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(vendor);
    }

    private object InvokePrivateMethod(string methodName, params object[] args)
    {
        MethodInfo method = typeof(NPCWeaponVendor)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        return method?.Invoke(vendor, args);
    }

    #endregion

    [Test]
    public void Awake_ConfiguraAudioSourceParaIgnorarPausa()
    {
        // Arrange
        AudioSource audioSource = vendorObject.AddComponent<AudioSource>();
        audioSource.ignoreListenerPause = false;

        // Act: Forzamos o simulamos la ejecución de Awake llamando a un método interno o reinicializando
        // Nota: Como Awake es privado de Unity, podemos invocarlo vía reflexión si requerimos probarlo explícitamente,
        // o dejar que Unity lo dispare al hacer AddComponent (si el GameObject se activa).
        InvokePrivateMethod("Awake");

        // Assert
        Assert.IsTrue(audioSource.ignoreListenerPause, 
            "Awake() debería configurar ignoreListenerPause en true en el AudioSource.");
    }

    [Test]
    public void NombreMercader_DevuelveElNombreCorrectoDelGameObject()
    {
        // Arrange
        vendorObject.name = "MercaderArmasTest";

        // Act & Assert
        Assert.AreEqual("MercaderArmasTest", vendor.NombreMercader, 
            "La propiedad NombreMercader debe retornar exactamente el nombre del GameObject.");
    }

    [Test]
    public void RequestPurchase_ConWeaponIdInexistente_NoGeneraEstadoPendiente()
    {
        // Act: Intentamos comprar un arma que no existe en las ofertas
        InvokePrivateMethod("RequestPurchase", "arma_inexistente_999");

        // Assert: Los campos privados pendientes deben permanecer en null
        Assert.IsNull(GetPrivateField("pendingWeaponId"), 
            "pendingWeaponId debe ser null si el arma solicitada no existe.");
        Assert.IsNull(GetPrivateField("pendingSwitcher"), 
            "pendingSwitcher debe ser null si no hay oferta válida.");
    }

    [UnityTest]
    public IEnumerator OnPurchaseResultInterno_ConExito_DesbloqueaArmaYDisparaEvento()
    {
        // Arrange
        string testWeaponId = "plasma_rifle";
        SetPrivateField("pendingWeaponId", testWeaponId);

        bool eventoDisparado = false;
        string armaRecibida = "";
        bool resultadoRecibido = false;

        vendor.OnPurchaseResult += (id, exito) =>
        {
            eventoDisparado = true;
            armaRecibida = id;
            resultadoRecibido = exito;
        };

        // Act: Invocamos el callback interno simulando una respuesta exitosa del servidor
        InvokePrivateMethod("OnPurchaseResultInterno", testWeaponId, true);

        yield return null;

        // Assert
        Assert.IsTrue(eventoDisparado, "El evento OnPurchaseResult debería haberse disparado.");
        Assert.AreEqual(testWeaponId, armaRecibida, "El ID del arma en el evento debe coincidir.");
        Assert.IsTrue(resultadoRecibido, "El resultado del evento debe ser true.");
        
        // Verificar limpieza de estados pendientes
        Assert.IsNull(GetPrivateField("pendingWeaponId"), "pendingWeaponId debe limpiarse tras procesar el resultado.");
    }
}
