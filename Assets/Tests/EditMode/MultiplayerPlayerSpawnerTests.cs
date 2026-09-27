using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

[TestFixture]
public class MultiplayerPlayerSpawnerTests
{
    private GameObject host;
    private MultiplayerPlayerSpawner spawner;
    private GameObject spawn1Object;
    private GameObject spawn2Object;
 
    [SetUp]
    public void SetUp()
    {
        host = new GameObject("MultiplayerPlayerSpawner");
        spawner = host.AddComponent<MultiplayerPlayerSpawner>();
 
        spawn1Object = new GameObject("Spawn1");
        spawn2Object = new GameObject("Spawn2");
 
        SetPrivateField("playerSpawn1", spawn1Object.transform);
        SetPrivateField("playerSpawn2", spawn2Object.transform);
    }
 
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(spawn1Object);
        Object.DestroyImmediate(spawn2Object);
    }
 
    private void SetPrivateField(string fieldName, object value)
    {
        typeof(MultiplayerPlayerSpawner)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(spawner, value);
    }
 
    private object InvokePrivateMethod(string methodName, params object[] args)
    {
        MethodInfo method = typeof(MultiplayerPlayerSpawner)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        return method.Invoke(spawner, args);
    }
 
    [Test]
    public void GetSpawnPoint_ConIndiceCero_DevuelvePlayerSpawn1()
    {
        Assert.AreSame(spawn1Object.transform, spawner.GetSpawnPoint(0));
    }
 
    [Test]
    public void GetSpawnPoint_ConIndiceUno_DevuelvePlayerSpawn2()
    {
        Assert.AreSame(spawn2Object.transform, spawner.GetSpawnPoint(1));
    }
 
    [Test]
    public void GetSpawnPoint_ConIndiceDistintoDeCeroYUno_CaeEnPlayerSpawn2()
    {
        // Documenta el comportamiento actual del ternario: cualquier índice
        // distinto de 0 (incluso uno "inválido") devuelve playerSpawn2.
        Assert.AreSame(spawn2Object.transform, spawner.GetSpawnPoint(99));
    }
 
    [Test]
    public void OnDestroy_SinNetworkManagerEnEscena_NoLanzaExcepcion()
    {
        Assert.IsNull(NetworkManager.Singleton,
            "Esta prueba asume que no hay un NetworkManager activo en la escena de test.");
 
        Assert.DoesNotThrow(() => InvokePrivateMethod("OnDestroy"));
    }
 
    [Test]
    public void AsignarSpawnIndex_ClientSinPlayerObject_NoLanzaExcepcion()
    {
        // NetworkClient con PlayerObject en null (valor por defecto) simula
        // el caso "el PlayerObject todavía no existe para este cliente".
        var clienteSinPlayerObject = new NetworkClient();
 
        Assert.DoesNotThrow(() =>
            InvokePrivateMethod("AsignarSpawnIndex", 0UL, clienteSinPlayerObject));
    }
}
