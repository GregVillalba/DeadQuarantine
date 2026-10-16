using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Niebla y luz ambiente propias de un escenario. Va en el objeto raíz del escenario (por ejemplo "Laboratorio").
/// Mientras ese objeto esté activo se aplican sus valores; al desactivarse vuelven los valores originales de la escena.
/// Así la niebla del laboratorio no afecta a la casa, que está en la misma escena.
///
/// Importante: la luz ambiente cambiada en juego NO modifica los lightmaps ya bakeados (esos se calcularon con el
/// ambiente que había al hacer Generate Lighting). Sirve sobre todo para la niebla.
/// </summary>
public class ScenarioAtmosphere : MonoBehaviour
{
    [Header("Niebla")]
    [SerializeField] private bool overrideFog = true;
    [SerializeField] private bool fogEnabled = true;
    [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;
    [SerializeField] private Color fogColor = new Color(0.10f, 0.14f, 0.19f);
    [Tooltip("Valores chicos: 0.005 es muy suave, 0.02 ya se nota bastante.")]
    [SerializeField, Min(0f)] private float fogDensity = 0.008f;

    [Header("Luz ambiente (solo para lo que no usa lightmaps ni probes)")]
    [SerializeField] private bool overrideAmbient = false;
    [SerializeField] private Color ambientColor = new Color(0.17f, 0.21f, 0.26f);

    // Valores originales de la escena (se guardan una sola vez) y escenario que está aplicado ahora.
    private static bool baselineCaptured;
    private static bool baseFog;
    private static FogMode baseFogMode;
    private static Color baseFogColor;
    private static float baseFogDensity;
    private static AmbientMode baseAmbientMode;
    private static Color baseAmbientLight;
    private static ScenarioAtmosphere active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        baselineCaptured = false;
        active = null;
    }

    private static void CaptureBaseline()
    {
        if (baselineCaptured)
            return;

        baselineCaptured = true;
        baseFog = RenderSettings.fog;
        baseFogMode = RenderSettings.fogMode;
        baseFogColor = RenderSettings.fogColor;
        baseFogDensity = RenderSettings.fogDensity;
        baseAmbientMode = RenderSettings.ambientMode;
        baseAmbientLight = RenderSettings.ambientLight;
    }

    private static void RestoreBaseline()
    {
        RenderSettings.fog = baseFog;
        RenderSettings.fogMode = baseFogMode;
        RenderSettings.fogColor = baseFogColor;
        RenderSettings.fogDensity = baseFogDensity;
        RenderSettings.ambientMode = baseAmbientMode;
        RenderSettings.ambientLight = baseAmbientLight;
    }

    private void OnEnable()
    {
        CaptureBaseline();

        // Parte de los valores originales y encima aplica solo lo que este escenario cambia.
        RestoreBaseline();
        active = this;

        if (overrideFog)
        {
            RenderSettings.fog = fogEnabled;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }

        if (overrideAmbient)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
        }
    }

    private void OnDisable()
    {
        // Si otro escenario ya tomó el control (se activó antes de que este se apagara), no se toca nada.
        if (active != this)
            return;

        active = null;

        if (baselineCaptured)
            RestoreBaseline();
    }
}