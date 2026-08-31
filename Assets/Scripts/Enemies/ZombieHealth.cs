using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections.Generic;

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
        
    private NetworkVariable<int> maxHealthNetwork =
    new NetworkVariable<int>(
        3,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Animator animator;
    private NavMeshAgent agent;
    private ZombieAI zombieAI;
    private ZombieAudio zombieAudio;

    // Colliders del objeto raíz del zombie.
    // Se desactivan al morir para dejar solamente
    // los colliders de los huesos del ragdoll.
    private Collider[] rootColliders;

    // Rigidbodies y colliders creados en los huesos
    // mediante GameObject > 3D Object > Ragdoll.
    private Rigidbody[] ragdollRigidbodies;
    private Collider[] ragdollColliders;
    private int ragdollLayer;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        zombieAI = GetComponent<ZombieAI>();
        zombieAudio = GetComponent<ZombieAudio>();

        rootColliders = GetComponents<Collider>();

        ragdollRigidbodies =
            GetComponentsInChildren<Rigidbody>(true);

        ragdollColliders =
            GetRagdollColliders();

        ConfigureRagdollInitialState();
        ragdollLayer =
            LayerMask.NameToLayer("Ragdoll");
    }

    // =========================================================
    // RAGDOLL
    // =========================================================

    private Collider[] GetRagdollColliders()
    {
        List<Collider> colliders =
            new List<Collider>();

        foreach (Rigidbody body in ragdollRigidbodies)
        {
            if (body == null)
                continue;

            // El Rigidbody del objeto raíz no forma
            // parte del ragdoll.
            if (body.transform == transform)
                continue;

            Collider[] boneColliders =
                body.GetComponents<Collider>();

            foreach (Collider boneCollider in boneColliders)
            {
                if (boneCollider != null)
                {
                    colliders.Add(boneCollider);
                }
            }
        }

        return colliders.ToArray();
    }

    private void ConfigureRagdollInitialState()
    {
        foreach (Rigidbody body in ragdollRigidbodies)
        {
            if (body == null)
                continue;

            body.isKinematic = true;
            body.useGravity = false;
        }

        foreach (Collider ragdollCollider in ragdollColliders)
        {
            if (ragdollCollider == null)
                continue;

            ragdollCollider.enabled = false;
        }
    }

    private void ActivateRagdoll()
    {
        if (animator != null)
        {
            animator.enabled = false;
        }

        if (zombieAI != null)
        {
            zombieAI.enabled = false;
        }

        if (agent != null)
        {
            agent.enabled = false;
        }

        foreach (Collider rootCollider in rootColliders)
        {
            if (rootCollider != null)
            {
                rootCollider.enabled = false;
            }
        }

        foreach (Rigidbody body in ragdollRigidbodies)
        {
            if (body == null)
                continue;

            if (ragdollLayer != -1)
            {
                body.gameObject.layer =
                    ragdollLayer;
            }

            // Primero deja de ser cinemático.
            body.isKinematic = false;

            // Ahora sí se puede limpiar cualquier impulso
            // heredado de la animación o del NavMeshAgent.
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            body.useGravity = true;
        }

        foreach (Collider ragdollCollider in ragdollColliders)
        {
            if (ragdollCollider == null)
                continue;

            if (ragdollLayer != -1)
                {
                    ragdollCollider.gameObject.layer =
                        ragdollLayer;
                }

            ragdollCollider.enabled = true;
        }
    }

    // =========================================================
    // CONFIGURAR VIDA SEGÚN LA RONDA
    // =========================================================

    public void InitializeHealth(int newMaxHealth)
    {
        maxHealth = newMaxHealth;
    }

    public override void OnNetworkSpawn()
    {
        currentHealthNetwork.OnValueChanged +=
            OnHealthChanged;

        maxHealthNetwork.OnValueChanged +=
            OnMaxHealthChanged;

        if (IsServer)
        {
            maxHealthNetwork.Value =
                maxHealth;

            currentHealthNetwork.Value =
                maxHealth;
        }
        else
        {
            maxHealth =
                maxHealthNetwork.Value;
        }

        ActualizarBarraDeVida();
    }

    public override void OnNetworkDespawn()
    {
        currentHealthNetwork.OnValueChanged -=
            OnHealthChanged;
        maxHealthNetwork.OnValueChanged -=
        OnMaxHealthChanged;
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

        ulong shooterClientId =
            rpcParams.Receive.SenderClientId;

        currentHealthNetwork.Value -=
            amount;

        if (currentHealthNetwork.Value <= 0)
        {
            currentHealthNetwork.Value = 0;

            ShowHitMarkerClientRpc(
                shooterClientId,
                true
            );

            Die();
        }
        else
        {
            PlayHitEffectsClientRpc();

            ShowHitMarkerClientRpc(
                shooterClientId,
                false
            );
        }

        PlayBloodEffectClientRpc(
            hitPoint,
            hitNormal
        );
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
    private void ShowHitMarkerClientRpc(
        ulong shooterClientId,
        bool killedZombie
    )
    {
        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.LocalClientId !=
            shooterClientId)
        {
            return;
        }

        HUDController hud = null;

        if (NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            hud =
                NetworkManager.Singleton
                    .LocalClient
                    .PlayerObject
                    .GetComponentInChildren<HUDController>(
                        true
                    );
        }

        if (hud != null)
        {
            hud.ShowHitMarker(
                killedZombie
            );
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

    private void OnMaxHealthChanged(
        int previousMaxHealth,
        int newMaxHealth
    )
    {
        maxHealth = newMaxHealth;

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
        if (animator != null &&
            animator.enabled)
        {
            animator.SetTrigger("Hit");
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
            healthBarCanvas.SetActive(false);
        }
        else if (healthSlider != null)
        {
            healthSlider.gameObject.SetActive(false);
        }

        // Lo ejecuta en todos los clientes:
        // desactiva animación y activa físicas.
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
    // EFECTOS DE MUERTE / RAGDOLL
    // =========================================================

    [ClientRpc]
    private void PlayDeathEffectsClientRpc()
    {
        ActivateRagdoll();

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
            NetworkObject.Despawn(true);
        }
    }
}