using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ConfiguracionesPanelControllerTests
{
    private GameObject holderGO;
    private ConfiguracionesPanelController controller;

    private GameObject menuPrincipalPanel;
    private GameObject configuracionesPanel;
    private GameObject sonidoPanel;
    private GameObject sensibilidadMousePanel;

    [SetUp]
    public void SetUp()
    {
        holderGO = new GameObject("ConfiguracionesPanelController");
        controller = holderGO.AddComponent<ConfiguracionesPanelController>();

        menuPrincipalPanel = new GameObject("MenuPrincipalPanel");
        configuracionesPanel = new GameObject("ConfiguracionesPanel");
        sonidoPanel = new GameObject("SonidoPanel");
        sensibilidadMousePanel = new GameObject("SensibilidadMousePanel");

        SetField("menuPrincipalPanel", menuPrincipalPanel);
        SetField("configuracionesPanel", configuracionesPanel);
        SetField("sonidoPanel", sonidoPanel);
        SetField("sensibilidadMousePanel", sensibilidadMousePanel);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(menuPrincipalPanel);
        Object.DestroyImmediate(configuracionesPanel);
        Object.DestroyImmediate(sonidoPanel);
        Object.DestroyImmediate(sensibilidadMousePanel);
        Object.DestroyImmediate(holderGO);
    }

    // ---------------------------------------------------------------
    // MostrarConfiguraciones
    // ---------------------------------------------------------------

    [Test]
    public void MostrarConfiguraciones_OcultaMenuYSubpanelesYMuestraConfiguraciones()
    {
        menuPrincipalPanel.SetActive(true);
        configuracionesPanel.SetActive(false);
        sonidoPanel.SetActive(true);
        sensibilidadMousePanel.SetActive(true);

        controller.MostrarConfiguraciones();

        Assert.IsFalse(menuPrincipalPanel.activeSelf);
        Assert.IsTrue(configuracionesPanel.activeSelf);
        Assert.IsFalse(sonidoPanel.activeSelf);
        Assert.IsFalse(sensibilidadMousePanel.activeSelf);
    }

    [Test]
    public void MostrarConfiguraciones_ConTodosLosPanelesNulos_NoRompe()
    {
        SetField("menuPrincipalPanel", null);
        SetField("configuracionesPanel", null);
        SetField("sonidoPanel", null);
        SetField("sensibilidadMousePanel", null);

        Assert.DoesNotThrow(() => controller.MostrarConfiguraciones());
    }

    // ---------------------------------------------------------------
    // MostrarSonido
    // ---------------------------------------------------------------

    [Test]
    public void MostrarSonido_OcultaConfiguracionesYMuestraSonido()
    {
        configuracionesPanel.SetActive(true);
        sonidoPanel.SetActive(false);

        controller.MostrarSonido();

        Assert.IsFalse(configuracionesPanel.activeSelf);
        Assert.IsTrue(sonidoPanel.activeSelf);
    }

    [Test]
    public void MostrarSonido_SiConfiguracionesPanelEsNull_NoRompeYMuestraSonido()
    {
        SetField("configuracionesPanel", null);
        sonidoPanel.SetActive(false);

        Assert.DoesNotThrow(() => controller.MostrarSonido());

        Assert.IsTrue(sonidoPanel.activeSelf);
    }

    [Test]
    public void MostrarSonido_SiSonidoPanelEsNull_NoRompeYOcultaConfiguraciones()
    {
        SetField("sonidoPanel", null);
        configuracionesPanel.SetActive(true);

        Assert.DoesNotThrow(() => controller.MostrarSonido());

        Assert.IsFalse(configuracionesPanel.activeSelf);
    }

    // ---------------------------------------------------------------
    // MostrarSensibilidadMouse
    // ---------------------------------------------------------------

    [Test]
    public void MostrarSensibilidadMouse_OcultaConfiguracionesYMuestraSensibilidad()
    {
        configuracionesPanel.SetActive(true);
        sensibilidadMousePanel.SetActive(false);

        controller.MostrarSensibilidadMouse();

        Assert.IsFalse(configuracionesPanel.activeSelf);
        Assert.IsTrue(sensibilidadMousePanel.activeSelf);
    }

    [Test]
    public void MostrarSensibilidadMouse_ConAmbosPanelesNulos_NoRompe()
    {
        SetField("configuracionesPanel", null);
        SetField("sensibilidadMousePanel", null);

        Assert.DoesNotThrow(() => controller.MostrarSensibilidadMouse());
    }

    // ---------------------------------------------------------------
    // VolverAConfiguraciones
    // ---------------------------------------------------------------

    [Test]
    public void VolverAConfiguraciones_OcultaAmbosSubpanelesYMuestraConfiguraciones()
    {
        sonidoPanel.SetActive(true);
        sensibilidadMousePanel.SetActive(true);
        configuracionesPanel.SetActive(false);

        controller.VolverAConfiguraciones();

        Assert.IsFalse(sonidoPanel.activeSelf);
        Assert.IsFalse(sensibilidadMousePanel.activeSelf);
        Assert.IsTrue(configuracionesPanel.activeSelf);
    }

    [Test]
    public void VolverAConfiguraciones_ConTodosLosPanelesNulos_NoRompe()
    {
        SetField("sonidoPanel", null);
        SetField("sensibilidadMousePanel", null);
        SetField("configuracionesPanel", null);

        Assert.DoesNotThrow(() => controller.VolverAConfiguraciones());
    }

    // ---------------------------------------------------------------
    // VolverAMenuPrincipalDesdeConfiguraciones
    // ---------------------------------------------------------------

    [Test]
    public void VolverAMenuPrincipalDesdeConfiguraciones_OcultaConfiguracionesYMuestraMenu()
    {
        configuracionesPanel.SetActive(true);
        menuPrincipalPanel.SetActive(false);

        controller.VolverAMenuPrincipalDesdeConfiguraciones();

        Assert.IsFalse(configuracionesPanel.activeSelf);
        Assert.IsTrue(menuPrincipalPanel.activeSelf);
    }

    [Test]
    public void VolverAMenuPrincipalDesdeConfiguraciones_ConAmbosPanelesNulos_NoRompe()
    {
        SetField("configuracionesPanel", null);
        SetField("menuPrincipalPanel", null);

        Assert.DoesNotThrow(() => controller.VolverAMenuPrincipalDesdeConfiguraciones());
    }

    // ---------------------------------------------------------------
    // Helper de reflexión (los campos son [SerializeField] privados)
    // ---------------------------------------------------------------

    private void SetField(string name, object value)
    {
        var field = typeof(ConfiguracionesPanelController).GetField(
            name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}' en ConfiguracionesPanelController");
        field.SetValue(controller, value);
    }
}