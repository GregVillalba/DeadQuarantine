using UnityEngine;
using UnityEngine.AI;

public class ZombieAI : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackCooldown = 1.5f;

    private NavMeshAgent agent;
    private Animator animator;
    private ZombieHealth zombieHealth;
    private float nextAttackTime;
    private ZombieAudio zombieAudio;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        zombieHealth = GetComponent<ZombieHealth>();
        zombieAudio = GetComponent<ZombieAudio>();
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    private void Update()
    {
        if (zombieHealth != null && zombieHealth.IsDead) return;
        if (target == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget <= attackRange)
        {
            agent.isStopped = true;
            TryAttack();
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
        }

        UpdateAnimator();
    }

    private void TryAttack()
    {
        if (Time.time < nextAttackTime) return;

        nextAttackTime = Time.time + attackCooldown;

        if (animator != null) animator.SetTrigger("Attack");
        if (zombieAudio != null) zombieAudio.PlayAttackSound();

        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage);
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        float speed = agent.velocity.magnitude;
        animator.SetFloat("Speed", speed);
    }
}