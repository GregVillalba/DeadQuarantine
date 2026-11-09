
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;

public class MultiplayerPlayerSpawnAssigner : NetworkBehaviour
{
    public NetworkVariable<int> AssignedSpawnIndex =
        new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<bool> IsAtSpawn =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    public NetworkVariable<bool> SpawnCheckpointActivo =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    /// <summary>
    /// Número de jugador (1 a 4) según su lugar de spawn.
    /// 0 mientras el servidor no le asignó uno.
    /// </summary>
    public int PlayerNumber => AssignedSpawnIndex.Value + 1;

    private float proximoReintento;

    // Solo servidor: si en este tiempo ningún spawner le dio lugar, se lo asigna el propio jugador.
    private const float EsperaAsignacionPropia = 1f;
    private float momentoSpawn;

    private void Update()
    {
        if (!IsSpawned)
            return;

        AsignarIndiceSiNadieLoHizo();

        if (!IsOwner || IsAtSpawn.Value)
            return;

        // No utilizar el spawn normal mientras se carga un checkpoint.
        if (HayCheckpointPendiente() || SpawnCheckpointActivo.Value)
            return;

        if (AssignedSpawnIndex.Value < 0)
            return;

        if (Time.unscaledTime < proximoReintento)
            return;

        proximoReintento = Time.unscaledTime + 0.5f;

        MoveToAssignedSpawn(AssignedSpawnIndex.Value);
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log(
            "[SpawnAssigner] OnNetworkSpawn | IsOwner=" + IsOwner +
            " | IsServer=" + IsServer +
            " | Index=" + AssignedSpawnIndex.Value
        );

        AssignedSpawnIndex.OnValueChanged += OnSpawnIndexChanged;
        SceneManager.activeSceneChanged += OnEscenaActivaCambiada;
        momentoSpawn = Time.unscaledTime;

        if (
            IsOwner &&
            AssignedSpawnIndex.Value != -1 &&
            !HayCheckpointPendiente() &&
            !SpawnCheckpointActivo.Value
        )
        {
            MoveToAssignedSpawn(AssignedSpawnIndex.Value);
        }
    }

    private void OnSpawnIndexChanged(int previousValue, int newValue)
    {
        Debug.Log(
            "[SpawnAssigner] Indice cambio " + previousValue + " -> " + newValue +
            " | IsOwner=" + IsOwner
        );

        if (!IsOwner)
            return;

        if (newValue == -1)
            return;

        if (HayCheckpointPendiente() || SpawnCheckpointActivo.Value)
            return;

        MoveToAssignedSpawn(newValue);
    }

    // Los spawners viven dentro de cada escenario y solo asigna lugar el del escenario activo.
    // Si ese escenario no tiene spawner (ej: Escenario_2), nadie lo haría y el jugador quedaría
    // donde lo creó Netcode, fuera del mapa: en ese caso el servidor le asigna el lugar acá.
    private void AsignarIndiceSiNadieLoHizo()
    {
        if (!IsServer || AssignedSpawnIndex.Value >= 0)
            return;

        if (Time.unscaledTime < momentoSpawn + EsperaAsignacionPropia)
            return;

        // Solo en la escena de juego con el escenario ya elegido: en el lobby (multijugador) no hay
        // escenario jugable y el lugar lo asigna el spawner al cargar la partida.
        if (EscenarioActivo() == null)
            return;

        if (MultiplayerPlayerSpawner.BuscarDelEscenarioActivo() != null)
            return;

        MultiplayerPlayerSpawner cualquiera = FindAnyObjectByType<MultiplayerPlayerSpawner>();
        int maxJugadores = cualquiera != null ? cualquiera.MaxPlayers : 4;

        AssignedSpawnIndex.Value =
            MultiplayerPlayerSpawner.CalcularIndiceLibre(OwnerClientId, maxJugadores);

        Debug.Log(
            "[SpawnAssigner] El escenario activo no tiene spawner: ClientId " + OwnerClientId +
            " asignado a SpawnIndex " + AssignedSpawnIndex.Value
        );
    }

