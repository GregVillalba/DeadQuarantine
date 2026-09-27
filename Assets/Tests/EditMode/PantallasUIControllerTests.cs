using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PantallasUIControllerTests
{
   private GameObject controllerGO;
    private GameObject menuPrincipalPanel;
    private GameObject comoJugarPanel;
    private GameObject elegirModoPanel;
    private PantallasUIController controller;
 
    [SetUp]
    public void SetUp()
    {
        PantallasUIController.VolverAElegirModo = false;
 
        controllerGO = new GameObject("PantallasUIController");
        controller = controllerGO.AddComponent<PantallasUIController>();
 
        menuPrincipalPanel = new GameObject("MenuPrincipalPanel");
        comoJugarPanel = new GameObject("ComoJugarPanel");
        elegirModoPanel = new GameObject("ElegirModoPanel");
 
        SetPrivateField(controller, "menuPrincipalPanel", menuPrincipalPanel);
        SetPrivateField(controller, "comoJugarPanel", comoJugarPanel);
        SetPrivateField(controller, "elegirModoPanel", elegirModoPanel);
    }
 
    [TearDown]
    public void TearDown()
    {
        PantallasUIController.VolverAElegirModo = false;
 
        Object.DestroyImmediate(controllerGO);
        Object.DestroyImmediate(menuPrincipalPanel);
        Object.DestroyImmediate(comoJugarPanel);
        Object.DestroyImmediate(elegirModoPanel);
    }
 
    // ---------- Helpers de reflexión ----------
 
    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(fi, $"No se encontró el campo privado '{fieldName}'.");
        fi.SetValue(target, value);
    }
 
    private static void InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo mi = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mi, $"No se encontró el método privado '{methodName}'.");
        mi.Invoke(target, null);
    }
 
    // ---------- MostrarMenuPrincipal ----------
 
    [Test]
    public void MostrarMenuPrincipal_ActivaMenuYDesactivaLosOtrosDosPaneles()
    {
        menuPrincipalPanel.SetActive(false);
        comoJugarPanel.SetActive(true);
        elegirModoPanel.SetActive(true);
 
        controller.MostrarMenuPrincipal();
 
        Assert.IsTrue(menuPrincipalPanel.activeSelf);
        Assert.IsFalse(comoJugarPanel.activeSelf);
        Assert.IsFalse(elegirModoPanel.activeSelf);
    }
 
    [Test]
    public void MostrarMenuPrincipal_ConPanelesNulos_NoLanzaExcepcion()
    {
        SetPrivateField(controller, "menuPrincipalPanel", null);
        SetPrivateField(controller, "comoJugarPanel", null);
        SetPrivateField(controller, "elegirModoPanel", null);
 
        Assert.DoesNotThrow(() => controller.MostrarMenuPrincipal());
    }
 
    // ---------- MostrarElegirModo ----------
 
    [Test]
    public void MostrarElegirModo_ActivaElegirModoYDesactivaLosOtrosDosPaneles()
    {
        menuPrincipalPanel.SetActive(true);
        comoJugarPanel.SetActive(true);
        elegirModoPanel.SetActive(false);
 
        controller.MostrarElegirModo();
 
        Assert.IsFalse(menuPrincipalPanel.activeSelf);
        Assert.IsFalse(comoJugarPanel.activeSelf);
        Assert.IsTrue(elegirModoPanel.activeSelf);
    }
 
    [Test]
    public void MostrarElegirModo_ConPanelesNulos_NoLanzaExcepcion()
    {
        SetPrivateField(controller, "menuPrincipalPanel", null);
        SetPrivateField(controller, "comoJugarPanel", null);
        SetPrivateField(controller, "elegirModoPanel", null);
 
        Assert.DoesNotThrow(() => controller.MostrarElegirModo());
    }
 
    // ---------- Start (privado, invocado por reflexión) ----------
 
    [Test]
    public void Start_ConVolverAElegirModoEnFalse_MuestraMenuPrincipal()
    {
        PantallasUIController.VolverAElegirModo = false;
        menuPrincipalPanel.SetActive(false);
        elegirModoPanel.SetActive(true);
 
        InvokePrivateMethod(controller, "Start");
 
        Assert.IsTrue(menuPrincipalPanel.activeSelf);
        Assert.IsFalse(elegirModoPanel.activeSelf);
        Assert.IsFalse(PantallasUIController.VolverAElegirModo);
    }
 
    [Test]
    public void Start_ConVolverAElegirModoEnTrue_MuestraElegirModoYReseteaElFlag()
    {
        PantallasUIController.VolverAElegirModo = true;
        menuPrincipalPanel.SetActive(true);
        elegirModoPanel.SetActive(false);
 
        InvokePrivateMethod(controller, "Start");
 
        Assert.IsFalse(menuPrincipalPanel.activeSelf);
        Assert.IsTrue(elegirModoPanel.activeSelf);
        Assert.IsFalse(PantallasUIController.VolverAElegirModo, "Start debe resetear el flag a false.");
    }
}
