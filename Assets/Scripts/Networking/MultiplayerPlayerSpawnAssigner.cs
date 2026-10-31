using UnityEngine;
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
    public NetworkVariable<bool> IsAtSpawn = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    /// <summary>Número de jugador (1 a 4) según su lugar de spawn. 0 mientras el servidor no le asignó uno.</summary>
    public int PlayerNumber => AssignedSpawnIndex.Value + 1;

    private float proximoReintento;

    private void Update()
    {
        // Si soy el dueño y todavia no me ubique, reintento cada medio segundo.
        if (!IsSpawned || !IsOwner || IsAtSpawn.Value)
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

       /* Debug.Log(
            "[SpawnAssigner] OnNetworkSpawn | IsOwner=" + IsOwner +
            " | IsServer=" + IsServer +
            " | Index=" + AssignedSpawnIndex.Value +
            " | Escena=" + gameObject.scene.name
        );*/

        Debug.Log(
            "[SpawnAssigner] OnNetworkSpawn | IsOwner=" + IsOwner +
            " | IsServer=" + IsServer +
            " | Index=" + AssignedSpawnIndex.Value
        );


        AssignedSpawnIndex.OnValueChanged +=
            OnSpawnIndexChanged;

        if (
            IsOwner &&
            AssignedSpawnIndex.Value != -1
        )
        {
            MoveToAssignedSpawn(
                AssignedSpawnIndex.Value
            );
        }
    }

    private void OnSpawnIndexChanged(
        int previousValue,
        int newValue
    )
    {
       /* Debug.Log(
            "[SpawnAssigner] Indice cambio " + previousValue + " -> " + newValue +
            " | IsOwner=" + IsOwner
        );*/

        Debug.Log(
            "[SpawnAssigner] Indice cambio " + previousValue + " -> " + newValue +
            " | IsOwner=" + IsOwner
        );
        
        if (!IsOwner)
            return;

        if (newValue == -1)
            return;

        MoveToAssignedSpawn(newValue);
    }

    // =========================================================
    // COMPROBAR SI ESTÁ EN SU SPAWN
    // =========================================================

    public bool IsAtAssignedSpawn(
        float tolerance = 0.5f
    )
    {
        if (AssignedSpawnIndex.Value < 0)
            return false;

        MultiplayerPlayerSpawner spawner =
            FindFirstObjectByType<
                MultiplayerPlayerSpawner
            >();

        if (spawner == null)
            return false;

        Transform spawnPoint =
            spawner.GetSpawnPoint(
                AssignedSpawnIndex.Value
            );

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

        int index =
            AssignedSpawnIndex.Value;

        if (index < 0)
        {
            Debug.LogWarning(
                "[SpawnAssigner] No hay spawn asignado."
            );

            return;
        }

        if (IsOwner) { IsAtSpawn.Value = false; MoveToAssignedSpawn(index); }
        else RespawnOwnerClientRpc();
    }

    [ClientRpc]
    private void RespawnOwnerClientRpc()
    {
        if (!IsOwner) return;
        IsAtSpawn.Value = false;
        MoveToAssignedSpawn(AssignedSpawnIndex.Value);
    }

    // =========================================================
    // MOVER AL SPAWN
    // =========================================================

    private void MoveToAssignedSpawn(int index)
    {
        MultiplayerPlayerSpawner spawner =
            FindFirstObjectByType<
                MultiplayerPlayerSpawner
            >();

        if (spawner == null)
        {
            Debug.LogWarning(
                "[SpawnAssigner] No se encontró MultiplayerPlayerSpawner en la escena."
            );

            return;
        }

        Transform spawnPoint =
            spawner.GetSpawnPoint(index);

        if (spawnPoint == null)
        {
            Debug.LogWarning(
                "[SpawnAssigner] SpawnPoint nulo para índice " +
                index
            );

            return;
        }

        // Player NetworkTransform is owner-authoritative. Its cached state must
        // be teleported too, otherwise the first network tick can restore the old position.
        var networkTransform = GetComponent<NetworkTransform>();
        if (!IsOwner) return;
        CharacterController cc =
            GetComponent<CharacterController>();

        if (cc != null)
        {
            cc.enabled = false;

            transform.SetPositionAndRotation(
                spawnPoint.position,
                spawnPoint.rotation
            );

            cc.enabled = true;
        }
        else
        {
            transform.SetPositionAndRotation(
                spawnPoint.position,
                spawnPoint.rotation
            );
        }

        if (networkTransform && networkTransform.IsSpawned)
            networkTransform.Teleport(spawnPoint.position, spawnPoint.rotation, transform.localScale);

        Debug.Log(
            "[SpawnAssigner] Jugador movido a SpawnIndex " +
            index
        );
        if (IsOwner)
            IsAtSpawn.Value = true;
    }

    public override void OnNetworkDespawn()
    {
        AssignedSpawnIndex.OnValueChanged -=
            OnSpawnIndexChanged;
        base.OnNetworkDespawn();
    }
}