using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerNameplate : NetworkBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject nameplateContainer; // Arrastra DisplayName aquí
    [SerializeField] private TMP_Text txtPlayerName;          // Arrastra el Text (TMP)

    [Header("Personaje")]
    [SerializeField] private PlayerCharacter playerCharacter;

    [Header("Ajustes")]
    [SerializeField] private Vector3 offset = new Vector3(0, 2.2f, 0);

    [Header("Estado abatido")]
[SerializeField] private PlayerHealth playerHealth;
[SerializeField] private GameObject downedGroup;    // objeto "Abatido"
[SerializeField] private TMP_Text txtDownedTimer;   // objeto "Minutero"
[SerializeField] private Color downedNameColor = Color.red;

private Color normalNameColor = Color.white;
private int lastShownSeconds = -1;
[SerializeField] private Graphic downedIcon;   // Icono (si es Image)
private Material timerOverlayMaterial, timerOriginalMaterial, iconOverlayMaterial;


    private NetworkVariable<FixedString32Bytes> networkPlayerName = new NetworkVariable<FixedString32Bytes>(
        new FixedString32Bytes("JUGADOR"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );



    [SerializeField] private Material overlayTemplate;
    private Material overlayMaterial,originalMaterial;
    private Camera localCamera;
    private float nextOcclusionCheck;
    private float opacity=1;
    public bool IsOccluded {get;private set;}
    public bool NameVisible => nameplateContainer && nameplateContainer.activeInHierarchy;
    public float NameOpacity => opacity;
    private Image portrait;
    private RectTransform healthFill;
    private TMP_Text healthValue;
    private CanvasGroup plateAlpha;
    public float HealthFraction => playerHealth ? Mathf.Clamp01((float)playerHealth.CurrentHealth.Value / Mathf.Max(1, playerHealth.MaxHealth)) : 0;

    private void BuildTeammatePlate()
    {
        if (!nameplateContainer || !txtPlayerName || portrait) return;
        var root = nameplateContainer.GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(320, 72);
        root.localScale = Vector3.one * .006f;
        plateAlpha = root.GetComponent<CanvasGroup>();
        if (!plateAlpha) plateAlpha = root.gameObject.AddComponent<CanvasGroup>();
        plateAlpha.interactable = plateAlpha.blocksRaycasts = false;
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            if (label != txtPlayerName && (!downedGroup || !label.transform.IsChildOf(downedGroup.transform))) label.gameObject.SetActive(false);
        var material = Resources.Load<Material>("TeammatePlateOverlay");
        Image PlateImage(string name, float x, float y, float width, float height, Color color)
        {
            var rect = DeadQuarantine.PvPvE.PvPvEUIFactory.Rect(root, name, x, y, width, height);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
            if (material) image.material = material;
            return image;
        }
        var background = PlateImage("FondoCompanero", 0, 0, 320, 72, new Color(.025f, .055f, .042f, .88f));
        background.transform.SetAsFirstSibling();
        PlateImage("Acento", -158, 0, 3, 72, new Color(.25f, .85f, .45f));
        portrait = PlateImage("Personaje", -126, 0, 48, 56, Color.white); portrait.preserveAspect = true;
        var textRect = txtPlayerName.rectTransform;
        textRect.anchorMin = textRect.anchorMax = new Vector2(.5f, .5f);
        textRect.anchoredPosition = new Vector2(27, 14); textRect.sizeDelta = new Vector2(240, 32);
        var font = DeadQuarantine.PvPvE.PvPvEUIFactory.HeadingFont;
        if (font) { txtPlayerName.font = font; if (txtDownedTimer) txtDownedTimer.font = font; }
        txtPlayerName.fontSize = 23; txtPlayerName.enableAutoSizing = true; txtPlayerName.fontSizeMin = 14; txtPlayerName.fontSizeMax = 23;
        txtPlayerName.textWrappingMode = TextWrappingModes.NoWrap; txtPlayerName.overflowMode = TextOverflowModes.Ellipsis;
        txtPlayerName.alignment = TextAlignmentOptions.MidlineLeft; txtPlayerName.color = new Color(.94f, .97f, .95f); txtPlayerName.richText = false;
        var track = PlateImage("VidaFondo", 3, -17, 194, 7, new Color(.13f, .2f, .16f));
        var fill = new GameObject("Vida", typeof(RectTransform), typeof(Image));
        healthFill = fill.GetComponent<RectTransform>(); healthFill.SetParent(track.transform, false);
        healthFill.anchorMin = Vector2.zero; healthFill.anchorMax = Vector2.one; healthFill.offsetMin = healthFill.offsetMax = Vector2.zero;
        var fillImage = fill.GetComponent<Image>(); fillImage.color = new Color(.25f, .9f, .45f); fillImage.material = material; fillImage.raycastTarget = false;
        healthValue = DeadQuarantine.PvPvE.PvPvEUIFactory.Text(root, "Salud", "100%", 126, -17, 56, 24, font, 18);
        healthValue.rectTransform.anchorMin = healthValue.rectTransform.anchorMax = healthValue.rectTransform.pivot = new Vector2(.5f, .5f);
        healthValue.rectTransform.anchoredPosition = new Vector2(126, -17);
        if (font) healthValue.font = font;
        healthValue.alignment = TextAlignmentOptions.MidlineRight; healthValue.isOverlay = true;
        // Preserve the downed indicator layout authored in Player.prefab.
        UpdatePortrait();
    }

    private void UpdatePortrait()
    {
        if (!portrait || !playerCharacter || !playerCharacter.Catalog) return;
        var variant = playerCharacter.Catalog.GetVariant(playerCharacter.CharacterIndex.Value);
        portrait.sprite = variant != null ? playerCharacter.Catalog.GetIcon(variant.type, variant.gender) : null;
        portrait.enabled = portrait.sprite;
    }

    private static readonly Dictionary<ulong, string> nombresPorCliente = new Dictionary<ulong, string>();

public static string GetNombreCliente(ulong clientId)
{
    return nombresPorCliente.TryGetValue(clientId, out string n) && !string.IsNullOrWhiteSpace(n)
        ? n.Trim()
        : "UN JUGADOR";
}
public static bool ConoceNombreCliente(ulong clientId)
{
    return nombresPorCliente.TryGetValue(clientId, out string n) && !string.IsNullOrWhiteSpace(n);
}
    public string UsernameOnly
{
    get
    {
        string n = networkPlayerName.Value.ToString();
        return string.IsNullOrWhiteSpace(n) ? "JUGADOR" : n.Trim();
    }
}

    private void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        if (playerCharacter == null)
            playerCharacter = GetComponentInParent<PlayerCharacter>();

        if (playerHealth == null)
    playerHealth = GetComponent<PlayerHealth>();
if (playerHealth == null)
    playerHealth = GetComponentInParent<PlayerHealth>();
    

        networkPlayerName.OnValueChanged += OnNameChanged;
    }

    public override void OnDestroy()
    {
        networkPlayerName.OnValueChanged -= OnNameChanged;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        BuildTeammatePlate();
        if (nameplateContainer != null)
            nameplateContainer.SetActive(!IsOwner);
        if(txtPlayerName) {
            originalMaterial=txtPlayerName.fontSharedMaterial;
            overlayMaterial=new Material(originalMaterial){name="Player Name Overlay",renderQueue=4000};
            if(overlayTemplate)overlayMaterial.shader=overlayTemplate.shader;
            overlayMaterial.SetFloat(ShaderUtilities.ShaderTag_ZTestMode,(float)UnityEngine.Rendering.CompareFunction.Always);
            txtPlayerName.fontSharedMaterial=overlayMaterial;txtPlayerName.isOverlay=true;txtPlayerName.raycastTarget=false;
            if (healthValue) healthValue.fontSharedMaterial = overlayMaterial;
        }

        if (txtPlayerName != null)
            normalNameColor = txtPlayerName.color;

        if (playerHealth != null)
        {
            playerHealth.State.OnValueChanged += OnPlayerStateChanged;
            ApplyDownedVisuals(playerHealth.State.Value);
        }

        if (txtDownedTimer != null)
        {
            timerOriginalMaterial = txtDownedTimer.fontSharedMaterial;
            timerOverlayMaterial = new Material(timerOriginalMaterial) { name = "Player Timer Overlay", renderQueue = 4000 };
            if (overlayTemplate) timerOverlayMaterial.shader = overlayTemplate.shader;
            timerOverlayMaterial.SetFloat(ShaderUtilities.ShaderTag_ZTestMode, (float)UnityEngine.Rendering.CompareFunction.Always);
            txtDownedTimer.fontSharedMaterial = timerOverlayMaterial;
            txtDownedTimer.isOverlay = true;
            txtDownedTimer.raycastTarget = false;
        }

        if (downedIcon != null)
        {
            iconOverlayMaterial = new Material(downedIcon.material);
            iconOverlayMaterial.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            downedIcon.material = iconOverlayMaterial;
        }

        if (playerCharacter != null)
            playerCharacter.CharacterIndex.OnValueChanged += OnCharacterChanged;

        if (IsOwner)
        {
            string miNombre = PlayerIdentity.GetUsername();

            if (string.IsNullOrWhiteSpace(miNombre))
                miNombre = "JUGADOR";

            SetPlayerNameServerRpc(miNombre);
            ActualizarTexto(miNombre);
        }
        else
        {
            nombresPorCliente[OwnerClientId] = networkPlayerName.Value.ToString();
            ActualizarTexto(networkPlayerName.Value.ToString());
        }
    }

    public override void OnNetworkDespawn()
    {
        if (playerHealth != null)
            playerHealth.State.OnValueChanged -= OnPlayerStateChanged;

        if (playerCharacter != null)
            playerCharacter.CharacterIndex.OnValueChanged -= OnCharacterChanged;

        if(txtPlayerName && originalMaterial)txtPlayerName.fontSharedMaterial=originalMaterial;
        if(overlayMaterial)Destroy(overlayMaterial);
        if (txtDownedTimer && timerOriginalMaterial) txtDownedTimer.fontSharedMaterial = timerOriginalMaterial;
        if (timerOverlayMaterial) Destroy(timerOverlayMaterial);
        if (iconOverlayMaterial) Destroy(iconOverlayMaterial);
        base.OnNetworkDespawn();
    }

    [ServerRpc]
    private void SetPlayerNameServerRpc(string nuevoNombre)
    {
        networkPlayerName.Value = new FixedString32Bytes(nuevoNombre);
    }

    private void OnNameChanged(FixedString32Bytes antiguoNombre, FixedString32Bytes nuevoNombre)
    {
        nombresPorCliente[OwnerClientId] = nuevoNombre.ToString();
        ActualizarTexto(nuevoNombre.ToString());
    }

    private void ActualizarTexto(string nombre)
    {
        if (txtPlayerName == null)
            return;

        string username = string.IsNullOrWhiteSpace(nombre) ? "JUGADOR" : nombre.Trim();
        string characterName = string.Empty;

        if (playerCharacter != null && playerCharacter.Catalog != null)
        {
            characterName = playerCharacter.Catalog.GetCharacterNameForVariant(
                playerCharacter.CharacterIndex.Value
            );
        }

        txtPlayerName.text = string.IsNullOrWhiteSpace(characterName)
            ? username.ToUpperInvariant()
            : username.ToUpperInvariant() + " - " + characterName.ToUpperInvariant();
    }

    private void OnCharacterChanged(int previousIndex, int newIndex)
    {
        UpdatePortrait();
        ActualizarTexto(networkPlayerName.Value.ToString());
    }

    private void LateUpdate()
    {
        if(!nameplateContainer)return;
        bool visible=!IsOwner && (!playerHealth || !(playerHealth.IsDead || playerHealth.IsSpectating));
        var roster=DeadQuarantine.PvPvE.Networking.PvPvETeamRoster.Active;
        if(roster&&roster.IsSpawned){
            visible=visible&&roster.TryGetMember(NetworkManager.LocalClientId,out var local)&&roster.TryGetMember(OwnerClientId,out var other)&&local.Team==other.Team;
        }
        if(nameplateContainer.activeSelf!=visible)nameplateContainer.SetActive(visible);
        if(!visible)return;

        if (localCamera == null || !localCamera.isActiveAndEnabled)
        {
            localCamera = Camera.main;
            if (localCamera == null) return;
        }

        UpdateDownedTimer();

        if(IsOwner) {nameplateContainer.SetActive(false);return;}
        nameplateContainer.transform.position=transform.position+offset;
        float distance = Vector3.Distance(localCamera.transform.position, nameplateContainer.transform.position);
        nameplateContainer.transform.localScale = Vector3.one * (.006f * Mathf.Clamp(distance / 10f, 1, 2.5f));
        if(Time.unscaledTime>=nextOcclusionCheck) {
            nextOcclusionCheck=Time.unscaledTime+.12f;
            Vector3 target=transform.position+Vector3.up*1.4f;
            Vector3 ray=target-localCamera.transform.position;
            IsOccluded=false;
            foreach(var hit in Physics.RaycastAll(localCamera.transform.position,ray.normalized,ray.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) {
                if(hit.transform.root==transform.root || hit.transform.root==localCamera.transform.root)continue;
                IsOccluded=true;break;
            }
        }
        opacity=Mathf.MoveTowards(opacity,IsOccluded?.35f:1f,Time.unscaledDeltaTime*5);
        if (plateAlpha) plateAlpha.alpha = opacity;
        if (healthFill) healthFill.anchorMax = new Vector2(HealthFraction, 1);
        if (healthValue) healthValue.text = Mathf.RoundToInt(HealthFraction * 100) + "%";
        // Efecto Billboard hacia la cámara activa
        Vector3 direccionACamara = nameplateContainer.transform.position - localCamera.transform.position;
        if (direccionACamara.sqrMagnitude > 0.001f)
        {
            nameplateContainer.transform.rotation = Quaternion.LookRotation(direccionACamara);
        }
    }

    private void OnPlayerStateChanged(PlayerHealth.PlayerState previous, PlayerHealth.PlayerState current)
{
    ApplyDownedVisuals(current);
}

private void ApplyDownedVisuals(PlayerHealth.PlayerState state)
{
    bool downed = state == PlayerHealth.PlayerState.Downed;

    if (downedGroup != null)
        downedGroup.SetActive(downed);

    if (txtPlayerName != null)
        txtPlayerName.color = downed ? downedNameColor : normalNameColor;

    lastShownSeconds = -1;
    if (downed)
        UpdateDownedTimer();
}

private void UpdateDownedTimer()
{
    if (txtDownedTimer == null || playerHealth == null)
        return;

    if (playerHealth.State.Value != PlayerHealth.PlayerState.Downed)
        return;

    int seconds = Mathf.Max(0, Mathf.CeilToInt(playerHealth.DownedTimeRemaining.Value));
    if (seconds == lastShownSeconds)
        return;

    lastShownSeconds = seconds;
    txtDownedTimer.text = seconds.ToString();
}
}
