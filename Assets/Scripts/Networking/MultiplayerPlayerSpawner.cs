using UnityEngine;
using Unity.Netcode;

public class MultiplayerPlayerSpawner : NetworkBehaviour
{
    [Header("Jugadores")]
    [Tooltip("Cantidad máxima de jugadores (y de lugares de spawn). El host siempre ocupa el 0.")]
    [SerializeField] private int maxPlayers = 4;

    [Header("Spawns manuales de respaldo")]
    [SerializeField] private Transform playerSpawn1;
    [SerializeField] private Transform playerSpawn2;
    [SerializeField] private Transform playerSpawn3;
    [SerializeField] private Transform playerSpawn4;

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

        MultiplayerPlayerSpawnAssigner assigner =
            client.PlayerObject.GetComponent<
                MultiplayerPlayerSpawnAssigner
            >();

        if (assigner == null)
            return;

        // Si ya tiene lugar, no se lo cambia (podría pisar el de otro jugador).
        if (assigner.AssignedSpawnIndex.Value >= 0)
            return;

        int spawnIndex =
            GetFreeSpawnIndex(clientId);

        assigner.AssignedSpawnIndex.Value =
            spawnIndex;

        Debug.Log(
            "[Spawner] ClientId " +
            clientId +
            " asignado a SpawnIndex " +
            spawnIndex
        );
    }

    // El host siempre toma el 0. Los demás toman el lugar libre más bajo (1, 2, 3...).
    // Se calcula con los jugadores conectados, así si alguien se va su lugar se reutiliza.
    private int GetFreeSpawnIndex(ulong clientId)
    {
        if (clientId == NetworkManager.ServerClientId)
            return 0;

        int slots = Mathf.Max(2, maxPlayers);
        bool[] used = new bool[slots];

        // El 0 queda reservado para el host aunque todavía no haya aparecido.
        used[0] = true;

        foreach (NetworkClient other in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (other.ClientId == clientId || other.PlayerObject == null)
                continue;

            MultiplayerPlayerSpawnAssigner otherAssigner =
                other.PlayerObject.GetComponent<
                    MultiplayerPlayerSpawnAssigner
                >();

            if (otherAssigner == null)
                continue;

            int index = otherAssigner.AssignedSpawnIndex.Value;

            if (index >= 0 && index < slots)
                used[index] = true;
        }

        for (int i = 1; i < slots; i++)
        {
            if (!used[i])
                return i;
        }

        // Más jugadores que lugares: se repite uno antes que dejar a alguien sin spawn.
        Debug.LogWarning(
            "[Spawner] No quedan lugares libres (Max Players = " + slots +
            "). Se repite un spawn para ClientId " + clientId
        );

        return 1 + (int)(clientId % (ulong)(slots - 1));
    }

    public Transform GetSpawnPoint(int index)
    {
        var gestor = FindAnyObjectByType<GestorEscenariosPartida>();
        if (gestor != null)
        {
            escenarioActivo = gestor.ObtenerEscenarioActivo();
            return escenarioActivo != null ? escenarioActivo.ObtenerSpawnJugador(index) : null;
        }
        return GetBackupSpawn(index);
    }

    private Transform GetBackupSpawn(int index)
    {
        Transform[] backups =
        {
            playerSpawn1,
            playerSpawn2,
            playerSpawn3,
            playerSpawn4
        };

        if (index >= 0 && index < backups.Length && backups[index] != null)
            return backups[index];

        // Falta el punto de ese lugar: se usa el primero que exista para que nadie quede sin spawn.
        for (int i = 0; i < backups.Length; i++)
        {
            if (backups[i] != null)
                return backups[i];
        }

        return null;
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