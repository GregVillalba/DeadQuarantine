using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerLook : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private PlayerControls controls;
    private Vector2 lookInput;
    private float pitch;

    private NetworkObject networkObject;
    private Transform playerTransform;

    private void Awake()
    {
        controls = new PlayerControls();

        networkObject = GetComponentInParent<NetworkObject>();

        if (networkObject != null)
            playerTransform = networkObject.transform;
    }

    private void OnEnable()
    {
        controls.Player.Enable();

        controls.Player.Look.performed += OnLook;
        controls.Player.Look.canceled += OnLook;
    }

    private void OnDisable()
    {
        controls.Player.Look.performed -= OnLook;
        controls.Player.Look.canceled -= OnLook;

        controls.Player.Disable();
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    private void Update()
    {
        if (networkObject == null)
            return;

        if (!networkObject.IsSpawned)
            return;

        if (!networkObject.IsOwner)
            return;

        if (playerTransform == null)
            return;

        // GIRAR AL PERSONAJE HORIZONTALMENTE
        float yaw = lookInput.x * mouseSensitivity;

        playerTransform.Rotate(
            Vector3.up * yaw
        );

        // MIRAR ARRIBA / ABAJO
        pitch -= lookInput.y * mouseSensitivity;

        pitch = Mathf.Clamp(
            pitch,
            minPitch,
            maxPitch
        );

        if (cameraTransform != null)
        {
            cameraTransform.localRotation =
                Quaternion.Euler(
                    pitch,
                    0f,
                    0f
                );
        }
    }
}