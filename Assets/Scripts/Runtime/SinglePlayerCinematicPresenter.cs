using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presentacion local del lobby singleplayer. Reutiliza el mismo actor visual
/// que el multiplayer sin crear una sesion de red ni modificar al Player real.
/// </summary>
public sealed class SinglePlayerCinematicPresenter : MonoBehaviour
{
    [SerializeField] private LobbyCinematicPlayerDisplay actor;
    [SerializeField] private Camera lobbyCamera;
    [SerializeField] private RectTransform panel;
    [SerializeField] private RectTransform nameplate;
    [SerializeField] private TMP_Text nameplateText;
    [SerializeField] private TMP_Text userText;
    [SerializeField] private TMP_Text characterText;
    [SerializeField] private TMP_Text selectionText;
    [SerializeField] private TMP_Text mapText;
    [SerializeField] private Image characterIcon;
    [SerializeField] private TMP_Text partyRowText;
    [SerializeField] private CanvasGroup lobbyHudGroup;
    [SerializeField, Range(0f, 1f)] private float selectorBackgroundOpacity = 0.94f;
    [SerializeField] private Color selectorBackgroundColor = new Color(0.03f, 0.04f, 0.05f, 0.94f);
    [SerializeField] private CharacterCatalog catalog;
    [SerializeField] private Vector3 playerPosition = new Vector3(-1.2f, -1.26f, -65.6f);
    [SerializeField] private float playerYaw = 180f;
    [SerializeField] private float platePadding = 12f;

    private int previousIndex = -1;
    private string previousUsername;
    private string previousMode;
    private string previousMap;
    private bool previousSelectorOpen;
    [SerializeField] private GameObject fallbackSelector;

    // Ambientacion propia de NetworkLobby, restaurada al volver al menu principal.
    [SerializeField] private bool hasLightingProfile;
    [SerializeField] private Material lobbySkybox;
    [SerializeField] private UnityEngine.Rendering.AmbientMode lobbyAmbientMode;
    [SerializeField] private Color lobbyAmbientLight, lobbyAmbientSky, lobbyAmbientEquator, lobbyAmbientGround;
    [SerializeField] private float lobbyAmbientIntensity, lobbyReflectionIntensity;
    [SerializeField] private bool lobbyFog;
    [SerializeField] private FogMode lobbyFogMode;
    [SerializeField] private Color lobbyFogColor;
    [SerializeField] private float lobbyFogDensity, lobbyFogStart, lobbyFogEnd;

    private bool lightingApplied;
    private Material oldSkybox;
    private UnityEngine.Rendering.AmbientMode oldAmbientMode;
    private Color oldAmbientLight, oldAmbientSky, oldAmbientEquator, oldAmbientGround;
    private float oldAmbientIntensity, oldReflectionIntensity;
    private bool oldFog;
    private FogMode oldFogMode;
    private Color oldFogColor;
    private float oldFogDensity, oldFogStart, oldFogEnd;

    public void Configure(LobbyCinematicPlayerDisplay display, Camera camera,
        RectTransform panelRect, RectTransform plate, TMP_Text plateName,
        TMP_Text username, TMP_Text character, TMP_Text selection,
        TMP_Text map, Image icon, CharacterCatalog characterCatalog,
        Vector3 position, float yaw)
    {
        actor = display;
        lobbyCamera = camera;
        panel = panelRect;
        nameplate = plate;
        nameplateText = plateName;
        userText = username;
        characterText = character;
        selectionText = selection;
        mapText = map;
        characterIcon = icon;
        catalog = characterCatalog;
        playerPosition = position;
        playerYaw = yaw;
    }

    public void ConfigureSelector(GameObject selector)
    {
        // Selector original (CharacterSelectCanvas) o selector local de respaldo.
        fallbackSelector = selector;
        ConfigureSelectorBackground();
    }

    public void ConfigureSelectorBackgroundColor(Color background)
    {
        selectorBackgroundColor = background;
        selectorBackgroundOpacity = background.a;
        ConfigureSelectorBackground();
    }

    public void ConfigureLighting(Material skybox, UnityEngine.Rendering.AmbientMode ambientMode,
        Color ambientLight, Color ambientSky, Color ambientEquator, Color ambientGround,
        float ambientIntensity, float reflectionIntensity,
        bool fog, FogMode fogMode, Color fogColor, float fogDensity, float fogStart, float fogEnd)
    {
        hasLightingProfile = true;
        lobbySkybox = skybox;
        lobbyAmbientMode = ambientMode;
        lobbyAmbientLight = ambientLight;
        lobbyAmbientSky = ambientSky;
        lobbyAmbientEquator = ambientEquator;
        lobbyAmbientGround = ambientGround;
        lobbyAmbientIntensity = ambientIntensity;
        lobbyReflectionIntensity = reflectionIntensity;
        lobbyFog = fog;
        lobbyFogMode = fogMode;
        lobbyFogColor = fogColor;
        lobbyFogDensity = fogDensity;
        lobbyFogStart = fogStart;
        lobbyFogEnd = fogEnd;
    }

