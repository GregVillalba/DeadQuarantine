using System.Reflection;
using NUnit.Framework;
using UnityEngine;


public class WeaponAnimationEventsTests
{
    private GameObject holderGO;
    private WeaponAnimationEvents events;

    private GameObject casingPrefab;
    private GameObject ejectPointGO;
    private Weapon weapon;
    private WeaponSwitcher weaponSwitcher;

    [SetUp]
    public void SetUp()
    {
        holderGO = new GameObject("WeaponAnimationEvents");
        events = holderGO.AddComponent<WeaponAnimationEvents>();

        casingPrefab = new GameObject("CasingPrefab");
        casingPrefab.SetActive(false); // no molesta en la escena de test mientras no se usa

        ejectPointGO = new GameObject("CasingEjectPoint");
        ejectPointGO.transform.position = new Vector3(1f, 2f, 3f);
        ejectPointGO.transform.rotation = Quaternion.Euler(10f, 20f, 30f);

        weapon = holderGO.AddComponent<Weapon>();
        weaponSwitcher = holderGO.AddComponent<WeaponSwitcher>();

        // NUEVO: WeaponSwitcher necesita al menos un slot para que
        // NotifyUnholsterFinished() no rompa por slots == null.
        var slot = new WeaponSwitcher.WeaponSlot
        {
            weaponId = "test",
            weaponRoot = new GameObject("WeaponRoot")
        };
        SetFieldOn(weaponSwitcher, "slots", new[] { slot });
        SetFieldOn(weaponSwitcher, "currentIndex", 0);

        SetField("casingPrefab", casingPrefab);
        SetField("casingEjectPoint", ejectPointGO.transform);
        SetField("weapon", weapon);
        SetField("weaponSwitcher", weaponSwitcher);

    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(casingPrefab);
        Object.DestroyImmediate(ejectPointGO);
        Object.DestroyImmediate(holderGO);

        foreach (var clon in GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (clon.name == "CasingPrefab(Clone)")
                Object.DestroyImmediate(clon);
        }
    }

    // ---------------------------------------------------------------
    // OnEjectCasing
    // ---------------------------------------------------------------

    [Test]
    public void OnEjectCasing_ConReferenciasAsignadas_InstanciaLaVainaEnPosicionYRotacionCorrectas()
    {
        events.OnEjectCasing();

        var clon = FindIncludingInactive("CasingPrefab(Clone)");
        Assert.IsNotNull(clon, "Debería haberse instanciado una copia del prefab de vaina");

        Assert.That(clon.transform.position,
            Is.EqualTo(ejectPointGO.transform.position)
                .Using(UnityEngine.TestTools.Utils.Vector3EqualityComparer.Instance));
        Assert.That(clon.transform.rotation,
            Is.EqualTo(ejectPointGO.transform.rotation)
                .Using(UnityEngine.TestTools.Utils.QuaternionEqualityComparer.Instance));
    }

    [Test]
    public void OnEjectCasing_SiCasingPrefabEsNull_NoInstanciaNada()
    {
        SetField("casingPrefab", null);

        events.OnEjectCasing();

        Assert.IsNull(FindIncludingInactive("CasingPrefab(Clone)"));
    }

    [Test]
    public void OnEjectCasing_SiCasingEjectPointEsNull_NoInstanciaNada()
    {
        SetField("casingEjectPoint", null);

        events.OnEjectCasing();

        Assert.IsNull(FindIncludingInactive("CasingPrefab(Clone)"));
    }

    // ---------------------------------------------------------------
    // OnSlideBack
    // ---------------------------------------------------------------

    [Test]
    public void OnSlideBack_NoHaceNadaNiRompe()
    {
        Assert.DoesNotThrow(() => events.OnSlideBack());
    }

    // ---------------------------------------------------------------
    // OnAmmunitionFill
    // ---------------------------------------------------------------

    [Test]
    public void OnAmmunitionFill_ConWeaponAsignado_NoRompe()
    {
        Assert.DoesNotThrow(() => events.OnAmmunitionFill());
    }

    [Test]
    public void OnAmmunitionFill_SiWeaponEsNull_NoRompe()
    {
        SetField("weapon", null);

        Assert.DoesNotThrow(() => events.OnAmmunitionFill());
    }

