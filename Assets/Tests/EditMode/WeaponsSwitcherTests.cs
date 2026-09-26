using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

public class WeaponsSwitcher
{
private GameObject switcherGO;
    private WeaponSwitcher switcher;
 
    [SetUp]
    public void SetUp()
    {
        switcherGO = new GameObject("WeaponSwitcher");
        switcher = switcherGO.AddComponent<WeaponSwitcher>();
    }
 
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(switcherGO);
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
 
    private void ConfigurarSlots(params (string weaponId, bool unlockedByDefault)[] datos)
    {
        var slots = new WeaponSwitcher.WeaponSlot[datos.Length];
        var unlocked = new bool[datos.Length];
        for (int i = 0; i < datos.Length; i++)
        {
            slots[i] = new WeaponSwitcher.WeaponSlot
            {
                weaponId = datos[i].weaponId,
                unlockedByDefault = datos[i].unlockedByDefault
            };
            unlocked[i] = datos[i].unlockedByDefault;
        }
 
        SetPrivateField(switcher, "slots", slots);
        SetPrivateField(switcher, "unlocked", unlocked);
    }
 
    // ---------- Awake ----------
 
    [Test]
    public void Awake_InicializaUnlockedSegunUnlockedByDefaultDeCadaSlot()
    {
        SetPrivateField(switcher, "slots", new[]
        {
            new WeaponSwitcher.WeaponSlot { weaponId = "Pistola", unlockedByDefault = true },
            new WeaponSwitcher.WeaponSlot { weaponId = "Rifle", unlockedByDefault = false }
        });
 
        InvokePrivateMethod(switcher, "Awake");
 
        var unlocked = (bool[])GetPrivateField(switcher, "unlocked");
        Assert.IsTrue(unlocked[0]);
        Assert.IsFalse(unlocked[1]);
    }
 
    [Test]
    public void Awake_ConControlesDeInput_NoLanzaExcepcion()
    {
        SetPrivateField(switcher, "slots", new WeaponSwitcher.WeaponSlot[0]);
 
        Assert.DoesNotThrow(() => InvokePrivateMethod(switcher, "Awake"));
        Assert.IsNotNull(GetPrivateField(switcher, "controls"));
    }
 
    // ---------- IsUnlocked / UnlockWeapon ----------
 
    [Test]
    public void IsUnlocked_ConArmaDesbloqueada_DevuelveTrue()
    {
        ConfigurarSlots(("Rifle", true));
 
        Assert.IsTrue(switcher.IsUnlocked("Rifle"));
    }
 
    [Test]
    public void IsUnlocked_ConArmaNoDesbloqueada_DevuelveFalse()
    {
        ConfigurarSlots(("Rifle", false));
 
        Assert.IsFalse(switcher.IsUnlocked("Rifle"));
    }
 
    [Test]
    public void IsUnlocked_ConIdInexistente_DevuelveFalseSinLanzarExcepcion()
    {
        ConfigurarSlots(("Rifle", true));
 
        bool resultado = false;
        Assert.DoesNotThrow(() => resultado = switcher.IsUnlocked("ArmaQueNoExiste"));
        Assert.IsFalse(resultado);
    }
 
    [Test]
    public void UnlockWeapon_ConEquipAfterUnlockFalse_SoloMarcaDesbloqueadaYNoLlamaRequestEquip()
    {
        ConfigurarSlots(("Rifle", false));
        SetPrivateField(switcher, "currentIndex", -1); // valor imposible: si RequestEquip
                                                        // se ejecutara, currentIndex cambiaría a 0.
 
        switcher.UnlockWeapon("Rifle", equipAfterUnlock: false);
 
        Assert.IsTrue(switcher.IsUnlocked("Rifle"));
        Assert.AreEqual(-1, GetPrivateField(switcher, "currentIndex"));
    }
 
    [Test]
    public void UnlockWeapon_ConIdInexistente_NoLanzaExcepcionYNoDesbloqueaNada()
    {
        ConfigurarSlots(("Rifle", false));
 
        Assert.DoesNotThrow(() => switcher.UnlockWeapon("ArmaQueNoExiste", equipAfterUnlock: false));
        Assert.IsFalse(switcher.IsUnlocked("Rifle"));
    }
 
