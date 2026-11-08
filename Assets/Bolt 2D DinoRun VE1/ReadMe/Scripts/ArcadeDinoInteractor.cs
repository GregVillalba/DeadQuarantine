using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Va en el Player (junto a SlotMachineInteractor y NPCShopUI). Detecta la máquina arcade con un raycast
/// desde la cámara, muestra el cartel "E para jugar" y, al presionar Interactuar, abre el minijuego.
/// El cartel está en el prefab del jugador, así que la máquina no necesita referenciarlo.
/// </summary>
public class ArcadeInteractor : MonoBehaviour
{
    [Header("Cartel (duplicá 'Interface (1)' dentro de InteractionsUI)")]
    [Tooltip("Tiene que ser un objeto DISTINTO al de NPCShopUI y al de SlotMachineInteractor, porque esos scripts lo ocultan en cada frame.")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private TextMeshProUGUI interactText;
    [SerializeField] private float interactRange = 3f;

    [Header("Textos")]
    [SerializeField] private string textoJugar = "para jugar";

    [Header("Diagnóstico")]
    [Tooltip("Muestra en la consola qué está mirando el jugador. Desactivalo cuando todo ande.")]
    [SerializeField] private bool mostrarDebug = true;
    private string ultimoDebug;

    private PlayerControls controls;
    private Camera playerCamera;
    private NetworkObject networkObject;

    private void Awake()
    {
        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);

        networkObject = GetComponentInParent<NetworkObject>();

        BuscarCamara();
        OcultarPrompt();
    }

    private void Start()
    {
        Diag("Activo en '" + name + "'. Cartel asignado: " + (interactPrompt != null) + ", texto asignado: " + (interactText != null));
    }

    private void OnEnable()
    {
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();
        OcultarPrompt();
    }

    private void Update()
    {
        // En multiplayer, la copia del jugador remoto no maneja UI.
        if (networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner)
            return;

        // Con el minijuego abierto o la tienda abierta no se interactúa.
        if (MinijuegoArcadeTrigger.MinijuegoEnCurso || NPCShopUI.LocalShopOpen)
        {
            OcultarPrompt();
            return;
        }

        if (playerCamera == null)
        {
            BuscarCamara();

            if (playerCamera == null)
            {
                Diag("No se encontró la cámara del jugador");
                OcultarPrompt();
                return;
            }
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        bool golpeo = Physics.Raycast(ray, out RaycastHit hit, interactRange, ~0, QueryTriggerInteraction.Ignore);

        if (!golpeo)
            Diag("El rayo no golpea nada a menos de " + interactRange + " m");

        if (golpeo)
        {
            MinijuegoArcadeTrigger arcade = hit.collider.GetComponentInParent<MinijuegoArcadeTrigger>();

            Diag("El rayo golpea '" + hit.collider.name + "' a " + hit.distance.ToString("0.0") + " m. Script del arcade encontrado: " + (arcade != null));

            if (arcade != null && !arcade.Ocupado)
            {
                MostrarPrompt();

                if (controls.Player.Interact.triggered)
                    arcade.Jugar(transform.root.gameObject);

                return;
            }
        }

        OcultarPrompt();
    }

    private void MostrarPrompt()
    {
        if (interactPrompt == null)
            return;

        if (interactText != null)
            interactText.text = controls.Player.Interact.GetBindingDisplayString() + " " + textoJugar;

        interactPrompt.SetActive(true);
    }

    private void OcultarPrompt()
    {
        if (interactPrompt != null && interactPrompt.activeSelf)
            interactPrompt.SetActive(false);
    }

    // Escribe en la consola solo cuando el mensaje cambia, para no llenarla
    private void Diag(string mensaje)
    {
        if (!mostrarDebug || mensaje == ultimoDebug)
            return;

        ultimoDebug = mensaje;
        Debug.Log("[ArcadeInteractor] " + mensaje, this);
    }

    private void BuscarCamara()
    {
        Camera[] cameras = transform.root.GetComponentsInChildren<Camera>(true);

        foreach (Camera cam in cameras)
        {
            NetworkObject camNetworkObject = cam.GetComponentInParent<NetworkObject>();

            if (camNetworkObject != null)
            {
                if (camNetworkObject.IsOwner)
                {
                    playerCamera = cam;
                    return;
                }
            }
            else if (cam == Camera.main)
            {
                playerCamera = cam;
                return;
            }
        }
    }
}