using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DeadQuarantine.PvPvE;

/// <summary>Selección local de Supervivencia; nunca pausa el reloj ni la partida de otro jugador.</summary>
[RequireComponent(typeof(SurvivalUpgrades))]
public sealed class SurvivalUpgradeUI : MonoBehaviour
{
    [Header("Ilustraciones: vida, daño, regeneración, movimiento")]
    [SerializeField] Sprite[] illustrations = new Sprite[4];
    [SerializeField] TMP_FontAsset headingFont, bodyFont;
    static SurvivalUpgradeUI local;
    static int closeFrame = -10;
    public static bool BlocksLocalGameplay => local && local.IsOpen;
    public static bool BlocksPauseInput => BlocksLocalGameplay || Time.frameCount - closeFrame <= 2;
    public static bool BlocksMouse => BlocksPauseInput;
    public bool IsOpen => modal && modal.activeSelf;
    SurvivalUpgrades upgrades;
    GameObject canvasRoot, modal, badge;
    TMP_Text points, status, badgeText;
    readonly Button[] cards = new Button[4];
    readonly TMP_Text[] totals = new TMP_Text[4];
    readonly TMP_Text[] benefits = new TMP_Text[4];
    readonly Image[] bars = new Image[4];
    readonly Outline[] borders = new Outline[4];
    int selected = -1, observedRound, requestedSpent;
    bool pending, queuedOpen, acceptedReply;
    float pendingAt;
    SurvivalUpgradeState observed;
    static readonly Color green = new(.25f, .9f, .39f), muted = new(.57f, .66f, .63f), panel = new(.045f, .052f, .049f, .98f);
    static readonly string[] names = { "VIDA MÁXIMA", "DAÑO GENERAL", "REGENERACIÓN DE ESTAMINA", "VELOCIDAD DE MOVIMIENTO" };
    static readonly string[] descriptions = { "Aumenta la vida máxima del personaje.", "Aumenta el daño de todas tus armas.", "Acelera la recuperación de estamina.", "Aumenta la velocidad al caminar y correr." };
    void Awake() { upgrades = GetComponent<SurvivalUpgrades>(); }
    void Update()
    {
        if (!upgrades || !upgrades.IsSpawned || !upgrades.IsOwner) { if (IsOpen) Close(); return; }
        if (!upgrades.Active) { if (IsOpen) Close(); queuedOpen = false; if (canvasRoot) canvasRoot.SetActive(false); return; }
        local = this;
        if (!canvasRoot) Build();
        canvasRoot.SetActive(true);
        var state = upgrades.Progress.Value;
        if (!state.Equals(observed)) { observed = state; Refresh(); }
        CompleteSelectionIfReady();
        if (state.lastRewardRound > observedRound) { observedRound = state.lastRewardRound; queuedOpen = state.points > 0; }
        bool otherMenu = PauseController.LocalPlayerPaused || NPCShopUI.LocalShopOpen;
        bool available = upgrades.CanChoose && state.points > 0;
        if ((!available || otherMenu) && IsOpen) Close();
        if (queuedOpen && available && !otherMenu) { queuedOpen = false; Open(); }
        badge.SetActive(available && !IsOpen && !otherMenu);
        badgeText.text = $"[U]  MEJORAS PENDIENTES  ·  {state.points}";
        if (Keyboard.current != null)
        {
            if (IsOpen && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
            else if (Keyboard.current.uKey.wasPressedThisFrame && available && !otherMenu) { if (IsOpen) Close(); else Open(); }
        }
        if (pending && Time.unscaledTime - pendingAt > 4) { pending = false; Refresh(); status.text = "No se confirmó la elección. Intentá nuevamente."; }
    }
    void LateUpdate() { if (IsOpen) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }
    public void Open()
    {
        if (pending || !upgrades.CanChoose || upgrades.Progress.Value.points < 1 || PauseController.LocalPlayerPaused || NPCShopUI.LocalShopOpen) return;
        if (!canvasRoot) Build();
        local = this; pending = false; selected = -1;
        modal.SetActive(true); Refresh();
        var weapons = GetComponent<WeaponSwitcher>();
        if (weapons && weapons.CurrentWeapon) weapons.CurrentWeapon.ForceStopAiming();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    public void Close()
    {
        if (!IsOpen) return;
        modal.SetActive(false); queuedOpen = false; closeFrame = Time.frameCount;
        if (upgrades && upgrades.IsSpawned && upgrades.Active && upgrades.Rounds && !upgrades.Rounds.GameLostNetwork.Value &&
            !PauseController.LocalPlayerPaused && !NPCShopUI.LocalShopOpen)
        { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }
    public void Select(int index)
    {
        if (!IsOpen || pending || !upgrades.CanChoose || upgrades.Progress.Value.points < 1 || index < 0 || index > 3 ||
            upgrades.Progress.Value.Level(index) >= SurvivalUpgradeState.Limit(index)) return;
        selected = index; requestedSpent = upgrades.Progress.Value.Spent;
        pending = true; acceptedReply = false; pendingAt = Time.unscaledTime;
        Refresh(); upgrades.Choose(index);
    }
    void Reply(bool accepted)
    {
        if (!pending) return;
        acceptedReply = accepted;
        if (!accepted) pending = false;
        CompleteSelectionIfReady();
        Refresh();
        if (!accepted) status.text = "La elección no está disponible. Revisá tus puntos o la fase de la ronda.";
    }
    void CompleteSelectionIfReady()
    {
        // El RPC y el estado replicado pueden llegar en distinto orden.
        if (!pending || !acceptedReply || upgrades.Progress.Value.Spent <= requestedSpent) return;
        pending = false; acceptedReply = false; selected = -1;
        if (upgrades.Progress.Value.points == 0) Close();
        Refresh();
    }
    void Refresh()
    {
        if (!modal) return;
        var state = upgrades.Progress.Value;
        points.text = state.points == 1 ? "1 PUNTO DISPONIBLE" : state.points + " PUNTOS DISPONIBLES";
        for (int i = 0; i < 4; i++)
        {
            bool full = state.Level(i) >= SurvivalUpgradeState.Limit(i);
            benefits[i].text = full ? "AL MÁXIMO" : "+" + SurvivalUpgradeState.Step(i).ToString("0.#").Replace('.', ',') + "%";
            benefits[i].color = full ? muted : green;
            totals[i].text = $"ACTUAL  +{state.Percent(i):0.#}%      MÁX.  +{SurvivalUpgradeState.Step(i) * SurvivalUpgradeState.Limit(i):0}%";
            bars[i].rectTransform.sizeDelta = new(356 * state.Level(i) / (float)SurvivalUpgradeState.Limit(i), 4);
            cards[i].interactable = !pending && !full && state.points > 0;
            cards[i].GetComponent<Image>().color = selected == i ? new Color(.065f, .095f, .075f, 1) : panel;
            borders[i].effectColor = selected == i ? green : new Color(.26f, .33f, .29f, .6f);
        }
        status.text = pending ? "Aplicando mejora…" : "Elegí una tarjeta para aplicar la mejora.  [U] Volver a abrir durante la preparación.";
    }
    RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new(0, 1); rect.pivot = new(0, 1);
        rect.anchoredPosition = new(x, -y); rect.sizeDelta = new(w, h); return rect;
    }
    Image Image(Transform parent, string name, float x, float y, float w, float h, Color color)
    { var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
    TMP_Text Text(Transform parent, string name, string value, float x, float y, float w, float h, int size, Color color, bool title = false)
    {
        var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = title ? headingFont : bodyFont; text.fontSize = size; text.text = value; text.color = color;
        text.raycastTarget = false; text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = true; text.fontSizeMin = size - 3; text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.Normal; return text;
    }
    Button Button(Transform parent, string name, string value, float x, float y, float w, float h)
    {
        var image = Image(parent, name, x, y, w, h, panel); image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        PvPvEUIFactory.StyleButton(button);
        if (!string.IsNullOrEmpty(value)) Text(image.transform, "Label", value, 14, 0, w - 28, h, 23, Color.white, true).alignment = TextAlignmentOptions.Center;
        return button;
    }
    void Build()
    {
        headingFont = headingFont ? headingFont : PvPvEUIFactory.HeadingFont;
        bodyFont = bodyFont ? bodyFont : PvPvEUIFactory.BodyFont;
        if (!bodyFont) bodyFont = TMP_Settings.defaultFontAsset;
        if (!headingFont) headingFont = bodyFont;
        canvasRoot = new GameObject("Survival upgrades", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(transform, false);
        var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 25000;
        var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        modal = new GameObject("Upgrade selection", typeof(RectTransform), typeof(Image)); modal.transform.SetParent(canvasRoot.transform, false);
        var background = modal.GetComponent<Image>(); background.color = new(.018f, .023f, .021f, .94f);
        var full = (RectTransform)modal.transform; full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.offsetMin = full.offsetMax = Vector2.zero;
        var stage = Rect(modal.transform, "Content", 0, 0, 1680, 820); stage.anchorMin = stage.anchorMax = new(.5f, .5f); stage.pivot = new(.5f, .5f); stage.anchoredPosition = Vector2.zero;
        Image(stage, "Header accent", 0, 22, 3, 42, green);
        Text(stage, "Title", "MEJORAS DE SUPERVIVENCIA", 20, 8, 1230, 65, 42, Color.white, true);
        Text(stage, "Round", "Ronda completada", 20, 70, 1230, 30, 19, muted);
        points = Text(stage, "Points", "", 1310, 28, 370, 44, 23, green, true); points.alignment = TextAlignmentOptions.MidlineRight;
        Image(stage, "Header rule", 0, 115, 1680, 1, new(.22f, .29f, .25f));
        for (int i = 0; i < 4; i++)
        {
            int index = i; float x = i * 426;
            var button = cards[i] = Button(stage, "Upgrade " + names[i], "", x, 150, 402, 516);
            borders[i] = button.GetComponent<Outline>(); borders[i].effectDistance = new(1, -1);
            button.onClick.AddListener(() => Select(index));
            var colors = button.colors; colors.highlightedColor = new(1.15f, 1.2f, 1.16f); colors.selectedColor = colors.highlightedColor; colors.pressedColor = new(1.3f, 1.4f, 1.32f); button.colors = colors;
            var artwork = Image(button.transform, "Illustration", 12, 12, 378, 265, Color.white);
            artwork.sprite = i < illustrations.Length ? illustrations[i] : null; artwork.preserveAspect = true;
            artwork.enabled = artwork.sprite;
            Text(button.transform, "Name", names[i], 23, 287, 356, 44, 24, Color.white, true);
            benefits[i] = Text(button.transform, "Benefit", "", 23, 337, 356, 48, 35, green, true);
            Text(button.transform, "Description", descriptions[i], 23, 393, 356, 43, 17, muted);
            Image(button.transform, "Divider", 23, 450, 356, 1, new(.27f, .36f, .31f));
            totals[i] = Text(button.transform, "Progress text", "", 23, 463, 356, 25, 16, Color.white);
            var track = Image(button.transform, "Progress track", 23, 496, 356, 4, new(.14f, .2f, .17f));
            var bar = bars[i] = Image(track.transform, "Progress", 0, 0, 356, 4, green);
        }
        var later = Button(stage, "Later", "VOLVER  [ESC]", 1330, 712, 350, 52); later.onClick.AddListener(Close);
        status = Text(stage, "Hint", "", 0, 714, 1260, 50, 18, muted);
        badge = new GameObject("Pending upgrades", typeof(RectTransform)); badge.transform.SetParent(canvasRoot.transform, false);
        var badgeRect = (RectTransform)badge.transform; badgeRect.anchorMin = badgeRect.anchorMax = new(.5f, 0); badgeRect.pivot = new(.5f, 0); badgeRect.anchoredPosition = new(0, 80); badgeRect.sizeDelta = new(480, 44);
        var open = Button(badge.transform, "Open upgrades", "", 0, 0, 480, 44); open.onClick.AddListener(Open);
        badgeText = Text(open.transform, "Label", "", 12, 0, 456, 44, 20, green, true); badgeText.alignment = TextAlignmentOptions.Center;
        upgrades.SelectionReplied += Reply;
        modal.SetActive(false); Refresh();
    }
    void OnDestroy()
    {
        if (upgrades) upgrades.SelectionReplied -= Reply;
        if (local == this) { Close(); local = null; }
        if (canvasRoot) Destroy(canvasRoot);
    }
}
