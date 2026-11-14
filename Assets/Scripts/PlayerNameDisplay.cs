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
        if (nameplateContainer != null)
            nameplateContainer.SetActive(!IsOwner);
        if(txtPlayerName) {
            originalMaterial=txtPlayerName.fontSharedMaterial;
            overlayMaterial=new Material(originalMaterial){name="Player Name Overlay",renderQueue=4000};
            if(overlayTemplate)overlayMaterial.shader=overlayTemplate.shader;
            overlayMaterial.SetFloat(ShaderUtilities.ShaderTag_ZTestMode,(float)UnityEngine.Rendering.CompareFunction.Always);
            txtPlayerName.fontSharedMaterial=overlayMaterial;txtPlayerName.isOverlay=true;txtPlayerName.raycastTarget=false;
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
        ActualizarTexto(networkPlayerName.Value.ToString());
    }

    private void LateUpdate()
    {
        if(!nameplateContainer)return;
        bool visible=!IsOwner;
        var roster=DeadQuarantine.PvPvE.Networking.PvPvETeamRoster.Active;
        if(roster&&roster.IsSpawned){
            visible=visible&&roster.TryGetMember(NetworkManager.LocalClientId,out var local)&&roster.TryGetMember(OwnerClientId,out var other)&&local.Team==other.Team;
        }
        if(nameplateContainer.activeSelf!=visible)nameplateContainer.SetActive(visible);
        if(!visible)return;

        if (localCamera == null)
        {
            localCamera = Camera.main;
            if (localCamera == null) return;
        }

        UpdateDownedTimer();

        if(IsOwner) {nameplateContainer.SetActive(false);return;}
        nameplateContainer.transform.position=transform.position+offset;
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
        if(txtPlayerName)txtPlayerName.alpha=opacity;
        if (txtDownedTimer) txtDownedTimer.alpha = opacity;
        if (downedIcon) downedIcon.canvasRenderer.SetAlpha(opacity);
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