    // Escenario que se está jugando. null en el lobby, en el tutorial o mientras la partida lo elige.
    private static ConfiguracionEscenarioJugable EscenarioActivo()
    {
        GestorEscenariosPartida gestor = GestorEscenariosPartida.Instance;
        return gestor != null ? gestor.ObtenerEscenarioActivo() : null;
    }

    // Punto de spawn del escenario que se está jugando (no el de un escenario oculto).
    private static Transform ObtenerPuntoSpawn(int index)
    {
        // Spawner del escenario activo (o el del tutorial).
        MultiplayerPlayerSpawner spawner =
            MultiplayerPlayerSpawner.BuscarDelEscenarioActivo();

        if (spawner != null)
            return spawner.GetSpawnPoint(index);

        // Escenario activo sin spawner propio (ej: Escenario_2): su punto directo.
        // Sin escenario activo (lobby, o la partida todavía eligiendo) no hay dónde ir: se
        // reintenta más tarde (Update cada 0.5 s). Nunca se usa el spawner de un escenario
        // oculto o del fondo del menú: sus colliders están apagados y el jugador cae al vacío.
        ConfiguracionEscenarioJugable activo = EscenarioActivo();
        return activo != null ? activo.ObtenerSpawnJugador(index) : null;
    }

    // =========================================================
    // COMPROBAR SI ESTÁ EN SU SPAWN
    // =========================================================

    public bool IsAtAssignedSpawn(float tolerance = 0.5f)
    {
        if (AssignedSpawnIndex.Value < 0)
            return false;

        Transform spawnPoint =
            ObtenerPuntoSpawn(AssignedSpawnIndex.Value);

        if (spawnPoint == null)
            return false;

        return Vector3.Distance(
            transform.position,
            spawnPoint.position
        ) <= tolerance;
    }

    // =========================================================
    // RESPAWN AL SPAWN ORIGINAL
    // =========================================================

    public void RespawnAtAssignedSpawn()
    {
        if (!IsServer)
            return;

        int index = AssignedSpawnIndex.Value;

        if (index < 0)
        {
            Debug.LogWarning(
                "[SpawnAssigner] No hay spawn asignado."
            );

            return;
        }

        if (IsOwner)
        {
            IsAtSpawn.Value = false;
            MoveToAssignedSpawn(index);
        }
        else
        {
            RespawnOwnerClientRpc();
        }
    }

    [ClientRpc]
    private void RespawnOwnerClientRpc()
    {
        if (!IsOwner)
            return;

        IsAtSpawn.Value = false;

        MoveToAssignedSpawn(AssignedSpawnIndex.Value);
    }

    // =========================================================
    // MOVER AL SPAWN NORMAL
    // =========================================================

    private void MoveToAssignedSpawn(int index)
    {
        GestorEscenariosPartida gestor = GestorEscenariosPartida.Instance;
        if (gestor != null && gestor.UsaEscenasAditivasMultiplayer && !gestor.MapaMultiplayerListo)
            return;

        if (HayCheckpointPendiente() || SpawnCheckpointActivo.Value)
            return;

        Transform spawnPoint = ObtenerPuntoSpawn(index);

        if (spawnPoint == null)
        {
            Debug.LogWarning(
                "[SpawnAssigner] SpawnPoint nulo para índice " + index
            );

            return;
        }

        if (!IsOwner)
            return;

        NetworkTransform networkTransform =
            GetComponent<NetworkTransform>();

        CharacterController cc =
            GetComponent<CharacterController>();

        if (cc != null)
            cc.enabled = false;

        transform.SetPositionAndRotation(
            spawnPoint.position,
            spawnPoint.rotation
        );

        if (cc != null)
            cc.enabled = true;

        if (networkTransform != null && networkTransform.IsSpawned)
        {
            networkTransform.Teleport(
                spawnPoint.position,
                spawnPoint.rotation,
                transform.localScale
            );
        }

        Debug.Log(
            "[SpawnAssigner] Jugador movido a SpawnIndex " + index
        );

        IsAtSpawn.Value = true;
    }

    // =========================================================
    // APLICAR SPAWN DE CHECKPOINT
    // =========================================================

