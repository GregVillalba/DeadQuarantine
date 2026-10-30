using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Configuración gráfica persistente del juego.
///
/// Los menús pueden editar una copia de GraphicsSettingsSnapshot y recién
/// llamar ApplySnapshot() cuando el jugador pulse APLICAR.
/// </summary>
[DefaultExecutionOrder(-10000)]
public sealed class GraphicsSettingsManager : MonoBehaviour
{
    [Serializable]
    public sealed class GraphicsSettingsSnapshot
    {
        public int preset = 2;

        public int textureQuality = 1;
        public int shadowQuality = 2;
        public int shadowDistance = 1;
        public int antiAliasing = 1;
        public int anisotropic = 1;
        public int renderScale = 3;
        public int lodQuality = 1;

        public bool realtimeReflections = true;
        public bool softParticles = true;
        public bool postProcessing = true;

        public bool vSync = false;
        public bool showFPS = false;
        public int fpsLimitIndex = 4;

        public int width;
        public int height;
        public FullScreenMode screenMode;

        public GraphicsSettingsSnapshot Clone()
        {
            return (GraphicsSettingsSnapshot)MemberwiseClone();
        }
    }

    public static GraphicsSettingsManager Instance { get; private set; }

    public static readonly string[] PresetNames =
    {
        "MUY BAJO",
        "BAJO",
        "NORMAL",
        "ALTO",
        "ULTRA"
    };

    public static readonly string[] TextureNames =
    {
        "ALTA",
        "MEDIA",
        "BAJA",
        "MUY BAJA"
    };

    public static readonly string[] ShadowNames =
    {
        "DESACTIVADAS",
        "BAJAS",
        "NORMALES",
        "ALTAS",
        "ULTRA"
    };

    public static readonly string[] ShadowDistanceNames =
    {
        "CORTA",
        "MEDIA",
        "LARGA",
        "MUY LARGA"
    };

    public static readonly string[] AntiAliasingNames =
    {
        "DESACTIVADO",
        "2X",
        "4X",
        "8X"
    };

    public static readonly string[] AnisotropicNames =
    {
        "DESACTIVADO",
        "POR TEXTURA",
        "FORZADO"
    };

    public static readonly string[] RenderScaleNames =
    {
        "50%",
        "67%",
        "75%",
        "100%",
        "115%"
    };

    public static readonly string[] LodNames =
    {
        "BAJO",
        "NORMAL",
        "ALTO",
        "ULTRA"
    };

    public static readonly string[] FpsLimitNames =
    {
        "SIN LÍMITE",
        "30",
        "60",
        "75",
        "120",
        "144",
        "165",
        "240"
    };

    public static readonly int[] FpsLimits =
    {
        -1,
        30,
        60,
        75,
        120,
        144,
        165,
        240
    };

    private static readonly float[] RenderScales =
    {
        0.50f,
        0.67f,
        0.75f,
        1.00f,
        1.15f
    };

    private static readonly float[] ShadowDistances =
    {
        25f,
        60f,
        110f,
        180f
    };

    private static readonly float[] LodBiases =
    {
        0.60f,
        1.00f,
        1.50f,
        2.00f
    };

    private const string KPreset = "GFX_Preset";
    private const string KTexture = "GFX_Texture";
    private const string KShadow = "GFX_Shadow";
    private const string KShadowDistance = "GFX_ShadowDistance";
    private const string KAA = "GFX_AA";
    private const string KAniso = "GFX_Aniso";
    private const string KRenderScale = "GFX_RenderScale";
    private const string KLod = "GFX_LOD";
    private const string KReflections = "GFX_Reflections";
    private const string KSoftParticles = "GFX_SoftParticles";
    private const string KPost = "GFX_PostProcessing";
    private const string KVsync = "GFX_VSync";
    private const string KFpsLimit = "GFX_FpsLimit";
    private const string KShowFps = "GFX_ShowFPS";
    private const string KScreenMode = "GFX_ScreenMode";
    private const string KWidth = "GFX_Width";
    private const string KHeight = "GFX_Height";

