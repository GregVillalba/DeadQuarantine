using System.Reflection;
using NUnit.Framework;
using UnityEngine;


[TestFixture]
public class MultiplayerPlayerSpawnAssignerTests
{
    private GameObject assignerHost;
    private MultiplayerPlayerSpawnAssigner assigner;
 
    [SetUp]
    public void SetUp()
    {
        assignerHost = new GameObject("SpawnAssigner");
        assigner = assignerHost.AddComponent<MultiplayerPlayerSpawnAssigner>();
    }
 
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(assignerHost);
    }
 
    private MultiplayerPlayerSpawner CrearSpawnerConPuntos(Transform spawn1, Transform spawn2)
    {
        GameObject spawnerHost = new GameObject("Spawner");
        MultiplayerPlayerSpawner spawner = spawnerHost.AddComponent<MultiplayerPlayerSpawner>();
 
        typeof(MultiplayerPlayerSpawner)
            .GetField("playerSpawn1", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(spawner, spawn1);
        typeof(MultiplayerPlayerSpawner)
            .GetField("playerSpawn2", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(spawner, spawn2);
 
        return spawner;
    }
 
    [Test]
    public void IsAtAssignedSpawn_ConIndiceNegativo_DevuelveFalseSinBuscarSpawner()
    {
        assigner.AssignedSpawnIndex.Value = -1;
 
        Assert.IsFalse(assigner.IsAtAssignedSpawn());
    }
 
    [Test]
    public void IsAtAssignedSpawn_SinSpawnerEnLaEscena_DevuelveFalse()
    {
        assigner.AssignedSpawnIndex.Value = 0;
 
        Assert.IsFalse(assigner.IsAtAssignedSpawn());
    }
 
    [Test]
    public void IsAtAssignedSpawn_ConSpawnPointNulo_DevuelveFalse()
    {
        GameObject spawnerHost = new GameObject("Spawner");
        spawnerHost.AddComponent<MultiplayerPlayerSpawner>();
        // playerSpawn1/playerSpawn2 quedan sin asignar (null) a propósito.
 
        assigner.AssignedSpawnIndex.Value = 0;
 
        try
        {
            Assert.IsFalse(assigner.IsAtAssignedSpawn());
        }
        finally
        {
            Object.DestroyImmediate(spawnerHost);
        }
    }
 
    [Test]
    public void IsAtAssignedSpawn_DentroDeLaTolerancia_DevuelveTrue()
    {
        GameObject spawnPointObject = new GameObject("SpawnPoint");
        spawnPointObject.transform.position = new Vector3(10f, 0f, 0f);
 
        MultiplayerPlayerSpawner spawner = CrearSpawnerConPuntos(spawnPointObject.transform, null);
 
        assigner.AssignedSpawnIndex.Value = 0;
        assigner.transform.position = new Vector3(10.2f, 0f, 0f); // a 0.2 del spawn
 
        try
        {
            Assert.IsTrue(assigner.IsAtAssignedSpawn(0.5f));
        }
        finally
        {
            Object.DestroyImmediate(spawnPointObject);
            Object.DestroyImmediate(spawner.gameObject);
        }
    }
 
    [Test]
    public void IsAtAssignedSpawn_FueraDeLaTolerancia_DevuelveFalse()
    {
        GameObject spawnPointObject = new GameObject("SpawnPoint");
        spawnPointObject.transform.position = Vector3.zero;
 
        MultiplayerPlayerSpawner spawner = CrearSpawnerConPuntos(spawnPointObject.transform, null);
 
        assigner.AssignedSpawnIndex.Value = 0;
        assigner.transform.position = new Vector3(5f, 0f, 0f); // lejos del spawn
 
        try
        {
            Assert.IsFalse(assigner.IsAtAssignedSpawn(0.5f));
        }
        finally
        {
            Object.DestroyImmediate(spawnPointObject);
            Object.DestroyImmediate(spawner.gameObject);
        }
    }
 
    [Test]
    public void OnNetworkDespawn_SinHabersePreviamenteSuscripto_NoLanzaExcepcion()
    {
        Assert.DoesNotThrow(() => assigner.OnNetworkDespawn());
    }
}
