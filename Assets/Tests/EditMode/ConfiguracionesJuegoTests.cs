using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ConfiguracionesJuegoTests
{
    private const string KEY_VOLUMEN = "Config_Volumen";
    private const string KEY_SENSIBILIDAD = "Config_Sensibilidad";

    private bool teniaVolumenGuardado;
    private float volumenOriginal;
    private bool teniaSensibilidadGuardada;
    private float sensibilidadOriginal;
    private float audioListenerVolumeOriginal;

    [SetUp]
    public void SetUp()
    {
        teniaVolumenGuardado = PlayerPrefs.HasKey(KEY_VOLUMEN);
        if (teniaVolumenGuardado)
            volumenOriginal = PlayerPrefs.GetFloat(KEY_VOLUMEN);

        teniaSensibilidadGuardada = PlayerPrefs.HasKey(KEY_SENSIBILIDAD);
        if (teniaSensibilidadGuardada)
            sensibilidadOriginal = PlayerPrefs.GetFloat(KEY_SENSIBILIDAD);

        audioListenerVolumeOriginal = AudioListener.volume;

        PlayerPrefs.DeleteKey(KEY_VOLUMEN);
        PlayerPrefs.DeleteKey(KEY_SENSIBILIDAD);
    }

    [TearDown]
    public void TearDown()
    {
        if (teniaVolumenGuardado)
            PlayerPrefs.SetFloat(KEY_VOLUMEN, volumenOriginal);
        else
            PlayerPrefs.DeleteKey(KEY_VOLUMEN);

        if (teniaSensibilidadGuardada)
            PlayerPrefs.SetFloat(KEY_SENSIBILIDAD, sensibilidadOriginal);
        else
            PlayerPrefs.DeleteKey(KEY_SENSIBILIDAD);

        AudioListener.volume = audioListenerVolumeOriginal;
    }

    // ---------------------------------------------------------------
    // ObtenerVolumen
    // ---------------------------------------------------------------

    [Test]
    public void ObtenerVolumen_SinValorGuardado_DevuelveElValorPorDefecto()
    {
        float resultado = ConfiguracionesJuego.ObtenerVolumen();

        Assert.AreEqual(ConfiguracionesJuego.VolumenPorDefecto, resultado);
    }

    [Test]
    public void ObtenerVolumen_ConValorGuardado_DevuelveElValorGuardado()
    {
        PlayerPrefs.SetFloat(KEY_VOLUMEN, 0.35f);

        float resultado = ConfiguracionesJuego.ObtenerVolumen();

        Assert.AreEqual(0.35f, resultado, 0.0001f);
    }

    // ---------------------------------------------------------------
    // GuardarVolumen
    // ---------------------------------------------------------------

    [Test]
    public void GuardarVolumen_GuardaElValorEnPlayerPrefs()
    {
        ConfiguracionesJuego.GuardarVolumen(0.6f);

        Assert.AreEqual(0.6f, PlayerPrefs.GetFloat(KEY_VOLUMEN), 0.0001f);
    }

    [Test]
    public void GuardarVolumen_ActualizaAudioListenerVolumeInmediatamente()
    {
        ConfiguracionesJuego.GuardarVolumen(0.42f);

        Assert.AreEqual(0.42f, AudioListener.volume, 0.0001f);
    }

    // ---------------------------------------------------------------
    // ObtenerSensibilidad
    // ---------------------------------------------------------------

    [Test]
    public void ObtenerSensibilidad_SinValorGuardado_DevuelveElValorPorDefecto()
    {
        float resultado = ConfiguracionesJuego.ObtenerSensibilidad();

        Assert.AreEqual(ConfiguracionesJuego.SensibilidadPorDefecto, resultado);
    }

    [Test]
    public void ObtenerSensibilidad_ConValorGuardado_DevuelveElValorGuardado()
    {
        PlayerPrefs.SetFloat(KEY_SENSIBILIDAD, 0.25f);

        float resultado = ConfiguracionesJuego.ObtenerSensibilidad();

        Assert.AreEqual(0.25f, resultado, 0.0001f);
    }

    // ---------------------------------------------------------------
    // GuardarSensibilidad
    // ---------------------------------------------------------------

    [Test]
    public void GuardarSensibilidad_GuardaElValorEnPlayerPrefs()
    {
        ConfiguracionesJuego.GuardarSensibilidad(0.18f);

        Assert.AreEqual(0.18f, PlayerPrefs.GetFloat(KEY_SENSIBILIDAD), 0.0001f);
    }

    // ---------------------------------------------------------------
    // AplicarAlIniciar (privado, invocado por reflexión)
    // ---------------------------------------------------------------

    [Test]
    public void AplicarAlIniciar_AsignaAudioListenerVolumeSegunElVolumenGuardado()
    {
        PlayerPrefs.SetFloat(KEY_VOLUMEN, 0.77f);
        AudioListener.volume = 0f; // valor distinto, para confirmar que el método lo cambia

        InvokePrivateStatic("AplicarAlIniciar");

        Assert.AreEqual(0.77f, AudioListener.volume, 0.0001f);
    }

    [Test]
    public void AplicarAlIniciar_SinValorGuardado_UsaElVolumenPorDefecto()
    {
        AudioListener.volume = 0f;

        InvokePrivateStatic("AplicarAlIniciar");

        Assert.AreEqual(ConfiguracionesJuego.VolumenPorDefecto, AudioListener.volume, 0.0001f);
    }

    // ---------------------------------------------------------------
    // Helper de reflexión
    // ---------------------------------------------------------------

    private void InvokePrivateStatic(string methodName)
    {
        var method = typeof(ConfiguracionesJuego).GetMethod(
            methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, $"No se encontró el método '{methodName}' en ConfiguracionesJuego");
        method.Invoke(null, null);
    }
}
