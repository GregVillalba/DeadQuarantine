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

    private Camera localCamera;

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
            nameplateContainer.SetActive(true);

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

        // Efecto Billboard hacia la cámara activa
        Vector3 direccionACamara = nameplateContainer.transform.position - localCamera.transform.position;
        if (direccionACamara.sqrMagnitude > 0.001f)
        {
            nameplateContainer.transform.rotation = Quaternion.LookRotation(direccionACamara);
        }
    }
}