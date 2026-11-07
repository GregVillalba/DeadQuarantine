using System.Collections;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(ZombieHealth))]
public class TutorialZombieRespawn : NetworkBehaviour
{
    [SerializeField] private float respawnDelay = 5f;

    [Header("Recurso que suelta al morir")]
    [Tooltip("En el tutorial no se usa la tabla de drop: cada vez que muere suelta siempre este recurso, " +
             "para practicar juntar recursos y comerciar con el mercader. Desmarcado = no suelta nada.")]
    [SerializeField] private bool soltarRecurso = true;
    [SerializeField] private TipoRecurso recursoQueSuelta = TipoRecurso.PilaDeLatas;
    [SerializeField, Min(1)] private int cantidadQueSuelta = 1;

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

    /// <summary>
    /// Qué suelta este zombie al morir (lo usa DropRecursosZombie en vez de la tabla).
    /// false = no suelta nada.
    /// </summary>
    public bool DropFijo(out TipoRecurso tipo, out int cantidad)
    {
        tipo = recursoQueSuelta;
        cantidad = Mathf.Max(1, cantidadQueSuelta);
        return soltarRecurso;
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