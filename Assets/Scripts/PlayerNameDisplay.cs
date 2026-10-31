using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using TMPro;

public class PlayerNameplate : NetworkBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject nameplateContainer; // Arrastra DisplayName aquí
    [SerializeField] private TMP_Text txtPlayerName;          // Arrastra el Text (TMP)

    [Header("Personaje")]
    [SerializeField] private PlayerCharacter playerCharacter;

    [Header("Ajustes")]
    [SerializeField] private Vector3 offset = new Vector3(0, 2.2f, 0);

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

    private void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        if (playerCharacter == null)
            playerCharacter = GetComponentInParent<PlayerCharacter>();

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
            ActualizarTexto(networkPlayerName.Value.ToString());
        }
    }

    public override void OnNetworkDespawn()
    {
        if (playerCharacter != null)
            playerCharacter.CharacterIndex.OnValueChanged -= OnCharacterChanged;

        if(txtPlayerName && originalMaterial)txtPlayerName.fontSharedMaterial=originalMaterial;
        if(overlayMaterial)Destroy(overlayMaterial);
        base.OnNetworkDespawn();
    }

    [ServerRpc]
    private void SetPlayerNameServerRpc(string nuevoNombre)
    {
        networkPlayerName.Value = new FixedString32Bytes(nuevoNombre);
    }

    private void OnNameChanged(FixedString32Bytes antiguoNombre, FixedString32Bytes nuevoNombre)
    {
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
        if (nameplateContainer == null || !nameplateContainer.activeInHierarchy)
            return;

        if (localCamera == null)
        {
            localCamera = Camera.main;
            if (localCamera == null) return;
        }

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
        // Efecto Billboard hacia la cámara activa
        Vector3 direccionACamara = nameplateContainer.transform.position - localCamera.transform.position;
        if (direccionACamara.sqrMagnitude > 0.001f)
        {
            nameplateContainer.transform.rotation = Quaternion.LookRotation(direccionACamara);
        }
    }
}