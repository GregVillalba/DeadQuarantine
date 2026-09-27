using System.Reflection;
using NUnit.Framework;
using UnityEngine;
 
 
[TestFixture]
public class GameStartCurtainTests
{
    private GameObject curtainHost;
    private GameObject canvasObject;
    private GameStartCurtain curtain;
 
    [SetUp]
    public void SetUp()
    {
        canvasObject = new GameObject("CurtainCanvas");
        canvasObject.SetActive(false);
 
        curtainHost = new GameObject("GameStartCurtain");
        curtain = curtainHost.AddComponent<GameStartCurtain>();
 
        // AddComponent ya disparó un Awake() automático, pero en ese momento
        // curtainCanvas todavía era null (recién lo inyectamos acá, simulando
        // lo que en el Inspector sería una referencia ya cableada). Por eso
        // volvemos a invocar Awake() manualmente una vez seteados los campos.
        SetPrivateField("curtainCanvas", canvasObject);
        SetPrivateField("expectedPlayerCount", 2);
        InvokePrivateMethod("Awake");
    }
 
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(curtainHost);
        Object.DestroyImmediate(canvasObject);
    }
 
    private void SetPrivateField(string fieldName, object value)
    {
        typeof(GameStartCurtain)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(curtain, value);
    }
 
    private object GetPrivateField(string fieldName)
    {
        return typeof(GameStartCurtain)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(curtain);
    }
 
    private object InvokePrivateMethod(string methodName, params object[] args)
    {
        MethodInfo method = typeof(GameStartCurtain)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        return method.Invoke(curtain, args);
    }
 
    [Test]
    public void Awake_ActivaLaCortinaCuandoHayReferenciaAsignada()
    {
        Assert.IsTrue(canvasObject.activeSelf,
            "Awake() debería dejar la cortina visible (SetActive(true)) cuando curtainCanvas no es null.");
    }
 
    [Test]
    public void BloquearJugadorLocalInmediatamente_SinJugadoresEnEscena_NoLanzaExcepcion()
    {
        // Cubre el "camino sin red": si no hay PlayerMovement/PlayerLook/WeaponSwitcher
        // en la escena, los foreach no deberían iterar ni romper nada.
        Assert.DoesNotThrow(() =>
            InvokePrivateMethod("BloquearJugadorLocalInmediatamente"));
    }
 
    [Test]
    public void TodosListos_SinAssignersEnEscena_DevuelveFalse()
    {
        bool resultado = (bool)InvokePrivateMethod("TodosListos");
 
        Assert.IsFalse(resultado,
            "Con 0 MultiplayerPlayerSpawnAssigner en la escena y expectedPlayerCount=2, TodosListos() debe devolver false.");
    }
 
    [Test]
    public void OcultarCortina_OcultaElCanvasYMarcaCurtainHiddenEnTrue()
    {
        InvokePrivateMethod("OcultarCortina");
 
        Assert.IsFalse(canvasObject.activeSelf,
            "OcultarCortina() debería desactivar el canvas de la cortina.");
        Assert.IsTrue((bool)GetPrivateField("curtainHidden"),
            "OcultarCortina() debería marcar curtainHidden = true.");
    }
 
    [Test]
    public void DesbloquearJugadorLocal_SinJugadoresEnEscena_NoLanzaExcepcion()
    {
        Assert.DoesNotThrow(() =>
            InvokePrivateMethod("DesbloquearJugadorLocal"));
    }
 
    [Test]
    public void RevelarHUDParaJugadorLocal_SinControllersEnEscena_NoLanzaExcepcion()
    {
        Assert.DoesNotThrow(() =>
            InvokePrivateMethod("RevelarHUDParaJugadorLocal"));
    }
}
