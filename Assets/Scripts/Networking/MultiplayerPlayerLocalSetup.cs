using UnityEngine;
using Unity.Netcode;

public class MultiplayerLocalPlayerSetup : NetworkBehaviour
{
    [Header("Cámaras")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera weaponCamera;

    [Header("Audio")]
    [SerializeField] private AudioListener audioListener;

    [Header("Cuerpo")]
    [SerializeField] private GameObject playerBody;

    [Header("Layers")]
    [SerializeField] private string localBodyLayer = "LocalPlayerBody";
    [SerializeField] private string remoteBodyLayer = "ThirdPersonBody";

    public override void OnNetworkSpawn()
    {
        if (NetworkManager.Singleton == null)
            return;

        bool esMiJugador =
            OwnerClientId ==
            NetworkManager.Singleton.LocalClientId;

        Debug.Log(
            "[LOCAL PLAYER] " +
            gameObject.name +
            " | OwnerClientId=" +
            OwnerClientId +
            " | LocalClientId=" +
            NetworkManager.Singleton.LocalClientId +
            " | ES_MIO=" +
            esMiJugador
        );

        // =========================================================
        // CUERPO
        // =========================================================

        if (playerBody != null)
        {
            int layer;

            if (esMiJugador)
            {
                layer = LayerMask.NameToLayer(localBodyLayer);
            }
            else
            {
                layer = LayerMask.NameToLayer(remoteBodyLayer);
            }

            if (layer != -1)
            {
                CambiarLayerRecursivo(
                    playerBody,
                    layer
                );
            }
        }

        // =========================================================
        // CÁMARA DEL JUGADOR
        // =========================================================

        if (playerCamera != null)
            playerCamera.gameObject.SetActive(esMiJugador);

        // =========================================================
        // CÁMARA DEL ARMA
        // =========================================================

        if (weaponCamera != null)
            weaponCamera.gameObject.SetActive(esMiJugador);

        // =========================================================
        // AUDIO
        // =========================================================

        if (audioListener != null)
            audioListener.enabled = esMiJugador;
    }

    private void CambiarLayerRecursivo(
        GameObject objeto,
        int layer
    )
    {
        objeto.layer = layer;

        foreach (Transform hijo in objeto.transform)
        {
            CambiarLayerRecursivo(
                hijo.gameObject,
                layer
            );
        }
    }
}