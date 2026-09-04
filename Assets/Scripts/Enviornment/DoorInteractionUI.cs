using UnityEngine;
using TMPro;
using Unity.Netcode;

public class DoorInteractionUI : MonoBehaviour
{
    [Header("Interfaz")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private TextMeshProUGUI interactText;

    [Header("Configuración")]
    [SerializeField] private float interactRange = 3f;

    private Camera playerCamera;
    private NetworkObject playerNetworkObject;

    private void Awake()
    {
        playerNetworkObject =
            GetComponentInParent<NetworkObject>();

        BuscarCamara();

        Ocultar();
    }

    private void Update()
    {
        if (playerNetworkObject != null)
        {
            if (!playerNetworkObject.IsOwner)
            {
                Ocultar();
                return;
            }
        }

        if (playerCamera == null)
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

    // =========================================================
    // BUSCAR CÁMARA DEL JUGADOR
    // =========================================================

    private void BuscarCamara()
    {
        Transform root =
            transform.root;

        Camera[] cameras =
            root.GetComponentsInChildren<Camera>(
                true
            );

        foreach (Camera cam in cameras)
        {
            NetworkObject networkObject =
                cam.GetComponentInParent<NetworkObject>();

            if (networkObject != null)
            {
                if (networkObject.IsOwner)
                {
                    playerCamera = cam;
                    return;
                }
            }
            else
            {
                // Singleplayer
                if (cam == Camera.main)
                {
                    playerCamera = cam;
                    return;
                }
            }
        }
    }

    // =========================================================
    // BUSCAR PUERTA
    // =========================================================

    private void BuscarPuerta()
    {
        Ray ray =
            new Ray(
                playerCamera.transform.position,
                playerCamera.transform.forward
            );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactRange
        ))
        {
            Door door =
                hit.collider.GetComponentInParent<Door>();

            if (door != null)
            {
                Mostrar(
                    door.IsOpen
                );

                return;
            }
        }

        Ocultar();
    }

    // =========================================================
    // MOSTRAR
    // =========================================================

    private void Mostrar(bool puertaAbierta)
    {
        if (interactPrompt == null)
            return;

        if (interactText != null)
        {
            interactText.text =
                puertaAbierta
                    ? "E para cerrar"
                    : "E para abrir";
        }

        interactPrompt.SetActive(true);
    }

    // =========================================================
    // OCULTAR
    // =========================================================

    private void Ocultar()
    {
        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }
    }
}