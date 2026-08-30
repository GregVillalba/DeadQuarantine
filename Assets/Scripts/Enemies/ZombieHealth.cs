using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Unity.Netcode;

public class ZombieHealth : NetworkBehaviour
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
    [SerializeField] private Color colorVidaAlta = Color.green;
    [SerializeField] private Color colorVidaMedia = Color.yellow;
    [SerializeField] private Color colorVidaBaja = Color.red;

    // =========================================================
    // ESTADO DE RED
    // =========================================================

    public NetworkVariable<int> CurrentHealth =
        new NetworkVariable<int>(
            3,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<bool> IsDeadNetwork =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public bool IsDead => IsDeadNetwork.Value;

    private Animator animator;
    private NavMeshAgent agent;
    private ZombieAI zombieAI;
    private ZombieAudio zombieAudio;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        zombieAI = GetComponent<ZombieAI>();
        zombieAudio = GetComponent<ZombieAudio>();
    }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        // El servidor establece la vida inicial.
        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;
            IsDeadNetwork.Value = false;
        }

        // Todos los clientes escuchan cambios de vida.
        CurrentHealth.OnValueChanged += OnHealthChanged;

        // Todos escuchan cuando el zombie muere.
        IsDeadNetwork.OnValueChanged += OnDeathStateChanged;

        // Actualizar inmediatamente la barra.
        ActualizarBarraDeVida();
    }

    // =========================================================
    // NETWORK DESPAWN
    // =========================================================

    public override void OnNetworkDespawn()
    {
        CurrentHealth.OnValueChanged -= OnHealthChanged;
        IsDeadNetwork.OnValueChanged -= OnDeathStateChanged;
    }

    // =========================================================
    // CAMBIO DE VIDA
    // =========================================================

    private void OnHealthChanged(
        int previousHealth,
        int newHealth
    )
    {
        ActualizarBarraDeVida();

        Debug.Log(
            "[ZombieHealth] Vida sincronizada: " +
            newHealth +
            "/" +
            maxHealth
        );
    }

    // =========================================================
    // RECIBIR DAÑO
    // =========================================================

    public void TakeDamage(int amount)
    {
        // SOLO EL SERVIDOR MODIFICA LA VIDA.
        if (!IsServer)
            return;

        if (IsDeadNetwork.Value)
            return;

        if (amount <= 0)
            return;

        CurrentHealth.Value -= amount;

        CurrentHealth.Value = Mathf.Clamp(
            CurrentHealth.Value,
            0,
            maxHealth
        );

        Debug.Log(
            "[ZombieHealth] Recibió " +
            amount +
            " de daño. Vida actual: " +
            CurrentHealth.Value +
            "/" +
            maxHealth
        );

        if (CurrentHealth.Value <= 0)
        {
            Die();
        }
        else
        {
            // La animación de Hit debe ejecutarse
            // en el servidor y NetworkAnimator
            // la replica al resto.
            if (animator != null)
                animator.SetTrigger("Hit");

            if (zombieAudio != null)
                zombieAudio.PlayHitSound();
        }
    }

    // =========================================================
    // BARRA DE VIDA
    // =========================================================

    private void ActualizarBarraDeVida()
    {
        float porcentaje =
            maxHealth > 0
                ? (float)CurrentHealth.Value / maxHealth
                : 0f;

        // Slider
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = CurrentHealth.Value;
        }

        // Image Filled
        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = porcentaje;
            healthFillImage.color =
                ObtenerColorSegunVida(porcentaje);
        }
    }

    // =========================================================
    // COLOR DE VIDA
    // =========================================================

    private Color ObtenerColorSegunVida(
        float porcentaje
    )
    {
        if (porcentaje > 0.66f)
            return colorVidaAlta;

        if (porcentaje > 0.33f)
            return colorVidaMedia;

        return colorVidaBaja;
    }

    // =========================================================
    // MUERTE
    // =========================================================

    private void Die()
    {
        // Solamente el servidor puede decidir
        // que el zombie murió.
        if (!IsServer)
            return;

        if (IsDeadNetwork.Value)
            return;

        IsDeadNetwork.Value = true;

        // Ocultar barra de vida.
        if (healthBarCanvas != null)
        {
            healthBarCanvas.SetActive(false);
        }
        else if (healthSlider != null)
        {
            healthSlider.gameObject.SetActive(false);
        }

        // Desactivar IA.
        if (zombieAI != null)
            zombieAI.enabled = false;

        // Desactivar NavMesh.
        if (agent != null && agent.enabled)
            agent.enabled = false;

        // Animación de muerte.
        if (animator != null)
            animator.SetTrigger("Death");

        // Sonido de muerte.
        if (zombieAudio != null)
            zombieAudio.PlayDeathSound();

        // Avisar al RoundManager.
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.ZombieDied();
        }

        // Destruir después del delay.
        Destroy(gameObject, destroyDelay);
    }

    // =========================================================
    // ESTADO DE MUERTE EN CLIENTES
    // =========================================================

    private void OnDeathStateChanged(
        bool previousState,
        bool newState
    )
    {
        if (!newState)
            return;

        // Los clientes también ocultan
        // su barra de vida.
        if (healthBarCanvas != null)
        {
            healthBarCanvas.SetActive(false);
        }
        else if (healthSlider != null)
        {
            healthSlider.gameObject.SetActive(false);
        }
    }
}