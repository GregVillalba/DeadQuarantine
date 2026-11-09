using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Menú unificado de configuración.
/// Todo queda PENDIENTE hasta pulsar APLICAR.
/// </summary>
public sealed class ConfiguracionMenuController : MonoBehaviour
{
    [Header("Navegación")]
    [SerializeField] private GameObject menuPrincipalPanel;
    [SerializeField] private GameObject[] secciones;
    [SerializeField] private Button[] botonesSeccion;
    [SerializeField] private TMP_Text[] textosBotonesSeccion;

    [Header("Usuario / Perfil")]
    [Tooltip("Botón USUARIO de la barra lateral")]
    [SerializeField] private Button usuarioButton;
    [Tooltip("Panel UsernameFirstRunUI que se abrirá")]
    [SerializeField] private GameObject panelUsuario;
    [Tooltip("Botón CANCELAR dentro de UsernameFirstRunUI para cerrarlo (opcional)")]
    [SerializeField] private Button cancelarUsuarioButton;

    [Header("Pantalla")]
    [SerializeField] private TMP_Dropdown resolucionDropdown;
    [SerializeField] private TMP_Dropdown modoPantallaDropdown;

    [Header("Gráficos")]
    [SerializeField] private TMP_Dropdown presetDropdown;
    [SerializeField] private TMP_Text presetEstadoText;
    [SerializeField] private TMP_Dropdown renderScaleDropdown;

    [Header("Avanzado")]
    [SerializeField] private TMP_Dropdown texturasDropdown;
    [SerializeField] private TMP_Dropdown sombrasDropdown;
    [SerializeField] private TMP_Dropdown distanciaSombrasDropdown;
    [SerializeField] private TMP_Dropdown antialiasingDropdown;
    [SerializeField] private TMP_Dropdown anisotropicoDropdown;
    [SerializeField] private TMP_Dropdown lodDropdown;
    [SerializeField] private Toggle reflejosToggle;
    [SerializeField] private Toggle particulasToggle;
    [SerializeField] private Toggle postProcessingToggle;

    [Header("Rendimiento")]
    [SerializeField] private TMP_Dropdown limiteFpsDropdown;
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private Toggle mostrarFpsToggle;
    [SerializeField] private TMP_Text vSyncAdvertencia;
    [SerializeField] private TMP_Text hardwareText;

    [Header("Audio")]
    [SerializeField] private Slider volumenSlider;
    [SerializeField] private TMP_Text volumenValor;

    [Header("Sensibilidad")]
    [SerializeField] private Slider sensibilidadSlider;
    [SerializeField] private TMP_Text sensibilidadValor;

    [Header("Controles")]
    [SerializeField] private ControlesRebind controles;

    [Header("Acciones")]
    [SerializeField] private Button aplicarButton;
    [SerializeField] private Button restaurarButton;
    [SerializeField] private Button volverButton;
    [SerializeField] private TMP_Text estadoCambiosText;

    private GraphicsSettingsManager manager;
    private GraphicsSettingsManager.GraphicsSettingsSnapshot pending;
    private readonly List<Vector2Int> resoluciones = new();

    private float pendingVolume;
    private float pendingSensitivity;

    private bool initialized;
    private bool syncing;
    private bool dirty;

    private static readonly Color Accent =
        new Color(0.38f, 0.78f, 0.48f, 1f);

    private static readonly Color Normal =
        new Color(0.47f, 0.57f, 0.65f, 1f);

    private static readonly Color Warm =
        new Color(0.91f, 0.84f, 0.81f, 1f);

    private static readonly Color Amber =
        new Color(0.91f, 0.69f, 0.37f, 1f);

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();

        if (!initialized)
            return;

        ReloadAppliedValues();
        ShowSection(0);

