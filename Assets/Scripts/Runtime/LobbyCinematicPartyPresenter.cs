using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conecta los datos REALES del lobby con la presentación cinematográfica:
/// - Lee los 4 LobbyPlayerSlot (conectado, usuario, variante elegida, icono, nombre del personaje).
/// - Acomoda en el escenario SOLO a los jugadores conectados y le indica a cada uno su variante.
/// - Ubica el nombre del jugador encima de la cabeza (solo el nombre).
/// - Llena el panel compacto "JUGADORES X/4".
/// - Escribe la "SELECCIÓN ACTUAL" desde LobbyGameSettingsRuntime (ConfiguracionPartidaSeleccionada).
/// No cambia ninguna lógica de red, listo/no listo, host ni configuración.
/// </summary>
public class LobbyCinematicPartyPresenter : MonoBehaviour
{
    [Serializable]
    public struct NameplateView
    {
        public RectTransform root;        // fondo + posición (pivot abajo-centro, anclas al centro)
        public TMP_Text nameText;
    }

    [Serializable]
    public struct PartyRowView
    {
        public RectTransform root;
        public Image icon;
        public TMP_Text text;
    }

    [Serializable]
    public class PartyLayout
    {
        public Vector3[] positions;
        public float[] yaws;
    }

    [Header("Mundo")]
    [SerializeField] private Camera lobbyCamera;
    [SerializeField] private LobbyCinematicPlayerDisplay[] actors;
    [Tooltip("Distribución según cuántos jugadores hay conectados: [0] = 1 jugador ... [3] = 4 jugadores.")]
    [SerializeField] private PartyLayout[] layouts;

    [Header("Jugadores (slots reales)")]
    [SerializeField] private LobbyPlayerSlot[] slots;