    // ---------------------------------------------------------------
    // OnAnimationEndedReload
    // ---------------------------------------------------------------

    [Test]
    public void OnAnimationEndedReload_ConWeaponAsignado_NoRompe()
    {
        Assert.DoesNotThrow(() => events.OnAnimationEndedReload());
    }

    [Test]
    public void OnAnimationEndedReload_SiWeaponEsNull_NoRompe()
    {
        SetField("weapon", null);

        Assert.DoesNotThrow(() => events.OnAnimationEndedReload());
    }

    // ---------------------------------------------------------------
    // SetWeapon
    // ---------------------------------------------------------------

    [Test]
    public void SetWeapon_AsignaElNuevoWeapon()
    {
        var otroWeaponGO = new GameObject("OtroWeapon");
        var otroWeapon = otroWeaponGO.AddComponent<Weapon>();

        events.SetWeapon(otroWeapon);

        Assert.AreSame(otroWeapon, GetField("weapon"));

        Object.DestroyImmediate(otroWeaponGO);
    }

    // ---------------------------------------------------------------
    // OnAnimationEndedHolster
    // ---------------------------------------------------------------

    [Test]
    public void OnAnimationEndedHolster_ConWeaponSwitcherAsignado_NoRompe()
    {
        Assert.DoesNotThrow(() => events.OnAnimationEndedHolster());
    }

    [Test]
    public void OnAnimationEndedHolster_SiWeaponSwitcherEsNull_NoRompe()
    {
        SetField("weaponSwitcher", null);

        Assert.DoesNotThrow(() => events.OnAnimationEndedHolster());
    }

    // ---------------------------------------------------------------
    // SetCasingData
    // ---------------------------------------------------------------

    [Test]
    public void SetCasingData_AsignaPrefabYPuntoDeEyeccion()
    {
        var nuevoPrefab = new GameObject("NuevoCasingPrefab");
        nuevoPrefab.SetActive(false);
        var nuevoPuntoGO = new GameObject("NuevoPunto");

        events.SetCasingData(nuevoPrefab, nuevoPuntoGO.transform);

        Assert.AreSame(nuevoPrefab, GetField("casingPrefab"));
        Assert.AreSame(nuevoPuntoGO.transform, GetField("casingEjectPoint"));

        Object.DestroyImmediate(nuevoPrefab);
        Object.DestroyImmediate(nuevoPuntoGO);
    }

    [Test]
    public void SetCasingData_LosDatosNuevosSeUsanEnElSiguienteOnEjectCasing()
    {
        var nuevoPrefab = new GameObject("NuevoCasingPrefab");
        nuevoPrefab.SetActive(false);
        var nuevoPuntoGO = new GameObject("NuevoPunto");
        nuevoPuntoGO.transform.position = new Vector3(9f, 9f, 9f);

        events.SetCasingData(nuevoPrefab, nuevoPuntoGO.transform);
        events.OnEjectCasing();

        var clon = FindIncludingInactive("NuevoCasingPrefab(Clone)");
        Assert.IsNotNull(clon);
        Assert.That(clon.transform.position,
            Is.EqualTo(new Vector3(9f, 9f, 9f))
                .Using(UnityEngine.TestTools.Utils.Vector3EqualityComparer.Instance));

        Object.DestroyImmediate(clon);
        Object.DestroyImmediate(nuevoPrefab);
        Object.DestroyImmediate(nuevoPuntoGO);
    }

    private void SetFieldOn(object target, string name, object value)
    {
        var field = target.GetType().GetField(
            name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}' en {target.GetType().Name}");
        field.SetValue(target, value);
    }

    private GameObject FindIncludingInactive(string name)
    {
        foreach (var go in GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            if (go.name == name)
                return go;
        return null;
    }

    // ---------------------------------------------------------------
    // Helpers de reflexión
    // ---------------------------------------------------------------

    private void SetField(string name, object value)
    {
        var field = typeof(WeaponAnimationEvents).GetField(
            name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}' en WeaponAnimationEvents");
        field.SetValue(events, value);
    }

    private object GetField(string name)
    {
        var field = typeof(WeaponAnimationEvents).GetField(
            name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}' en WeaponAnimationEvents");
        return field.GetValue(events);
    }
}
