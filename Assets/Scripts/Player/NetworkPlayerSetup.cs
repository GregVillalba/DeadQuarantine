using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerSetup : NetworkBehaviour
{
    [Header("Primera Persona")]
    [SerializeField] private Camera weaponCamera;
    [SerializeField] private GameObject fpsArms;

    [Header("Tercera Persona")]
    [SerializeField] private GameObject polygonFPS;

    [Header("Cámara del jugador")]
    [SerializeField] private Camera playerCamera;

    public override void OnNetworkSpawn()
    {
        Debug.Log(
            "[NetworkPlayerSetup] Player spawned. IsOwner = " + IsOwner
        );

        if (IsOwner)
        {
            playerCamera.gameObject.SetActive(true);
            weaponCamera.gameObject.SetActive(true);

            fpsArms.SetActive(true);
            polygonFPS.SetActive(false);
        }
        else
        {
            playerCamera.gameObject.SetActive(false);
            weaponCamera.gameObject.SetActive(false);

            fpsArms.SetActive(false);
            polygonFPS.SetActive(true);
        }
    }
}