using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class MenuPrincipalAccionesTests
{
   private GameObject root;
    private MenuPrincipalAcciones controller;
 
    private GameObject menuPrincipalPanel;
    private GameObject comoJugarPanel;
    private GameObject elegirModoPanel;
 
    [SetUp]
    public void SetUp()
    {
        root = new GameObject("MenuPrincipalAccionesTestObject");
        controller = root.AddComponent<MenuPrincipalAcciones>();
 
        menuPrincipalPanel = new GameObject("MenuPrincipalPanel");
        comoJugarPanel = new GameObject("ComoJugarPanel");
        elegirModoPanel = new GameObject("ElegirModoPanel");
 
        SetPrivateField("menuPrincipalPanel", menuPrincipalPanel);
        SetPrivateField("comoJugarPanel", comoJugarPanel);
        SetPrivateField("elegirModoPanel", elegirModoPanel);
    }
 
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(menuPrincipalPanel);
        Object.DestroyImmediate(comoJugarPanel);
        Object.DestroyImmediate(elegirModoPanel);
    }
 
    private void SetPrivateField(string fieldName, object value)
    {
        FieldInfo field = typeof(MenuPrincipalAcciones).GetField(
            fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
 
        Assert.IsNotNull(field, $"No se encontró el campo privado '{fieldName}' en MenuPrincipalAcciones.");
        field.SetValue(controller, value);
    }
 
    // ---------------- MostrarModoJuego ----------------
 
    [Test]
    public void MostrarModoJuego_DesactivaMenuPrincipal()
    {
        menuPrincipalPanel.SetActive(true);
 
        controller.MostrarModoJuego();
 
        Assert.IsFalse(menuPrincipalPanel.activeSelf,
            "MostrarModoJuego() debería desactivar el panel de menú principal.");
    }
 
    [Test]
    public void MostrarModoJuego_ActivaElegirModoPanel()
    {
        elegirModoPanel.SetActive(false);
 
        controller.MostrarModoJuego();
 
        Assert.IsTrue(elegirModoPanel.activeSelf,
            "MostrarModoJuego() debería activar el panel de elegir modo.");
    }
 
    [Test]
    public void MostrarModoJuego_ConPanelNulo_NoLanzaExcepcion()
    {
        SetPrivateField("menuPrincipalPanel", null);
 
        Assert.DoesNotThrow(() => controller.MostrarModoJuego(),
            "MostrarModoJuego() no debería lanzar excepción si algún panel no está asignado.");
    }
 
    // ---------------- MostrarComoJugar ----------------
 
    [Test]
    public void MostrarComoJugar_DesactivaMenuPrincipal()
    {
        menuPrincipalPanel.SetActive(true);
 
        controller.MostrarComoJugar();
 
        Assert.IsFalse(menuPrincipalPanel.activeSelf,
            "MostrarComoJugar() debería desactivar el panel de menú principal.");
    }
 
    [Test]
    public void MostrarComoJugar_ActivaComoJugarPanel()
    {
        comoJugarPanel.SetActive(false);
 
        controller.MostrarComoJugar();
 
        Assert.IsTrue(comoJugarPanel.activeSelf,
            "MostrarComoJugar() debería activar el panel de cómo jugar.");
    }
 
    [Test]
    public void MostrarComoJugar_TambienActivaElegirModoPanel()
    {
        elegirModoPanel.SetActive(false);
 
        controller.MostrarComoJugar();
 
        Assert.IsTrue(elegirModoPanel.activeSelf,
            "Según la implementación actual, MostrarComoJugar() también activa elegirModoPanel. " +
            "Si no es el comportamiento deseado, corregir el script y este test.");
    }
 
    [Test]
    public void MostrarComoJugar_ConTodosLosPanelesNulos_NoLanzaExcepcion()
    {
        SetPrivateField("menuPrincipalPanel", null);
        SetPrivateField("comoJugarPanel", null);
        SetPrivateField("elegirModoPanel", null);
 
        Assert.DoesNotThrow(() => controller.MostrarComoJugar());
    }
 
    // ---------------- VolverAMenuPrincipal ----------------
 
    [Test]
    public void VolverAMenuPrincipal_ActivaMenuPrincipal()
    {
        menuPrincipalPanel.SetActive(false);
 
        controller.VolverAMenuPrincipal();
 
        Assert.IsTrue(menuPrincipalPanel.activeSelf,
            "VolverAMenuPrincipal() debería activar el panel de menú principal.");
    }
 
    [Test]
    public void VolverAMenuPrincipal_DesactivaComoJugarYElegirModo()
    {
        comoJugarPanel.SetActive(true);
        elegirModoPanel.SetActive(true);
 
        controller.VolverAMenuPrincipal();
 
        Assert.IsFalse(comoJugarPanel.activeSelf,
            "VolverAMenuPrincipal() debería desactivar el panel de cómo jugar.");
        Assert.IsFalse(elegirModoPanel.activeSelf,
            "VolverAMenuPrincipal() debería desactivar el panel de elegir modo.");
    }
 
    [Test]
    public void VolverAMenuPrincipal_ConPanelesNulos_NoLanzaExcepcion()
    {
        SetPrivateField("comoJugarPanel", null);
        SetPrivateField("elegirModoPanel", null);
 
        Assert.DoesNotThrow(() => controller.VolverAMenuPrincipal());
    }
 
    
    
}
