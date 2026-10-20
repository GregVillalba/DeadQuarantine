using UnityEngine;

/// <summary>
/// Calidad gráfica del juego por presets (Muy bajo, Bajo, Normal, Alto, Ultra...).
/// Usa los niveles de calidad de Project Settings > Quality: cada nivel tiene su propio URP Asset,
/// así que este script solo elige el nivel y lo recuerda (PlayerPrefs). Los nombres de los presets salen de
/// esos mismos niveles, así que se pueden renombrar y reordenar ahí.
///
/// - Al abrir el juego aplica el nivel guardado. La primera vez, elige uno según la PC (memoria de video y procesador).
/// - Desde el menú: GraphicsQuality.Set(indice), Next() o Previous(), o el componente GraphicsQualityDropdown.
/// </summary>
public static class GraphicsQuality
{
    private const string PrefKey = "GraphicsQualityLevel";

    /// <summary>Nivel de calidad activo (índice en Project Settings > Quality).</summary>
    public static int Current
    {
        get { return QualitySettings.GetQualityLevel(); }
    }

    /// <summary>Cantidad de niveles disponibles.</summary>
    public static int Count
    {
        get { return QualitySettings.names.Length; }
    }

    /// <summary>Nombre de un nivel, tal como está en Project Settings > Quality.</summary>
    public static string NameOf(int level)
    {
        string[] names = QualitySettings.names;
        return names[Mathf.Clamp(level, 0, names.Length - 1)];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedOnStartup()
    {
        int level = PlayerPrefs.HasKey(PrefKey) ? PlayerPrefs.GetInt(PrefKey) : DetectDefault();
        Apply(level, false);
    }

    /// <summary>Cambia el preset y lo guarda.</summary>
    public static void Set(int level)
    {
        Apply(level, true);
    }

    /// <summary>Sube (+1) o baja (-1) un preset. Sirve para botones de flecha.</summary>
    public static void Step(int delta)
    {
        Apply(Current + delta, true);
    }

    public static void Next()
    {
        Step(1);
    }

    public static void Previous()
    {
        Step(-1);
    }

    private static void Apply(int level, bool save)
    {
        int max = Mathf.Max(0, Count - 1);
        level = Mathf.Clamp(level, 0, max);

        QualitySettings.SetQualityLevel(level, true);

        if (save)
        {
            PlayerPrefs.SetInt(PrefKey, level);
            PlayerPrefs.Save();
        }
    }

    // Primera vez que se abre el juego: un preset razonable según la PC.
    private static int DetectDefault()
    {
        int max = Mathf.Max(0, Count - 1);
        int videoMemoryMb = SystemInfo.graphicsMemorySize;
        int cores = SystemInfo.processorCount;

        float fraction;

        if (videoMemoryMb < 1500 || cores <= 2)
            fraction = 0.25f;       // Bajo
        else if (videoMemoryMb < 3000 || cores <= 4)
            fraction = 0.5f;        // Normal
        else
            fraction = 0.75f;       // Alto (Ultra solo si el jugador lo elige)

        return Mathf.RoundToInt(max * fraction);
    }
}