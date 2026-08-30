using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Unity.Netcode;

public class ZombieHealth : NetworkBehaviour
{
    [Header("--- SALUD ---")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float destroyDelay = 3f;

    [Header("--- BARRA DE VIDA FLOTANTE ---")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image healthFillImage;
    [SerializeField] private GameObject healthBarCanvas;

    [Header("--- COLORES SEGÚN VIDA ---")]
    [SerializeField] private Color colorVidaAlta = Color.green;
    [SerializeField] private Color colorVidaMedia = Color.yellow;
    [SerializeField] private Color colorVidaBaja = Color.red;

    public bool IsDead { get; private set; }

    // VIDA SINCRONIZADA
    private NetworkVariable<int> currentHealthNetwork =
        new NetworkVariable<int>(
            3,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private Animator animator;
    private NavMeshAgent agent;
    private ZombieAI zombieAI;
    private ZombieAudio zombieAudio;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        zombieAI = GetComponent<ZombieAI>();
        zombieAudio = GetComponent<ZombieAudio>();
    }

    public override void OnNetworkSpawn()
    {
        currentHealthNetwork.OnValueChanged += OnHealthChanged;

        if (IsServer)
        {
            currentHealthNetwork.Value = maxHealth;
        }

        ActualizarBarraDeVida();
    }

    public override void OnNetworkDespawn()
    {
        currentHealthNetwork.OnValueChanged -= OnHealthChanged;
    }

    // =========================================================
    // DAÑO
    // =========================================================

    public void TakeDamage(int amount)
    {
        if (IsDead)
            return;

        TakeDamageServerRpc(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(int amount)
    {
        if (IsDead)
            return;

        currentHealthNetwork.Value -= amount;

        if (currentHealthNetwork.Value <= 0)
        {
            currentHealthNetwork.Value = 0;
            Die();
        }
        else
        {
            PlayHitEffectsClientRpc();
        }
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
    }

    // =========================================================
    // BARRA DE VIDA
    // =========================================================

    private void ActualizarBarraDeVida()
    {
        float porcentaje =
            maxHealth > 0
                ? (float)currentHealthNetwork.Value / maxHealth
                : 0f;

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value =
                currentHealthNetwork.Value;
        }

        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = porcentaje;
            healthFillImage.color =
                ObtenerColorSegunVida(porcentaje);
        }
    }

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
    // EFECTO HIT
    // =========================================================

    [ClientRpc]
    private void PlayHitEffectsClientRpc()
    {
        if (animator != null)
            animator.SetTrigger("Hit");

        if (zombieAudio != null)
            zombieAudio.PlayHitSound();
    }

    // =========================================================
    // MUERTE
    // =========================================================

    private void Die()
    {
        if (!IsServer)
            return;

        if (IsDead)
            return;

        IsDead = true;

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

        PlayDeathEffectsClientRpc();

        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.ZombieDied();
        }

        Invoke(
            nameof(DespawnZombie),
            destroyDelay
        );
    }

    [ClientRpc]
    private void PlayDeathEffectsClientRpc()
    {
        if (animator != null)
            animator.SetTrigger("Death");

        if (zombieAudio != null)
            zombieAudio.PlayDeathSound();
    }

    private void DespawnZombie()
    {
        if (!IsServer)
            return;

        if (NetworkObject != null &&
            NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}