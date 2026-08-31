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

    [Header("--- EFECTO DE SANGRE ---")]
    [SerializeField] private GameObject bloodImpactPrefab;
    [SerializeField] private float bloodDestroyDelay = 2f;

    public bool IsDead { get; private set; }

    // =========================================================
    // VIDA SINCRONIZADA
    // =========================================================

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

    // =========================================================
    // CONFIGURAR VIDA SEGÚN LA RONDA
    // =========================================================

    // Debe llamarse DESPUÉS de Instantiate()
    // y ANTES de NetworkObject.Spawn().
    public void InitializeHealth(int newMaxHealth)
    {
        maxHealth = newMaxHealth;
    }

    public override void OnNetworkSpawn()
    {
        currentHealthNetwork.OnValueChanged +=
            OnHealthChanged;

        if (IsServer)
        {
            currentHealthNetwork.Value =
                maxHealth;
        }

        ActualizarBarraDeVida();
    }

    public override void OnNetworkDespawn()
    {
        currentHealthNetwork.OnValueChanged -=
            OnHealthChanged;
    }

    // =========================================================
    // DAÑO
    // =========================================================

    public void TakeDamage(
        int amount,
        Vector3 hitPoint,
        Vector3 hitNormal
    )
    {
        if (IsDead)
            return;

        TakeDamageServerRpc(
            amount,
            hitPoint,
            hitNormal
        );
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(
        int amount,
        Vector3 hitPoint,
        Vector3 hitNormal,
        ServerRpcParams rpcParams = default
    )
    {
        if (IsDead)
            return;

        // Jugador que realizó el disparo.
        ulong shooterClientId =
            rpcParams.Receive.SenderClientId;

        currentHealthNetwork.Value -=
            amount;

        // =====================================================
        // MUERTE
        // =====================================================

        if (currentHealthNetwork.Value <= 0)
        {
            currentHealthNetwork.Value = 0;

            // Hit Marker ROJO solamente
            // para el jugador que mató al zombie.
            ShowHitMarkerClientRpc(
                true,
                GetTargetClientRpcParams(
                    shooterClientId
                )
            );

            Die();

            // Sangre.
            PlayBloodEffectClientRpc(
                hitPoint,
                hitNormal
            );

            return;
        }

        // =====================================================
        // IMPACTO NORMAL
        // =====================================================

        // Hit Marker BLANCO solamente
        // para el jugador que disparó.
        ShowHitMarkerClientRpc(
            false,
            GetTargetClientRpcParams(
                shooterClientId
            )
        );

        PlayHitEffectsClientRpc();

        // Sangre.
        PlayBloodEffectClientRpc(
            hitPoint,
            hitNormal
        );
    }

    // =========================================================
    // TARGET DEL CLIENT RPC
    // =========================================================

    private ClientRpcParams GetTargetClientRpcParams(
        ulong clientId
    )
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds =
                    new ulong[]
                    {
                        clientId
                    }
            }
        };
    }

    // =========================================================
    // HIT MARKER
    // =========================================================

    [ClientRpc]
    private void ShowHitMarkerClientRpc(
        bool killedZombie,
        ClientRpcParams clientRpcParams = default
    )
    {
        if (NetworkManager.Singleton == null)
            return;

        HUDController hud = null;

        if (NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            hud =
                NetworkManager.Singleton
                    .LocalClient
                    .PlayerObject
                    .GetComponentInChildren<
                        HUDController
                    >(true);
        }

        if (hud != null)
        {
            hud.ShowHitMarker(
                killedZombie
            );
        }
    }

    // =========================================================
    // SANGRE
    // =========================================================

    [ClientRpc]
    private void PlayBloodEffectClientRpc(
        Vector3 hitPoint,
        Vector3 hitNormal
    )
    {
        if (bloodImpactPrefab == null)
            return;

        Quaternion rotation =
            Quaternion.LookRotation(
                -hitNormal
            );

        GameObject blood =
            Instantiate(
                bloodImpactPrefab,
                hitPoint,
                rotation
            );

        Destroy(
            blood,
            bloodDestroyDelay
        );
    }

    // =========================================================
    // HIT MARKER DE MUERTE
    // =========================================================

    [ClientRpc]
    private void ShowKillHitMarkerClientRpc(
        ulong shooterClientId
    )
    {
        if (NetworkManager.Singleton == null)
            return;

        // Solamente el jugador que realizó
        // el disparo recibe el Hit Marker rojo.
        if (NetworkManager.Singleton.LocalClientId !=
            shooterClientId)
        {
            return;
        }

        HUDController hud =
            null;

        if (NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            hud =
                NetworkManager.Singleton
                    .LocalClient
                    .PlayerObject
                    .GetComponentInChildren<
                        HUDController
                    >(true);
        }

        if (hud != null)
        {
            hud.ShowHitMarker(true);
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
                ? (float)currentHealthNetwork.Value /
                  maxHealth
                : 0f;

        if (healthSlider != null)
        {
            healthSlider.maxValue =
                maxHealth;

            healthSlider.value =
                currentHealthNetwork.Value;
        }

        if (healthFillImage != null)
        {
            healthFillImage.fillAmount =
                porcentaje;

            healthFillImage.color =
                ObtenerColorSegunVida(
                    porcentaje
                );
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
        {
            animator.SetTrigger(
                "Hit"
            );
        }

        if (zombieAudio != null)
        {
            zombieAudio.PlayHitSound();
        }
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
            healthBarCanvas.SetActive(
                false
            );
        }
        else if (healthSlider != null)
        {
            healthSlider.gameObject.SetActive(
                false
            );
        }

        if (zombieAI != null)
        {
            zombieAI.enabled = false;
        }

        if (agent != null)
        {
            agent.enabled = false;
        }

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

    // =========================================================
    // EFECTOS DE MUERTE
    // =========================================================

    [ClientRpc]
    private void PlayDeathEffectsClientRpc()
    {
        if (animator != null)
        {
            animator.SetTrigger(
                "Death"
            );
        }

        if (zombieAudio != null)
        {
            zombieAudio.PlayDeathSound();
        }
    }

    // =========================================================
    // DESPAWN
    // =========================================================

    private void DespawnZombie()
    {
        if (!IsServer)
            return;

        if (NetworkObject != null &&
            NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(
                true
            );
        }
    }
}