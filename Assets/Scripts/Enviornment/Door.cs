using UnityEngine;
using Unity.Netcode;

public class Door : NetworkBehaviour
{
    [Header("Puerta")]
    [SerializeField] private Transform doorLeaf;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 2f;

    [Header("Dirección")]
    [SerializeField] private bool invertOpenDirection = false;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioClip doorCloseSound;

    private PlayerControls controls;

    private Quaternion closedRotation;
    private Quaternion startRotation;

    private float timeElapsed = 1f;

    private Camera localPlayerCamera;

    // =========================================================
    // SINGLEPLAYER
    // =========================================================

    private bool isOpenLocal = false;
    private float openDirectionLocal = 1f;

    // =========================================================
    // MULTIPLAYER
    // =========================================================

    private NetworkVariable<bool> isOpenNetwork =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private NetworkVariable<float> openDirectionNetwork =
        new NetworkVariable<float>(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // =========================================================
    // PROPIEDADES
    // =========================================================

    public bool IsOpen
    {
        get
        {
            return ObtenerEstadoAbierto();
        }
    }

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        controls =
            new PlayerControls();

        if (doorLeaf != null)
        {
            closedRotation =
                doorLeaf.localRotation;

            startRotation =
                closedRotation;
        }
    }

    // =========================================================
    // ENABLE / DISABLE
    // =========================================================

    private void OnEnable()
    {
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();
    }

    // =========================================================
    // NETWORK
    // =========================================================

    public override void OnNetworkSpawn()
    {
        isOpenNetwork.OnValueChanged +=
            OnDoorStateChanged;

        openDirectionNetwork.OnValueChanged +=
            OnOpenDirectionChanged;

        AplicarEstadoActual();
    }

    public override void OnNetworkDespawn()
    {
        isOpenNetwork.OnValueChanged -=
            OnDoorStateChanged;

        openDirectionNetwork.OnValueChanged -=
            OnOpenDirectionChanged;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        BuscarCamaraJugadorLocal();

        if (localPlayerCamera == null)
            return;

        if (controls.Player.Interact.triggered &&
            EstaMirandoLaPuerta())
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

            // Multiplayer
            if (networkObject != null &&
                networkObject.IsSpawned &&
                networkObject.IsOwner)
            {
                localPlayerCamera =
                    cam;

                return;
            }

            // Singleplayer
            if (networkObject == null &&
                cam == Camera.main)
            {
                localPlayerCamera =
                    cam;

                return;
            }
        }

