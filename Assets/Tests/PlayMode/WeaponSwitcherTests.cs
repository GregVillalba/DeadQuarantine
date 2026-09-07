using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class WeaponSwitcherTests
{
    private GameObject testGO;
    private WeaponSwitcher switcher;


    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = typeof(WeaponSwitcher).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"No se encontró el campo '{fieldName}' por reflexión.");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = typeof(WeaponSwitcher).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"No se encontró el campo '{fieldName}' por reflexión.");
        return (T)field.GetValue(target);
    }

    private static object InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = typeof(WeaponSwitcher).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"No se encontró el método '{methodName}' por reflexión.");
        return method.Invoke(target, args);
    }

    private static WeaponSwitcher.WeaponSlot MakeSlot(string id, bool unlockedByDefault)
    {
        return new WeaponSwitcher.WeaponSlot
        {
            weaponId = id,
            weaponRoot = new GameObject("Root_" + id),
            weaponComponent = null,
            overrideController = null,
            unlockedByDefault = unlockedByDefault,
            weaponIcon = null,
            casingPrefab = null,
            casingEjectPoint = null
        };
    }

    // ---------------------------------------------------------
    // Setup / Teardown
    // ---------------------------------------------------------

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Slot 0 "pistol": desbloqueada por defecto.
        // Slot 1 "rifle" y 2 "shotgun": bloqueadas.
        var slots = new[]
        {
            MakeSlot("pistol", true),
            MakeSlot("rifle", false),
            MakeSlot("shotgun", false),
        };

     
        testGO = new GameObject("WeaponSwitcherTestObject");
        testGO.SetActive(false);

        switcher = testGO.AddComponent<WeaponSwitcher>();
        SetPrivateField(switcher, "slots", slots);
        SetPrivateField(switcher, "startingSlotIndex", 0);
        

        testGO.SetActive(true); // dispara Awake() y OnEnable()
        yield return null;      // deja correr Start() antes de testear
    }

    [TearDown]
    public void TearDown()
    {
        if (testGO != null)
            Object.DestroyImmediate(testGO);
    }

    // ---------------------------------------------------------
    // IsUnlocked
    // ---------------------------------------------------------

    [Test]
    public void IsUnlocked_DevuelveTrue_ParaArmaDesbloqueadaPorDefecto()
    {
        Assert.IsTrue(switcher.IsUnlocked("pistol"));
    }

    [Test]
    public void IsUnlocked_DevuelveFalse_ParaArmaBloqueada()
    {
        Assert.IsFalse(switcher.IsUnlocked("rifle"));
    }

    [Test]
    public void IsUnlocked_DevuelveFalse_ParaIdInexistente()
    {
        Assert.IsFalse(switcher.IsUnlocked("id_que_no_existe"));
    }

    // ---------------------------------------------------------
    // UnlockWeapon
    // ---------------------------------------------------------

    [Test]
    public void UnlockWeapon_MarcaElArmaComoDesbloqueada()
    {
        switcher.UnlockWeapon("rifle", equipAfterUnlock: false);
        Assert.IsTrue(switcher.IsUnlocked("rifle"));
    }

    [Test]
    public void UnlockWeapon_ConIdInexistente_NoLanzaExcepcionYNoAfectaEstado()
    {
        Assert.DoesNotThrow(() => switcher.UnlockWeapon("id_que_no_existe"));
        int currentIndex = GetPrivateField<int>(switcher, "currentIndex");
        Assert.AreEqual(0, currentIndex);
    }

    [Test]
    public void UnlockWeapon_EquipAfterUnlockFalse_NoCambiaElArmaActual()
    {
        switcher.UnlockWeapon("rifle", equipAfterUnlock: false);

        int currentIndex = GetPrivateField<int>(switcher, "currentIndex");
        Assert.AreEqual(0, currentIndex, "No debería re-equipar si equipAfterUnlock es false.");
    }

    [Test]
    public void UnlockWeapon_EquipAfterUnlockTrue_CambiaAlArmaRecienDesbloqueada()
    {
        switcher.UnlockWeapon("rifle", equipAfterUnlock: true);

        int currentIndex = GetPrivateField<int>(switcher, "currentIndex");
        Assert.AreEqual(1, currentIndex, "Sin Animator asignado, el cambio de arma debería resolverse en el mismo frame.");
    }

    // ---------------------------------------------------------
    // RequestEquip
    // ---------------------------------------------------------

    [Test]
    public void RequestEquip_ConIndiceFueraDeRango_NoHaceNada()
    {
        switcher.RequestEquip(99);

        int currentIndex = GetPrivateField<int>(switcher, "currentIndex");
        Assert.AreEqual(0, currentIndex);
    }

    [Test]
    public void RequestEquip_ConArmaBloqueada_NoHaceNada()
    {
        switcher.RequestEquip(1); // "rifle" sigue bloqueada

        int currentIndex = GetPrivateField<int>(switcher, "currentIndex");
        bool isSwitching = GetPrivateField<bool>(switcher, "isSwitching");
        Assert.AreEqual(0, currentIndex);
        Assert.IsFalse(isSwitching);
    }

    [Test]
    public void RequestEquip_ConElMismoIndiceActual_NoHaceNada()
    {
        switcher.RequestEquip(0);

        bool isSwitching = GetPrivateField<bool>(switcher, "isSwitching");
        Assert.IsFalse(isSwitching);
    }

    [Test]
    public void RequestEquip_SiYaHayUnCambioEnCurso_IgnoraLaNuevaSolicitud()
    {
        switcher.UnlockWeapon("rifle", equipAfterUnlock: false);
        switcher.UnlockWeapon("shotgun", equipAfterUnlock: false);

        SetPrivateField(switcher, "isSwitching", true);
        switcher.RequestEquip(1);

        int pendingIndex = GetPrivateField<int>(switcher, "pendingIndex");
        Assert.AreNotEqual(1, pendingIndex, "No debería aceptar una nueva solicitud mientras isSwitching es true.");
    }

    [Test]
    public void RequestEquip_SinAnimator_CompletaElCambioYActivaElNuevoRoot()
    {
        switcher.UnlockWeapon("rifle", equipAfterUnlock: false);
        var slots = GetPrivateField<WeaponSwitcher.WeaponSlot[]>(switcher, "slots");

        switcher.RequestEquip(1);

        int currentIndex = GetPrivateField<int>(switcher, "currentIndex");
        Assert.AreEqual(1, currentIndex);
        Assert.IsFalse(slots[0].weaponRoot.activeSelf, "El arma anterior debería desactivarse.");
        Assert.IsTrue(slots[1].weaponRoot.activeSelf, "El arma nueva debería activarse.");

        // isSwitching queda en true hasta que un Animation Event real
        // llame a NotifyUnholsterFinished(); eso se prueba por separado.
        bool isSwitching = GetPrivateField<bool>(switcher, "isSwitching");
        Assert.IsTrue(isSwitching);
    }

    // ---------------------------------------------------------
    // NotifyUnholsterFinished
    // ---------------------------------------------------------

    [Test]
    public void NotifyUnholsterFinished_BajaElFlagIsSwitching()
    {
        SetPrivateField(switcher, "isSwitching", true);

        switcher.NotifyUnholsterFinished();

        bool isSwitching = GetPrivateField<bool>(switcher, "isSwitching");
        Assert.IsFalse(isSwitching);
    }


    [Test]
    public void GetNextUnlockedIndex_SaltaLosSlotsBloqueados()
    {
        SetPrivateField(switcher, "unlocked", new[] { true, false, true });
        SetPrivateField(switcher, "currentIndex", 0);

        int next = (int)InvokePrivateMethod(switcher, "GetNextUnlockedIndex");

        Assert.AreEqual(2, next);
    }

    [Test]
    public void GetNextUnlockedIndex_SiNoHayOtraArmaDesbloqueada_DevuelveLaActual()
    {
        SetPrivateField(switcher, "unlocked", new[] { true, false, false });
        SetPrivateField(switcher, "currentIndex", 0);

        int next = (int)InvokePrivateMethod(switcher, "GetNextUnlockedIndex");

        Assert.AreEqual(0, next);
    }

    [Test]
    public void GetNextUnlockedIndex_DaLaVueltaAlPrincipioDeLaLista()
    {
        SetPrivateField(switcher, "unlocked", new[] { true, true, false });
        SetPrivateField(switcher, "currentIndex", 1);

        int next = (int)InvokePrivateMethod(switcher, "GetNextUnlockedIndex");

        Assert.AreEqual(0, next);
    }
}