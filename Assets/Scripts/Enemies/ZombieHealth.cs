using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class ZombieHealth : MonoBehaviour
{
    [Header("--- SALUD ---")]
    [Tooltip("Cantidad de balazos que soporta el zombie antes de morir")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float destroyDelay = 3f;

    [Header("--- BARRA DE VIDA FLOTANTE ---")]
    [Tooltip("Arrastra el Slider flotante que está sobre su cabeza")]
    [SerializeField] private Slider healthSlider;
    [Tooltip("Arrastra el objeto Fill (dentro de Fill Area) para que cambie de color")]
    [SerializeField] private Image healthFillImage;
    [Tooltip("El Canvas completo, para ocultarlo al morir")]
    [SerializeField] private GameObject healthBarCanvas;

    [Header("--- COLORES SEGÚN VIDA ---")]
    [SerializeField] private Color colorVidaAlta = Color.green;   // 3/3 (o más de 2/3 de vida)
    [SerializeField] private Color colorVidaMedia = Color.yellow; // 2/3 de vida
    [SerializeField] private Color colorVidaBaja = Color.red;     // 1/3 de vida o menos

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

        // Inicializa los valores de la UI
        ActualizarBarraDeVida();
    }

    public void TakeDamage(int amount)
    {
        if (IsDead)
            return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log($"[ZombieHealth] Recibió {amount} de daño. Vida actual: {currentHealth}/{maxHealth}");

        // Actualiza el decremento visual en la barra
        ActualizarBarraDeVida();

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            if (animator != null)
                animator.SetTrigger("Hit");

            if (zombieAudio != null)
                zombieAudio.PlayHitSound();
        }
    }

    private void ActualizarBarraDeVida()
    {
        float porcentaje = (float)currentHealth / maxHealth;

        // 1. Si estás usando un Slider
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        // 2. Si estás usando una Image tipo Filled
        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = porcentaje;
            healthFillImage.color = ObtenerColorSegunVida(porcentaje);
        }
    }

    private Color ObtenerColorSegunVida(float porcentaje)
    {
        // Con maxHealth = 3: 3/3 = 100% verde, 2/3 = 66% amarillo, 1/3 = 33% rojo
        if (porcentaje > 0.66f)
            return colorVidaAlta;
        else if (porcentaje > 0.33f)
            return colorVidaMedia;
        else
            return colorVidaBaja;
    }

    private void Die()
    {
        if (IsDead)
            return;

        IsDead = true;

        // Oculta la barra de vida inmediatamente cuando el zombie cae
        if (healthBarCanvas != null)
        {
            healthBarCanvas.SetActive(false);
        }
        else if (healthSlider != null)
        {
            healthSlider.gameObject.SetActive(false);
        }

        if (zombieAI != null)
            zombieAI.enabled = false;

        if (agent != null)
            agent.enabled = false;

        if (animator != null)
            animator.SetTrigger("Death");

        if (zombieAudio != null)
            zombieAudio.PlayDeathSound();

        // Avisar al sistema de rondas.
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.ZombieDied();
        }

        Destroy(gameObject, destroyDelay);
    }
}