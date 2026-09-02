using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class ZombieAI : NetworkBehaviour
{
    [Header("Ataque")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private int attackDamage = 10;

    [SerializeField] private float attackCooldown = 0.9f;

    [SerializeField] private float attackHitDelay = 0.35f;

    [Header("Velocidad")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float runSpeed = 3f;

    public bool IsRunning { get; private set; }

    private NavMeshAgent agent;
    private Animator animator;
    private ZombieHealth zombieHealth;
    private ZombieAudio zombieAudio;

    private float nextAttackTime;
    private Transform target;

    private bool isAttacking;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        zombieHealth = GetComponent<ZombieHealth>();
        zombieAudio = GetComponent<ZombieAudio>();
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log(
            "[ZombieAI] Spawn | " +
            gameObject.name +
            " | IsServer=" +
            IsServer +
            " | IsClient=" +
            IsClient
        );

        // La IA solamente funciona en el servidor.
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        if (agent != null)
        {
            agent.speed = walkSpeed;
        }

        FindClosestPlayer();
    }

    public void SetRunning(bool isRunning)
    {
        IsRunning = isRunning;

        if (agent != null)
        {
            agent.speed =
                isRunning
                    ? runSpeed
                    : walkSpeed;
        }
    }

    public bool IsBehindTarget()
    {
        if (target == null)
            return false;

        Vector3 directionToZombie =
            (
                transform.position -
                target.position
            ).normalized;

        float dot =
            Vector3.Dot(
                target.forward,
                directionToZombie
            );

        return dot < -0.3f;
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (
            zombieHealth != null &&
            zombieHealth.IsDead
        )
        {
            return;
        }

        FindClosestPlayer();

        if (target == null)
            return;

        float distanceToTarget =
            Vector3.Distance(
                transform.position,
                target.position
            );

        if (distanceToTarget <= attackRange)
        {
            if (agent != null)
            {
                agent.isStopped = true;
            }

            TryAttack();
        }
        else
        {
            if (!isAttacking)
            {
                if (agent != null)
                {
                    agent.isStopped = false;

                    if (agent.isOnNavMesh)
                    {
                        agent.SetDestination(
                            target.position
                        );
                    }
                }
            }
        }

        UpdateAnimator();
    }

    // =========================================================
    // BUSCAR JUGADOR VIVO
    // =========================================================

    private void FindClosestPlayer()
    {
        if (NetworkManager.Singleton == null)
            return;

        float closestDistance =
            Mathf.Infinity;

        Transform closestPlayer = null;

        foreach (
            NetworkClient client
            in NetworkManager.Singleton
                .ConnectedClientsList
        )
        {
            if (client.PlayerObject == null)
                continue;

            PlayerHealth playerHealth =
                client.PlayerObject.GetComponent<PlayerHealth>();

            if (playerHealth == null)
                continue;

            // Solamente jugadores vivos.
            if (!playerHealth.IsAlive)
                continue;

            Transform player =
                client.PlayerObject.transform;

            float distance =
                Vector3.Distance(
                    transform.position,
                    player.position
                );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPlayer = player;
            }
        }

        target = closestPlayer;
    }

    // =========================================================
    // ATAQUE
    // =========================================================

    private void TryAttack()
    {
        if (isAttacking)
            return;

        if (Time.time < nextAttackTime)
            return;

        if (target == null)
            return;

        nextAttackTime =
            Time.time +
            attackCooldown;

        isAttacking = true;

        if (agent != null)
        {
            agent.isStopped = true;
        }

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        if (zombieAudio != null)
        {
            zombieAudio.PlayAttackSound();
        }

        StartCoroutine(
            ApplyAttackDamage()
        );
    }

    private IEnumerator ApplyAttackDamage()
    {
        yield return new WaitForSeconds(
            attackHitDelay
        );

        if (
            zombieHealth != null &&
            zombieHealth.IsDead
        )
        {
            isAttacking = false;
            yield break;
        }

        if (target != null)
        {
            float distanceToTarget =
                Vector3.Distance(
                    transform.position,
                    target.position
                );

            if (distanceToTarget <= attackRange)
            {
                PlayerHealth playerHealth =
                    target.GetComponentInParent<PlayerHealth>();

                if (playerHealth != null)
                {
                    // Puede haber muerto o quedado abatido
                    // durante la animación del golpe.
                    if (!playerHealth.IsAlive)
                    {
                        isAttacking = false;
                        yield break;
                    }

                    playerHealth.TakeDamage(
                        attackDamage
                    );

                    Debug.Log(
                        "[ZombieAI] Zombie atacó a Player " +
                        playerHealth.name
                    );
                }
            }
        }

        isAttacking = false;
    }

    // =========================================================
    // ANIMACIÓN
    // =========================================================

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        if (
            agent == null ||
            !agent.isOnNavMesh
        )
        {
            return;
        }

        float speed =
            agent.velocity.magnitude;

        animator.SetFloat(
            "Speed",
            speed
        );
    }
}