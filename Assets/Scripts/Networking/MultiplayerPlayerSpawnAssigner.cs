using UnityEngine;
using Unity.Netcode;

public class MultiplayerPlayerSpawnAssigner : NetworkBehaviour
{
    public NetworkVariable<int> AssignedSpawnIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        AssignedSpawnIndex.OnValueChanged += OnSpawnIndexChanged;

        // Por si el valor ya estaba asignado antes de que este cliente
        // terminara de suscribirse al evento (orden de ejecución).
        if (IsOwner && AssignedSpawnIndex.Value != -1)
        {
            MoveToAssignedSpawn(AssignedSpawnIndex.Value);
        }
    }

    private void OnSpawnIndexChanged(int previousValue, int newValue)
    {
        if (!IsOwner) return;
        if (newValue == -1) return;

        MoveToAssignedSpawn(newValue);
    }

    private void MoveToAssignedSpawn(int index)
    {
        MultiplayerPlayerSpawner spawner =
            FindFirstObjectByType<MultiplayerPlayerSpawner>();

        if (spawner == null)
        {
            Debug.LogWarning("[SpawnAssigner] No se encontró MultiplayerPlayerSpawner en la escena.");
            return;
        }

        Transform spawnPoint = spawner.GetSpawnPoint(index);

        if (spawnPoint == null)
        {
            Debug.LogWarning("[SpawnAssigner] SpawnPoint nulo para índice " + index);
            return;
        }

        CharacterController cc = GetComponent<CharacterController>();

        if (cc != null)
        {
            // Hay que desactivar el CharacterController un frame
            // para poder reposicionar sin que choque contra sí mismo.
            cc.enabled = false;
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            cc.enabled = true;
        }
        else
        {
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }

        Debug.Log("[SpawnAssigner] Jugador local movido a SpawnIndex " + index);
    }

    public override void OnNetworkDespawn()
    {
        AssignedSpawnIndex.OnValueChanged -= OnSpawnIndexChanged;
    }
}