    [Header("Nombres sobre la cabeza")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private NameplateView[] nameplates;
    [SerializeField] private float plateHorizontalPadding = 22f;
    [SerializeField] private float plateGapAboveHead = 4f;

    [Header("Panel compacto de jugadores")]
    [SerializeField] private RectTransform partyCard;
    [SerializeField] private TMP_Text partyHeader;
    [SerializeField] private PartyRowView[] rows;
    [SerializeField] private float headerHeight = 46f;
    [SerializeField] private float rowHeight = 52f;
    [SerializeField] private float cardBottomPadding = 8f;
    [SerializeField] private bool uppercaseCharacterNames = true;

    [Header("Selección actual")]
    [SerializeField] private TMP_Text modeDifficultyText;
    [SerializeField] private TMP_Text mapText;

    [Header("Permisos del anfitrión")]
    [SerializeField] private Transform configureMatchButton;

    // El botón cinematográfico es el botón REAL del lobby. En runtime lo conectamos
    // también al flujo real del MultiplayerLobbyController para garantizar que el
    // LobbyGameSettingsRuntime se inicialice antes de abrir el popup.
    private Button configureMatchButtonComponent;
    private MultiplayerLobbyController multiplayerLobbyController;
    private bool configureRuntimeListenerBound;

    // Caché para no reescribir textos todos los frames.
    private string lastModeDifficulty;
    private string lastMap;
    private string lastHeader;
    private string[] lastPlateNames;
    private string[] lastRowTexts;
    private int[] connectedSlots = new int[4];

    public void ConfigureWorld(Camera camera, LobbyCinematicPlayerDisplay[] newActors, PartyLayout[] newLayouts)
    {
        lobbyCamera = camera;
        actors = newActors;
        layouts = newLayouts;
    }

    public void ConfigureUI(
        RectTransform newPanelRect,
        LobbyPlayerSlot[] newSlots,
        NameplateView[] newNameplates,
        RectTransform newPartyCard,
        TMP_Text newPartyHeader,
        PartyRowView[] newRows,
        TMP_Text newModeDifficultyText,
        TMP_Text newMapText)
    {
        panelRect = newPanelRect;
        slots = newSlots;
        nameplates = newNameplates;
        partyCard = newPartyCard;
        partyHeader = newPartyHeader;
        rows = newRows;
        modeDifficultyText = newModeDifficultyText;
        mapText = newMapText;

        // El builder reubica el botón REAL dentro de salaEsperaPanel. No se crea
        // ningún botón nuevo y sus listeners originales permanecen intactos.
        configureMatchButton = FindDeep(panelRect, "Btn_ConfigurarPartida");
        configureMatchButtonComponent = configureMatchButton != null
            ? configureMatchButton.GetComponent<Button>()
            : null;
        configureRuntimeListenerBound = false;
    }

    /// <summary>
    /// Distribuciones por defecto. Se centran un poco a la izquierda para dejar libre el panel de la derecha
    /// y se escalonan en profundidad para que no se tapen.
    /// </summary>
    public static PartyLayout[] CreateDefaultLayouts(float groundY, float baseZ, float centerX)
    {
        PartyLayout[] result = new PartyLayout[4];
        float[] spacing = { 0f, 2.4f, 2.25f, 2.05f };

        for (int count = 1; count <= 4; count++)
        {
            PartyLayout layout = new PartyLayout();
            layout.positions = new Vector3[count];
            layout.yaws = new float[count];

            for (int k = 0; k < count; k++)
            {
                float offset = k - (count - 1) * 0.5f;
                float x = centerX + offset * spacing[count - 1];

                // Los de afuera un poco más atrás, los del medio un poco más adelante.
                float z = baseZ;
                if (count >= 3)
                    z += Mathf.Abs(offset) * 0.45f - 0.2f;

                layout.positions[k] = new Vector3(x, groundY, z);
                layout.yaws[k] = 180f + offset * 7f;   // miran a cámara, apenas hacia el centro
            }

            result[count - 1] = layout;
        }

        return result;
    }

    private void OnEnable()
    {
        lastModeDifficulty = null;
        lastMap = null;
        lastHeader = null;
        lastPlateNames = null;
        lastRowTexts = null;
    }

    private void OnDisable()
    {
        if (configureRuntimeListenerBound && configureMatchButtonComponent != null)
            configureMatchButtonComponent.onClick.RemoveListener(OpenMatchConfiguration);

        configureRuntimeListenerBound = false;
    }

    private void LateUpdate()
    {
        UpdateParty();
        UpdateConfigureButtonVisibility();
        UpdateSelection();
    }

    // =========================================================
    // JUGADORES
    // =========================================================

    private void UpdateParty()
    {
        int slotCount = slots != null ? slots.Length : 0;
        int connected = 0;

        for (int i = 0; i < slotCount && connected < connectedSlots.Length; i++)
        {
            if (slots[i] != null && slots[i].IsConnected)
                connectedSlots[connected++] = i;
        }

        PartyLayout layout = GetLayout(connected);

        EnsureCaches(slotCount);

        for (int i = 0; i < slotCount; i++)
        {
            LobbyCinematicPlayerDisplay actor = actors != null && i < actors.Length ? actors[i] : null;

            int order = -1;
            for (int k = 0; k < connected; k++)
            {
                if (connectedSlots[k] == i)
                {
                    order = k;
                    break;
                }
            }

            if (order < 0)
            {
                if (actor != null)
                    actor.SetPlayer(false, 0);

                HidePlate(i);
                continue;
            }

            LobbyPlayerSlot slot = slots[i];

            if (actor != null)
            {
                if (layout != null && layout.positions != null && order < layout.positions.Length)
                {
                    float yaw = layout.yaws != null && order < layout.yaws.Length ? layout.yaws[order] : 180f;
                    actor.SetPose(layout.positions[order], yaw);
                }

                actor.SetPlayer(true, slot.CharacterIndex);
            }

            UpdatePlate(i, actor, GetDisplayName(slot, i));
            UpdateReadyBadge(i, slot.IsReady);
        }

        UpdatePartyCard(connected);
    }

    private PartyLayout GetLayout(int connected)
    {
        if (layouts == null || connected <= 0)
            return null;

        int index = Mathf.Clamp(connected, 1, layouts.Length) - 1;
        return layouts[index];
    }

    private static string GetDisplayName(LobbyPlayerSlot slot, int slotIndex)
    {
        if (slot != null && !string.IsNullOrWhiteSpace(slot.Username))
            return slot.Username;

        return "JUGADOR " + (slotIndex + 1);
    }

    private void EnsureCaches(int slotCount)
    {
        if (lastPlateNames == null || lastPlateNames.Length != slotCount)
            lastPlateNames = new string[slotCount];

        int rowCount = rows != null ? rows.Length : 0;

        if (lastRowTexts == null || lastRowTexts.Length != rowCount)
            lastRowTexts = new string[rowCount];
    }

    // =========================================================
    // NOMBRE ENCIMA DE LA CABEZA
    // =========================================================

    private void UpdatePlate(int slotIndex, LobbyCinematicPlayerDisplay actor, string playerName)
    {
        if (nameplates == null || slotIndex >= nameplates.Length)
            return;

        NameplateView plate = nameplates[slotIndex];

        if (plate.root == null || lobbyCamera == null || actor == null)
        {
            HidePlate(slotIndex);
            return;
        }

        Vector3 head;

        if (!actor.TryGetHeadWorldPosition(out head))
        {
            HidePlate(slotIndex);
            return;
        }

        Vector3 screen = lobbyCamera.WorldToScreenPoint(head);

        if (screen.z <= 0f)
        {
            HidePlate(slotIndex);
            return;
        }

        RectTransform parent = plate.root.parent as RectTransform;

        if (parent == null)
        {
            HidePlate(slotIndex);
            return;
        }

        Canvas canvas = parent.GetComponentInParent<Canvas>();
        Camera uiCamera = null;

        if (canvas != null)
        {
            canvas = canvas.rootCanvas;

            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                uiCamera = canvas.worldCamera;
        }

        Vector2 local;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out local))
        {
            HidePlate(slotIndex);
            return;
        }

