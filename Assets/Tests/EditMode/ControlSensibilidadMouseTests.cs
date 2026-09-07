using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class ControlSensibilidadMouseTests
{
    private const string KEY_SENSIBILIDAD = "Config_Sensibilidad";

    private GameObject holderGO;
    private ControlSensibilidadMouse controller;
    private GameObject sliderGO;
    private Slider slider;

    private bool teniaSensibilidadGuardada;
    private float sensibilidadOriginal;

    [SetUp]
    public void SetUp()
    {
        teniaSensibilidadGuardada = PlayerPrefs.HasKey(KEY_SENSIBILIDAD);
        if (teniaSensibilidadGuardada)
            sensibilidadOriginal = PlayerPrefs.GetFloat(KEY_SENSIBILIDAD);
        PlayerPrefs.DeleteKey(KEY_SENSIBILIDAD);

        holderGO = new GameObject("ControlSensibilidadMouse");
        controller = holderGO.AddComponent<ControlSensibilidadMouse>();

        sliderGO = new GameObject("SliderSensibilidad", typeof(RectTransform));
        slider = sliderGO.AddComponent<Slider>();

        SetField("sliderSensibilidad", slider);
    }

    [TearDown]
    public void TearDown()
    {
        if (teniaSensibilidadGuardada)
            PlayerPrefs.SetFloat(KEY_SENSIBILIDAD, sensibilidadOriginal);
        else
            PlayerPrefs.DeleteKey(KEY_SENSIBILIDAD);

        Object.DestroyImmediate(sliderGO);
        Object.DestroyImmediate(holderGO);
    }

    // ---------------------------------------------------------------
    // OnEnable
    // ---------------------------------------------------------------

    [Test]
    public void OnEnable_ConValorGuardado_ActualizaElSliderSinNotificar()
    {
        PlayerPrefs.SetFloat(KEY_SENSIBILIDAD, 0.33f);
        slider.value = 0f;

        bool notifico = false;
        slider.onValueChanged.AddListener(_ => notifico = true);

        Invoke("OnEnable");

        Assert.AreEqual(0.33f, slider.value, 0.0001f);
        Assert.IsFalse(notifico, "SetValueWithoutNotify no debería disparar onValueChanged");
    }

    [Test]
    public void OnEnable_SinValorGuardado_UsaLaSensibilidadPorDefecto()
    {
        slider.value = 0f;

        Invoke("OnEnable");

        Assert.AreEqual(ConfiguracionesJuego.SensibilidadPorDefecto, slider.value, 0.0001f);
    }

    [Test]
    public void OnEnable_SiElSliderEsNull_NoRompe()
    {
        SetField("sliderSensibilidad", null);

        Assert.DoesNotThrow(() => Invoke("OnEnable"));
    }

    // ---------------------------------------------------------------
    // CambiarSensibilidad
    // ---------------------------------------------------------------

    [Test]
    public void CambiarSensibilidad_GuardaElValorEnPlayerPrefs()
    {
        controller.CambiarSensibilidad(0.55f);

        Assert.AreEqual(0.55f, PlayerPrefs.GetFloat(KEY_SENSIBILIDAD), 0.0001f);
    }

    // ---------------------------------------------------------------
    // Helpers de reflexión
    // ---------------------------------------------------------------

    private void SetField(string name, object value)
    {
        var field = typeof(ControlSensibilidadMouse).GetField(
            name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}' en ControlSensibilidadMouse");
        field.SetValue(controller, value);
    }

    private void Invoke(string methodName, params object[] args)
    {
        var method = typeof(ControlSensibilidadMouse).GetMethod(
            methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"No se encontró el método '{methodName}' en ControlSensibilidadMouse");
        method.Invoke(controller, args);
    }
}
