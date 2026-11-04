using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// Spawner autoritativo del escenario activo.
///
/// IMPORTANTE:
/// - NO es NetworkBehaviour y NO necesita NetworkObject.
/// - Debe vivir dentro de cada escenario, junto a su ConfiguracionEscenarioJugable.
/// - Solo el servidor asigna los indices de spawn a los PlayerObjects que Netcode ya creo.
/// </summary>
public class MultiplayerPlayerSpawner : MonoBehaviour
{
    [Header("Jugadores")]
    [Tooltip("Cantidad maxima de jugadores. El host siempre ocupa el indice 0.")]
    [SerializeField] private int maxPlayers = 4;

    [Header("Spawns manuales de respaldo")]
    [SerializeField] private Transform playerSpawn1;
    [SerializeField] private Transform playerSpawn2;
    [SerializeField] private Transform playerSpawn3;
    [SerializeField] private Transform playerSpawn4;

    private ConfiguracionEscenarioJugable configuracionPropia;
    private ConfiguracionEscenarioJugable escenarioActivo;
    private bool registradoComoSpawnerActivo;

    private const string MultiplayerScene = "MainSceneMultiPlayer";
    private const string TutorialScene = "TutorialScene";

    public ConfiguracionEscenarioJugable ConfiguracionPropia => configuracionPropia;

    private void Awake()
    {
        ResolverConfiguracionPropia();
    }

    private void Start()
    {
        ActualizarEstadoSpawner();
    }

    private bool SoyServidor()
    {
        return NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsListening &&
               NetworkManager.Singleton.IsServer;
    }

    private void ResolverConfiguracionPropia()
    {
        // El SpawnPlayer y ConfiguracionEscenarioJugable viven dentro de la misma
        // raiz del escenario. Se busca hacia arriba y luego dentro de esa raiz.
        Transform raiz = transform.root;

        configuracionPropia = raiz != null
            ? raiz.GetComponentInChildren<ConfiguracionEscenarioJugable>(true)
            : null;

        if (configuracionPropia == null && !EsSpawnerTutorial())
        {
            Debug.LogWarning(
                "[Spawner] '" + gameObject.name +
                "' no pertenece a ningun ConfiguracionEscenarioJugable. " +
                "Fuera de TutorialScene se ignora: el spawn multiplayer debe vivir dentro del escenario."
            );
        }
    }

    private void ActualizarEstadoSpawner()
    {
        if (!SoyServidor())
        {
            Desregistrar();
            return;
        }

        if (configuracionPropia == null)
            ResolverConfiguracionPropia();

        bool tutorial = EsSpawnerTutorial();

        GestorEscenariosPartida gestor = tutorial
            ? null
            : FindAnyObjectByType<GestorEscenariosPartida>();

        escenarioActivo = gestor != null
            ? gestor.ObtenerEscenarioActivo()
            : null;

        bool deboSerActivo = tutorial ||
            (configuracionPropia != null &&
             escenarioActivo != null &&
             configuracionPropia == escenarioActivo);

        if (deboSerActivo && !registradoComoSpawnerActivo)
        {
            registradoComoSpawnerActivo = true;

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            Debug.Log(
                tutorial
                    ? "[Spawner] ACTIVO en TutorialScene | objeto=" + gameObject.name
                    : "[Spawner] ACTIVO desde escenario: " + configuracionPropia.TipoEscenario +
                      " | objeto=" + gameObject.name
            );

            AsignarTodosLosConectados();
        }
        else if (!deboSerActivo && registradoComoSpawnerActivo)
        {
            Desregistrar();
        }
    }

    private void Desregistrar()
    {
        if (!registradoComoSpawnerActivo)
            return;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }

