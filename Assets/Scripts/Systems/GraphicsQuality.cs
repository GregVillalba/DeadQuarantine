using UnityEngine;

/// <summary>
/// Compatibilidad con llamadas antiguas a GraphicsQuality.
/// </summary>
public static class GraphicsQuality
{
    public static int Current
    {
        get
        {
            GraphicsSettingsManager manager =
                GraphicsSettingsManager.EnsureInstance();

            GraphicsSettingsManager.GraphicsSettingsSnapshot s =
                manager.CaptureSnapshot();

            return s.preset >= 0
                ? s.preset
                : Mathf.Clamp(
                    QualitySettings.GetQualityLevel(),
                    0,
                    4
                );
        }
    }

    public static int Count => 5;

    public static string NameOf(int level)
    {
        return GraphicsSettingsManager.PresetNames[
            Mathf.Clamp(level, 0, 4)
        ];
    }

    public static void Set(int level)
    {
        GraphicsSettingsManager
            .EnsureInstance()
            .SetPreset(
                Mathf.Clamp(level, 0, 4)
            );
    }

    public static void Step(int delta)
    {
        Set(
            Mathf.Clamp(
                Current + delta,
                0,
                Count - 1
            )
        );
    }

    public static void Next() => Step(1);
    public static void Previous() => Step(-1);
}
