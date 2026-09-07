using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class ControlVolumenTests
{
    private const string KEY_VOLUMEN = "Config_Volumen";

    private GameObject holderGO;
    private ControlVolumen controller;
    private GameObject sliderGO;
    private Slider slider;

    private bool teniaVolumenGuardado;
    private float volumenOriginal;
    private float audioListenerVolumeOriginal;

    [SetUp]
    public void SetUp()
    {
        teniaVolumenGuardado = PlayerPrefs.HasKey(KEY_VOLUMEN);
        if (teniaVolumenGuardado)
            volumenOriginal = PlayerPrefs.GetFloat(KEY_VOLUMEN);
        PlayerPrefs.DeleteKey(KEY_VOLUMEN);

        audioListenerVolumeOriginal = AudioListener.volume;

        holderGO = new GameObject("ControlVolumen");
        controller = holderGO.AddComponent<ControlVolumen>();

        sliderGO = new GameObject("SliderVolumen", typeof(RectTransform));
        slider = sliderGO.AddComponent<Slider>();

        SetField("sliderVolumen", slider);
    }

    [TearDown]
    public void TearDown()
    {
        if (teniaVolumenGuardado)
            PlayerPrefs.SetFloat(KEY_VOLUMEN, volumenOriginal);
        else
            PlayerPrefs.DeleteKey(KEY_VOLUMEN);

        AudioListener.volume = audioListenerVolumeOriginal;

        Object.DestroyImmediate(sliderGO);
        Object.DestroyImmediate(holderGO);
    }

    // ---------------------------------------------------------------
    // OnEnable
    // ---------------------------------------------------------------

    [Test]
    public void OnEnable_ConValorGuardado_ActualizaElSliderSinNotificar()
    {
        PlayerPrefs.SetFloat(KEY_VOLUMEN, 0.62f);
        slider.value = 0f;

        bool notifico = false;
        slider.onValueChanged.AddListener(_ => notifico = true);

        Invoke("OnEnable");

        Assert.AreEqual(0.62f, slider.value, 0.0001f);
        Assert.IsFalse(notifico, "SetValueWithoutNotify no debería disparar onValueChanged");
    }

    [Test]
    public void OnEnable_SinValorGuardado_UsaElVolumenPorDefecto()
    {
        slider.value = 0f;

        Invoke("OnEnable");

        Assert.AreEqual(ConfiguracionesJuego.VolumenPorDefecto, slider.value, 0.0001f);
    }

    [Test]
    public void OnEnable_SiElSliderEsNull_NoRompe()
    {
        SetField("sliderVolumen", null);

        Assert.DoesNotThrow(() => Invoke("OnEnable"));
    }

    // ---------------------------------------------------------------
    // CambiarVolumen
    // ---------------------------------------------------------------

    [Test]
    public void CambiarVolumen_GuardaElValorEnPlayerPrefs()
    {
        controller.CambiarVolumen(0.48f);

        Assert.AreEqual(0.48f, PlayerPrefs.GetFloat(KEY_VOLUMEN), 0.0001f);
    }

    [Test]
    public void CambiarVolumen_ActualizaAudioListenerVolumeInmediatamente()
    {
        controller.CambiarVolumen(0.2f);

        Assert.AreEqual(0.2f, AudioListener.volume, 0.0001f);
    }

    // ---------------------------------------------------------------
    // Helpers de reflexión
    // ---------------------------------------------------------------

    private void SetField(string name, object value)
    {
        var field = typeof(ControlVolumen).GetField(
            name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}' en ControlVolumen");
        field.SetValue(controller, value);
    }

    private void Invoke(string methodName, params object[] args)
    {
        var method = typeof(ControlVolumen).GetMethod(
            methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"No se encontró el método '{methodName}' en ControlVolumen");
        method.Invoke(controller, args);
    }
}
