using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using TMPro;

public class RoundStartHUDTests
{
    private GameObject hudGO;
    private RoundStartHUD hud;
    private TextMeshProUGUI roundText;
    private TextMeshProUGUI countdownText;

    [SetUp]
    public void SetUp()
    {
        hudGO = new GameObject("RoundStartHUD");

        var roundGO = new GameObject("RoundText");
        roundGO.transform.SetParent(hudGO.transform);
        roundText = roundGO.AddComponent<TextMeshProUGUI>();

        var countdownGO = new GameObject("CountdownText");
        countdownGO.transform.SetParent(hudGO.transform);
        countdownText = countdownGO.AddComponent<TextMeshProUGUI>();

        // AddComponent dispara Awake() automáticamente, que desactiva
        // hudGO. Seteamos las referencias ANTES, así Show()/Hide() ya
        // las tienen disponibles en los tests.
        hud = hudGO.AddComponent<RoundStartHUD>();
        SetField("roundText", roundText);
        SetField("countdownText", countdownText);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(hudGO);
    }

    // ---------------------------------------------------------------
    // Awake
    // ---------------------------------------------------------------

    [Test]
    public void Awake_DesactivaElGameObjectAlCrearse()
    {
        // AddComponent en SetUp ya disparó Awake(); se verifica el efecto.
        Assert.IsFalse(hudGO.activeSelf);
    }

    // ---------------------------------------------------------------
    // Show
    // ---------------------------------------------------------------

    [Test]
    public void Show_ActivaElGameObjectYActualizaAmbosTextos()
    {
        hud.Show(3, 5);

        Assert.IsTrue(hudGO.activeSelf);
        Assert.AreEqual("3", roundText.text);
        Assert.AreEqual("5", countdownText.text);
    }

    [Test]
    public void Show_SiRoundTextEsNull_NoRompeYActualizaCountdown()
    {
        SetField("roundText", null);

        Assert.DoesNotThrow(() => hud.Show(1, 10));

        Assert.IsTrue(hudGO.activeSelf);
        Assert.AreEqual("10", countdownText.text);
    }

    [Test]
    public void Show_SiCountdownTextEsNull_NoRompeYActualizaRound()
    {
        SetField("countdownText", null);

        Assert.DoesNotThrow(() => hud.Show(2, 7));

        Assert.IsTrue(hudGO.activeSelf);
        Assert.AreEqual("2", roundText.text);
    }

    [Test]
    public void Show_SiAmbosTextosSonNull_SoloActivaElGameObject()
    {
        SetField("roundText", null);
        SetField("countdownText", null);

        Assert.DoesNotThrow(() => hud.Show(4, 8));

        Assert.IsTrue(hudGO.activeSelf);
    }

    // ---------------------------------------------------------------
    // Hide
    // ---------------------------------------------------------------

    [Test]
    public void Hide_DesactivaElGameObject()
    {
        hud.Show(1, 1); // primero lo activa
        Assert.IsTrue(hudGO.activeSelf);

        hud.Hide();

        Assert.IsFalse(hudGO.activeSelf);
    }

    // ---------------------------------------------------------------
    // Helper de reflexión (los campos son [SerializeField] privados)
    // ---------------------------------------------------------------

    private void SetField(string name, object value)
    {
        var field = typeof(RoundStartHUD).GetField(
            name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}' en RoundStartHUD");
        field.SetValue(hud, value);
    }
}