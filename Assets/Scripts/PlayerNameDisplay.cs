using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using TMPro;

public class PlayerNameplate : NetworkBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject nameplateContainer; // Arrastra DisplayName aquí
    [SerializeField] private TMP_Text txtPlayerName;          // Arrastra el Text (TMP)

    [Header("Ajustes")]
    [SerializeField] private Vector3 offset = new Vector3(0, 2.2f, 0);

    // Claves de PlayerPrefs de CharacterRenameManager
    private const string PREF_ULTIMO_ACTIVO = "PLAYER_CUSTOM_USERNAME";
    private const string PREF_OSCAR = "NOMBRE_PERSONAJE_OSCAR";

    private NetworkVariable<FixedString32Bytes> networkPlayerName = new NetworkVariable<FixedString32Bytes>(
        new FixedString32Bytes("Oscar"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Camera localCamera;

    private void Awake()
    {
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

        if (IsOwner)
        {
            // 1. Intentar leer el último nombre activo guardado por CharacterRenameManager
            string miNombre = PlayerPrefs.GetString(PREF_ULTIMO_ACTIVO, "");

            // 2. Si no hay último activo, intentar leer el nombre guardado de Oscar
            if (string.IsNullOrWhiteSpace(miNombre))
            {
                miNombre = PlayerPrefs.GetString(PREF_OSCAR, "");
            }

            // 3. Fallback final si nunca se abrió el menú o está vacío
            if (string.IsNullOrWhiteSpace(miNombre))
            {
                miNombre = "Oscar";
            }

            Debug.Log($"[PlayerNameplate] Nombre sincronizado desde CharacterRenameManager: '{miNombre}'");

            SetPlayerNameServerRpc(miNombre);
            ActualizarTexto(miNombre);
        }
        else
        {
            ActualizarTexto(networkPlayerName.Value.ToString());
        }
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
        if (txtPlayerName != null)
        {
            txtPlayerName.text = nombre;
        }
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