using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class PlayerLook : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    [Header("Mouse")]
    [SerializeField] private float mouseSensitivity = 0.1f;

    [Header("Vertical Look")]
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private PlayerControls controls;

    private NetworkObject networkObject;
    private Transform playerTransform;

    private float pitch = 0f;
    public float Pitch => pitch;

    private void Awake()
    {
        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);

        mouseSensitivity = ConfiguracionesJuego.ObtenerSensibilidad();

        networkObject = GetComponentInParent<NetworkObject>();

        if (networkObject != null)
            playerTransform = networkObject.transform;

        pitch = 0f;

        // La sensibilidad se puede cambiar desde el menú de pausa.
        ConfiguracionesJuego.CambiosAplicados += ActualizarSensibilidad;

        // Arranca SIEMPRE bloqueado; se libera explícitamente después.
        enabled = false;
    }

    private void OnDestroy()
    {
        ConfiguracionesJuego.CambiosAplicados -= ActualizarSensibilidad;
    }

    private void ActualizarSensibilidad()
    {
        mouseSensitivity = ConfiguracionesJuego.ObtenerSensibilidad();
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

        // Pausa (y los primeros frames tras reanudar): no leer el mouse. Aunque otro sistema
        // (revivir, espectador) reactive este componente con el menú abierto, la cámara no gira.
        if (PauseController.MouseInputBlocked)
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

        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // El recoil de cámara (patada al disparar + shake) ya no se calcula
        // acá — lo maneja CameraRecoil.cs, que corre en LateUpdate sobre
        // este mismo cameraTransform y se suma encima de este valor base.
    }
}