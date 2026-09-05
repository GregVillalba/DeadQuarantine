using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

public class DecalManagerTests
{
    private GameObject managerGO;
    private DecalManager manager;
    private GameObject prefab;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        managerGO = new GameObject("DecalManager");
        manager = managerGO.AddComponent<DecalManager>();

        // Prefab "fake": un GameObject simple que se puede Instantiate/Destroy
        // sin depender de un asset real de proyecto.
        prefab = new GameObject("BulletHolePrefab");
        prefab.SetActive(false); // no molesta en la escena de test mientras no se usa

        SetField("bulletHolePrefab", prefab);
        SetField("maxDecals", 3);

        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // Limpia todos los decals que hayan quedado vivos en la escena
        foreach (var decal in GetActiveDecals())
        {
            if (decal != null)
                Object.Destroy(decal);
        }

        if (prefab != null) Object.Destroy(prefab);
        if (managerGO != null) Object.Destroy(managerGO);

        yield return null;
    }

    // ---------------------------------------------------------------
    // Awake
    // ---------------------------------------------------------------

    [Test]
    public void Awake_AsignaLaInstanciaSingleton()
    {
        Invoke("Awake");

        Assert.AreSame(manager, DecalManager.Instance);
    }

    // ---------------------------------------------------------------
    // SpawnBulletHole
    // ---------------------------------------------------------------

    [Test]
    public void SpawnBulletHole_SiElPrefabEsNull_NoInstanciaNiEncolaNada()
    {
        SetField("bulletHolePrefab", null);

        Assert.DoesNotThrow(() =>
            manager.SpawnBulletHole(Vector3.zero, Vector3.up));

        Assert.AreEqual(0, GetActiveDecals().Count);
    }

    [Test]
    public void SpawnBulletHole_InstanciaElDecalConPosicionYRotacionCorrectas()
    {
        SetField("surfaceOffset", 0.05f);

        var posicion = new Vector3(1f, 2f, 3f);
        var normal = Vector3.up;

        manager.SpawnBulletHole(posicion, normal);

        var decals = GetActiveDecals();
        Assert.AreEqual(1, decals.Count);

        GameObject decal = null;
        foreach (var d in decals) decal = d; // única entrada
        Assert.IsNotNull(decal);

        Vector3 posicionEsperada = posicion + normal * 0.05f;
        Quaternion rotacionEsperada = Quaternion.LookRotation(-normal);

        Assert.That(decal.transform.position,
            Is.EqualTo(posicionEsperada).Using(Vector3EqualityComparer.Instance));
        Assert.That(decal.transform.rotation,
            Is.EqualTo(rotacionEsperada).Using(QuaternionEqualityComparer.Instance));
    }

    [Test]
    public void SpawnBulletHole_EncolaCadaDecalInstanciado()
    {
        manager.SpawnBulletHole(Vector3.zero, Vector3.up);
        manager.SpawnBulletHole(Vector3.one, Vector3.up);

        Assert.AreEqual(2, GetActiveDecals().Count);
    }

    [UnityTest]
    public IEnumerator SpawnBulletHole_AlSuperarMaxDecals_DestruyeElMasViejo()
    {
        // maxDecals = 3 (seteado en SetUp)
        manager.SpawnBulletHole(new Vector3(0, 0, 0), Vector3.up);
        var decals = GetActiveDecals();
        GameObject primero = null;
        foreach (var d in decals) { primero = d; break; }

        manager.SpawnBulletHole(new Vector3(1, 0, 0), Vector3.up);
        manager.SpawnBulletHole(new Vector3(2, 0, 0), Vector3.up);

        // Esta 4ta instancia hace que la cola supere maxDecals y descarte al más viejo
        manager.SpawnBulletHole(new Vector3(3, 0, 0), Vector3.up);

        yield return null;

        Assert.AreEqual(3, GetActiveDecals().Count,
            "La cantidad de decals activos no debería superar maxDecals");
        Assert.IsTrue(primero == null,
            "El decal más viejo debería haber sido destruido");
    }

    [Test]
    public void SpawnBulletHole_MientrasNoSupereMaxDecals_NoDestruyeNinguno()
    {
        manager.SpawnBulletHole(Vector3.zero, Vector3.up);
        manager.SpawnBulletHole(Vector3.zero, Vector3.up);
        manager.SpawnBulletHole(Vector3.zero, Vector3.up); // llega justo a maxDecals = 3

        var decals = GetActiveDecals();
        Assert.AreEqual(3, decals.Count);
        foreach (var d in decals)
            Assert.IsNotNull(d);
    }

    // ---------------------------------------------------------------
    // Helpers de reflexión
    // ---------------------------------------------------------------

    private const BindingFlags Flags =
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    private void SetField(string name, object value)
    {
        var field = typeof(DecalManager).GetField(name, Flags);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}' en DecalManager");
        field.SetValue(manager, value);
    }

    private void Invoke(string methodName, params object[] args)
    {
        var method = typeof(DecalManager).GetMethod(methodName, Flags);
        Assert.IsNotNull(method, $"No se encontró el método '{methodName}' en DecalManager");
        method.Invoke(manager, args);
    }

    private Queue<GameObject> GetActiveDecals()
    {
        var field = typeof(DecalManager).GetField("activeDecals", Flags);
        return (Queue<GameObject>)field.GetValue(manager);
    }
}
