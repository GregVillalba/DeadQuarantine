using UnityEngine;
using UnityEngine.InputSystem;

public static class ConfiguracionesJuego
{
    private const string KEY_VOLUMEN = "Config_Volumen";
    private const string KEY_SENSIBILIDAD = "Config_Sensibilidad";
    private const string KEY_REBINDS = "Config_Rebinds";

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

    /// <summary>
    /// Aplica al asset los rebinds guardados (si hay). Cada script que haga
    /// "new PlayerControls()" crea su propia copia del asset, así que hay
    /// que llamar esto en su Awake (después de instanciar el asset y antes
    /// de habilitarlo) para que los rebinds guardados tengan efecto.
    /// </summary>
    public static void CargarRebinds(InputActionAsset asset)
    {
        if (asset == null) return;
        string json = PlayerPrefs.GetString(KEY_REBINDS, string.Empty);
        if (!string.IsNullOrEmpty(json)) asset.LoadBindingOverridesFromJson(json);
    }

    public static void GuardarRebinds(InputActionAsset asset)
    {
        if (asset == null) return;
        PlayerPrefs.SetString(KEY_REBINDS, asset.SaveBindingOverridesAsJson());
    }

    public static void RestablecerRebinds(InputActionAsset asset)
    {
        if (asset != null) asset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(KEY_REBINDS);
    }
}