    [Test]
    public void UnlockWeapon_ConEquipAfterUnlockTrueYYaEsElArmaActual_NoLanzaExcepcion()
    {
        // index 0 == currentIndex (0 por defecto): RequestEquip corta temprano
        // ("ya es el arma actual"), así no hace falta weaponRoot/Animator.
        ConfigurarSlots(("Rifle", false));
 
        Assert.DoesNotThrow(() => switcher.UnlockWeapon("Rifle", equipAfterUnlock: true));
        Assert.IsTrue(switcher.IsUnlocked("Rifle"));
    }
 
    // ---------- RequestEquip (guardas) ----------
 
    [Test]
    public void RequestEquip_ConIndiceFueraDeRango_NoLanzaExcepcion()
    {
        ConfigurarSlots(("Rifle", true));
 
        Assert.DoesNotThrow(() => switcher.RequestEquip(99));
        Assert.DoesNotThrow(() => switcher.RequestEquip(-1));
    }
 
    [Test]
    public void RequestEquip_ConArmaNoDesbloqueada_NoLanzaExcepcionYNoCambiaCurrentIndex()
    {
        ConfigurarSlots(("Pistola", true), ("Rifle", false));
 
        switcher.RequestEquip(1); // Rifle, no desbloqueado
 
        Assert.AreEqual(0, GetPrivateField(switcher, "currentIndex"));
    }
 
    [Test]
    public void RequestEquip_ConIsSwitchingEnTrue_NoLanzaExcepcionYNoCambiaCurrentIndex()
    {
        ConfigurarSlots(("Pistola", true), ("Rifle", true));
        SetPrivateField(switcher, "isSwitching", true);
 
        // Si esto disparara la corrutina, fallaría por weaponRoot nulo al querer
        // desactivar/activar los slots.
        Assert.DoesNotThrow(() => switcher.RequestEquip(1));
        Assert.AreEqual(0, GetPrivateField(switcher, "currentIndex"));
    }
 
    [Test]
    public void RequestEquip_ConIndiceIgualAlActual_NoLanzaExcepcion()
    {
        ConfigurarSlots(("Rifle", true));
 
        Assert.DoesNotThrow(() => switcher.RequestEquip(0));
    }
 
    // ---------- GetNextUnlockedIndex (privado) ----------
 
    [Test]
    public void GetNextUnlockedIndex_DevuelveElSiguienteDesbloqueadoEnOrdenCircular()
    {
        ConfigurarSlots(("Pistola", true), ("Rifle", false), ("Escopeta", true));
        SetPrivateField(switcher, "currentIndex", 0);
 
        int siguiente = (int)InvokePrivateMethod(switcher, "GetNextUnlockedIndex");
 
        Assert.AreEqual(2, siguiente); // salta el índice 1 (no desbloqueado)
    }
 
    [Test]
    public void GetNextUnlockedIndex_SinOtraArmaDesbloqueada_DevuelveElIndiceActual()
    {
        ConfigurarSlots(("Pistola", true), ("Rifle", false));
        SetPrivateField(switcher, "currentIndex", 0);
 
        int siguiente = (int)InvokePrivateMethod(switcher, "GetNextUnlockedIndex");
 
        Assert.AreEqual(0, siguiente);
    }
 
    // ---------- OnSwitchWeapon (privado; no usa el CallbackContext recibido) ----------
 
    [Test]
    public void OnSwitchWeapon_LlamaRequestEquipConElSiguienteDesbloqueado()
    {
        ConfigurarSlots(("Pistola", true), ("Rifle", true));
        SetPrivateField(switcher, "currentIndex", 0);
 
        // El método no usa el contenido del context, así que un valor
        // por defecto es seguro para invocarlo por reflexión.
        InvokePrivateMethod(switcher, "OnSwitchWeapon", default(InputAction.CallbackContext));
 
        Assert.AreEqual(1, GetPrivateField(switcher, "currentIndex"));
    }
}
