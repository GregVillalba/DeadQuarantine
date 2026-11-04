using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una columna del lobby (JUGADOR N). Maneja la tarjeta antigua (nombre, rol, personaje, conexión, listo)
/// y además EXPONE los datos reales del jugador para el lobby cinematográfico:
/// conectado, local, host, listo, índice de variante, nombre de usuario, nombre del personaje e icono.
/// Lo maneja MultiplayerLobbyController (Configure una vez + SetPlayer / SetEmpty).
/// Los campos serializados NO cambiaron: las referencias de la escena siguen funcionando.
/// </summary>
public class LobbyPlayerSlot : MonoBehaviour
{
    [SerializeField] private int slotIndex;

    [Header("Textos")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text connectionText;
    [SerializeField] private TMP_Text readyText;
    [SerializeField] private Image connectionIcon;
    [SerializeField] private Image readyIcon;

    [Header("Personaje elegido")]
    [SerializeField] private Image characterIcon;
    [SerializeField] private TMP_Text characterNameText;   // EXPLORADOR
    [SerializeField] private TMP_Text characterText;       // LIGERO - HOMBRE

    [Header("Jugador local")]
    [Tooltip("Marca que se enciende solo en tu propia columna.")]
    [SerializeField] private GameObject localHighlight;
    [Tooltip("Zona sobre el modelo (con CharacterPreviewDrag): solo el jugador local puede girar su modelo.")]
    [SerializeField] private GameObject dragZone;

    private CharacterCatalog catalog;
    private CharacterPreviewStage stage;
    private Color colorRojo;
    private Sprite iconoVerde, iconoRojo;

    // Para no reescribir textos cada frame.
    private bool hasLast;
    private bool lastConnected, lastLocal, lastHost, lastReady;
    private int lastIndex = -2;
    private int resolvedIndex = 0;
    private string lastUsername = string.Empty;

    // Datos del personaje elegido, guardados aparte de la UI (la UI vieja puede estar oculta).
    private string characterDisplayName = string.Empty;
    private Sprite characterSprite;

    // =========================================================
    // DATOS PÚBLICOS (solo lectura)
    // =========================================================

    public int SlotIndex => slotIndex;

    /// <summary>true cuando hay un jugador real en este slot.</summary>
    public bool IsConnected => hasLast && lastConnected;

    public bool IsLocal => IsConnected && lastLocal;
    public bool IsHost => IsConnected && lastHost;
    public bool IsReady => IsConnected && lastReady;

    /// <summary>Número de variante del catálogo elegido por ESTE jugador (el local usa su selección actual; el remoto, el publicado en la sesión).</summary>
    public int CharacterIndex => resolvedIndex;

    /// <summary>Nombre de usuario publicado por el jugador. Vacío si todavía no llegó.</summary>
    public string Username => lastUsername ?? string.Empty;

    /// <summary>Nombre de la clase del personaje elegido, tal cual está en el catálogo (sin pasar a mayúsculas).</summary>
    public string CharacterDisplayName => characterDisplayName ?? string.Empty;

    /// <summary>Icono real de la variante elegida (puede ser null si el catálogo no tiene uno).</summary>
    public Sprite CharacterSprite => characterSprite;

    // =========================================================

    public void Configure(CharacterCatalog catalog, CharacterPreviewStage stage, Color colorRojo, Sprite iconoVerde, Sprite iconoRojo)
    {
        this.catalog = catalog;
        this.stage = stage;
        this.colorRojo = colorRojo;
        this.iconoVerde = iconoVerde;
        this.iconoRojo = iconoRojo;
        hasLast = false;
    }

    public void SetEmpty()
    {
        if (hasLast && !lastConnected)
            return;

        hasLast = true;
        lastConnected = false;
        lastLocal = false;
        lastHost = false;
        lastReady = false;
        lastIndex = -2;
        resolvedIndex = 0;
        lastUsername = string.Empty;
        characterDisplayName = string.Empty;
        characterSprite = null;

        if (stage != null) stage.Clear(slotIndex);

        if (titleText != null) titleText.text = "JUGADOR " + (slotIndex + 1);
        if (roleText != null) roleText.text = "[ ESPERANDO JUGADOR ]";

        SetLine(connectionText, connectionIcon, false, "CONECTADO", "DESCONECTADO");
        SetLine(readyText, readyIcon, false, "ESTADO: LISTO", "ESTADO: NO LISTO");

        SetCharacterVisible(false);

        if (localHighlight != null) localHighlight.SetActive(false);
        if (dragZone != null) dragZone.SetActive(false);
    }

    /// <param name="remoteIndex">Personaje publicado por ese jugador en la sesión. Para el local se usa la selección actual.</param>
    /// <param name="username">Nombre de usuario publicado por el jugador (opcional).</param>
    public void SetPlayer(bool isLocal, bool isHost, bool ready, int remoteIndex, string username = "")
    {
        int index = isLocal ? CharacterSelection.SelectedIndex : remoteIndex;
        string cleanName = string.IsNullOrWhiteSpace(username) ? string.Empty : username.Trim();

        if (stage != null)
            stage.Show(slotIndex, index);

        if (hasLast && lastConnected && lastLocal == isLocal && lastHost == isHost && lastReady == ready
            && lastIndex == index && lastUsername == cleanName)
            return;

        hasLast = true;
        lastConnected = true;
        lastLocal = isLocal;
        lastHost = isHost;
        lastReady = ready;
        lastIndex = index;
        lastUsername = cleanName;

        if (titleText != null)
            titleText.text = cleanName.Length > 0 ? cleanName : "JUGADOR " + (slotIndex + 1);

        if (roleText != null)
            roleText.text = "[ " + (isHost ? "ANFITRION" : "CO - OP / REMOTO") + (isLocal ? " / TU" : "") + " ]";

        SetLine(connectionText, connectionIcon, true, "CONECTADO", "DESCONECTADO");
        SetLine(readyText, readyIcon, ready, "ESTADO: LISTO", "ESTADO: NO LISTO");

        ShowCharacter(index);

        if (localHighlight != null) localHighlight.SetActive(isLocal);
        if (dragZone != null) dragZone.SetActive(isLocal);
    }

    private void ShowCharacter(int index)
    {
        characterDisplayName = string.Empty;
        characterSprite = null;
        resolvedIndex = Mathf.Max(0, index);

        if (catalog == null)
        {
            SetCharacterVisible(false);
            return;
        }

        index = catalog.ResolveIndex(index);
        CharacterCatalog.Variant v = catalog.GetVariant(index);

        if (v == null)
        {
            SetCharacterVisible(false);
            return;
        }

        // Si el catálogo resolvió a otra variante jugable, ese es el modelo que realmente se muestra.
        resolvedIndex = index;

        SetCharacterVisible(true);

        CharacterCatalog.ClassData data = catalog.GetClassData(v.type);

        characterDisplayName = data != null && !string.IsNullOrEmpty(data.displayName)
            ? data.displayName
            : v.type.ToString();

        characterSprite = catalog.GetIcon(v.type, v.gender);

        if (characterNameText != null)
            characterNameText.text = characterDisplayName.ToUpperInvariant();

        if (characterText != null)
        {
            bool genderChoice = catalog.HasGenderChoice(v.type);
            string gender = genderChoice ? " - " + (v.gender == CharacterGender.Male ? "HOMBRE" : "MUJER") : "";
            characterText.text = CharacterSelectMenu.TierName(v.type) + gender;
        }

        if (characterIcon != null)
        {
            characterIcon.sprite = characterSprite;
            characterIcon.enabled = characterSprite != null;
        }
    }

    private void SetCharacterVisible(bool visible)
    {
        if (characterNameText != null) characterNameText.gameObject.SetActive(visible);
        if (characterText != null) characterText.gameObject.SetActive(visible);
        if (characterIcon != null) characterIcon.gameObject.SetActive(visible);
    }

    private void SetLine(TMP_Text text, Image icon, bool ok, string okText, string badText)
    {
        if (text != null)
        {
            text.text = ok ? okText : badText;
            text.color = ok ? Color.green : colorRojo;
        }

        if (icon != null)
            icon.sprite = ok ? iconoVerde : iconoRojo;
    }
}