        // Fallback
        if (Camera.main != null &&
            Camera.main.isActiveAndEnabled)
        {
            NetworkObject networkObject =
                Camera.main.GetComponentInParent<
                    NetworkObject
                >();

            if (networkObject == null ||
                networkObject.IsOwner)
            {
                localPlayerCamera =
                    Camera.main;
            }
        }
    }

    // =========================================================
    // DETECTAR SI EL JUGADOR MIRA LA PUERTA
    // =========================================================

    private bool EstaMirandoLaPuerta()
    {
        if (localPlayerCamera == null ||
            doorLeaf == null)
        {
            return false;
        }

        Ray ray =
            new Ray(
                localPlayerCamera.transform.position,
                localPlayerCamera.transform.forward
            );

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            3f
        ))
        {
            return false;
        }

        return
            hit.transform == doorLeaf ||
            hit.transform.IsChildOf(transform);
    }

    // =========================================================
    // ESTADO
    // =========================================================

    private bool ObtenerEstadoAbierto()
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            return isOpenNetwork.Value;
        }

        return isOpenLocal;
    }

    private float ObtenerDireccionApertura()
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            return openDirectionNetwork.Value;
        }

        return openDirectionLocal;
    }

    // =========================================================
    // ABRIR / CERRAR
    // =========================================================

    private void ToggleDoor()
    {
        bool currentlyOpen =
            ObtenerEstadoAbierto();

        // MULTIPLAYER
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            if (!currentlyOpen)
            {
                float direction =
                    CalcularDireccionApertura();

                ToggleDoorServerRpc(
                    direction
                );
            }
            else
            {
                ToggleDoorServerRpc(
                    openDirectionNetwork.Value
                );
            }

            return;
        }

        // SINGLEPLAYER
        if (!currentlyOpen)
        {
            openDirectionLocal =
                CalcularDireccionApertura();
        }

        bool wasOpen =
            isOpenLocal;

        isOpenLocal =
            !isOpenLocal;

        startRotation =
            doorLeaf.localRotation;

        timeElapsed = 0f;

        ReproducirSonidoLocal(
            !wasOpen
        );
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleDoorServerRpc(
        float requestedDirection
    )
    {
        bool wasOpen =
            isOpenNetwork.Value;

        if (!wasOpen)
        {
            openDirectionNetwork.Value =
                requestedDirection >= 0f
                    ? 1f
                    : -1f;
        }

        isOpenNetwork.Value =
            !isOpenNetwork.Value;

        PlayDoorSoundClientRpc(
            !wasOpen
        );
    }

    // =========================================================
    // DIRECCIÓN DE APERTURA
    // =========================================================

    private float CalcularDireccionApertura()
    {
        if (localPlayerCamera == null ||
            doorLeaf == null)
        {
            return 1f;
        }

        Vector3 directionFromDoor =
            localPlayerCamera.transform.position -
            doorLeaf.position;

        // Usamos el eje Z local del doorLeaf
        // para determinar de qué lado está el jugador.
        float side =
            Vector3.Dot(
                directionFromDoor,
                doorLeaf.forward
            );

        float direction;

        // El jugador está delante.
        // La puerta abre hacia atrás.
        if (side > 0f)
        {
            direction = 1f;
        }
        else
        {
            direction = -1f;
        }

        if (invertOpenDirection)
        {
            direction *= -1f;
        }

        return direction;
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

    private void OnOpenDirectionChanged(
        float previousValue,
        float newValue
    )
    {
        if (doorLeaf == null)
            return;

        if (ObtenerEstadoAbierto())
        {
            startRotation =
                doorLeaf.localRotation;

            timeElapsed = 0f;
        }
    }

    // =========================================================
    // APLICAR ESTADO ACTUAL
    // =========================================================

    private void AplicarEstadoActual()
    {
        if (doorLeaf == null)
            return;

        float direction =
            ObtenerDireccionApertura();

        Quaternion openRotation =
            closedRotation *
            Quaternion.Euler(
                0f,
                openAngle * direction,
                0f
            );

        startRotation =
            doorLeaf.localRotation;

        timeElapsed = 1f;

        doorLeaf.localRotation =
            ObtenerEstadoAbierto()
                ? openRotation
                : closedRotation;
    }

    // =========================================================
    // ROTACIÓN DE LA PUERTA
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

        float direction =
            ObtenerDireccionApertura();

        Quaternion openRotation =
            closedRotation *
            Quaternion.Euler(
                0f,
                openAngle * direction,
                0f
            );

        Quaternion targetRotation =
            ObtenerEstadoAbierto()
                ? openRotation
                : closedRotation;

        doorLeaf.localRotation =
            Quaternion.Slerp(
                startRotation,
                targetRotation,
                clampedTime
            );
    }

    // =========================================================
    // AUDIO
    // =========================================================

    [ClientRpc]
    private void PlayDoorSoundClientRpc(
        bool opening
    )
    {
        ReproducirSonidoLocal(
            opening
        );
    }

    private void ReproducirSonidoLocal(
        bool opening
    )
    {
        if (audioSource == null)
            return;

        AudioClip clip =
            opening
                ? doorOpenSound
                : doorCloseSound;

        if (clip == null)
            return;

        audioSource.PlayOneShot(
            clip
        );
    }
}