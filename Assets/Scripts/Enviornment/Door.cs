using UnityEngine;
using Unity.Netcode;

public class Door : NetworkBehaviour
{
    [Header("Puerta")]
    [SerializeField] private Transform doorLeaf;
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 2f;

    private PlayerControls controls;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion startRotation;

    private bool isPlayerLooking;
    private float timeElapsed = 1f;

    // Estado de la puerta sincronizado.
    private NetworkVariable<bool> isOpenNetwork =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private Camera localPlayerCamera;

    private void Awake()
    {
        controls =
            new PlayerControls();

        if (doorLeaf != null)
        {
            closedRotation =
                doorLeaf.localRotation;

            openRotation =
                closedRotation *
                Quaternion.Euler(
                    0f,
                    openAngle,
                    0f
                );

            startRotation =
                closedRotation;
        }
    }

    private void OnEnable()
    {
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();
    }

    public override void OnNetworkSpawn()
    {
        isOpenNetwork.OnValueChanged +=
            OnDoorStateChanged;

        // Aplicar inmediatamente el estado actual.
        AplicarEstadoPuerta(
            isOpenNetwork.Value
        );
    }

    public override void OnNetworkDespawn()
    {
        isOpenNetwork.OnValueChanged -=
            OnDoorStateChanged;
    }

    private void Update()
    {
        BuscarCamaraJugadorLocal();

        if (localPlayerCamera == null)
            return;

        CheckIfPlayerIsLooking();

        if (controls.Player.Interact.triggered &&
            isPlayerLooking)
        {
            ToggleDoor();
        }

        RotateDoor();
    }

    // =========================================================
    // BUSCAR CÁMARA DEL JUGADOR LOCAL
    // =========================================================

    private void BuscarCamaraJugadorLocal()
    {
        if (localPlayerCamera != null &&
            localPlayerCamera.isActiveAndEnabled)
        {
            return;
        }

        localPlayerCamera = null;

        // Primero intenta con la Main Camera.
        Camera mainCamera =
            Camera.main;

        if (mainCamera != null &&
            mainCamera.isActiveAndEnabled)
        {
            localPlayerCamera =
                mainCamera;

            return;
        }

        // Fallback para multiplayer:
        // busca una cámara perteneciente
        // al PlayerMultiplayer local.
        Camera[] cameras =
            FindObjectsByType<Camera>(
                FindObjectsSortMode.None
            );

        foreach (Camera cam in cameras)
        {
            if (!cam.isActiveAndEnabled)
                continue;

            NetworkObject networkObject =
                cam.GetComponentInParent<
                    NetworkObject
                >();

            if (networkObject != null &&
                networkObject.IsSpawned &&
                networkObject.IsOwner)
            {
                localPlayerCamera =
                    cam;

                return;
            }
        }
    }

    // =========================================================
    // DETECTAR PUERTA
    // =========================================================

    private void CheckIfPlayerIsLooking()
    {
        if (localPlayerCamera == null ||
            doorLeaf == null)
        {
            isPlayerLooking = false;
            return;
        }

        Ray ray =
            new Ray(
                localPlayerCamera.transform.position,
                localPlayerCamera.transform.forward
            );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactRange
        ))
        {
            isPlayerLooking =
                hit.transform == doorLeaf ||
                hit.transform.IsChildOf(transform);
        }
        else
        {
            isPlayerLooking = false;
        }
    }

    // =========================================================
    // ABRIR / CERRAR
    // =========================================================

    private void ToggleDoor()
    {
        // Si Netcode está funcionando,
        // el servidor controla el estado.
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            ToggleDoorServerRpc();
            return;
        }

        // Singleplayer sin Netcode activo.
        isOpenNetwork.Value =
            !isOpenNetwork.Value;
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleDoorServerRpc()
    {
        isOpenNetwork.Value =
            !isOpenNetwork.Value;
    }

    // =========================================================
    // CAMBIO DE ESTADO
    // =========================================================

    private void OnDoorStateChanged(
        bool previousValue,
        bool newValue
    )
    {
        if (doorLeaf == null)
            return;

        startRotation =
            doorLeaf.localRotation;

        timeElapsed = 0f;
    }

    private void AplicarEstadoPuerta(
        bool open
    )
    {
        if (doorLeaf == null)
            return;

        startRotation =
            doorLeaf.localRotation;

        timeElapsed = 1f;

        doorLeaf.localRotation =
            open
                ? openRotation
                : closedRotation;
    }

    // =========================================================
    // ROTACIÓN
    // =========================================================

    private void RotateDoor()
    {
        if (doorLeaf == null)
            return;

        if (timeElapsed >= 1f)
            return;

        timeElapsed +=
            Time.deltaTime *
            openSpeed;

        float clampedTime =
            Mathf.Clamp01(
                timeElapsed
            );

        Quaternion targetRotation =
            isOpenNetwork.Value
                ? openRotation
                : closedRotation;

        doorLeaf.localRotation =
            Quaternion.Slerp(
                startRotation,
                targetRotation,
                clampedTime
            );
    }
}