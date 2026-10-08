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
       /* if (NetworkManager.Singleton.ConnectedClients.TryGetValue(
            NetworkManager.ServerClientId,
            out NetworkClient hostClient))
        {
            AsignarSpawnIndex(
                NetworkManager.ServerClientId,
                hostClient
            );
        }*/

        // Asigna lugar a todos los que ya estan conectados (host e invitados).
        foreach (NetworkClient conectado in NetworkManager.Singleton.ConnectedClientsList)
        {
            AsignarSpawnIndex(conectado.ClientId, conectado);
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
        // En el cliente el escenario no se guarda al arrancar (solo en el servidor),
        // asi que se pide al gestor de escenarios cuando hace falta.
        if (escenarioActivo == null)
        {
            GestorEscenariosPartida gestor =
                FindAnyObjectByType<GestorEscenariosPartida>();

            if (gestor != null)
                escenarioActivo = gestor.ObtenerEscenarioActivo();
        }

        // =====================================================
        // USAR LOS SPAWNS DEL ESCENARIO ACTIVO
        // =====================================================

     /*   if (escenarioActivo != null)
        {
            Transform spawnJugador =
                escenarioActivo.SpawnJugador;

            if (spawnJugador != null)
            {
                return spawnJugador;
            }
        }*/

        if (escenarioActivo != null)
        {
            Transform spawnJugador =
                escenarioActivo.ObtenerSpawnJugador(index);

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

    private void Update()
    {
        if (!IsServer || !IsSpawned || NetworkManager.Singleton == null)
            return;

        foreach (NetworkClient c in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (c.PlayerObject == null)
                continue;

            MultiplayerPlayerSpawnAssigner assigner =
                c.PlayerObject.GetComponent<MultiplayerPlayerSpawnAssigner>();

            if (assigner != null && assigner.AssignedSpawnIndex.Value == -1)
                AsignarSpawnIndex(c.ClientId, c);
        }
    }
}