        // Asegurar que el modal de usuario inicie oculto si cerramos y abrimos config
        if (panelUsuario != null)
            panelUsuario.SetActive(false);
    }

    private void Initialize()
    {
        if (initialized)
            return;

        // El botón USUARIO se conecta primero y por separado
        WireUsuarioButton();

        manager =
            GraphicsSettingsManager.EnsureInstance();

        PrepareOptions();
        WireEvents();

        initialized = true;
    }

    private void PrepareOptions()
    {
        SetOptions(
            modoPantallaDropdown,
            new[]
            {
                "PANTALLA COMPLETA",
                "SIN BORDES",
                "VENTANA"
            }
        );

        SetOptions(
            presetDropdown,
            new[]
            {
                "MUY BAJO",
                "BAJO",
                "NORMAL",
                "ALTO",
                "ULTRA",
                "PERSONALIZADO"
            }
        );

        SetOptions(
            renderScaleDropdown,
            GraphicsSettingsManager.RenderScaleNames
        );

        SetOptions(
            texturasDropdown,
            GraphicsSettingsManager.TextureNames
        );

        SetOptions(
            sombrasDropdown,
            GraphicsSettingsManager.ShadowNames
        );

        SetOptions(
            distanciaSombrasDropdown,
            GraphicsSettingsManager.ShadowDistanceNames
        );

        SetOptions(
            antialiasingDropdown,
            GraphicsSettingsManager.AntiAliasingNames
        );

        SetOptions(
            anisotropicoDropdown,
            GraphicsSettingsManager.AnisotropicNames
        );

        SetOptions(
            lodDropdown,
            GraphicsSettingsManager.LodNames
        );

        SetOptions(
            limiteFpsDropdown,
            GraphicsSettingsManager.FpsLimitNames
        );

        PrepareResolutions();

        if (volumenSlider != null)
        {
            volumenSlider.minValue = 0f;
            volumenSlider.maxValue = 1f;
            volumenSlider.wholeNumbers = false;
        }

        if (sensibilidadSlider != null)
        {
            sensibilidadSlider.minValue = 0.02f;
            sensibilidadSlider.maxValue = 1f;
            sensibilidadSlider.wholeNumbers = false;
        }
    }

    private static void SetOptions(
        TMP_Dropdown dropdown,
        IReadOnlyList<string> options)
    {
        if (dropdown == null)
            return;

        List<string> values =
            new List<string>();

        for (int i = 0; i < options.Count; i++)
            values.Add(options[i]);

        dropdown.ClearOptions();
        dropdown.AddOptions(values);
    }

    private void PrepareResolutions()
    {
        resoluciones.Clear();

        Resolution[] available =
            Screen.resolutions;

        for (int i = 0; i < available.Length; i++)
        {
            Vector2Int r =
                new Vector2Int(
                    available[i].width,
                    available[i].height
                );

            if (!resoluciones.Contains(r))
                resoluciones.Add(r);
        }

        if (resoluciones.Count == 0)
        {
            resoluciones.Add(
                new Vector2Int(
                    Screen.width,
                    Screen.height
                )
            );
        }

        resoluciones.Sort(
            (a, b) =>
                (a.x * a.y).CompareTo(
                    b.x * b.y
                )
        );

        List<string> options =
            new List<string>();

        foreach (Vector2Int r in resoluciones)
            options.Add($"{r.x} x {r.y}");

        resolucionDropdown.ClearOptions();
        resolucionDropdown.AddOptions(options);
    }

    private void WireEvents()
    {
        for (int i = 0; i < botonesSeccion.Length; i++)
        {
            int index = i;

            if (botonesSeccion[i] != null)
            {
                botonesSeccion[i].onClick.AddListener(
                    () => ShowSection(index)
                );
            }
        }

        resolucionDropdown.onValueChanged.AddListener(
            OnResolutionChanged
        );

        modoPantallaDropdown.onValueChanged.AddListener(
            OnScreenModeChanged
        );

        presetDropdown.onValueChanged.AddListener(
            OnPresetChanged
        );

        renderScaleDropdown.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pending.renderScale = value;
                MarkCustomAndDirty();
            }
        );

        texturasDropdown.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pending.textureQuality = value;
                MarkCustomAndDirty();
            }
        );

        sombrasDropdown.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pending.shadowQuality = value;
                MarkCustomAndDirty();
            }
        );

        distanciaSombrasDropdown.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pending.shadowDistance = value;
                MarkCustomAndDirty();
            }
        );

        antialiasingDropdown.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pending.antiAliasing = value;
                MarkCustomAndDirty();
            }
        );

        anisotropicoDropdown.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pending.anisotropic = value;
                MarkCustomAndDirty();
            }
        );

        lodDropdown.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pending.lodQuality = value;
                MarkCustomAndDirty();
            }
        );

        reflejosToggle.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pending.realtimeReflections = value;
                MarkCustomAndDirty();
            }
        );

        particulasToggle.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pending.softParticles = value;
                MarkCustomAndDirty();
            }
        );

        postProcessingToggle.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pending.postProcessing = value;
                MarkCustomAndDirty();
            }
        );

        limiteFpsDropdown.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pending.fpsLimitIndex = value;
                SetDirty();
                UpdateVsyncWarning();
            }
        );

        vSyncToggle.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pending.vSync = value;
                SetDirty();
                UpdateVsyncWarning();
            }
        );

        mostrarFpsToggle.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pending.showFPS = value;
                SetDirty();
            }
        );

        volumenSlider.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pendingVolume = value;
                UpdateVolumeLabel();
                SetDirty();
            }
        );

        sensibilidadSlider.onValueChanged.AddListener(
            value =>
            {
                if (syncing) return;
                pendingSensitivity = value;
                UpdateSensitivityLabel();
                SetDirty();
            }
        );

        aplicarButton.onClick.AddListener(
            ApplyPending
        );

        restaurarButton.onClick.AddListener(
            RestoreRecommendedPending
        );

        volverButton.onClick.AddListener(
            Back
        );
    }

    // ─────────────────────────────────────────────
    // Usuario / Perfil
    // ─────────────────────────────────────────────

    private void WireUsuarioButton()
    {
        if (usuarioButton == null)
        {
            Debug.LogError("[Config] ERROR: El campo 'Usuario Button' NO está asignado en el Inspector de ConfiguracionMenuController.", this);
            return;
        }

        usuarioButton.onClick.RemoveListener(AbrirPanelUsuario);
        usuarioButton.onClick.AddListener(AbrirPanelUsuario);

        Debug.Log($"[Config] Evento de clic en botón USUARIO enlazado. (Interactable: {usuarioButton.interactable}, RaycastTarget: {usuarioButton.targetGraphic?.raycastTarget})", usuarioButton);

        if (cancelarUsuarioButton != null)
        {
            cancelarUsuarioButton.onClick.RemoveListener(CerrarPanelUsuario);
            cancelarUsuarioButton.onClick.AddListener(CerrarPanelUsuario);
        }

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            Debug.LogError("[Config] No se encontró EventSystem en la escena; los botones no recibirán eventos de clic.", this);
        }
    }

    public void AbrirPanelUsuario()
    {
        Debug.Log(">>> [Config] ¡CLICK RECIBIDO en el botón USUARIO! <<<", this);

        // Si existe la instancia del gestor de renombre, usamos su método oficial
        if (CharacterRenameManager.Instance != null)
        {
            CharacterRenameManager.Instance.AbrirPantallaNombre(esPrimerInicio: false);
            return;
        }

        // Si no está la instancia pero está asignado el panel
        if (panelUsuario != null)
        {
            var renameManager = panelUsuario.GetComponentInChildren<CharacterRenameManager>(true);
            if (renameManager != null)
            {
                renameManager.AbrirPantallaNombre(esPrimerInicio: false);
                return;
            }

            // Fallback de emergencia: activar raíz y todos sus hijos directos
            panelUsuario.SetActive(true);
            panelUsuario.transform.SetAsLastSibling();

            CanvasGroup cg = panelUsuario.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }

            for (int i = 0; i < panelUsuario.transform.childCount; i++)
            {
                panelUsuario.transform.GetChild(i).gameObject.SetActive(true);
            }
        }
        else
        {
            Debug.LogError("[Config] ERROR: 'Panel Usuario' es NULL en el Inspector.", this);
        }
    }

    private IEnumerator VerificarPanelUsuario()
    {
        yield return null;

        if (panelUsuario != null && !panelUsuario.activeInHierarchy)
        {
            Debug.LogWarning(
                "[Config] AVISO: El panel de usuario se apagó solo en el siguiente frame. " +
                "Verifica scripts dentro de UsernameFirstRunUI (como GoogleLoginUI) que puedan estar desactivándolo.",
                panelUsuario
            );
        }
    }

    public void CerrarPanelUsuario()
    {
        Debug.Log("[Config] Cerrando panel de usuario.");
        if (panelUsuario != null)
        {
            panelUsuario.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────

    private void ReloadAppliedValues()
    {
        pending =
            manager.CaptureSnapshot();

        pendingVolume =
            ConfiguracionesJuego.ObtenerVolumen();

        pendingSensitivity =
            ConfiguracionesJuego.ObtenerSensibilidad();

        if (controles != null)
            controles.ReloadSaved();

        dirty = false;
        SyncUI();
        SetStatus("SIN CAMBIOS PENDIENTES", Normal);
    }

    private void SyncUI()
    {
        if (pending == null)
            return;

        syncing = true;

        resolucionDropdown.SetValueWithoutNotify(
            FindResolution(
                pending.width,
                pending.height
            )
        );

        modoPantallaDropdown.SetValueWithoutNotify(
            ScreenModeToIndex(
                pending.screenMode
            )
        );

        presetDropdown.SetValueWithoutNotify(
            pending.preset >= 0
                ? pending.preset
                : 5
        );

        renderScaleDropdown.SetValueWithoutNotify(
            pending.renderScale
        );

        texturasDropdown.SetValueWithoutNotify(
            pending.textureQuality
        );

        sombrasDropdown.SetValueWithoutNotify(
            pending.shadowQuality
        );

        distanciaSombrasDropdown.SetValueWithoutNotify(
            pending.shadowDistance
        );

        antialiasingDropdown.SetValueWithoutNotify(
            pending.antiAliasing
        );

        anisotropicoDropdown.SetValueWithoutNotify(
            pending.anisotropic
        );

        lodDropdown.SetValueWithoutNotify(
            pending.lodQuality
        );

        reflejosToggle.SetIsOnWithoutNotify(
            pending.realtimeReflections
        );

        particulasToggle.SetIsOnWithoutNotify(
            pending.softParticles
        );

        postProcessingToggle.SetIsOnWithoutNotify(
            pending.postProcessing
        );

        limiteFpsDropdown.SetValueWithoutNotify(
            pending.fpsLimitIndex
        );

        vSyncToggle.SetIsOnWithoutNotify(
            pending.vSync
        );

        mostrarFpsToggle.SetIsOnWithoutNotify(
            pending.showFPS
        );

        volumenSlider.SetValueWithoutNotify(
            pendingVolume
        );

        sensibilidadSlider.SetValueWithoutNotify(
            pendingSensitivity
        );

        syncing = false;

        UpdatePresetState();
        UpdateVsyncWarning();
        UpdateVolumeLabel();
        UpdateSensitivityLabel();
        UpdateHardware();
    }

    private void OnResolutionChanged(int index)
    {
        if (syncing ||
            index < 0 ||
            index >= resoluciones.Count)
        {
            return;
        }

        pending.width =
            resoluciones[index].x;

        pending.height =
            resoluciones[index].y;

        SetDirty();
    }

    private void OnScreenModeChanged(int index)
    {
        if (syncing)
            return;

        switch (Mathf.Clamp(index, 0, 2))
        {
            case 0:
                pending.screenMode =
                    FullScreenMode.ExclusiveFullScreen;
                break;

            case 1:
                pending.screenMode =
                    FullScreenMode.FullScreenWindow;
                break;

            default:
                pending.screenMode =
                    FullScreenMode.Windowed;
                break;
        }

        SetDirty();
    }

    private void OnPresetChanged(int index)
    {
        if (syncing)
            return;

        if (index >= 5)
            return;

        pending =
            manager.CreatePresetSnapshot(
                index,
                pending
            );

        SetDirty();
        SyncUI();
    }

    private void MarkCustomAndDirty()
    {
        pending.preset = -1;
        SetDirty();
        UpdatePresetState();

        syncing = true;
        presetDropdown.SetValueWithoutNotify(5);
        syncing = false;
    }

    private void SetDirty()
    {
        dirty = true;
        SetStatus("CAMBIOS PENDIENTES // PULSÁ APLICAR", Amber);
    }

    private void ApplyPending()
    {
        if (pending == null)
            return;

        manager.ApplySnapshot(pending);

        ConfiguracionesJuego.GuardarVolumen(
            pendingVolume
        );

        ConfiguracionesJuego.GuardarSensibilidad(
            pendingSensitivity
        );

        if (controles != null)
            controles.Commit();

        ConfiguracionesJuego.AplicarCambios();

        dirty = false;

        SetStatus(
            "CAMBIOS APLICADOS",
            Accent
        );

        pending =
            manager.CaptureSnapshot();

        SyncUI();
    }

    private void RestoreRecommendedPending()
    {
        pending =
            manager.CreateRecommendedSnapshot();

        pendingVolume =
            ConfiguracionesJuego.VolumenPorDefecto;

        pendingSensitivity =
            ConfiguracionesJuego.SensibilidadPorDefecto;

        if (controles != null)
            controles.ResetPendingToDefault();

        SetDirty();
        SyncUI();
    }

    private void Back()
    {
        // Si el modal de usuario quedó abierto, lo cerramos
        if (panelUsuario != null)
            panelUsuario.SetActive(false);

        gameObject.SetActive(false);

        if (menuPrincipalPanel != null)
            menuPrincipalPanel.SetActive(true);
    }

    public void ShowSection(int index)
    {
        if (secciones == null ||
            secciones.Length == 0)
        {
            return;
        }

        index =
            Mathf.Clamp(
                index,
                0,
                secciones.Length - 1
            );

        for (int i = 0; i < secciones.Length; i++)
        {
            if (secciones[i] != null)
                secciones[i].SetActive(i == index);

            if (i < textosBotonesSeccion.Length &&
                textosBotonesSeccion[i] != null)
            {
                textosBotonesSeccion[i].color =
                    i == index
                        ? Warm
                        : Normal;
            }
        }
    }

    private int FindResolution(
        int width,
        int height)
    {
        for (int i = 0; i < resoluciones.Count; i++)
        {
            if (resoluciones[i].x == width &&
                resoluciones[i].y == height)
            {
                return i;
            }
        }

        int best = 0;
        long bestError = long.MaxValue;

        for (int i = 0; i < resoluciones.Count; i++)
        {
            long dx = resoluciones[i].x - width;
            long dy = resoluciones[i].y - height;
            long error = dx * dx + dy * dy;

            if (error < bestError)
            {
                bestError = error;
                best = i;
            }
        }

        return best;
    }

    private static int ScreenModeToIndex(
        FullScreenMode mode)
    {
        if (mode == FullScreenMode.ExclusiveFullScreen)
            return 0;

        if (mode == FullScreenMode.FullScreenWindow ||
            mode == FullScreenMode.MaximizedWindow)
        {
            return 1;
        }

        return 2;
    }

    private void UpdatePresetState()
    {
        if (presetEstadoText == null)
            return;

        if (pending.preset >= 0)
        {
            presetEstadoText.text =
                "PERFIL  //  " +
                GraphicsSettingsManager.PresetNames[
                    pending.preset
                ];

            presetEstadoText.color = Accent;
        }
        else
        {
            presetEstadoText.text =
                "PERFIL  //  PERSONALIZADO";

            presetEstadoText.color = Amber;
        }
    }

    private void UpdateVsyncWarning()
    {
        if (vSyncAdvertencia == null ||
            pending == null)
        {
            return;
        }

        vSyncAdvertencia.text =
            pending.vSync
                ? "V-SYNC ACTIVO // el monitor puede imponer el límite real de FPS."
                : "V-SYNC DESACTIVADO // se usa el límite de FPS seleccionado.";
    }

    private void UpdateVolumeLabel()
    {
        if (volumenValor != null)
        {
            volumenValor.text =
                Mathf.RoundToInt(
                    pendingVolume * 100f
                ) + "%";
        }
    }

    private void UpdateSensitivityLabel()
    {
        if (sensibilidadValor != null)
        {
            sensibilidadValor.text =
                pendingSensitivity.ToString("F2");
        }
    }

    private void UpdateHardware()
    {
        if (hardwareText == null)
            return;

        hardwareText.text =
            "GPU       " +
            SystemInfo.graphicsDeviceName +
            "\nVRAM      " +
            SystemInfo.graphicsMemorySize +
            " MB\nCPU       " +
            SystemInfo.processorType +
            "\nNÚCLEOS   " +
            SystemInfo.processorCount +
            "\nRAM       " +
            SystemInfo.systemMemorySize +
            " MB\nPANTALLA  " +
            Screen.currentResolution.width +
            " x " +
            Screen.currentResolution.height;
    }

    private void SetStatus(
        string text,
        Color color)
    {
        if (estadoCambiosText == null)
            return;

        estadoCambiosText.text = text;
        estadoCambiosText.color = color;
    }
}