        registradoComoSpawnerActivo = false;
    }

    public bool EsSpawnerDelEscenarioActivo()
    {
        if (EsSpawnerTutorial())
            return true;

        if (configuracionPropia == null)
            ResolverConfiguracionPropia();

        GestorEscenariosPartida gestor =
            FindAnyObjectByType<GestorEscenariosPartida>();

        ConfiguracionEscenarioJugable activo =
            gestor != null ? gestor.ObtenerEscenarioActivo() : null;

        return configuracionPropia != null &&
               activo != null &&
               configuracionPropia == activo;
    }

    private bool EsSpawnerTutorial()
    {
        Scene activa = SceneManager.GetActiveScene();

        return activa.IsValid() &&
               string.Equals(
                   activa.name,
                   TutorialScene,
                   System.StringComparison.OrdinalIgnoreCase
               ) &&
               gameObject.scene == activa;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!registradoComoSpawnerActivo || !SoyServidor())
            return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(
            clientId,
            out NetworkClient client))
        {
            AsignarSpawnIndex(clientId, client);
        }
    }

    private void AsignarTodosLosConectados()
    {
        if (!SoyServidor())
            return;

        foreach (NetworkClient client in
                 NetworkManager.Singleton.ConnectedClientsList)
        {
            AsignarSpawnIndex(client.ClientId, client);
        }
    }

    private void AsignarSpawnIndex(ulong clientId, NetworkClient client)
    {
        if (client == null || client.PlayerObject == null)
            return;

        MultiplayerPlayerSpawnAssigner assigner =
            client.PlayerObject.GetComponent<MultiplayerPlayerSpawnAssigner>();

        if (assigner == null)
        {
            Debug.LogWarning(
                "[Spawner] PlayerObject de ClientId " + clientId +
                " no tiene MultiplayerPlayerSpawnAssigner."
            );
            return;
        }

        if (assigner.AssignedSpawnIndex.Value >= 0)
            return;

        int spawnIndex = GetFreeSpawnIndex(clientId);
        assigner.AssignedSpawnIndex.Value = spawnIndex;

        Debug.Log(
            EsSpawnerTutorial()
                ? "[Spawner] ClientId " + clientId +
                  " asignado en TutorialScene a SpawnIndex " + spawnIndex
                : "[Spawner] ClientId " + clientId +
                  " asignado por " + configuracionPropia.TipoEscenario +
                  " a SpawnIndex " + spawnIndex
        );
    }

    private int GetFreeSpawnIndex(ulong clientId)
    {
        if (NetworkManager.Singleton != null &&
            clientId == NetworkManager.ServerClientId)
        {
            return 0;
        }

        int slots = Mathf.Max(2, maxPlayers);
        bool[] used = new bool[slots];
        used[0] = true;

        if (NetworkManager.Singleton != null)
        {
            foreach (NetworkClient other in
                     NetworkManager.Singleton.ConnectedClientsList)
            {
                if (other.ClientId == clientId || other.PlayerObject == null)
                    continue;

                MultiplayerPlayerSpawnAssigner otherAssigner =
                    other.PlayerObject.GetComponent<MultiplayerPlayerSpawnAssigner>();

                if (otherAssigner == null)
                    continue;

                int index = otherAssigner.AssignedSpawnIndex.Value;

                if (index >= 0 && index < slots)
                    used[index] = true;
            }
        }

        for (int i = 1; i < slots; i++)
        {
            if (!used[i])
                return i;
        }

        return 1 + (int)(clientId % (ulong)(slots - 1));
    }

    public Transform GetSpawnPoint(int index)
    {
        if (configuracionPropia == null)
            ResolverConfiguracionPropia();

        if (configuracionPropia != null)
        {
            Transform punto = configuracionPropia.ObtenerSpawnJugador(index);
            if (punto != null)
                return punto;
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

        for (int i = 0; i < backups.Length; i++)
        {
            if (backups[i] != null)
                return backups[i];
        }

        return null;
    }

    private void Update()
    {
        ActualizarEstadoSpawner();

        if (!registradoComoSpawnerActivo || !SoyServidor())
            return;

        // El PlayerObject del invitado puede quedar disponible algunos frames
        // despues del evento de conexion. Se reintenta hasta asignarlo.
        AsignarTodosLosConectados();
    }

    private void OnDestroy()
    {
        Desregistrar();
    }
}
