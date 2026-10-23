using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una columna del lobby (JUGADOR N): textos y estado de conexión/listo, modelo 3D en el escenario y,
/// solo si es el jugador local y todavía no está listo, el selector de personaje.
/// Lo maneja MultiplayerLobbyController (Configure una vez + SetPlayer / SetEmpty en cada actualización).
/// </summary>
public class LobbyPlayerSlot : MonoBehaviour
{
    [SerializeField] private int slotIndex;

    [Header("Textos")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text connectionText;
    [SerializeField] private TMP_Text readyText;
    [SerializeField] private TMP_Text characterText;
    [SerializeField] private Image connectionIcon;
    [SerializeField] private Image readyIcon;

    [Header("Solo jugador local")]
    [Tooltip("Contiene el CharacterSelectMenu (botones de tipo/género y estadísticas).")]
    [SerializeField] private GameObject localControls;
    [Tooltip("Image transparente sobre el modelo, con CharacterPreviewDrag.")]
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

        if (characterText != null) characterText.gameObject.SetActive(false);
        if (localControls != null) localControls.SetActive(false);
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

        // El local elige mientras no esté listo; al marcar LISTO queda bloqueado (C.A. 7).
        bool editing = isLocal && !ready;

        if (localControls != null) localControls.SetActive(editing);
        if (dragZone != null) dragZone.SetActive(isLocal);

        if (characterText != null)
        {
            characterText.gameObject.SetActive(!editing);
            characterText.text = DescribeCharacter(index);
        }
    }

    private string DescribeCharacter(int index)
    {
        CharacterCatalog.Variant v = catalog != null ? catalog.GetVariant(index) : null;

        if (v == null)
            return "";

        return CharacterSelectMenu.TierName(v.type) + " - " + (v.gender == CharacterGender.Male ? "HOMBRE" : "MUJER");
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