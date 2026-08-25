using UnityEngine;
using UnityEngine.AI;

public class ZombieHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float destroyDelay = 3f;

    public bool IsDead { get; private set; }

    private int currentHealth;
    private Animator animator;
    private NavMeshAgent agent;
    private ZombieAI zombieAI;
    private ZombieAudio zombieAudio;

    private void Awake()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        zombieAI = GetComponent<ZombieAI>();
        zombieAudio = GetComponent<ZombieAudio>();
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        currentHealth -= amount;

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            if (animator != null) animator.SetTrigger("Hit");
            if (zombieAudio != null) zombieAudio.PlayHitSound();
        }
    }

    private void Die()
    {
        IsDead = true;

        if (zombieAI != null) zombieAI.enabled = false;
        if (agent != null) agent.enabled = false;
        if (animator != null) animator.SetTrigger("Death");
        if (zombieAudio != null) zombieAudio.PlayDeathSound();

        Destroy(gameObject, destroyDelay);
    }

}