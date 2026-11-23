
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
        if (controls != null)
        {
            controls.Disable();
            controls.Dispose();
            controls = null;
        }
    }

    private void Update()
    {
        if (PlayerInputGate.ActionsBlocked)
        {
            Ocultar();
            return;
        }

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

        EasterEgg egg = BuscarEasterEgg();
        if (egg == null)
        {
            Ocultar();
            return;
        }

        if (controls.Player.Interact.WasPressedThisFrame())
        {
            egg.ActivarPorInteraccion();
            if (!egg.PuedeMostrarCartel())
            {
                Ocultar();
                return;
            }
        }

        Mostrar();
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

    private EasterEgg BuscarEasterEgg()
    {
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            interactRange,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // La cámara puede estar detrás de colliders del propio jugador.
            if (hit.collider.transform.IsChildOf(playerCamera.transform.root))
                continue;

            EasterEgg egg = hit.collider.GetComponentInParent<EasterEgg>();
            if (egg != null)
            {
                if (hit.distance <= egg.InteractionDistance && egg.PuedeMostrarCartel())
                    return egg;

                if (hit.collider.isTrigger)
                    continue;

                break;
            }

            // Triggers de volumen, sonido o navegación no deben tapar el
            // objeto. Los colliders sólidos sí bloquean la interacción.
            if (hit.collider.isTrigger)
                continue;

            break;
        }

        return null;
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
