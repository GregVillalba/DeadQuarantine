
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Unity.Netcode;

public class EasterEggInteractionUI : MonoBehaviour
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
        playerNetworkObject =
            GetComponentInParent<NetworkObject>();

        controls = new PlayerControls();

        ConfiguracionesJuego.CargarRebinds(controls.asset);

        BuscarCamara();

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
        // En multiplayer, solo actúa el jugador propietario.
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            if (playerNetworkObject == null ||
                !playerNetworkObject.IsOwner)
            {
                Ocultar();
                return;
            }
        }

        if (playerCamera == null ||
            !playerCamera.isActiveAndEnabled)
        {
            BuscarCamara();
        }

        if (playerCamera == null)
        {
            Ocultar();
            return;
        }

        BuscarEasterEgg();
    }

    // =========================================================
    // BUSCAR CÁMARA DEL JUGADOR
    // =========================================================

    private void BuscarCamara()
    {
        playerCamera = null;

        Transform root = transform.root;

        Camera[] cameras =
            root.GetComponentsInChildren<Camera>(true);

        foreach (Camera cam in cameras)
        {
            if (!cam.isActiveAndEnabled)
                continue;

            if (cam.gameObject.name != "PlayerCamera")
                continue;

            NetworkObject networkObject =
                cam.GetComponentInParent<NetworkObject>();

            if (networkObject != null)
            {
                if (networkObject.IsSpawned &&
                    networkObject.IsOwner)
                {
                    playerCamera = cam;
                    return;
                }
            }
            else if (cam == Camera.main)
            {
                // Singleplayer
                playerCamera = cam;
                return;
            }
        }

        // Fallback para singleplayer.
        if (Camera.main != null &&
            Camera.main.isActiveAndEnabled &&
            Camera.main.gameObject.name == "PlayerCamera")
        {
            NetworkObject networkObject =
                Camera.main.GetComponentInParent<NetworkObject>();

            if (networkObject == null ||
                (networkObject.IsSpawned && networkObject.IsOwner))
            {
                playerCamera = Camera.main;
            }
        }
    }

    // =========================================================
    // BUSCAR EASTER EGG
    // =========================================================

    private void BuscarEasterEgg()
{
    Ray ray = new Ray(
        playerCamera.transform.position,
        playerCamera.transform.forward
    );

    if (Physics.Raycast(
        ray,
        out RaycastHit hit,
        interactRange,
        Physics.DefaultRaycastLayers,
        QueryTriggerInteraction.Collide))
    {
        EasterEgg egg = hit.collider.GetComponentInParent<EasterEgg>();

        if (egg != null && egg.PuedeMostrarCartel())
        {
            Mostrar();
            return;
        }
    }

    Ocultar();
}

    // =========================================================
    // MOSTRAR
    // =========================================================

    private void Mostrar()
    {
        if (interactPrompt == null)
            return;

        if (interactText != null)
        {
            string tecla =
                controls.Player.Interact.GetBindingDisplayString();

            interactText.text = tecla + " para interactuar";
        }

        if (!interactPrompt.activeSelf)
            interactPrompt.SetActive(true);
    }

    // =========================================================
    // OCULTAR
    // =========================================================

    private void Ocultar()
    {
        if (interactPrompt != null &&
            interactPrompt.activeSelf)
        {
            interactPrompt.SetActive(false);
        }
    }
}