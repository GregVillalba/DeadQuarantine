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

    [UnityTest]
    public IEnumerator PurchasePanelStaysAboveOverlappingCatalogAndReceivesPointerHits()
    {
        var root = new GameObject("Merchant priority UI",typeof(RectTransform),typeof(UnityEngine.Canvas),typeof(UnityEngine.UI.GraphicRaycaster));
        var eventsObject = new GameObject("Merchant priority events",typeof(UnityEngine.EventSystems.EventSystem));
        var shop = root.AddComponent<NPCShopUI>();shop.enabled=false;
        bool oldVisible=Cursor.visible;var oldLock=Cursor.lockState;
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        try
        {
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder=100;
            var cameraObject=new GameObject("Merchant priority camera",typeof(Camera));cameraObject.transform.SetParent(root.transform,false);cameraObject.transform.localPosition=new Vector3(0,0,-10);
            var purchase=new GameObject("Purchase",typeof(RectTransform),typeof(UnityEngine.UI.Image));
            purchase.transform.SetParent(root.transform,false);
            var rect=purchase.GetComponent<RectTransform>();rect.anchoredPosition=new Vector2(24,-70);rect.sizeDelta=new Vector2(300,60);
            var catalog=new GameObject("Catalog",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.GraphicRaycaster));
            catalog.transform.SetParent(root.transform,false);
            var catalogRect=catalog.GetComponent<RectTransform>();catalogRect.anchoredPosition=rect.anchoredPosition;catalogRect.sizeDelta=rect.sizeDelta;
            var catalogCanvas=catalog.GetComponent<Canvas>();catalogCanvas.overrideSorting=true;catalogCanvas.sortingOrder=32000;
            typeof(NPCShopUI).GetField("panelCompra",flags).SetValue(shop,purchase);
            typeof(NPCShopUI).GetField("shopPanel",flags).SetValue(shop,root);
            LogAssert.Expect(LogType.Error,"[NPCShopUI] Falta rowPrefab, rowsContainer o mercader.");
            typeof(NPCShopUI).GetMethod("AbrirTienda",flags).Invoke(shop,new object[]{vendor});
            typeof(NPCShopUI).GetMethod("SeleccionarOferta",flags).Invoke(shop,new object[]{new WeaponOffer{weaponId="Rifle",precioPilaDeLatas=3}});
            yield return null;Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();

            Assert.IsTrue(purchase.activeInHierarchy);
            Assert.AreEqual(new Vector2(24,-70),rect.anchoredPosition);
            Assert.AreEqual(new Vector2(300,60),rect.sizeDelta);
            var pointer=new UnityEngine.EventSystems.PointerEventData(eventsObject.GetComponent<UnityEngine.EventSystems.EventSystem>()) {position=RectTransformUtility.WorldToScreenPoint(null,rect.position)};
            var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            eventsObject.GetComponent<UnityEngine.EventSystems.EventSystem>().RaycastAll(pointer,hits);
            Assert.IsNotEmpty(hits,"The visible purchase panel receives pointer hits; screen="+Screen.width+"x"+Screen.height+" canvas="+root.GetComponent<Canvas>().pixelRect+" pointer="+pointer.position+" graphicDepth="+purchase.GetComponent<UnityEngine.UI.Image>().depth);
            Assert.AreEqual(purchase,hits[0].gameObject,"Purchase stays above the overlapping catalog image");
        }
        finally
        {
            shop.CerrarTienda();Object.Destroy(root);Object.Destroy(eventsObject);Cursor.visible=oldVisible;Cursor.lockState=oldLock;
        }
        yield return null;
    }
}
