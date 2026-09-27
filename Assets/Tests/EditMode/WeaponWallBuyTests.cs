using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

public class WeaponWallBuyTests
{
    private GameObject controllerGO;
    private WeaponWallBuy controller;
 
    private System.Collections.Generic.List<GameObject> scratch;
 
    [SetUp]
    public void SetUp()
    {
        scratch = new System.Collections.Generic.List<GameObject>();
 
        controllerGO = new GameObject("WeaponWallBuy_AR");
        controllerGO.AddComponent<BoxCollider>();
        controller = controllerGO.AddComponent<WeaponWallBuy>();
        SetPrivateField(controller, "interactRange", 5f);
    }
 
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(controllerGO);
        foreach (GameObject go in scratch)
        {
            if (go != null) Object.DestroyImmediate(go);
        }
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
 
    private static object InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo mi = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mi, $"No se encontró el método privado '{methodName}'.");
        return mi.Invoke(target, args);
    }
 
    private Camera CreateCamera(string name, Vector3 position, Vector3 forward)
    {
        var camGO = new GameObject(name);
        scratch.Add(camGO);
        camGO.transform.position = position;
        camGO.transform.rotation = Quaternion.LookRotation(forward);
        return camGO.AddComponent<Camera>();
    }
 
    private GameObject CreateColliderTarget(string name, Vector3 position)
    {
        var go = new GameObject(name);
        scratch.Add(go);
        go.transform.position = position;
        go.AddComponent<BoxCollider>();
        return go;
    }
 
    // ---------- Awake ----------
 
    [Test]
    public void Awake_InicializaLosControlesDeInput()
    {
        InvokePrivateMethod(controller, "Awake");
 
        Assert.IsNotNull(GetPrivateField(controller, "controls"));
    }
 
    // ---------- OnEnable / OnDisable ----------
 
    [Test]
    public void OnEnable_NoLanzaExcepcionLuegoDeAwake()
    {
        InvokePrivateMethod(controller, "Awake");
 
        Assert.DoesNotThrow(() => InvokePrivateMethod(controller, "OnEnable"));
    }
 
    [Test]
    public void OnDisable_NoLanzaExcepcionLuegoDeAwakeYOnEnable()
    {
        InvokePrivateMethod(controller, "Awake");
        InvokePrivateMethod(controller, "OnEnable");
 
        Assert.DoesNotThrow(() => InvokePrivateMethod(controller, "OnDisable"));
    }
 
    // ---------- BuscarCamaraJugadorLocal ----------
 
    [Test]
    public void BuscarCamaraJugadorLocal_SinCamarasEnLaEscena_DejaLocalPlayerCameraEnNullYAvisa()
    {
        LogAssert.Expect(LogType.Warning, "[WeaponWallBuy] No se encontró cámara del jugador local.");
 
        InvokePrivateMethod(controller, "BuscarCamaraJugadorLocal");
 
        Assert.IsNull(GetPrivateField(controller, "localPlayerCamera"));
    }
 
    [Test]
    public void BuscarCamaraJugadorLocal_ConCamaraMainCameraSinNetworkObject_LaAsigna()
    {
        Camera cam = CreateCamera("MainCam", Vector3.zero, Vector3.forward);
        cam.tag = "MainCamera";
 
        InvokePrivateMethod(controller, "BuscarCamaraJugadorLocal");
 
        Assert.AreSame(cam, GetPrivateField(controller, "localPlayerCamera"));
    }
 
    [Test]
    public void BuscarCamaraJugadorLocal_ConCamaraCacheadaYActiva_NoVuelveABuscar()
    {
        Camera camActual = CreateCamera("CamActual", Vector3.zero, Vector3.forward);
        Camera otraMain = CreateCamera("OtraMain", new Vector3(10, 0, 0), Vector3.forward);
        otraMain.tag = "MainCamera";
        SetPrivateField(controller, "localPlayerCamera", camActual);
 
        InvokePrivateMethod(controller, "BuscarCamaraJugadorLocal");
 
        Assert.AreSame(camActual, GetPrivateField(controller, "localPlayerCamera"));
    }
 
    [Test]
    public void BuscarCamaraJugadorLocal_ConCamaraCacheadaInactiva_BuscaUnaNueva()
    {
        Camera camVieja = CreateCamera("CamVieja", Vector3.zero, Vector3.forward);
        camVieja.gameObject.SetActive(false);
        Camera camNueva = CreateCamera("CamNueva", new Vector3(10, 0, 0), Vector3.forward);
        camNueva.tag = "MainCamera";
        SetPrivateField(controller, "localPlayerCamera", camVieja);
 
        InvokePrivateMethod(controller, "BuscarCamaraJugadorLocal");
 
        Assert.AreSame(camNueva, GetPrivateField(controller, "localPlayerCamera"));
    }
 
    // ---------- EstaMirandoElArma ----------
 
    [Test]
    public void EstaMirandoElArma_SinLocalPlayerCamera_DevuelveFalseYAvisa()
    {
        LogAssert.Expect(LogType.Warning, "[WeaponWallBuy] Falta localPlayerCamera.");
 
        bool resultado = (bool)InvokePrivateMethod(controller, "EstaMirandoElArma");
 
        Assert.IsFalse(resultado);
    }
 
    [Test]
    public void EstaMirandoElArma_RaycastSinPegarEnNada_DevuelveFalse()
    {
        Camera cam = CreateCamera("Cam", Vector3.zero, Vector3.forward);
        SetPrivateField(controller, "localPlayerCamera", cam);
        // El collider de controllerGO está lejos del rayo (no en su trayectoria).
        controllerGO.transform.position = new Vector3(100, 100, 100);
 
        bool resultado = (bool)InvokePrivateMethod(controller, "EstaMirandoElArma");
 
        Assert.IsFalse(resultado);
    }
 
    [Test]
    public void EstaMirandoElArma_RaycastPegaEnEsteMismoWeaponWallBuy_DevuelveTrue()
    {
        Camera cam = CreateCamera("Cam", Vector3.zero, Vector3.forward);
        SetPrivateField(controller, "localPlayerCamera", cam);
        controllerGO.transform.position = Vector3.forward * 2f;
        Physics.SyncTransforms();
 
        bool resultado = (bool)InvokePrivateMethod(controller, "EstaMirandoElArma");
 
        Assert.IsTrue(resultado);
    }
 
    [Test]
    public void EstaMirandoElArma_RaycastPegaEnOtroWeaponWallBuy_DevuelveFalse()
    {
        Camera cam = CreateCamera("Cam", Vector3.zero, Vector3.forward);
        SetPrivateField(controller, "localPlayerCamera", cam);
        controllerGO.transform.position = new Vector3(100, 100, 100); // fuera del rayo
 
        var otroGO = new GameObject("OtraArma");
        scratch.Add(otroGO);
        otroGO.AddComponent<BoxCollider>();
        otroGO.AddComponent<WeaponWallBuy>();
        otroGO.transform.position = Vector3.forward * 2f;
        Physics.SyncTransforms();
 
        bool resultado = (bool)InvokePrivateMethod(controller, "EstaMirandoElArma");
 
        Assert.IsFalse(resultado);
    }
 
    // ---------- Update (sólo la rama sin cámara disponible) ----------
 
    [Test]
    public void Update_SinCamaraDisponible_NoLanzaExcepcionYSaleTemprano()
    {
        InvokePrivateMethod(controller, "Awake");
 
        Assert.DoesNotThrow(() => InvokePrivateMethod(controller, "Update"));
        Assert.IsNull(GetPrivateField(controller, "localPlayerCamera"));
    }
 
    // ---------- TryPurchase (sólo ramas de "falta componente") ----------
 
    [Test]
    public void TryPurchase_SinWeaponSwitcherEnElJugador_NoLanzaExcepcionYAvisa()
    {
        Camera cam = CreateCamera("PlayerCam", Vector3.zero, Vector3.forward);
        SetPrivateField(controller, "localPlayerCamera", cam);
 
        LogAssert.Expect(LogType.Warning, "[WeaponWallBuy] No se encontró WeaponSwitcher en el jugador.");
 
        Assert.DoesNotThrow(() => InvokePrivateMethod(controller, "TryPurchase"));
    }
 
    [Test]
    public void TryPurchase_ConWeaponSwitcherSinPlayerScore_NoLanzaExcepcionYAvisa()
    {
        Camera cam = CreateCamera("PlayerCam", Vector3.zero, Vector3.forward);
        cam.gameObject.AddComponent<WeaponSwitcher>();
        SetPrivateField(controller, "localPlayerCamera", cam);
 
        LogAssert.Expect(LogType.Warning, "[WeaponWallBuy] No se encontró PlayerScore en el jugador.");
 
        Assert.DoesNotThrow(() => InvokePrivateMethod(controller, "TryPurchase"));
    }
 
    [Test]
    public void TryPurchase_ConArmaYaDesbloqueada_NoLanzaExcepcionNiIntentaComprar()
    {
        Camera cam = CreateCamera("PlayerCam", Vector3.zero, Vector3.forward);
        string weaponId = (string)GetPrivateField(controller, "weaponId");
 
        var switcherGO = cam.gameObject; // mismo root que la cámara
        WeaponSwitcher switcher = switcherGO.AddComponent<WeaponSwitcher>();
        SetPrivateField(switcher, "slots", new[] { new WeaponSwitcher.WeaponSlot { weaponId = weaponId } });
        SetPrivateField(switcher, "unlocked", new[] { true });
 
        switcherGO.AddComponent<PlayerScore>();
 
        SetPrivateField(controller, "localPlayerCamera", cam);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod(controller, "TryPurchase"));
 
        // Al cortar por "ya desbloqueada", no debería haber quedado nada pendiente
        // (si hubiera intentado comprar, pendingSwitcher/pendingScore no serían null).
        Assert.IsNull(GetPrivateField(controller, "pendingSwitcher"));
        Assert.IsNull(GetPrivateField(controller, "pendingScore"));
    }
 
    // ---------- OnPurchaseResult ----------
 
    [Test]
    public void OnPurchaseResult_ConWeaponIdDistinto_NoModificaPendientes()
    {
        var scoreGO = new GameObject("PlayerScore");
        scratch.Add(scoreGO);
        PlayerScore score = scoreGO.AddComponent<PlayerScore>();
        SetPrivateField(controller, "pendingScore", score);
        SetPrivateField(controller, "pendingSwitcher", null);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod(controller, "OnPurchaseResult", "OtraArma", true));
 
        Assert.AreSame(score, GetPrivateField(controller, "pendingScore"));
    }
 
    [Test]
    public void OnPurchaseResult_ConWeaponIdCoincidenteYExitoFalso_LimpiaPendientesSinLanzarExcepcion()
    {
        var scoreGO = new GameObject("PlayerScore");
        scratch.Add(scoreGO);
        PlayerScore score = scoreGO.AddComponent<PlayerScore>();
        SetPrivateField(controller, "pendingScore", score);
        SetPrivateField(controller, "pendingSwitcher", null); // evita tocar WeaponSwitcher.UnlockWeapon
 
        string weaponId = (string)GetPrivateField(controller, "weaponId");
 
        Assert.DoesNotThrow(() => InvokePrivateMethod(controller, "OnPurchaseResult", weaponId, false));
 
        Assert.IsNull(GetPrivateField(controller, "pendingScore"));
        Assert.IsNull(GetPrivateField(controller, "pendingSwitcher"));
    }
 
    [Test]
    public void OnPurchaseResult_ConWeaponIdCoincidenteYExitoTrue_DesbloqueaElArmaYLimpiaPendientes()
    {
        string weaponId = (string)GetPrivateField(controller, "weaponId");
 
        var switcherGO = new GameObject("WeaponSwitcher");
        scratch.Add(switcherGO);
        WeaponSwitcher switcher = switcherGO.AddComponent<WeaponSwitcher>();
        SetPrivateField(switcher, "slots", new[] { new WeaponSwitcher.WeaponSlot { weaponId = weaponId } });
        SetPrivateField(switcher, "unlocked", new[] { false });
        // currentIndex ya vale 0 por defecto: coincide con el índice del slot
        // desbloqueado, así RequestEquip() corta temprano ("ya es el arma
        // actual") y no hace falta configurar weaponRoot/Animator.
 
        var scoreGO = new GameObject("PlayerScore");
        scratch.Add(scoreGO);
        PlayerScore score = scoreGO.AddComponent<PlayerScore>();
 
        SetPrivateField(controller, "pendingSwitcher", switcher);
        SetPrivateField(controller, "pendingScore", score);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod(controller, "OnPurchaseResult", weaponId, true));
 
        Assert.IsTrue(switcher.IsUnlocked(weaponId));
        Assert.IsNull(GetPrivateField(controller, "pendingSwitcher"));
        Assert.IsNull(GetPrivateField(controller, "pendingScore"));
    }
}
