using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerLook : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    [Header("Mouse")]
    [SerializeField] private float mouseSensitivity = 0.1f;

    [Header("Vertical Look")]
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Recoil")]
    [SerializeField] private float recoilRecoverySpeed = 4f;

    private PlayerControls controls;

    private NetworkObject networkObject;
    private Transform playerTransform;

    private float pitch = 0f;
    private float recoilPitch = 0f; // Se acumula con AddRecoil(), no viene del mouse.

    private void Awake()
    {
        controls = new PlayerControls();

        networkObject = GetComponentInParent<NetworkObject>();

        if (networkObject != null)
            playerTransform = networkObject.transform;

        pitch = 0f;
    }

    private void OnEnable()
    {
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();
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

        if (cameraTransform == null)
            return;

        Vector2 lookInput = controls.Player.Look.ReadValue<Vector2>();

        // =====================================================
        // HORIZONTAL
        // =====================================================

        float yaw = lookInput.x * mouseSensitivity;
        playerTransform.Rotate(Vector3.up * yaw);

        // =====================================================
        // VERTICAL (mouse)
        // =====================================================

        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // =====================================================
        // RECOIL (independiente del mouse, decae solo)
        // =====================================================

        recoilPitch = Mathf.Lerp(recoilPitch, 0f, recoilRecoverySpeed * Time.deltaTime);

        float finalPitch = Mathf.Clamp(pitch - recoilPitch, minPitch, maxPitch);

        cameraTransform.localRotation = Quaternion.Euler(finalPitch, 0f, 0f);
    }

    /// <summary>
    /// Suma recoil vertical (en grados). Positivo = la cámara sube.
    /// Llamado por Weapon.cs en cada disparo.
    /// </summary>
    public void AddRecoil(float amount)
    {
        recoilPitch += amount;
    }
}