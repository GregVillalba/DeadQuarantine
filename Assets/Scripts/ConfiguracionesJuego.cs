using UnityEngine;

public static class ConfiguracionesJuego
{
    private const string KEY_VOLUMEN = "Config_Volumen";
    private const string KEY_SENSIBILIDAD = "Config_Sensibilidad";

    public const float VolumenPorDefecto = 1f;
    public const float SensibilidadPorDefecto = 0.1f;

    public static float ObtenerVolumen()
    {
        return PlayerPrefs.GetFloat(KEY_VOLUMEN, VolumenPorDefecto);
    }

    public static void GuardarVolumen(float valor)
    {
        PlayerPrefs.SetFloat(KEY_VOLUMEN, valor);
        AudioListener.volume = valor;
    }

    public static float ObtenerSensibilidad()
    {
        return PlayerPrefs.GetFloat(KEY_SENSIBILIDAD, SensibilidadPorDefecto);
    }

    public static void GuardarSensibilidad(float valor)
    {
        PlayerPrefs.SetFloat(KEY_SENSIBILIDAD, valor);
    }

    // Se ejecuta solo, apenas arranca el juego (cualquier escena),
    // así el volumen guardado se aplica sin que toques ningún otro script.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AplicarAlIniciar()
    {
        AudioListener.volume = ObtenerVolumen();
    }
}