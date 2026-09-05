using NUnit.Framework;
using UnityEngine;

public class PanelSwitcherTests
{
    private GameObject holderGO;
    private PanelSwitcher switcher;
    private GameObject panelCardControl1;
    private GameObject panelCardControl2;

    [SetUp]
    public void SetUp()
    {
        holderGO = new GameObject("PanelSwitcher");
        switcher = holderGO.AddComponent<PanelSwitcher>();

        panelCardControl1 = new GameObject("PanelCardControl1");
        panelCardControl2 = new GameObject("PanelCardControl2");

        SetField("panelCardControl1", panelCardControl1);
        SetField("panelCardControl2", panelCardControl2);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(panelCardControl1);
        Object.DestroyImmediate(panelCardControl2);
        Object.DestroyImmediate(holderGO);
    }

    // ---------------------------------------------------------------
    // MostrarCardControl
    // ---------------------------------------------------------------

    [Test]
    public void MostrarCardControl_OcultaPanel1YMuestraPanel2()
    {
        panelCardControl1.SetActive(true);
        panelCardControl2.SetActive(false);

        switcher.MostrarCardControl();

        Assert.IsFalse(panelCardControl1.activeSelf);
        Assert.IsTrue(panelCardControl2.activeSelf);
    }

    [Test]
    public void MostrarCardControl_SiPanel1EsNull_NoRompeYMuestraPanel2()
    {
        SetField("panelCardControl1", null);
        panelCardControl2.SetActive(false);

        Assert.DoesNotThrow(() => switcher.MostrarCardControl());

        Assert.IsTrue(panelCardControl2.activeSelf);
    }

    [Test]
    public void MostrarCardControl_SiPanel2EsNull_NoRompeYOcultaPanel1()
    {
        SetField("panelCardControl2", null);
        panelCardControl1.SetActive(true);

        Assert.DoesNotThrow(() => switcher.MostrarCardControl());

        Assert.IsFalse(panelCardControl1.activeSelf);
    }

    // ---------------------------------------------------------------
    // MostrarCardControl1
    // ---------------------------------------------------------------

    [Test]
    public void MostrarCardControl1_OcultaPanel2YMuestraPanel1()
    {
        panelCardControl2.SetActive(true);
        panelCardControl1.SetActive(false);

        switcher.MostrarCardControl1();

        Assert.IsFalse(panelCardControl2.activeSelf);
        Assert.IsTrue(panelCardControl1.activeSelf);
    }

    [Test]
    public void MostrarCardControl1_SiPanel2EsNull_NoRompeYMuestraPanel1()
    {
        SetField("panelCardControl2", null);
        panelCardControl1.SetActive(false);

        Assert.DoesNotThrow(() => switcher.MostrarCardControl1());

        Assert.IsTrue(panelCardControl1.activeSelf);
    }

    [Test]
    public void MostrarCardControl1_SiPanel1EsNull_NoRompeYOcultaPanel2()
    {
        SetField("panelCardControl1", null);
        panelCardControl2.SetActive(true);

        Assert.DoesNotThrow(() => switcher.MostrarCardControl1());

        Assert.IsFalse(panelCardControl2.activeSelf);
    }

    [Test]
    public void MostrarCardControl1_SiAmbosPanelesSonNull_NoRompe()
    {
        SetField("panelCardControl1", null);
        SetField("panelCardControl2", null);

        Assert.DoesNotThrow(() => switcher.MostrarCardControl1());
    }

    // ---------------------------------------------------------------
    // Helper de reflexión (los campos son [SerializeField] privados)
    // ---------------------------------------------------------------

    private void SetField(string name, object value)
    {
        var field = typeof(PanelSwitcher).GetField(
            name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}' en PanelSwitcher");
        field.SetValue(switcher, value);
    }
}