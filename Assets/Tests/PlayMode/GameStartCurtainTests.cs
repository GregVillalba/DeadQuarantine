using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class GameStartCurtainPlayModeTests
{
    private GameObject curtainHost;
    private GameObject canvasObject;
    private GameStartCurtain curtain;
 
    [SetUp]
    public void SetUp()
    {
        canvasObject = new GameObject("CurtainCanvas");
        curtainHost = new GameObject("GameStartCurtain");
        curtain = curtainHost.AddComponent<GameStartCurtain>();
 
        typeof(GameStartCurtain)
            .GetField("curtainCanvas", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(curtain, canvasObject);
        typeof(GameStartCurtain)
            .GetField("expectedPlayerCount", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(curtain, 2);
 
        typeof(GameStartCurtain)
            .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(curtain, null);
    }
 
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(curtainHost);
        Object.DestroyImmediate(canvasObject);
    }
 
    [UnityTest]
    public IEnumerator Update_SinAssignersEnEscena_MantieneLaCortinaVisibleFrameTrasFrame()
    {
        // Con 0 MultiplayerPlayerSpawnAssigner en escena, TodosListos() siempre
        // devuelve false, así que Update() nunca debería llamar a OcultarCortina().
        yield return null;
        yield return null;
        yield return null;
 
        Assert.IsTrue(canvasObject.activeSelf,
            "Sin jugadores listos, Update() no debe ocultar la cortina.");
    }
}
 
