using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class RoundManagerTests
{
    private GameObject managerGO;
    private RoundManager manager;
    private GameObject hudGO;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        ResetInstance();

        managerGO = new GameObject("RoundManager");
        manager = managerGO.AddComponent<RoundManager>(); // dispara Awake -> Instance = manager

        hudGO = new GameObject("RoundStartHUD");
        var hud = hudGO.AddComponent<RoundStartHUD>();
        SetField("roundStartHUD", hud);

        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (managerGO != null) Object.Destroy(managerGO);
        if (hudGO != null) Object.Destroy(hudGO);
        ResetInstance();
        yield return null;
    }

    // ---------------------------------------------------------------
    // Awake (singleton)
    // ---------------------------------------------------------------

    [Test]
    public void Awake_PrimeraInstancia_SeAsignaComoInstance()
    {
        Assert.AreSame(manager, RoundManager.Instance);
    }

    [UnityTest]
    public IEnumerator Awake_SegundaInstancia_SeDestruyeYNoReemplazaLaInstance()
    {
        var duplicadoGO = new GameObject("RoundManagerDuplicado");
        var duplicado = duplicadoGO.AddComponent<RoundManager>(); // dispara Awake -> se autodestruye

        yield return null;

        Assert.AreSame(manager, RoundManager.Instance);
        Assert.IsTrue(duplicadoGO == null, "La segunda instancia debería haberse destruido");
    }

    // ---------------------------------------------------------------
    // HasBossPrefab / GetRandomBossPrefab
    // ---------------------------------------------------------------

    [Test]
    public void HasBossPrefab_ConAlMenosUnPrefabValido_DevuelveTrue()
    {
        var boss = new GameObject("Boss");
        SetField("bossPrefabs", new[] { null, boss });

        bool resultado = (bool)InvokeReturning("HasBossPrefab");

        Assert.IsTrue(resultado);

        Object.Destroy(boss);
    }

    [Test]
    public void HasBossPrefab_ConArrayNuloOVacio_DevuelveFalse()
    {
        SetField("bossPrefabs", null);
        Assert.IsFalse((bool)InvokeReturning("HasBossPrefab"));

        SetField("bossPrefabs", new GameObject[0]);
        Assert.IsFalse((bool)InvokeReturning("HasBossPrefab"));
    }

    [Test]
    public void GetRandomBossPrefab_SiNoHayPrefabsValidos_DevuelveNull()
    {
        SetField("bossPrefabs", new GameObject[] { null, null });

        var resultado = InvokeReturning("GetRandomBossPrefab");

        Assert.IsNull(resultado);
    }

    [Test]
    public void GetRandomBossPrefab_DevuelveSiempreUnoDeLosPrefabsNoNulos()
    {
        var boss1 = new GameObject("Boss1");
        var boss2 = new GameObject("Boss2");
        SetField("bossPrefabs", new[] { null, boss1, boss2 });

        for (int i = 0; i < 10; i++)
        {
            var resultado = InvokeReturning("GetRandomBossPrefab") as GameObject;
            Assert.IsTrue(resultado == boss1 || resultado == boss2);
        }

        Object.Destroy(boss1);
        Object.Destroy(boss2);
    }

    // ---------------------------------------------------------------
    // GetRandomSpawnInterval
    // ---------------------------------------------------------------

    [Test]
    public void GetRandomSpawnInterval_ConMaxRoundsMenorOIgualAUno_UsaElRangoDeLaRonda1()
    {
        SetField("maxRounds", 1);
        SetField("round1MinSpawnInterval", 2f);
        SetField("round1MaxSpawnInterval", 4f);

        float resultado = (float)InvokeReturning("GetRandomSpawnInterval", 1);

        Assert.That(resultado, Is.InRange(2f, 4f));
    }

    [Test]
    public void GetRandomSpawnInterval_EnLaPrimeraRonda_UsaElRangoInicial()
    {
        SetField("maxRounds", 5);
        SetField("round1MinSpawnInterval", 2f);
        SetField("round1MaxSpawnInterval", 4f);

        float resultado = (float)InvokeReturning("GetRandomSpawnInterval", 1);

        Assert.That(resultado, Is.InRange(2f, 4f));
    }

    [Test]
    public void GetRandomSpawnInterval_EnLaRondaFinal_UsaElRangoFinal()
    {
        SetField("maxRounds", 5);
        SetField("finalRoundMinSpawnInterval", 0.8f);
        SetField("finalRoundMaxSpawnInterval", 1.6f);

        float resultado = (float)InvokeReturning("GetRandomSpawnInterval", 5);

        Assert.That(resultado, Is.InRange(0.8f, 1.6f));
    }

    // ---------------------------------------------------------------
    // SpawnZombie (solo caminos de falla)
    // ---------------------------------------------------------------

    [Test]
    public void SpawnZombie_SiElPrefabEsNull_DevuelveFalse()
    {
        var spawnPointGO = new GameObject("SpawnPoint");

        bool resultado = (bool)InvokeReturning(
            "SpawnZombie", null, spawnPointGO.transform, 100, false);

        Assert.IsFalse(resultado);

        Object.Destroy(spawnPointGO);
    }

    [Test]
    public void SpawnZombie_SiElSpawnPointEsNull_DevuelveFalse()
    {
        var prefab = new GameObject("ZombiePrefab");

        bool resultado = (bool)InvokeReturning(
            "SpawnZombie", prefab, null, 100, false);

        Assert.IsFalse(resultado);

        Object.Destroy(prefab);
    }

    [UnityTest]
    public IEnumerator SpawnZombie_SiElPrefabNoTieneNetworkObject_DestruyeLaInstanciaYDevuelveFalse()
    {
        var prefab = new GameObject("ZombieSinNetworkObject");
        var spawnPointGO = new GameObject("SpawnPoint");

        bool resultado = (bool)InvokeReturning(
            "SpawnZombie", prefab, spawnPointGO.transform, 100, false);

        yield return null;

        Assert.IsFalse(resultado);
        Assert.IsNull(GameObject.Find("ZombieSinNetworkObject(Clone)"),
            "La instancia creada sin NetworkObject debería haberse destruido");

        Object.Destroy(prefab);
        Object.Destroy(spawnPointGO);
    }

    // ---------------------------------------------------------------
    // OnNetworkSpawn / OnNetworkDespawn + OnCountdownChanged
    // ---------------------------------------------------------------

    [Test]
    public void OnNetworkSpawn_SuscribeEventos_YOnCountdownChangedActualizaElHUD()
    {
        var hud = (RoundStartHUD)GetField("roundStartHUD");
        hud.gameObject.SetActive(false);

        manager.OnNetworkSpawn(); // seguro: IsServer=false, GameLostNetwork=false por defecto

        manager.CountdownNetwork.Value = 5; // dispara OnCountdownChanged

        Assert.IsTrue(hud.gameObject.activeSelf,
            "El HUD debería activarse cuando cambia el countdown estando suscripto");
    }

    [Test]
    public void OnNetworkDespawn_DesuscribeEventos_YaNoActualizaElHUD()
    {
        var hud = (RoundStartHUD)GetField("roundStartHUD");

        manager.OnNetworkSpawn();
        manager.CountdownNetwork.Value = 5; // confirma que estaba suscripto
        Assert.IsTrue(hud.gameObject.activeSelf);

        manager.OnNetworkDespawn();
        hud.gameObject.SetActive(false); // reseteo manual para verificar que ya no reacciona

        manager.CountdownNetwork.Value = 3;

        Assert.IsFalse(hud.gameObject.activeSelf,
            "El HUD no debería reaccionar después de OnNetworkDespawn");
    }

    // ---------------------------------------------------------------
    // OnRoundChanged / OnGameLostChanged (rama segura)
    // ---------------------------------------------------------------

    [Test]
    public void OnRoundChanged_NoHaceNadaNiRompe()
    {
        Assert.DoesNotThrow(() => Invoke("OnRoundChanged", 1, 2));
    }

    [Test]
    public void OnGameLostChanged_ConNewValueFalse_NoHaceNada()
    {
        // Con newValue=true llamaría a MostrarDerrota() -> ClientRpc ->
        // NetworkManager.Singleton, que no existe en este entorno de test.
        Assert.DoesNotThrow(() => Invoke("OnGameLostChanged", false, false));
    }

    // ---------------------------------------------------------------
    // Guard "no soy servidor" de los métodos públicos gateados
    // ---------------------------------------------------------------

    [Test]
    public void StartRound_SinSerServidor_NoModificaNingunaNetworkVariable()
    {
        manager.StartRound(2);

        Assert.AreEqual(0, manager.CurrentRoundNetwork.Value);
        Assert.AreEqual(0, manager.AliveZombiesNetwork.Value);
        Assert.AreEqual(0, manager.ZombiesThisRoundNetwork.Value);
    }

    [Test]
    public void ZombieDied_SinSerServidor_NoModificaAliveZombies()
    {
        manager.AliveZombiesNetwork.Value = 5;

        manager.ZombieDied();

        Assert.AreEqual(5, manager.AliveZombiesNetwork.Value);
    }

    [Test]
    public void PlayerDied_SinSerServidor_NoCambiaGameLost()
    {
        manager.PlayerDied();

        Assert.IsFalse(manager.GameLostNetwork.Value);
    }

    [Test]
    public void ConfirmarSiguienteRonda_SinSerServidor_NoRompe()
    {
        Assert.DoesNotThrow(() => manager.ConfirmarSiguienteRonda());
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private const BindingFlags Flags =
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    private void SetField(string name, object value)
    {
        var field = typeof(RoundManager).GetField(name, Flags);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}' en RoundManager");
        field.SetValue(manager, value);
    }

    private object GetField(string name)
    {
        var field = typeof(RoundManager).GetField(name, Flags);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}' en RoundManager");
        return field.GetValue(manager);
    }

    private void Invoke(string methodName, params object[] args)
    {
        var method = typeof(RoundManager).GetMethod(methodName, Flags);
        Assert.IsNotNull(method, $"No se encontró el método '{methodName}' en RoundManager");
        method.Invoke(manager, args);
    }

    private object InvokeReturning(string methodName, params object[] args)
    {
        var method = typeof(RoundManager).GetMethod(methodName, Flags);
        Assert.IsNotNull(method, $"No se encontró el método '{methodName}' en RoundManager");
        return method.Invoke(manager, args);
    }

    private static void ResetInstance()
    {
        var prop = typeof(RoundManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        prop.GetSetMethod(true).Invoke(null, new object[] { null });
    }
}
