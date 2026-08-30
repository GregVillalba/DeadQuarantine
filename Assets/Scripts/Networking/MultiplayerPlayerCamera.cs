using UnityEngine;
using Unity.Netcode;

public class MultiplayerPlayerCamera : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;

    public override void OnNetworkSpawn()
    {
        bool local = IsOwner;

        if (playerCamera != null)
            playerCamera.gameObject.SetActive(local);

        if (audioListener != null)
            audioListener.enabled = local;
    }
}