    private GraphicsSettingsSnapshot current;

    private float fpsSmooth = 60f;
    private float nextCameraRefresh;

    public GraphicsSettingsSnapshot Current => current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static GraphicsSettingsManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

#if UNITY_2023_1_OR_NEWER
        Instance = FindFirstObjectByType<GraphicsSettingsManager>();
#else
        Instance = FindObjectOfType<GraphicsSettingsManager>();
#endif

        if (Instance != null)
            return Instance;

        GameObject go = new GameObject("[GraphicsSettings]");

        Instance = go.AddComponent<GraphicsSettingsManager>();

        DontDestroyOnLoad(go);

        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        current = LoadSnapshot();

        ApplyRuntime(
            current,
            true
        );
    }

    private void Update()
    {
        float dt =
            Mathf.Max(
                Time.unscaledDeltaTime,
                0.00001f
            );

        float instant =
            1f / dt;

        fpsSmooth =
            Mathf.Lerp(
                fpsSmooth,
                instant,
                1f - Mathf.Exp(-6f * dt)
            );

        /*
         * Las cámaras del jugador pueden aparecer después
         * de cargar la escena o después del spawn.
         *
         * Por eso refrescamos cada cierto tiempo el estado
         * del postprocesado.
         *
         * IMPORTANTE:
         * Las cámaras que renderizan a RenderTexture quedan
         * excluidas para no romper el fondo transparente del
         * personaje en el lobby.
         */
        if (Time.unscaledTime >= nextCameraRefresh)
        {
            nextCameraRefresh =
                Time.unscaledTime + 1f;

            ApplyPostProcessingToGameplayCameras(
                current.postProcessing
            );
        }
    }

    private void OnGUI()
    {
        if (current == null ||
            !current.showFPS)
        {
            return;
        }

        GUIStyle style =
            new GUIStyle(
                GUI.skin.label
            )
            {
                alignment =
                    TextAnchor.UpperRight,

                fontSize =
                    Mathf.RoundToInt(
                        Mathf.Clamp(
                            Screen.height * 0.025f,
                            20f,
                            32f
                        )
                    ),

                fontStyle =
                    FontStyle.Bold
            };

        style.normal.textColor =
            new Color(
                0.72f,
                0.88f,
                0.76f,
                1f
            );

        GUI.Label(
            new Rect(
                Screen.width - 300f,
                20f,
                270f,
                46f
            ),
            $"FPS  {Mathf.RoundToInt(fpsSmooth)}",
            style
        );
    }

    // =========================================================
    // SNAPSHOTS
    // =========================================================

    public GraphicsSettingsSnapshot CaptureSnapshot()
    {
        return current != null
            ? current.Clone()
            : LoadSnapshot();
    }

    public GraphicsSettingsSnapshot CreatePresetSnapshot(
        int preset,
        GraphicsSettingsSnapshot baseSnapshot = null)
    {
        GraphicsSettingsSnapshot snapshot =
            baseSnapshot != null
                ? baseSnapshot.Clone()
                : CaptureSnapshot();

        ApplyPresetValues(
            snapshot,
            Mathf.Clamp(
                preset,
                0,
                4
            )
        );

        return snapshot;
    }

    public GraphicsSettingsSnapshot CreateRecommendedSnapshot()
    {
        GraphicsSettingsSnapshot snapshot =
            CaptureSnapshot();

        ApplyPresetValues(
            snapshot,
            2
        );

        snapshot.vSync = false;

        // 120 FPS
        snapshot.fpsLimitIndex = 4;

        snapshot.showFPS = false;

        snapshot.width =
            Mathf.Max(
                640,
                Screen.currentResolution.width
            );

        snapshot.height =
            Mathf.Max(
                360,
                Screen.currentResolution.height
            );

        snapshot.screenMode =
            FullScreenMode.FullScreenWindow;

        return snapshot;
    }

    public void ApplySnapshot(
        GraphicsSettingsSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        current =
            snapshot.Clone();

        Clamp(current);

        ApplyRuntime(
            current,
            true
        );

        Save(current);
    }

    /// <summary>
    /// Compatibilidad con el sistema anterior.
    /// </summary>
    public void SetPreset(int preset)
    {
        GraphicsSettingsSnapshot snapshot =
            CreatePresetSnapshot(
                preset,
                CaptureSnapshot()
            );

        ApplySnapshot(snapshot);
    }

    // =========================================================
    // LOAD
    // =========================================================

    private GraphicsSettingsSnapshot LoadSnapshot()
    {
        GraphicsSettingsSnapshot snapshot =
            new GraphicsSettingsSnapshot();

        snapshot.preset =
            PlayerPrefs.GetInt(
                KPreset,
                2
            );

        snapshot.textureQuality =
            PlayerPrefs.GetInt(
                KTexture,
                1
            );

        snapshot.shadowQuality =
            PlayerPrefs.GetInt(
                KShadow,
                2
            );

        snapshot.shadowDistance =
            PlayerPrefs.GetInt(
                KShadowDistance,
                1
            );

        snapshot.antiAliasing =
            PlayerPrefs.GetInt(
                KAA,
                1
            );

        snapshot.anisotropic =
            PlayerPrefs.GetInt(
                KAniso,
                1
            );

        snapshot.renderScale =
            PlayerPrefs.GetInt(
                KRenderScale,
                3
            );

        snapshot.lodQuality =
            PlayerPrefs.GetInt(
                KLod,
                1
            );

        snapshot.realtimeReflections =
            PlayerPrefs.GetInt(
                KReflections,
                1
            ) != 0;

        snapshot.softParticles =
            PlayerPrefs.GetInt(
                KSoftParticles,
                1
            ) != 0;

        snapshot.postProcessing =
            PlayerPrefs.GetInt(
                KPost,
                1
            ) != 0;

        snapshot.vSync =
            PlayerPrefs.GetInt(
                KVsync,
                0
            ) != 0;

        snapshot.showFPS =
            PlayerPrefs.GetInt(
                KShowFps,
                0
            ) != 0;

        snapshot.fpsLimitIndex =
            PlayerPrefs.GetInt(
                KFpsLimit,
                4
            );

        snapshot.width =
            PlayerPrefs.GetInt(
                KWidth,
                Mathf.Max(
                    640,
                    Screen.width
                )
            );

        snapshot.height =
            PlayerPrefs.GetInt(
                KHeight,
                Mathf.Max(
                    360,
                    Screen.height
                )
            );

        snapshot.screenMode =
            (FullScreenMode)
            PlayerPrefs.GetInt(
                KScreenMode,
                (int)Screen.fullScreenMode
            );

        /*
         * Si es la primera vez que existe este sistema,
         * configuramos los valores detallados desde NORMAL.
         */
        if (!PlayerPrefs.HasKey(KTexture))
        {
            ApplyPresetValues(
                snapshot,
                2
            );
        }

        Clamp(snapshot);

        return snapshot;
    }

    // =========================================================
    // PRESETS
    // =========================================================

    private static void ApplyPresetValues(
        GraphicsSettingsSnapshot snapshot,
        int preset)
    {
        snapshot.preset = preset;

        switch (preset)
        {
            // MUY BAJO
            case 0:
                snapshot.textureQuality = 3;
                snapshot.shadowQuality = 0;
                snapshot.shadowDistance = 0;
                snapshot.antiAliasing = 0;
                snapshot.anisotropic = 0;
                snapshot.renderScale = 1;
                snapshot.lodQuality = 0;

                snapshot.realtimeReflections = false;
                snapshot.softParticles = false;
                snapshot.postProcessing = false;
                break;

            // BAJO
            case 1:
                snapshot.textureQuality = 2;
                snapshot.shadowQuality = 1;
                snapshot.shadowDistance = 0;
                snapshot.antiAliasing = 0;
                snapshot.anisotropic = 1;
                snapshot.renderScale = 2;
                snapshot.lodQuality = 0;

                snapshot.realtimeReflections = false;
                snapshot.softParticles = false;
                snapshot.postProcessing = true;
                break;

            // NORMAL
            case 2:
                snapshot.textureQuality = 1;
                snapshot.shadowQuality = 2;
                snapshot.shadowDistance = 1;
                snapshot.antiAliasing = 1;
                snapshot.anisotropic = 1;
                snapshot.renderScale = 3;
                snapshot.lodQuality = 1;

                snapshot.realtimeReflections = true;
                snapshot.softParticles = true;
                snapshot.postProcessing = true;
                break;

            // ALTO
            case 3:
                snapshot.textureQuality = 0;
                snapshot.shadowQuality = 3;
                snapshot.shadowDistance = 2;
                snapshot.antiAliasing = 2;
                snapshot.anisotropic = 2;
                snapshot.renderScale = 3;
                snapshot.lodQuality = 2;

                snapshot.realtimeReflections = true;
                snapshot.softParticles = true;
                snapshot.postProcessing = true;
                break;

            // ULTRA
            default:
                snapshot.textureQuality = 0;
                snapshot.shadowQuality = 4;
                snapshot.shadowDistance = 3;
                snapshot.antiAliasing = 3;
                snapshot.anisotropic = 2;
                snapshot.renderScale = 4;
                snapshot.lodQuality = 3;

                snapshot.realtimeReflections = true;
                snapshot.softParticles = true;
                snapshot.postProcessing = true;
                break;
        }
    }

    private static void Clamp(
        GraphicsSettingsSnapshot snapshot)
    {
        snapshot.preset =
            Mathf.Clamp(
                snapshot.preset,
                -1,
                4
            );

        snapshot.textureQuality =
            Mathf.Clamp(
                snapshot.textureQuality,
                0,
                TextureNames.Length - 1
            );

        snapshot.shadowQuality =
            Mathf.Clamp(
                snapshot.shadowQuality,
                0,
                ShadowNames.Length - 1
            );

        snapshot.shadowDistance =
            Mathf.Clamp(
                snapshot.shadowDistance,
                0,
                ShadowDistanceNames.Length - 1
            );

        snapshot.antiAliasing =
            Mathf.Clamp(
                snapshot.antiAliasing,
                0,
                AntiAliasingNames.Length - 1
            );

        snapshot.anisotropic =
            Mathf.Clamp(
                snapshot.anisotropic,
                0,
                AnisotropicNames.Length - 1
            );

        snapshot.renderScale =
            Mathf.Clamp(
                snapshot.renderScale,
                0,
                RenderScaleNames.Length - 1
            );

        snapshot.lodQuality =
            Mathf.Clamp(
                snapshot.lodQuality,
                0,
                LodNames.Length - 1
            );

        snapshot.fpsLimitIndex =
            Mathf.Clamp(
                snapshot.fpsLimitIndex,
                0,
                FpsLimitNames.Length - 1
            );

        snapshot.width =
            Mathf.Max(
                640,
                snapshot.width
            );

        snapshot.height =
            Mathf.Max(
                360,
                snapshot.height
            );
    }

    // =========================================================
    // APPLY
    // =========================================================

    private void ApplyRuntime(
        GraphicsSettingsSnapshot snapshot,
        bool applyDisplay)
    {
        /*
         * QualitySettings.SetQualityLevel sigue siendo útil
         * porque puede cambiar el URP Asset asociado al nivel
         * de calidad del proyecto.
         */
        if (snapshot.preset >= 0)
        {
            int qualityIndex =
                MapPresetToProjectQualityLevel(
                    snapshot.preset
                );

            QualitySettings.SetQualityLevel(
                qualityIndex,
                true
            );
        }

        ApplyTexture(snapshot);
        ApplyShadows(snapshot);
        ApplyAntiAliasing(snapshot);
        ApplyAnisotropic(snapshot);
        ApplyRenderScale(snapshot);
        ApplyLod(snapshot);
        ApplyEffects(snapshot);

        QualitySettings.vSyncCount =
            snapshot.vSync
                ? 1
                : 0;

        Application.targetFrameRate =
            FpsLimits[
                snapshot.fpsLimitIndex
            ];

        if (applyDisplay)
        {
            Screen.SetResolution(
                snapshot.width,
                snapshot.height,
                snapshot.screenMode
            );
        }

        ApplyPostProcessingToGameplayCameras(
            snapshot.postProcessing
        );
    }

    private static int MapPresetToProjectQualityLevel(int preset)
    {
        string[] qualityNames = QualitySettings.names;

        if (qualityNames == null || qualityNames.Length == 0)
            return 0;

        preset = Mathf.Clamp(preset, 0, 4);

        // Los presets de nuestro menú:
        // 0 = Muy Bajo
        // 1 = Bajo
        // 2 = Normal
        // 3 = Alto
        // 4 = Ultra

        string[] nombresUnity =
        {
            "Very Low",
            "Low",
            "Normal",
            "High",
            "Ultra"
        };

        string objetivo = nombresUnity[preset];

        for (int i = 0; i < qualityNames.Length; i++)
        {
            if (string.Equals(
                    qualityNames[i],
                    objetivo,
                    StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        Debug.LogWarning(
            $"[GraphicsSettingsManager] No encontré el Quality Level '{objetivo}'. " +
            $"Niveles disponibles: {string.Join(", ", qualityNames)}"
        );

        return Mathf.Clamp(preset, 0, qualityNames.Length - 1);
    }

    // =========================================================
    // TEXTURAS
    // =========================================================

    private static void ApplyTexture(
        GraphicsSettingsSnapshot snapshot)
    {
#if UNITY_2022_2_OR_NEWER
        QualitySettings.globalTextureMipmapLimit =
            snapshot.textureQuality;
#else
        QualitySettings.masterTextureLimit =
            snapshot.textureQuality;
#endif
    }

    // =========================================================
    // SOMBRAS
    // =========================================================

    private static void ApplyShadows(
        GraphicsSettingsSnapshot snapshot)
    {
        if (snapshot.shadowQuality == 0)
        {
            QualitySettings.shadows =
                UnityEngine.ShadowQuality.Disable;

            TrySetPipelineProperty(
                "mainLightShadowsSupported",
                false
            );

            TrySetPipelineProperty(
                "additionalLightShadowsSupported",
                false
            );

            return;
        }

        QualitySettings.shadows =
            UnityEngine.ShadowQuality.All;

        switch (snapshot.shadowQuality)
        {
            // BAJAS
            case 1:
                QualitySettings.shadowResolution =
                    UnityEngine.ShadowResolution.Low;

                TrySetPipelineProperty(
                    "mainLightShadowmapResolution",
                    512
                );

                TrySetPipelineProperty(
                    "additionalLightsShadowmapResolution",
                    256
                );

                TrySetPipelineProperty(
                    "supportsSoftShadows",
                    false
                );
                break;

            // NORMALES
            case 2:
                QualitySettings.shadowResolution =
                    UnityEngine.ShadowResolution.Medium;

                TrySetPipelineProperty(
                    "mainLightShadowmapResolution",
                    1024
                );

                TrySetPipelineProperty(
                    "additionalLightsShadowmapResolution",
                    512
                );

                TrySetPipelineProperty(
                    "supportsSoftShadows",
                    true
                );
                break;

            // ALTAS
            case 3:
                QualitySettings.shadowResolution =
                    UnityEngine.ShadowResolution.High;

                TrySetPipelineProperty(
                    "mainLightShadowmapResolution",
                    2048
                );

                TrySetPipelineProperty(
                    "additionalLightsShadowmapResolution",
                    1024
                );

                TrySetPipelineProperty(
                    "supportsSoftShadows",
                    true
                );
                break;

            // ULTRA
            default:
                QualitySettings.shadowResolution =
                    UnityEngine.ShadowResolution.VeryHigh;

                TrySetPipelineProperty(
                    "mainLightShadowmapResolution",
                    4096
                );

                TrySetPipelineProperty(
                    "additionalLightsShadowmapResolution",
                    2048
                );

                TrySetPipelineProperty(
                    "supportsSoftShadows",
                    true
                );
                break;
        }

        TrySetPipelineProperty(
            "mainLightShadowsSupported",
            true
        );

        TrySetPipelineProperty(
            "additionalLightShadowsSupported",
            true
        );

        float distance =
            ShadowDistances[
                snapshot.shadowDistance
            ];

        QualitySettings.shadowDistance =
            distance;

        TrySetPipelineProperty(
            "shadowDistance",
            distance
        );
    }

    // =========================================================
    // ANTI ALIASING
    // =========================================================

    private static void ApplyAntiAliasing(
        GraphicsSettingsSnapshot snapshot)
    {
        int samples;

        switch (snapshot.antiAliasing)
        {
            case 1:
                samples = 2;
                break;

            case 2:
                samples = 4;
                break;

            case 3:
                samples = 8;
                break;

            default:
                samples = 1;
                break;
        }

        QualitySettings.antiAliasing =
            samples <= 1
                ? 0
                : samples;

        TrySetPipelineProperty(
            "msaaSampleCount",
            samples
        );
    }

    // =========================================================
    // ANISOTROPIC
    // =========================================================

    private static void ApplyAnisotropic(
        GraphicsSettingsSnapshot snapshot)
    {
        switch (snapshot.anisotropic)
        {
            case 0:
                QualitySettings.anisotropicFiltering =
                    AnisotropicFiltering.Disable;
                break;

            case 2:
                QualitySettings.anisotropicFiltering =
                    AnisotropicFiltering.ForceEnable;
                break;

            default:
                QualitySettings.anisotropicFiltering =
                    AnisotropicFiltering.Enable;
                break;
        }
    }

    // =========================================================
    // RENDER SCALE
    // =========================================================

    private static void ApplyRenderScale(
        GraphicsSettingsSnapshot snapshot)
    {
        TrySetPipelineProperty(
            "renderScale",
            RenderScales[
                snapshot.renderScale
            ]
        );
    }

    // =========================================================
    // LOD
    // =========================================================

    private static void ApplyLod(
        GraphicsSettingsSnapshot snapshot)
    {
        QualitySettings.lodBias =
            LodBiases[
                snapshot.lodQuality
            ];

        QualitySettings.maximumLODLevel =
            snapshot.lodQuality == 0
                ? 1
                : 0;
    }

    // =========================================================
    // EFECTOS
    // =========================================================

    private static void ApplyEffects(
        GraphicsSettingsSnapshot snapshot)
    {
        QualitySettings.realtimeReflectionProbes =
            snapshot.realtimeReflections;

        QualitySettings.softParticles =
            snapshot.softParticles;

        TrySetPipelineProperty(
            "reflectionProbeBlending",
            snapshot.realtimeReflections
        );

        TrySetPipelineProperty(
            "reflectionProbeBoxProjection",
            snapshot.realtimeReflections
        );
    }

    // =========================================================
    // POST PROCESSING
    // =========================================================

    /// <summary>
    /// Modifica únicamente cámaras reales de gameplay.
    ///
    /// MUY IMPORTANTE:
    /// Si una cámara tiene targetTexture significa que probablemente
    /// sea un preview, minimapa, CCTV, mirror, etc.
    ///
    /// CharacterPreviewStage utiliza una RenderTexture transparente.
    /// Si modificamos el postprocesado de esa cámara podemos perder
    /// el alpha y aparecerá un rectángulo negro detrás del personaje.
    /// </summary>
    private static void ApplyPostProcessingToGameplayCameras(
        bool enabled)
    {
#if UNITY_2023_1_OR_NEWER
        Camera[] cameras =
            FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
#else
        Camera[] cameras =
            FindObjectsOfType<Camera>(true);
#endif

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera =
                cameras[i];

            if (camera == null)
                continue;

            /*
             * NO TOCAR:
             * - preview del personaje;
             * - minimapas;
             * - cámaras a RenderTexture;
             * - espejos;
             * - CCTV.
             */
            if (camera.targetTexture != null)
                continue;

            if (camera.cameraType != CameraType.Game)
                continue;

            UniversalAdditionalCameraData cameraData =
                camera.GetComponent<
                    UniversalAdditionalCameraData
                >();

            if (cameraData == null)
                continue;

            /*
             * Las cámaras Overlay forman parte de un Camera Stack.
             * El Post Processing se controla desde la cámara Base.
             */
            if (cameraData.renderType !=
                CameraRenderType.Base)
            {
                continue;
            }

            cameraData.renderPostProcessing =
                enabled;
        }
    }

    // =========================================================
    // URP
    // =========================================================

    private static void TrySetPipelineProperty(
        string propertyName,
        object value)
    {
        RenderPipelineAsset asset =
            UnityEngine.Rendering.GraphicsSettings
                .currentRenderPipeline;

        if (asset == null)
        {
            asset =
                QualitySettings.renderPipeline;
        }

        if (asset == null)
            return;

        PropertyInfo property =
            asset
                .GetType()
                .GetProperty(
                    propertyName,
                    BindingFlags.Instance |
                    BindingFlags.Public
                );

        if (property == null ||
            !property.CanWrite)
        {
            return;
        }

        try
        {
            object converted =
                ConvertValue(
                    value,
                    property.PropertyType
                );

            property.SetValue(
                asset,
                converted
            );
        }
        catch
        {
            /*
             * Algunas versiones de URP exponen ciertas
             * propiedades únicamente como lectura.
             *
             * QualitySettings ya recibe el fallback,
             * por lo que no queremos romper el menú.
             */
        }
    }

    private static object ConvertValue(
        object value,
        Type destinationType)
    {
        if (value == null)
            return null;

        Type sourceType =
            value.GetType();

        if (destinationType.IsAssignableFrom(sourceType))
            return value;

        if (destinationType.IsEnum)
        {
            if (value is int intValue)
            {
                return Enum.ToObject(
                    destinationType,
                    intValue
                );
            }

            return Enum.Parse(
                destinationType,
                value.ToString()
            );
        }

        return Convert.ChangeType(
            value,
            destinationType
        );
    }

    // =========================================================
    // SAVE
    // =========================================================

    private static void Save(
        GraphicsSettingsSnapshot snapshot)
    {
        PlayerPrefs.SetInt(
            KPreset,
            snapshot.preset
        );

        PlayerPrefs.SetInt(
            KTexture,
            snapshot.textureQuality
        );

        PlayerPrefs.SetInt(
            KShadow,
            snapshot.shadowQuality
        );

        PlayerPrefs.SetInt(
            KShadowDistance,
            snapshot.shadowDistance
        );

        PlayerPrefs.SetInt(
            KAA,
            snapshot.antiAliasing
        );

        PlayerPrefs.SetInt(
            KAniso,
            snapshot.anisotropic
        );

        PlayerPrefs.SetInt(
            KRenderScale,
            snapshot.renderScale
        );

        PlayerPrefs.SetInt(
            KLod,
            snapshot.lodQuality
        );

        PlayerPrefs.SetInt(
            KReflections,
            snapshot.realtimeReflections
                ? 1
                : 0
        );

        PlayerPrefs.SetInt(
            KSoftParticles,
            snapshot.softParticles
                ? 1
                : 0
        );

        PlayerPrefs.SetInt(
            KPost,
            snapshot.postProcessing
                ? 1
                : 0
        );

        PlayerPrefs.SetInt(
            KVsync,
            snapshot.vSync
                ? 1
                : 0
        );

        PlayerPrefs.SetInt(
            KShowFps,
            snapshot.showFPS
                ? 1
                : 0
        );

        PlayerPrefs.SetInt(
            KFpsLimit,
            snapshot.fpsLimitIndex
        );

        PlayerPrefs.SetInt(
            KWidth,
            snapshot.width
        );

        PlayerPrefs.SetInt(
            KHeight,
            snapshot.height
        );

        PlayerPrefs.SetInt(
            KScreenMode,
            (int)snapshot.screenMode
        );

        PlayerPrefs.Save();
    }
}