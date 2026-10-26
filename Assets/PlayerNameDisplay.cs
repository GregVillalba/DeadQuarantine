using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using TMPro;

/// <summary>
/// Nameplate de multijugador.
/// Muestra: USERNAME - NOMBRE DEL PERSONAJE
///
/// Username: se sincroniza por red.
/// Personaje: se obtiene del CharacterIndex ya sincronizado por PlayerCharacter,
/// por lo que no se duplica ese dato en otra NetworkVariable.
/// </summary>
public class PlayerNameplate : NetworkBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject nameplateContainer;
    [SerializeField] private TMP_Text txtPlayerName;

    [Header("Personaje")]
    [Tooltip("Vacío = se busca PlayerCharacter en este mismo objeto.")]
    [SerializeField] private PlayerCharacter playerCharacter;

    [Header("Ajustes")]
    [SerializeField] private Vector3 offset = new Vector3(0, 2.2f, 0);

    private NetworkVariable<FixedString32Bytes> networkUsername =
        new NetworkVariable<FixedString32Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private Camera localCamera;

    private void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        networkUsername.OnValueChanged += OnUsernameChanged;
    }

    public override void OnNetworkSpawn()
    {
        if (nameplateContainer != null)
            nameplateContainer.SetActive(true);

        if (playerCharacter != null)
            playerCharacter.CharacterIndex.OnValueChanged += OnCharacterChanged;

        if (IsOwner)
        {
            string username = PlayerIdentity.GetUsername();

            if (string.IsNullOrWhiteSpace(username))
            {
                // No toma ningún nombre de personaje como fallback.
                // El popup inicial debería impedir llegar hasta acá sin username.
                username = "JUGADOR";
                Debug.LogWarning(
                    "[PlayerNameplate] No existe username persistente. " +
                    "Revisá que UsernameFirstRunPopup esté en la escena inicial."
                );
            }

            SetUsernameServerRpc(username);
        }

        RefreshText();
    }

    public override void OnNetworkDespawn()
    {
        if (playerCharacter != null)
            playerCharacter.CharacterIndex.OnValueChanged -= OnCharacterChanged;

        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        networkUsername.OnValueChanged -= OnUsernameChanged;
        base.OnDestroy();
    }

    [ServerRpc]
    private void SetUsernameServerRpc(string username)
    {
        username = SanitizeNetworkUsername(username);
        networkUsername.Value = new FixedString32Bytes(username);
    }

    private string SanitizeNetworkUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return "JUGADOR";

        username = username.Trim();

        if (username.Length > PlayerIdentity.MaxUsernameLength)
            username = username.Substring(0, PlayerIdentity.MaxUsernameLength);

        return username;
    }

    private void OnUsernameChanged(FixedString32Bytes previous, FixedString32Bytes current)
    {
        RefreshText();
    }

    private void OnCharacterChanged(int previous, int current)
    {
        RefreshText();
    }

    private void RefreshText()
    {
        if (txtPlayerName == null)
            return;

        string username = networkUsername.Value.ToString();

        if (string.IsNullOrWhiteSpace(username) && IsOwner)
            username = PlayerIdentity.GetUsername();

        if (string.IsNullOrWhiteSpace(username))
            username = "JUGADOR";

        string characterName = GetCurrentCharacterName();

        txtPlayerName.text = string.IsNullOrWhiteSpace(characterName)
            ? username
            : username + " - " + characterName.ToUpperInvariant();
    }

    private string GetCurrentCharacterName()
    {
        if (playerCharacter == null || playerCharacter.Catalog == null)
            return string.Empty;

        int index = playerCharacter.CharacterIndex.Value;
        return playerCharacter.Catalog.GetCharacterNameForVariant(index);
    }

    private void LateUpdate()
    {
        if (nameplateContainer == null || !nameplateContainer.activeInHierarchy)
            return;

        if (localCamera == null)
        {
            localCamera = Camera.main;

            if (localCamera == null)
                return;
        }

        Vector3 direccionACamara =
            nameplateContainer.transform.position -
            localCamera.transform.position;

        if (direccionACamara.sqrMagnitude > 0.001f)
            nameplateContainer.transform.rotation =
                Quaternion.LookRotation(direccionACamara);
    }
}
