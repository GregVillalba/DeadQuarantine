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

        // El Host ya está conectado cuando se registra el callback.
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(
            NetworkManager.ServerClientId,
            out NetworkClient hostClient))
        {
            AsignarSpawnIndex(
                NetworkManager.ServerClientId,
                hostClient
            );
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log(
            "[Spawner] Cliente conectado: " +
            clientId
        );

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(
            clientId,
            out NetworkClient client))
        {
            AsignarSpawnIndex(
                clientId,
                client
            );
        }
    }

    private void AsignarSpawnIndex(
        ulong clientId,
        NetworkClient client)
    {
        if (client.PlayerObject == null)
        {
            Debug.LogWarning(
                "[Spawner] PlayerObject todavía no existe para ClientId " +
                clientId
            );

            return;
        }

        int spawnIndex =
            (clientId == NetworkManager.ServerClientId) ? 0 : 1;

        MultiplayerPlayerSpawnAssigner assigner =
            client.PlayerObject.GetComponent<MultiplayerPlayerSpawnAssigner>();

        if (assigner != null)
        {
            assigner.AssignedSpawnIndex.Value = spawnIndex;
        }

        Debug.Log(
            "[Spawner] ClientId " +
            clientId +
            " asignado a SpawnIndex " +
            spawnIndex
        );
    }

    public Transform GetSpawnPoint(int index)
    {
        return index == 0 ? playerSpawn1 : playerSpawn2;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -=
                OnClientConnected;
        }
    }
}