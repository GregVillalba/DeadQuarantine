using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una columna del lobby (JUGADOR N): el modelo 3D se muestra encima (lo dibuja CharacterPreviewStage en este slot)
/// y este componente maneja la tarjeta de abajo: nombre del jugador, rol, personaje elegido (icono + nombre),
/// conexión y estado de listo. Lo maneja MultiplayerLobbyController (Configure una vez + SetPlayer / SetEmpty).
/// Lo arma solo Game > Construir UI del Lobby (ver LobbyUIBuilder).
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
    [SerializeField] private TMP_Text characterNameText;   // Nombre propio: RYDER / VEGA / NULL...
    [SerializeField] private TMP_Text characterText;       // EXPLORADOR - HOMBRE / SOLDADO - MUJER / TANQUE

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
        lastIndex = -2;

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
    public void SetPlayer(bool isLocal, bool isHost, bool ready, int remoteIndex)
    {
        int index = isLocal ? CharacterSelection.SelectedIndex : remoteIndex;

        if (stage != null)
            stage.Show(slotIndex, index);

        if (hasLast && lastConnected && lastLocal == isLocal && lastHost == isHost && lastReady == ready && lastIndex == index)
            return;

        hasLast = true;
        lastConnected = true;
        lastLocal = isLocal;
        lastHost = isHost;
        lastReady = ready;
        lastIndex = index;

        if (titleText != null) titleText.text = "JUGADOR " + (slotIndex + 1);

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

        SetCharacterVisible(true);

        if (characterNameText != null)
        {
            // El nombre propio pertenece a la VARIANTE, no a la clase.
            // Ejemplo:
            // Scout Male   -> RYDER
            // Scout Female -> VEGA
            // Tank         -> NULL
            string characterName = catalog.GetCharacterNameForVariant(index);

            characterNameText.text =
                !string.IsNullOrWhiteSpace(characterName)
                    ? characterName.ToUpperInvariant()
                    : v.type.ToString().ToUpperInvariant();
        }

        if (characterText != null)
        {
            // El tipo/clase se mantiene fijo:
            // EXPLORADOR / SOLDADO / TANQUE.
            // Solo mostramos HOMBRE/MUJER cuando esa clase realmente tiene elección de género.
            bool genderChoice = catalog.HasGenderChoice(v.type);

            string gender =
                genderChoice
                    ? " - " + (v.gender == CharacterGender.Male ? "HOMBRE" : "MUJER")
                    : "";

            characterText.text =
                CharacterSelectMenu.TierName(v.type) + gender;
        }

        if (characterIcon != null)
        {
            Sprite sprite = catalog.GetIcon(v.type, v.gender);
            characterIcon.sprite = sprite;
            characterIcon.enabled = sprite != null;
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