        if (plate.nameText != null && lastPlateNames[slotIndex] != playerName)
        {
            lastPlateNames[slotIndex] = playerName;
            plate.nameText.text = playerName;

            float width = plate.nameText.GetPreferredValues(playerName).x + plateHorizontalPadding * 2f;
            plate.root.sizeDelta = new Vector2(Mathf.Max(90f, width), plate.root.sizeDelta.y);
        }

        // Anclas al centro del padre: la posición es relativa al centro de su rectángulo.
        plate.root.anchoredPosition = local - parent.rect.center + new Vector2(0f, plateGapAboveHead);

        if (!plate.root.gameObject.activeSelf)
            plate.root.gameObject.SetActive(true);
    }

    private void HidePlate(int slotIndex)
    {
        if (nameplates == null || slotIndex >= nameplates.Length)
            return;

        RectTransform root = nameplates[slotIndex].root;

        if (root != null && root.gameObject.activeSelf)
            root.gameObject.SetActive(false);

        if (lastPlateNames != null && slotIndex < lastPlateNames.Length)
            lastPlateNames[slotIndex] = null;

        UpdateReadyBadge(slotIndex, false);
    }

    private void UpdateReadyBadge(int slotIndex, bool ready)
    {
        if (nameplates == null || slotIndex < 0 || slotIndex >= nameplates.Length)
            return;

        RectTransform root = nameplates[slotIndex].root;
        if (root == null)
            return;

        Transform badge = root.Find("ReadyBadge");
        if (badge != null && badge.gameObject.activeSelf != ready)
            badge.gameObject.SetActive(ready);
    }

    // =========================================================
    // PANEL COMPACTO "JUGADORES X/4"
    // =========================================================

    private void UpdatePartyCard(int connected)
    {
        int rowCount = rows != null ? rows.Length : 0;
        int maxPlayers = slots != null ? slots.Length : 4;

        for (int r = 0; r < rowCount; r++)
        {
            PartyRowView row = rows[r];

            if (row.root == null)
                continue;

            if (r >= connected)
            {
                if (row.root.gameObject.activeSelf)
                    row.root.gameObject.SetActive(false);

                lastRowTexts[r] = null;
                continue;
            }

            LobbyPlayerSlot slot = slots[connectedSlots[r]];

            if (!row.root.gameObject.activeSelf)
                row.root.gameObject.SetActive(true);

            string characterName = slot.CharacterDisplayName;

            if (uppercaseCharacterNames)
                characterName = characterName.ToUpperInvariant();

            string playerName = GetDisplayName(slot, connectedSlots[r]);
            string line = characterName.Length > 0 ? playerName + " - " + characterName : playerName;

            // Segundo indicio, estilo lista de grupo: todos ven quién ya confirmó.
            if (slot.IsReady)
                line += "  <color=#24E87D><b>LISTO</b></color>";

            if (row.text != null && lastRowTexts[r] != line)
            {
                lastRowTexts[r] = line;
                row.text.text = line;
            }

            if (row.icon != null)
            {
                Sprite sprite = slot.CharacterSprite;

                if (row.icon.sprite != sprite)
                    row.icon.sprite = sprite;

                row.icon.enabled = sprite != null;
            }
        }

        string header = "JUGADORES " + connected + "/" + maxPlayers;

        if (partyHeader != null && lastHeader != header)
        {
            lastHeader = header;
            partyHeader.text = header;
        }

        if (partyCard != null)
        {
            float height = headerHeight + connected * rowHeight + cardBottomPadding;

            if (!Mathf.Approximately(partyCard.sizeDelta.y, height))
                partyCard.sizeDelta = new Vector2(partyCard.sizeDelta.x, height);
        }
    }

    // =========================================================
    // CONFIGURAR PARTIDA: SOLO HOST
    // =========================================================

    private void UpdateConfigureButtonVisibility()
    {
        if (configureMatchButton == null)
        {
            configureMatchButton = FindDeep(panelRect, "Btn_ConfigurarPartida");
            configureMatchButtonComponent = configureMatchButton != null
                ? configureMatchButton.GetComponent<Button>()
                : null;
        }

        if (configureMatchButton == null)
            return;

        EnsureConfigureButtonRuntimeBinding();

        bool isHost = multiplayerLobbyController != null && multiplayerLobbyController.EsAnfitrion;

        if (configureMatchButton.gameObject.activeSelf != isHost)
            configureMatchButton.gameObject.SetActive(isHost);
    }

    private void EnsureConfigureButtonRuntimeBinding()
    {
        if (configureRuntimeListenerBound)
            return;

        if (configureMatchButtonComponent == null && configureMatchButton != null)
            configureMatchButtonComponent = configureMatchButton.GetComponent<Button>();

        if (configureMatchButtonComponent == null)
            return;

        if (multiplayerLobbyController == null)
        {
            MultiplayerLobbyController[] controllers =
                FindObjectsByType<MultiplayerLobbyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (controllers != null && controllers.Length > 0)
                multiplayerLobbyController = controllers[0];
        }

        if (multiplayerLobbyController == null)
            return;

        // No reemplaza los listeners originales del botón: agrega únicamente la llamada
        // al flujo real que inicializa LobbyGameSettingsRuntime y abre el popup.
        configureMatchButtonComponent.onClick.AddListener(OpenMatchConfiguration);
        configureRuntimeListenerBound = true;
    }

    private void OpenMatchConfiguration()
    {
        if (multiplayerLobbyController == null)
            return;

        multiplayerLobbyController.OnClick_IrAConfigurarPartida();
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null)
            return null;

        if (root.name == name)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null)
                return found;
        }

        return null;
    }

    // =========================================================
    // SELECCIÓN ACTUAL (datos reales del sistema de configuración)
    // =========================================================

    private void UpdateSelection()
    {
        bool inRoom = multiplayerLobbyController == null || multiplayerLobbyController.EnSala;
        if (modeDifficultyText != null)
        {
            string value = inRoom ? LobbyGameSettingsRuntime.ObtenerResumenModoDificultad() : "SIN CONFIGURAR";

            if (value != lastModeDifficulty)
            {
                lastModeDifficulty = value;
                modeDifficultyText.text = value;
            }
        }

        if (mapText != null)
        {
            string value = inRoom ? LobbyGameSettingsRuntime.ObtenerNombreMapaActual() : "-";

            if (value != lastMap)
            {
                lastMap = value;
                mapText.text = value;
            }
        }
    }
}