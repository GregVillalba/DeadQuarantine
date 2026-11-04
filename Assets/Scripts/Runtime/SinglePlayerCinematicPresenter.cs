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

    // No desactiva el lobbyPanel: WorldToggle necesita que permanezca abierto.
    // Oculta su CanvasGroup y evita que botones invisibles intercepten clics.
    private void ApplySelectorVisibility(bool selecting, bool force = false)
    {
        if (!force && selecting == previousSelectorOpen) return;
        previousSelectorOpen = selecting;
        if (lobbyHudGroup != null)
        {
            lobbyHudGroup.alpha = selecting ? 0f : 1f;
            lobbyHudGroup.interactable = !selecting;
            lobbyHudGroup.blocksRaycasts = !selecting;
        }
        if (nameplate != null && selecting)
            nameplate.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
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
        ApplySelectorVisibility(false, true);
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
            // Solo se muestra el modelo del lobby cuando NO está abierto el
            // selector, que tiene su propio CharacterPreviewStage.
            actor.SetPlayer(!selectorOpen, index);
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
