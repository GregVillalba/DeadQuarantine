using UnityEngine;
using Unity.Netcode;

public class MultiplayerPlayerSpawner : NetworkBehaviour
{
    [Header("Spawns manuales de respaldo")]
    [SerializeField] private Transform playerSpawn1;
    [SerializeField] private Transform playerSpawn2;

    private ConfiguracionEscenarioJugable escenarioActivo;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        // Obtener el escenario seleccionado.
        GestorEscenariosPartida gestorEscenarios =
        FindAnyObjectByType<GestorEscenariosPartida>();

        if (gestorEscenarios != null)
        {
            escenarioActivo =
                gestorEscenarios.ObtenerEscenarioActivo();

            if (escenarioActivo != null)
            {
                Debug.Log(
                    "[Spawner] Escenario activo detectado: " +
                    escenarioActivo.TipoEscenario
                );
            }
            else
            {
                Debug.LogWarning(
                    "[Spawner] No se encontró un escenario activo."
                );
            }
        }
        else
        {
            Debug.LogWarning(
                "[Spawner] No se encontró GestorEscenariosPartida."
            );
        }

        NetworkManager.Singleton.OnClientConnectedCallback +=
            OnClientConnected;

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
            (clientId == NetworkManager.ServerClientId)
                ? 0
                : 1;

        MultiplayerPlayerSpawnAssigner assigner =
            client.PlayerObject.GetComponent<
                MultiplayerPlayerSpawnAssigner
            >();

        if (assigner != null)
        {
            assigner.AssignedSpawnIndex.Value =
                spawnIndex;
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
        // =====================================================
        // USAR LOS SPAWNS DEL ESCENARIO ACTIVO
        // =====================================================

        if (escenarioActivo != null)
        {
            Transform spawnJugador =
                escenarioActivo.SpawnJugador;

            if (spawnJugador != null)
            {
                return spawnJugador;
            }
        }

        // =====================================================
        // RESPALDO: SPAWNS MANUALES
        // =====================================================

        Debug.LogWarning(
            "[Spawner] No se pudo obtener el SpawnJugador " +
            "del escenario activo. Se utilizará el spawn manual."
        );

        return index == 0
            ? playerSpawn1
            : playerSpawn2;
    }

    public override void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -=
                OnClientConnected;
        }

        base.OnDestroy();
    }
}