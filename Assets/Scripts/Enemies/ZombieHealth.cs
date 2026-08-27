using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using TMPro;

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
    public TextMeshProUGUI cantidadZombies;
    public int cantidadZombiesVivos = 6;


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

        UpdateZombieCount();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        
    }

    public void UpdateZombieCount()
    {
        if (cantidadZombies != null)
        {
            cantidadZombiesVivos--;
            cantidadZombies.text = cantidadZombiesVivos.ToString() + "/6";
        }
    }
    

}