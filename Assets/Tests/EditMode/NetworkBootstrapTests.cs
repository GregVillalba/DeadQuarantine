using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

[TestFixture]
public class NetworkBootstrapTests
{
    private GameObject hostObject;
    private NetworkBootstrap bootstrap;
 
    [SetUp]
    public void SetUp()
    {
        hostObject = new GameObject("NetworkBootstrap");
        hostObject.SetActive(false); // evita que Awake()/DontDestroyOnLoad se disparen acá
        bootstrap = hostObject.AddComponent<NetworkBootstrap>();
    }
 
    [TearDown]
    public void TearDown()
    {
        if (hostObject != null)
            Object.DestroyImmediate(hostObject);
 
        ResetSingletonInstance();
    }
 
    private void ResetSingletonInstance()
    {
        typeof(NetworkBootstrap)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            .GetSetMethod(true)
            .Invoke(null, new object[] { null });
    }
 
    private object InvokePrivateMethod(string methodName, params object[] args)
    {
        MethodInfo method = typeof(NetworkBootstrap)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        return method.Invoke(bootstrap, args);
    }
 
    private object GetPrivateField(string fieldName)
    {
        return typeof(NetworkBootstrap)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(bootstrap);
    }
 
    [Test]
    public async Task StartClient_ConJoinCodeVacio_DevuelveFalseSinTocarLaRed()
    {
        bool resultado = await bootstrap.StartClient("");
        Assert.IsFalse(resultado);
    }
 
    [Test]
    public async Task StartClient_ConJoinCodeDeEspacios_DevuelveFalse()
    {
        bool resultado = await bootstrap.StartClient("   ");
        Assert.IsFalse(resultado);
    }
 
    [Test]
    public async Task StartClient_ConJoinCodeNull_DevuelveFalse()
    {
        bool resultado = await bootstrap.StartClient(null);
        Assert.IsFalse(resultado);
    }
 
    [Test]
    public void NotifyIntentionalLeave_SeteaElFlagPrivadoEnTrue()
    {
        bootstrap.NotifyIntentionalLeave();
 
        Assert.IsTrue((bool)GetPrivateField("isIntentionalLeave"));
    }
 
    [Test]
    public void OnNetworkStateChanged_NoLanzaExcepcion()
    {
        Assert.DoesNotThrow(() =>
            InvokePrivateMethod("OnNetworkStateChanged", default(NetworkState)));
    }
 
    [Test]
    public void OnNetworkStartFailed_NoLanzaExcepcion()
    {
        Assert.DoesNotThrow(() =>
            InvokePrivateMethod("OnNetworkStartFailed", default(SessionError)));
    }
 
    [Test]
    public void OnClientDisconnected_SinNetworkManagerEnEscena_NoLanzaExcepcion()
    {
        Assert.IsNull(NetworkManager.Singleton,
            "Esta prueba asume que no hay un NetworkManager activo en la escena de test.");
 
        Assert.DoesNotThrow(() =>
            InvokePrivateMethod("OnClientDisconnected", 0UL));
    }
 
    [Test]
    public void BothPlayersConnected_SinSesionActiva_DevuelveFalse()
    {
        Assert.IsFalse(bootstrap.BothPlayersConnected());
    }
 
    [Test]
    public void IsHost_SinSesionActiva_DevuelveFalse()
    {
        Assert.IsFalse(bootstrap.IsHost);
    }
 
    [Test]
    public void PlayerCount_SinSesionActiva_DevuelveCero()
    {
        Assert.AreEqual(0, bootstrap.PlayerCount);
    }
 
    [Test]
    public void CurrentSession_SinSesionActiva_DevuelveNull()
    {
        Assert.IsNull(bootstrap.CurrentSession);
    }
 
    [Test]
    public async Task LeaveSession_SinSesionActiva_NoLanzaExcepcionYCurrentJoinCodeQuedaNull()
    {
        Assert.DoesNotThrowAsync(async () => await bootstrap.LeaveSession());
        // Reconstruimos la instancia porque el assert anterior ya consumió el Task una vez;
        // llamamos de nuevo directo para chequear el estado resultante.
        await bootstrap.LeaveSession();
 
        Assert.IsNull(bootstrap.CurrentJoinCode);
    }
 
    [Test]
    public void StartMultiplayerGame_SinSesionActiva_TerminaSinLanzarExcepcion()
    {
        Assert.DoesNotThrowAsync(async () => await bootstrap.StartMultiplayerGame());
    }
}
