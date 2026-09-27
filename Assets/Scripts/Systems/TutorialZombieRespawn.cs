using System.Collections;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(ZombieHealth))]
public class TutorialZombieRespawn : NetworkBehaviour
{
    [SerializeField] private float respawnDelay = 5f;

    private ZombieHealth zombieHealth;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool wasDead;
    private bool respawnScheduled;

    private void Awake()
    {
        zombieHealth = GetComponent<ZombieHealth>();
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    private void Update()
    {
        if (!IsServer || zombieHealth == null)
            return;

        if (zombieHealth.IsDead && !wasDead)
        {
            wasDead = true;
            zombieHealth.CancelScheduledDestroy();

            if (!respawnScheduled)
            {
                respawnScheduled = true;
                StartCoroutine(RespawnRoutine());
            }
        }
        else if (!zombieHealth.IsDead)
        {
            wasDead = false;
        }
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        zombieHealth.ResetForRespawn(spawnPosition, spawnRotation);
        respawnScheduled = false;
    }
}