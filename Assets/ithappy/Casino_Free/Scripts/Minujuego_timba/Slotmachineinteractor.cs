using ithappy.Casino;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Va en el Player (junto a NPCShopUI). Detecta la máquina tragamonedas con un raycast desde la cámara,
/// muestra el cartel "E para jugar, te quedan N chances" y, al presionar Interactuar, hace girar la máquina.
/// </summary>
public class SlotMachineInteractor : MonoBehaviour
{
    [Header("Cartel (duplicá 'Interface (1)' dentro de InteractionsUI)")]
    [Tooltip("Objeto del cartel. Tiene que ser DISTINTO al que usa NPCShopUI, porque esa tienda lo oculta en cada frame.")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private TextMeshProUGUI interactText;
    [SerializeField] private float interactRange = 3f;

    [Header("Textos")]
    [SerializeField] private string textoJugar = "para jugar, te quedan";
    [SerializeField] private string textoChanceSingular = "chance";
    [SerializeField] private string textoChancePlural = "chances";
    [SerializeField] private string textoSinChances = "La máquina no tiene más chances";

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

        // Con la tienda abierta no se interactúa con la máquina.
        if (NPCShopUI.LocalShopOpen)
        {
            OcultarPrompt();
            return;
        }

        if (playerCamera == null)
        {
            BuscarCamara();

            if (playerCamera == null)
            {
                OcultarPrompt();
                return;
            }
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, ~0, QueryTriggerInteraction.Ignore))
        {
            SlotMachineJugable machine = hit.collider.GetComponentInParent<SlotMachineJugable>();

            if (machine != null)
            {
                // Mientras gira no hay cartel ni se puede volver a jugar.
                if (machine.IsSpinning)
                {
                    OcultarPrompt();
                    return;
                }

                MostrarPrompt(machine);

                if (controls.Player.Interact.triggered)
                    machine.TryPlay();

                return;
            }
        }

        OcultarPrompt();
    }

    private void MostrarPrompt(SlotMachineJugable machine)
    {
        if (interactPrompt == null)
            return;

        if (interactText != null)
        {
            int chances = machine.ChancesLeft;

            interactText.text = chances > 0
                ? controls.Player.Interact.GetBindingDisplayString() + " " + textoJugar + " " + chances + " " +
                  (chances == 1 ? textoChanceSingular : textoChancePlural)
                : textoSinChances;
        }

        interactPrompt.SetActive(true);
    }

    private void OcultarPrompt()
    {
        if (interactPrompt != null && interactPrompt.activeSelf)
            interactPrompt.SetActive(false);
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