    public void AplicarSpawnCheckpoint(
        Vector3 posicion,
        Quaternion rotacion
    )
    {
        if (!IsServer || !IsSpawned)
            return;

        // Impide que el spawn normal vuelva a ejecutarse.
        SpawnCheckpointActivo.Value = true;

        AplicarSpawnCheckpointClientRpc(
            posicion,
            rotacion
        );
    }

    [ClientRpc]
    private void AplicarSpawnCheckpointClientRpc(
        Vector3 posicion,
        Quaternion rotacion
    )
    {
        if (!IsOwner)
            return;

        CharacterController cc =
            GetComponent<CharacterController>();

        if (cc != null)
            cc.enabled = false;

        transform.SetPositionAndRotation(
            posicion,
            rotacion
        );

        if (cc != null)
            cc.enabled = true;

        NetworkTransform networkTransform =
            GetComponent<NetworkTransform>();

        if (networkTransform != null && networkTransform.IsSpawned)
        {
            networkTransform.Teleport(
                posicion,
                rotacion,
                transform.localScale
            );
        }

        IsAtSpawn.Value = true;

        Debug.Log(
            "[SpawnAssigner] Jugador trasladado al checkpoint."
        );
    }

    public void SolicitarMapaMultiplayer(string sceneName)
    {
        if (!IsOwner || !IsSpawned) return;
        SolicitarMapaMultiplayerServerRpc(sceneName);
    }

    [ServerRpc]
    private void SolicitarMapaMultiplayerServerRpc(string sceneName)
    {
        GestorEscenariosPartida gestor = GestorEscenariosPartida.Instance;
        if (gestor != null) gestor.SolicitarMapaDesdeRpc(sceneName);
    }

    public void AplicarSpawnDeEscenario(Vector3 posicion, Quaternion rotacion)
    {
        if (!IsServer || !IsSpawned) return;
        SpawnCheckpointActivo.Value = false;
        AplicarSpawnEscenarioClientRpc(posicion, rotacion);
    }

    public void ColocarJugadorLocalEnEscenario(Transform spawn)
    {
        if (!IsSpawned || !IsOwner || spawn == null)
            return;

        IsAtSpawn.Value = false;
        AplicarTeletransporteLocal(spawn.position, spawn.rotation);
        IsAtSpawn.Value = true;
    }

    [ClientRpc]
    private void AplicarSpawnEscenarioClientRpc(Vector3 posicion, Quaternion rotacion)
    {
        if (!IsOwner) return;
        IsAtSpawn.Value = false;
        AplicarTeletransporteLocal(posicion, rotacion);
        IsAtSpawn.Value = true;
    }

    private void AplicarTeletransporteLocal(Vector3 posicion, Quaternion rotacion)
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.SetPositionAndRotation(posicion, rotacion);
        if (cc != null) cc.enabled = true;
        NetworkTransform nt = GetComponent<NetworkTransform>();
        if (nt != null && nt.IsSpawned)
            nt.Teleport(posicion, rotacion, transform.localScale);
    }
    // =========================================================
    // CHECKPOINT PENDIENTE
    // =========================================================

    private bool HayCheckpointPendiente()
    {
        return CheckpointManager.Instance != null &&
               CheckpointManager.Instance.CargaPendiente;
    }

    // =========================================================
    // DESPAWN
    // =========================================================

    // El jugador de multijugador se crea en el lobby y pasa a la escena de juego: lo que haya
    // quedado de la escena anterior no sirve, hay que volver a ubicarlo en el spawn de la nueva.
    private void OnEscenaActivaCambiada(Scene anterior, Scene escena)
    {
        if (!IsSpawned || !IsOwner)
            return;

        IsAtSpawn.Value = false;
        proximoReintento = 0f;

        Debug.Log(
            "[SpawnAssigner] Escena '" + escena.name + "' cargada: se vuelve a ubicar al jugador."
        );
    }

    public override void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnEscenaActivaCambiada;
        base.OnDestroy();
    }

    public override void OnNetworkDespawn()
    {
        SceneManager.activeSceneChanged -= OnEscenaActivaCambiada;
        AssignedSpawnIndex.OnValueChanged -= OnSpawnIndexChanged;

        base.OnNetworkDespawn();
    }
}

