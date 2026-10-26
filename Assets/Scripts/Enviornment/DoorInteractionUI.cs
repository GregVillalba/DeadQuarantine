
using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class DoorInteractionUI : MonoBehaviour
{
    [Header("Interfaz")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private TextMeshProUGUI interactText;

    [Header("Configuración")]
    [SerializeField] private float interactRange = 3f;

    private Camera playerCamera;
    private NetworkObject playerNetworkObject;
    private PlayerControls controls;

    private void Awake()
    {
        playerNetworkObject = GetComponentInParent<NetworkObject>();

        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);

        Ocultar();
    }

    private void OnEnable()
    {
        if (controls != null)
            controls.Player.Enable();
    }

    private void OnDisable()
    {
        if (controls != null)
            controls.Player.Disable();

        Ocultar();
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }

    private void Update()
    {
        if (playerNetworkObject != null &&
            !playerNetworkObject.IsOwner)
        {
            Ocultar();
            return;
        }

        if (playerCamera == null || !playerCamera.isActiveAndEnabled)
        {
            BuscarCamara();

            if (playerCamera == null)
            {
                Ocultar();
                return;
            }
        }

        BuscarPuerta();
    }

private void BuscarCamara()
{
    playerCamera = null;

    // DoorInteractionUI está dentro de HUD_Canvas,
    // que es hijo directo de WeaponCamera.
    Transform posibleCamara = transform.parent;

    if (posibleCamara != null &&
        posibleCamara.name == "WeaponCamera")
    {
        Camera cam = posibleCamara.GetComponent<Camera>();

        if (cam != null && cam.isActiveAndEnabled)
        {
            NetworkObject networkObject =
                cam.GetComponentInParent<NetworkObject>();

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                if (networkObject != null &&
                    networkObject.IsSpawned &&
                    networkObject.IsOwner)
                {
                    playerCamera = cam;
                }
            }
            else
            {
                playerCamera = cam;
            }
        }
    }
}


    private void BuscarPuerta()
    {
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            Door door = hit.collider.GetComponentInParent<Door>();

            if (door != null && door.isActiveAndEnabled)
            {
                Mostrar(door.IsOpen);
                return;
            }
        }

        Ocultar();
    }
private void Mostrar(bool puertaAbierta)
{
    if (interactPrompt == null)
        return;

    if (interactText != null)
    {
        string tecla =
            InputActionRebindingExtensions.GetBindingDisplayString(
                controls.Player.Interact
            );

        interactText.text = puertaAbierta
            ? tecla + " para cerrar"
            : tecla + " para abrir";
    }

    if (!interactPrompt.activeSelf)
        interactPrompt.SetActive(true);
}

    private void Ocultar()
    {
        if (interactPrompt != null && interactPrompt.activeSelf)
            interactPrompt.SetActive(false);
    }
}