    private void ApplyLobbyLighting()
    {
        if (!hasLightingProfile || lightingApplied) return;
        oldSkybox = RenderSettings.skybox;
        oldAmbientMode = RenderSettings.ambientMode;
        oldAmbientLight = RenderSettings.ambientLight;
        oldAmbientSky = RenderSettings.ambientSkyColor;
        oldAmbientEquator = RenderSettings.ambientEquatorColor;
        oldAmbientGround = RenderSettings.ambientGroundColor;
        oldAmbientIntensity = RenderSettings.ambientIntensity;
        oldReflectionIntensity = RenderSettings.reflectionIntensity;
        oldFog = RenderSettings.fog;
        oldFogMode = RenderSettings.fogMode;
        oldFogColor = RenderSettings.fogColor;
        oldFogDensity = RenderSettings.fogDensity;
        oldFogStart = RenderSettings.fogStartDistance;
        oldFogEnd = RenderSettings.fogEndDistance;
        lightingApplied = true;
        RenderSettings.skybox = lobbySkybox;
        RenderSettings.ambientMode = lobbyAmbientMode;
        RenderSettings.ambientLight = lobbyAmbientLight;
        RenderSettings.ambientSkyColor = lobbyAmbientSky;
        RenderSettings.ambientEquatorColor = lobbyAmbientEquator;
        RenderSettings.ambientGroundColor = lobbyAmbientGround;
        RenderSettings.ambientIntensity = lobbyAmbientIntensity;
        RenderSettings.reflectionIntensity = lobbyReflectionIntensity;
        RenderSettings.fog = lobbyFog;
        RenderSettings.fogMode = lobbyFogMode;
        RenderSettings.fogColor = lobbyFogColor;
        RenderSettings.fogDensity = lobbyFogDensity;
        RenderSettings.fogStartDistance = lobbyFogStart;
        RenderSettings.fogEndDistance = lobbyFogEnd;
    }

    private void RestoreLobbyLighting()
    {
        if (!lightingApplied) return;
        RenderSettings.skybox = oldSkybox;
        RenderSettings.ambientMode = oldAmbientMode;
        RenderSettings.ambientLight = oldAmbientLight;
        RenderSettings.ambientSkyColor = oldAmbientSky;
        RenderSettings.ambientEquatorColor = oldAmbientEquator;
        RenderSettings.ambientGroundColor = oldAmbientGround;
        RenderSettings.ambientIntensity = oldAmbientIntensity;
        RenderSettings.reflectionIntensity = oldReflectionIntensity;
        RenderSettings.fog = oldFog;
        RenderSettings.fogMode = oldFogMode;
        RenderSettings.fogColor = oldFogColor;
        RenderSettings.fogDensity = oldFogDensity;
        RenderSettings.fogStartDistance = oldFogStart;
        RenderSettings.fogEndDistance = oldFogEnd;
        lightingApplied = false;
    }

    public void ConfigurePartyRow(TMP_Text row)
    {
        partyRowText = row;
    }

    public void ConfigureHudGroup(CanvasGroup group)
    {
        lobbyHudGroup = group;
        ApplySelectorVisibility(false, true);
    }

    // El selector se muestra sobre el lobby. No apagamos el mundo ni el HUD;
    // solo bloqueamos sus clics para que interactues con el selector.
    private void ApplySelectorVisibility(bool selecting, bool force = false)
    {
        if (!force && selecting == previousSelectorOpen) return;
        previousSelectorOpen = selecting;
        if (lobbyHudGroup != null)
        {
            lobbyHudGroup.alpha = 1f;
            lobbyHudGroup.interactable = !selecting;
            lobbyHudGroup.blocksRaycasts = !selecting;
        }
        if (nameplate != null && selecting)
            nameplate.gameObject.SetActive(false);
    }

    private void ConfigureSelectorBackground()
    {
        if (fallbackSelector == null) return;

        // Misma sombra del CharacterSelectCanvas multiplayer. El mundo sigue activo,
        // solo queda oscurecido, como se ve en el multiplayer.
        Transform background = fallbackSelector.transform.Find("Fondo");
        Image image = background != null
            ? background.GetComponent<Image>()
            : fallbackSelector.GetComponent<Image>();
        if (image == null) return;

        Color color = selectorBackgroundColor;
        color.a = selectorBackgroundOpacity;
        image.color = color;
    }

