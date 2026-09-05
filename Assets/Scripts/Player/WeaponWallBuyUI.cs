using UnityEngine;
using TMPro;
using Unity.Netcode;

public class WeaponWallBuyUI : MonoBehaviour
{
    [Header("Interfaz")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private TextMeshProUGUI interactText;
    [SerializeField] private Color colorAlcanza = Color.white;
    [SerializeField] private Color colorNoAlcanza = Color.red;

    [Header("Configuración")]
    [SerializeField] private float interactRange = 3f;

    private Camera playerCamera;
    private NetworkObject playerNetworkObject;
    private PlayerScore playerScore;
    private WeaponSwitcher weaponSwitcher;

    private void Awake()
    {
        playerNetworkObject = GetComponentInParent<NetworkObject>();

        playerScore = transform.root.GetComponentInChildren<PlayerScore>(true);
        weaponSwitcher = transform.root.GetComponentInChildren<WeaponSwitcher>(true);

        BuscarCamara();
        Ocultar();
    }

    private void Update()
    {
        if (playerNetworkObject != null && !playerNetworkObject.IsOwner)
        {
            Ocultar();
            return;
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

        BuscarArmaEnPared();
    }

    // Idéntico a DoorInteractionUI.BuscarCamara().
    private void BuscarCamara()
    {
        Transform root = transform.root;
        Camera[] cameras = root.GetComponentsInChildren<Camera>(true);

        foreach (Camera cam in cameras)
        {
            NetworkObject networkObject = cam.GetComponentInParent<NetworkObject>();

            if (networkObject != null)
            {
                if (networkObject.IsOwner)
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

    private void BuscarArmaEnPared()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            WeaponWallBuy wallBuy = hit.collider.GetComponentInParent<WeaponWallBuy>();

            if (wallBuy != null)
            {
                bool yaComprada = weaponSwitcher != null &&
                                   weaponSwitcher.IsUnlocked(wallBuy.WeaponId);

                if (!yaComprada)
                {
                    Mostrar(wallBuy);
                    return;
                }
            }
        }

        Ocultar();
    }

    private void Mostrar(WeaponWallBuy wallBuy)
    {
        if (interactPrompt == null)
            return;

        if (interactText != null)
        {
            interactText.text = "E para comprar " + wallBuy.WeaponId + " - " + wallBuy.Cost;

            bool alcanza = playerScore == null ||
                           playerScore.ScoreNetwork.Value >= wallBuy.Cost;

            interactText.color = alcanza ? colorAlcanza : colorNoAlcanza;
        }

        interactPrompt.SetActive(true);
    }

    private void Ocultar()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }
}