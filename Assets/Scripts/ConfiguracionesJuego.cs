using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public static class ConfiguracionesJuego
{
    private const string KEY_VOLUMEN = "Config_Volumen";
    private const string KEY_SENSIBILIDAD = "Config_Sensibilidad";
    private const string KEY_REBINDS = "Config_Rebinds";

    public const float VolumenPorDefecto = 1f;
    public const float SensibilidadPorDefecto = 0.1f;

    /// <summary>
    /// Se dispara cuando se aplican los cambios de configuración en plena
    /// partida (al cerrar el menú de pausa). Los scripts que leen la
    /// sensibilidad u otro valor solo en Awake se suscriben acá.
    /// </summary>
    public static event Action CambiosAplicados;

    // Todos los assets que pasaron por CargarRebinds (uno por cada
    // "new PlayerControls()"), para poder recargarles las teclas en caliente.
    private static readonly List<WeakReference<InputActionAsset>> assetsRegistrados =
        new List<WeakReference<InputActionAsset>>();

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

    // Con "Enter Play Mode Options" (sin recargar dominio) los estáticos sobreviven entre partidas.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        assetsRegistrados.Clear();
        CambiosAplicados = null;
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
        Registrar(asset);
        string json = PlayerPrefs.GetString(KEY_REBINDS, string.Empty);
        if (!string.IsNullOrEmpty(json)) asset.LoadBindingOverridesFromJson(json);
    }

    /// <summary>
    /// Vuelve a aplicar lo guardado (teclas y sensibilidad) a todo lo que ya
    /// está en la escena. Lo llama el menú de pausa al cerrarse; el volumen
    /// no lo necesita porque GuardarVolumen ya lo aplica al instante.
    /// </summary>
    public static void AplicarCambios()
    {
        string json = PlayerPrefs.GetString(KEY_REBINDS, string.Empty);

        for (int i = assetsRegistrados.Count - 1; i >= 0; i--)
        {
            // "asset == null" también detecta los que Unity ya destruyó (controls.Dispose()).
            if (!assetsRegistrados[i].TryGetTarget(out var asset) || asset == null)
            {
                assetsRegistrados.RemoveAt(i);
                continue;
            }

            RecargarRebinds(asset, json);
        }

        CambiosAplicados?.Invoke();
    }

    private static void Registrar(InputActionAsset asset)
    {
        foreach (var referencia in assetsRegistrados)
        {
            if (referencia.TryGetTarget(out var existente) && ReferenceEquals(existente, asset)) return;
        }
        assetsRegistrados.Add(new WeakReference<InputActionAsset>(asset));
    }

    // Quita los overrides viejos (por si se restablecieron las teclas) y carga
    // los guardados. Los mapas habilitados se apagan mientras tanto y se
    // vuelven a prender, así las acciones toman las teclas nuevas.
    private static void RecargarRebinds(InputActionAsset asset, string json)
    {
        var mapasActivos = new List<InputActionMap>();
        foreach (var mapa in asset.actionMaps)
        {
            if (!mapa.enabled) continue;
            mapasActivos.Add(mapa);
            mapa.Disable();
        }

        asset.RemoveAllBindingOverrides();
        if (!string.IsNullOrEmpty(json)) asset.LoadBindingOverridesFromJson(json);

        foreach (var mapa in mapasActivos) mapa.Enable();
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