    private void OnDisable()
    {
        RestoreLobbyLighting();
        ApplySelectorVisibility(false, true);
    }

    public void SelectCharacter(int index)
    {
        CharacterSelection.SelectedIndex = index;
        if (fallbackSelector != null) fallbackSelector.SetActive(false);
    }

    public void RotateFromDrag(BaseEventData eventData)
    {
        PointerEventData pointer = eventData as PointerEventData;
        if (pointer != null) playerYaw -= pointer.delta.x * 0.35f;
    }

    private void OnEnable()
    {
        previousIndex = -1;
        previousUsername = null;
        previousMode = null;
        previousMap = null;
        previousSelectorOpen = false;
        ConfigureSelectorBackground();
        ApplySelectorVisibility(false, true);
        ApplyLobbyLighting();
    }

    private void Update()
    {
        bool selectorOpen = fallbackSelector != null && fallbackSelector.activeInHierarchy;
        ApplySelectorVisibility(selectorOpen);

        int index = CharacterSelection.SelectedIndex;
        if (catalog != null)
            index = catalog.ResolveIndex(index);

        if (actor != null)
        {
            actor.SetPose(playerPosition, playerYaw);
            // El personaje del lobby permanece visible en el mundo 3D,
            // detras del selector de personaje.
            actor.SetPlayer(true, index);
        }

        string username = PlayerIdentity.GetUsername();
        if (string.IsNullOrWhiteSpace(username))
            username = "JUGADOR";

        if (index != previousIndex || username != previousUsername)
        {
            previousIndex = index;
            previousUsername = username;
            if (nameplateText != null) nameplateText.text = username;
            if (userText != null) userText.text = username;
            string partyCharacterName = string.Empty;

            if (catalog != null)
            {
                CharacterCatalog.Variant variant = catalog.GetVariant(index);
                if (variant != null)
                {
                    CharacterCatalog.ClassData type = catalog.GetClassData(variant.type);
                    string role = type != null && !string.IsNullOrWhiteSpace(type.displayName)
                        ? type.displayName : variant.type.ToString();
                    string personalName = catalog.GetCharacterNameForVariant(index);
                    partyCharacterName = string.IsNullOrWhiteSpace(personalName) ? role : personalName;
                    if (characterText != null)
                        characterText.text = string.IsNullOrWhiteSpace(personalName)
                            ? role.ToUpperInvariant()
                            : role.ToUpperInvariant() + " // " + personalName.ToUpperInvariant();
                    if (characterIcon != null)
                    {
                        Sprite sprite = catalog.GetIcon(variant.type, variant.gender);
                        characterIcon.sprite = sprite;
                        characterIcon.enabled = sprite != null;
                        characterIcon.preserveAspect = true;
                        characterIcon.color = Color.white;
                    }
                }
            }
            // Misma fila que en LobbyCinematicPartyPresenter del multiplayer.
            if (partyRowText != null)
                partyRowText.text = string.IsNullOrWhiteSpace(partyCharacterName)
                    ? username : username + " - " + partyCharacterName.ToUpperInvariant();
        }

        string mode = LobbyGameSettingsRuntime.ObtenerResumenModoDificultad();
        if (mode != previousMode)
        {
            previousMode = mode;
            if (selectionText != null) selectionText.text = mode;
        }
        string mapName = LobbyGameSettingsRuntime.ObtenerNombreMapaActual();
        if (mapName != previousMap)
        {
            previousMap = mapName;
            if (mapText != null) mapText.text = mapName;
        }
    }

    private void LateUpdate()
    {
        if (fallbackSelector != null && fallbackSelector.activeInHierarchy)
        {
            if (nameplate != null) nameplate.gameObject.SetActive(false);
            return;
        }
        if (nameplate == null || lobbyCamera == null || panel == null || actor == null)
            return;

        Vector3 head;
        if (!actor.TryGetHeadWorldPosition(out head))
        {
            if (nameplate.gameObject.activeSelf) nameplate.gameObject.SetActive(false);
            return;
        }

        Vector3 screen = lobbyCamera.WorldToScreenPoint(head);
        if (screen.z <= 0f)
        {
            if (nameplate.gameObject.activeSelf) nameplate.gameObject.SetActive(false);
            return;
        }

        Canvas canvas = panel.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, screen, uiCamera, out local))
            return;

        if (!nameplate.gameObject.activeSelf) nameplate.gameObject.SetActive(true);
        nameplate.anchoredPosition = local + Vector2.up * platePadding;
    }
}
