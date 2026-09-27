using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public class NetworkLobbyUITests
{
    private GameObject hostObject;
    private NetworkLobbyUI lobbyUI;
 
    private GameObject createButtonObject;
    private GameObject joinButtonObject;
    private GameObject joinCodeDisplayObject;
    private GameObject joinCodeInputObject;
 
    private Button createButton;
    private Button joinButton;
    private TextMeshProUGUI joinCodeDisplay;
    private TMP_InputField joinCodeInput;
 
    [SetUp]
    public void SetUp()
    {
        hostObject = new GameObject("NetworkLobbyUI");
        lobbyUI = hostObject.AddComponent<NetworkLobbyUI>();
 
        createButtonObject = new GameObject("CreateButton", typeof(RectTransform), typeof(Button));
        joinButtonObject = new GameObject("JoinButton", typeof(RectTransform), typeof(Button));
        joinCodeDisplayObject = new GameObject("JoinCodeDisplay", typeof(RectTransform), typeof(TextMeshProUGUI));
        joinCodeInputObject = new GameObject("JoinCodeInput", typeof(RectTransform), typeof(TMP_InputField));
 
        createButton = createButtonObject.GetComponent<Button>();
        joinButton = joinButtonObject.GetComponent<Button>();
        joinCodeDisplay = joinCodeDisplayObject.GetComponent<TextMeshProUGUI>();
        joinCodeInput = joinCodeInputObject.GetComponent<TMP_InputField>();
 
        SetPrivateField("createButton", createButton);
        SetPrivateField("joinButton", joinButton);
        SetPrivateField("joinCodeDisplay", joinCodeDisplay);
        SetPrivateField("joinCodeInput", joinCodeInput);
        // networkBootstrap se deja sin asignar (null): ninguno de los casos cubiertos
        // acá llega a tocarlo.
    }
 
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(hostObject);
        Object.DestroyImmediate(createButtonObject);
        Object.DestroyImmediate(joinButtonObject);
        Object.DestroyImmediate(joinCodeDisplayObject);
        Object.DestroyImmediate(joinCodeInputObject);
    }
 
    private void SetPrivateField(string fieldName, object value)
    {
        typeof(NetworkLobbyUI)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(lobbyUI, value);
    }
 
    private object InvokePrivateMethod(string methodName, params object[] args)
    {
        MethodInfo method = typeof(NetworkLobbyUI)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        return method.Invoke(lobbyUI, args);
    }
 
    [Test]
    public void Start_LimpiaElTextoDeJoinCodeDisplay()
    {
        joinCodeDisplay.text = "ALGO";
 
        InvokePrivateMethod("Start");
 
        Assert.AreEqual(string.Empty, joinCodeDisplay.text);
    }
 
    [Test]
    public void OnJoinButtonClicked_ConCodigoVacio_NoTocaLosBotonesNiLanzaExcepcion()
    {
        joinCodeInput.text = "";
 
        Assert.DoesNotThrow(() => InvokePrivateMethod("OnJoinButtonClicked"));
 
        Assert.IsTrue(joinButton.interactable);
        Assert.IsTrue(createButton.interactable);
    }
 
    [Test]
    public void OnJoinButtonClicked_ConCodigoDeSoloEspacios_NoTocaLosBotones()
    {
        // "   ".Trim() da "", así que debería tomar la misma rama que el código vacío.
        joinCodeInput.text = "   ";
 
        Assert.DoesNotThrow(() => InvokePrivateMethod("OnJoinButtonClicked"));
 
        Assert.IsTrue(joinButton.interactable);
        Assert.IsTrue(createButton.interactable);
    }
}
