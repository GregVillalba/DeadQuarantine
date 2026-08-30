using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class ZombieAI : NetworkBehaviour
{
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackCooldown = 1.5f;

    private NavMeshAgent agent;
    private Animator animator;
    private ZombieHealth zombieHealth;
    private ZombieAudio zombieAudio;

    private float nextAttackTime;
    private Transform target;

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
            " | IsServer=" + IsServer +
            " | IsClient=" + IsClient
        );

        // La IA solamente funciona en el servidor.
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        // El servidor busca inmediatamente
        // al jugador más cercano.
        FindClosestPlayer();
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (zombieHealth != null && zombieHealth.IsDead)
            return;

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
            agent.isStopped = true;
            TryAttack();
        }
        else
        {
            agent.isStopped = false;

            if (agent.isOnNavMesh)
            {
                agent.SetDestination(target.position);
            }
        }

        UpdateAnimator();
    }

    private void FindClosestPlayer()
    {
        if (NetworkManager.Singleton == null)
            return;

        float closestDistance = Mathf.Infinity;
        Transform closestPlayer = null;

        foreach (
            NetworkClient client
            in NetworkManager.Singleton.ConnectedClientsList
        )
        {
            if (client.PlayerObject == null)
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

    private void TryAttack()
    {
        if (Time.time < nextAttackTime)
            return;

        nextAttackTime =
            Time.time + attackCooldown;

        if (animator != null)
            animator.SetTrigger("Attack");

        if (zombieAudio != null)
            zombieAudio.PlayAttackSound();

        if (target == null)
            return;

        PlayerHealth playerHealth =
            target.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage);

            Debug.Log(
                "[ZombieAI] Zombie atacó a Player " +
                playerHealth.name
            );
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        if (agent == null || !agent.isOnNavMesh)
            return;

        float speed =
            agent.velocity.magnitude;

        animator.SetFloat(
            "Speed",
            speed
        );
    }
}