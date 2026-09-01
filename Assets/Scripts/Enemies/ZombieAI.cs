using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class ZombieAI : NetworkBehaviour
{
    [Header("Ataque")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private int attackDamage = 10;

    // Tiempo entre el inicio de un ataque y el siguiente.
    [SerializeField] private float attackCooldown = 0.9f;

    // Tiempo desde que comienza la animación
    // hasta que realmente impacta el golpe.
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

    // Evita que haya varios ataques ejecutándose
    // al mismo tiempo.
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
            " | IsServer=" + IsServer +
            " | IsClient=" + IsClient
        );

        // La IA solamente funciona en el servidor.
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        // Velocidad por defecto: caminando.
        // RoundManager puede sobreescribirla llamando SetRunning()
        // justo después del Spawn, según la ronda actual.
        if (agent != null)
        {
            agent.speed = walkSpeed;
        }

        // El servidor busca inmediatamente
        // al jugador más cercano.
        FindClosestPlayer();
    }

    public void SetRunning(bool isRunning)
    {
        IsRunning = isRunning;

        if (agent != null)
        {
            agent.speed = isRunning ? runSpeed : walkSpeed;
        }
    }

    public bool IsBehindTarget()
    {
        if (target == null)
            return false;

        Vector3 directionToZombie =
            (transform.position - target.position).normalized;

        float dot =
            Vector3.Dot(
                target.forward,
                directionToZombie
            );

        // dot negativo significa que el zombie está
        // del lado opuesto a hacia donde mira el jugador.
        return dot < -0.3f;
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (zombieHealth != null &&
            zombieHealth.IsDead)
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
            if (agent != null)
            {
                agent.isStopped = true;
            }

            TryAttack();
        }
        else
        {
            // Si está fuera del rango y no está ejecutando
            // un ataque, puede volver a perseguir.
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

    private void FindClosestPlayer()
    {
        if (NetworkManager.Singleton == null)
            return;

        float closestDistance =
            Mathf.Infinity;

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
        // Ya hay un ataque en curso.
        if (isAttacking)
            return;

        // Todavía está en cooldown.
        if (Time.time < nextAttackTime)
            return;

        if (target == null)
            return;

        // Comienza el cooldown inmediatamente
        // al iniciar la animación.
        nextAttackTime =
            Time.time + attackCooldown;

        isAttacking = true;

        // Detener completamente al zombie.
        if (agent != null)
        {
            agent.isStopped = true;
        }

        // Iniciar animación.
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        // Sonido del ataque.
        if (zombieAudio != null)
        {
            zombieAudio.PlayAttackSound();
        }

        // El daño se aplica después del delay.
        StartCoroutine(ApplyAttackDamage());
    }

    private IEnumerator ApplyAttackDamage()
    {
        yield return new WaitForSeconds(
            attackHitDelay
        );

        // El zombie podría haber muerto
        // mientras hacía la animación.
        if (zombieHealth != null &&
            zombieHealth.IsDead)
        {
            isAttacking = false;
            yield break;
        }

        if (target != null)
        {
            // Comprobamos nuevamente la distancia.
            // Así el zombie no pega si el jugador
            // salió del rango durante la animación.
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

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        if (agent == null ||
            !agent.isOnNavMesh)
            return;

        float speed =
            agent.velocity.magnitude;

        animator.SetFloat(
            "Speed",
            speed
        );
    }
}