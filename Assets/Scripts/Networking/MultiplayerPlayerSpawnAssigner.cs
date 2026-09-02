using UnityEngine;
using Unity.Netcode;

public class MultiplayerPlayerSpawnAssigner : NetworkBehaviour
{
    public NetworkVariable<int> AssignedSpawnIndex =
        new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public override void OnNetworkSpawn()
    {
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

        MoveToAssignedSpawn(index);
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

        Debug.Log(
            "[SpawnAssigner] Jugador movido a SpawnIndex " +
            index
        );
    }

    public override void OnNetworkDespawn()
    {
        AssignedSpawnIndex.OnValueChanged -=
            OnSpawnIndexChanged;
    }
}