using UnityEngine;
using Unity.Netcode;

public class MultiplayerPlayerSpawner : NetworkBehaviour
{
    [SerializeField] private Transform playerSpawn1;
    [SerializeField] private Transform playerSpawn2;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;

        Transform spawnPoint;

        if (playerCount == 1)
        {
            spawnPoint = playerSpawn1;
        }
        else
        {
            spawnPoint = playerSpawn2;
        }

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(
            clientId,
            out NetworkClient client))
        {
            if (client.PlayerObject != null)
            {
                client.PlayerObject.transform.SetPositionAndRotation(
                    spawnPoint.position,
                    spawnPoint.rotation
                );
            }
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}