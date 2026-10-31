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

    [Header("--- MUERTE ---")]
    [Tooltip("Activado = usa el ragdoll actual. Desactivado = mantiene el Animator y usa ZombieAnimationDriver para reproducir una animación de muerte.")]
    [SerializeField] private bool useRagdollOnDeath = true;

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

    [Header("--- QUEMADURA (granada incendiaria) ---")]
    [Tooltip("Fuego que se prende encima del zombie mientras se quema. Vacío = se quema sin efecto visual.")]
    [SerializeField] private GameObject efectoQuemadura;
    [SerializeField] private float alturaEfectoQuemadura = 1f;

    public bool IsDead { get; private set; }
    public bool EsBossHistoria { get; private set; }

public void MarcarComoBossHistoria()
{
    EsBossHistoria = true;
}

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

    // true mientras se está quemando: los clientes prenden / apagan el fuego encima del zombie.
    private NetworkVariable<bool> quemandoseNetwork =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Quemadura (solo servidor).
    private float finQuemadura;
    private int danoQuemadura;
    private float intervaloQuemadura;
    private ulong atacanteQuemadura;
    private Coroutine rutinaQuemadura;

    // Fuego instanciado en este cliente.
    private GameObject fuegoActivo;

    private Animator animator;
    private ZombieAnimationDriver animationDriver;
    private NavMeshAgent agent;
    private ZombieAI zombieAI;
    private ZombieAudio zombieAudio;

    private ZombieHeadshot zombieHeadshot;

    // Colliders del objeto raíz del zombie.
    // Se desactivan al morir para dejar solamente
    // los colliders de los huesos del ragdoll.
    private Collider[] rootColliders;

    // Rigidbodies y colliders creados en los huesos
    // mediante GameObject > 3D Object > Ragdoll.
    private Rigidbody[] ragdollRigidbodies;
    private Collider[] ragdollColliders;
    private int ragdollLayer;
    private int[] ragdollOriginalLayers;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        animationDriver = GetComponent<ZombieAnimationDriver>();
        if (animationDriver == null)
            animationDriver = GetComponentInChildren<ZombieAnimationDriver>(true);

        agent = GetComponent<NavMeshAgent>();
        zombieAI = GetComponent<ZombieAI>();
        zombieAudio = GetComponent<ZombieAudio>();
        zombieHeadshot = GetComponent<ZombieHeadshot>();

        rootColliders = GetComponents<Collider>();

        ragdollRigidbodies =
            GetComponentsInChildren<Rigidbody>(true);

        ragdollColliders =
            GetRagdollColliders();

        ConfigureRagdollInitialState();
        ragdollLayer =
            LayerMask.NameToLayer("Ragdoll");
            
        ragdollOriginalLayers = new int[ragdollColliders.Length];
        for (int i = 0; i < ragdollColliders.Length; i++)
            ragdollOriginalLayers[i] = ragdollColliders[i].gameObject.layer;
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

        quemandoseNetwork.OnValueChanged +=
            OnQuemandoseChanged;

        ActualizarEfectoQuemadura(quemandoseNetwork.Value);

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
        quemandoseNetwork.OnValueChanged -=
            OnQuemandoseChanged;

        ActualizarEfectoQuemadura(false);
    }

    // =========================================================
    // DAÑO
    // =========================================================

   public void TakeDamage(
    int amount,
    Vector3 hitPoint,
    Vector3 hitNormal,
    bool esHeadshot = false,
    bool registrarHeadshot = false
)
{
    if (IsDead)
        return;

    TakeDamageServerRpc(
        amount,
        hitPoint,
        hitNormal,
        esHeadshot,
        registrarHeadshot
    );
}

   [ServerRpc(RequireOwnership = false)]
private void TakeDamageServerRpc(
    int amount,
    Vector3 hitPoint,
    Vector3 hitNormal,
    bool esHeadshot,
    bool registrarHeadshot,
    ServerRpcParams rpcParams = default
)
    {
        AplicarDano(
            amount,
            hitPoint,
            hitNormal,
            esHeadshot,
            registrarHeadshot,
            rpcParams.Receive.SenderClientId,
            true
        );
    }

    /// <summary>
    /// Solo servidor. Daño que no viene de un disparo del cliente (granadas, fuego):
    /// el que tiró la granada se pasa a mano para que la kill y los puntos sean suyos y no del host.
    /// efectosDeImpacto = false para el daño por tick (sin sangre ni animación de golpe en cada tick).
    /// </summary>
    public void RecibirDanoServidor(
        int amount,
        Vector3 hitPoint,
        Vector3 hitNormal,
        ulong atacanteClientId,
        bool efectosDeImpacto = true
    )
    {
        if (!IsServer)
            return;

        AplicarDano(
            amount,
            hitPoint,
            hitNormal,
            false,
            false,
            atacanteClientId,
            efectosDeImpacto
        );
    }

    private void AplicarDano(
        int amount,
        Vector3 hitPoint,
        Vector3 hitNormal,
        bool esHeadshot,
        bool registrarHeadshot,
        ulong shooterClientId,
        bool efectosDeImpacto
    )
    {
        if (IsDead)
            return;

//======================= NUEVO =========================
        // Headshot: ZombieHeadshot decide cuánto daño hace.
    bool headshotAplicado = false;

    if (esHeadshot && zombieHeadshot != null)
    {
       /* amount = zombieHeadshot.CalcularDano(
            amount,
            currentHealthNetwork.Value
        );*/

        amount = zombieHeadshot.CalcularDano(
                amount,
                currentHealthNetwork.Value,
                maxHealthNetwork.Value
        );

        headshotAplicado = true;
    }

//=========================================================

        currentHealthNetwork.Value -=
            amount;

        if (currentHealthNetwork.Value <= 0)
        {
            currentHealthNetwork.Value = 0;

            ShowHitMarkerClientRpc(
                shooterClientId,
                true,
                headshotAplicado
            );

            Die(shooterClientId);
        }
        else if (efectosDeImpacto)
        {
            PlayHitEffectsClientRpc();

            ShowHitMarkerClientRpc(
                shooterClientId,
                false,
                headshotAplicado
            );
        }

       if (registrarHeadshot)
    SumarHeadshotAlJugador(shooterClientId);

if (headshotAplicado)
    zombieHeadshot.OtorgarPuntosExtra(shooterClientId);

        if (efectosDeImpacto)
        {
            PlayBloodEffectClientRpc(
                hitPoint,
                hitNormal
            );
        }
    }

    // =========================================================
    // QUEMADURA (granada incendiaria)
    // =========================================================

    public bool EstaQuemandose => quemandoseNetwork.Value;

    /// <summary>
    /// Solo servidor. Prende fuego al zombie o, si ya se está quemando, le renueva la duración.
    /// Hace danoPorTick cada intervalo hasta que pasa la duración sin que se vuelva a llamar.
    /// </summary>
    public void Quemar(
        float duracion,
        int danoPorTick,
        float intervalo,
        ulong atacanteClientId
    )
    {
        if (!IsServer || IsDead)
            return;

        finQuemadura =
            Mathf.Max(
                finQuemadura,
                Time.time + duracion
            );

        danoQuemadura = danoPorTick;
        intervaloQuemadura = Mathf.Max(0.1f, intervalo);
        atacanteQuemadura = atacanteClientId;

        if (rutinaQuemadura == null)
            rutinaQuemadura = StartCoroutine(RutinaQuemadura());
    }

    private System.Collections.IEnumerator RutinaQuemadura()
    {
        quemandoseNetwork.Value = true;

        while (!IsDead && Time.time < finQuemadura)
        {
            yield return new WaitForSeconds(intervaloQuemadura);

            if (IsDead)
                break;

            Vector3 punto =
                transform.position +
                Vector3.up * alturaEfectoQuemadura;

            AplicarDano(
                danoQuemadura,
                punto,
                Vector3.up,
                false,
                false,
                atacanteQuemadura,
                false
            );
        }

        ApagarQuemadura();
    }

    private void ApagarQuemadura()
    {
        if (!IsServer)
            return;

        if (rutinaQuemadura != null)
        {
            StopCoroutine(rutinaQuemadura);
            rutinaQuemadura = null;
        }

        finQuemadura = 0f;
        quemandoseNetwork.Value = false;
    }

    private void OnQuemandoseChanged(
        bool anterior,
        bool actual
    )
    {
        ActualizarEfectoQuemadura(actual);
    }

    private void ActualizarEfectoQuemadura(bool encendido)
    {
        if (encendido)
        {
            if (fuegoActivo != null || efectoQuemadura == null)
                return;

            fuegoActivo =
                Instantiate(
                    efectoQuemadura,
                    transform.position + Vector3.up * alturaEfectoQuemadura,
                    Quaternion.identity,
                    transform
                );

            return;
        }

        if (fuegoActivo == null)
            return;

        // Se suelta del zombie y deja de emitir: las llamas que ya salieron se apagan solas.
        fuegoActivo.transform.SetParent(null, true);

        foreach (ParticleSystem particulas in fuegoActivo.GetComponentsInChildren<ParticleSystem>())
            particulas.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        Destroy(fuegoActivo, 2f);
        fuegoActivo = null;
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
        bool killedZombie,
        bool headshot
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
                killedZombie,
                headshot
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
        if (animationDriver != null)
        {
            animationDriver.PlayHit();
        }
        else if (animator != null &&
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
private void Die(ulong shooterClientId)
{
    if (!IsServer)
        return;

    if (IsDead)
        return;

    IsDead = true;

    ApagarQuemadura();

    if (zombieAI != null)
        zombieAI.QuitarAturdimiento();

    Debug.Log(
        $"[ZombieHealth] Muere '{gameObject.name}'." +
        $" EsBossHistoria={EsBossHistoria}" +
        $" | Vida={currentHealthNetwork.Value}"
    );

    if (healthBarCanvas != null)
    {
        healthBarCanvas.SetActive(false);
    }
    else if (healthSlider != null)
    {
        healthSlider.gameObject.SetActive(false);
    }

    PlayDeathEffectsClientRpc();

    SumarPuntoAlJugador(shooterClientId);

    DropRecursosZombie dropRecursos =
        GetComponent<DropRecursosZombie>();

    if (dropRecursos != null)
        dropRecursos.Soltar();

    if (RoundManager.Instance != null)
    {
        if (EsBossHistoria)
        {
            Debug.Log(
                "[ZombieHealth] Notificando muerte del boss al RoundManager."
            );

            RoundManager.Instance.BossHistoriaDerrotado();
        }
        else
        {
            RoundManager.Instance.ZombieDied();
        }
    }
    else if (GetComponent<TutorialZombieRespawn>() == null)
    {
        // En el tutorial no hay RoundManager: sus zombies se reviven solos (TutorialZombieRespawn).
        Debug.LogError(
            "[ZombieHealth] No se encontró RoundManager al morir el zombie."
        );
    }

    Invoke(nameof(DespawnZombie), destroyDelay);
}

    // =========================================================
    // EFECTOS DE MUERTE / RAGDOLL
    // =========================================================

    [ClientRpc]
    private void PlayDeathEffectsClientRpc()
    {
        // También se marca en clientes para que ZombieAudio y otros componentes
        // locales sepan inmediatamente que el enemigo ya murió.
        IsDead = true;

        // Si muere aturdido, el Animator tiene que volver a moverse para la animación de muerte.
        if (zombieAI != null)
            zombieAI.QuitarEfectoAturdimiento();

        if (useRagdollOnDeath)
        {
            ActivateRagdoll();
        }
        else
        {
            PlayAnimatedDeath();
        }

//======================== NUEVO =========================
        if (zombieHeadshot != null)
            zombieHeadshot.DesactivarHitbox();
//=========================================================
        if (zombieAudio != null)
        {
            zombieAudio.PlayDeathSound();
        }
    }

    private void PlayAnimatedDeath()
    {
        if (zombieAI != null)
            zombieAI.enabled = false;

        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            agent.enabled = false;
        }

        // Evita que el cadáver siga recibiendo impactos o bloqueando disparos,
        // pero deja el Animator activo para poder reproducir DeathLeft/DeathRight.
        Collider[] deathColliders = GetComponentsInChildren<Collider>(true);

        foreach (Collider deathCollider in deathColliders)
        {
            if (deathCollider != null)
                deathCollider.enabled = false;
        }

        if (animationDriver != null)
        {
            animationDriver.PlayDeath();
        }
        else if (animator != null && animator.enabled)
        {
            animator.SetTrigger("Death");
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

    private void SumarPuntoAlJugador(ulong shooterClientId)
    {
        if(!IsServer)
            return;

        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
            shooterClientId,
            out NetworkClient shooterClient))
        {
            return;
        }
        if (shooterClient.PlayerObject == null)
        {
                return;
        }
        PlayerScore playerScore =
                    shooterClient.PlayerObject.GetComponentInChildren<PlayerScore>();

        if (playerScore != null)
                {
                    playerScore.SumarPuntos(150);
                    playerScore.SumarZombieEliminado();
                }
    }

    public void CancelScheduledDestroy()
    {
        CancelInvoke(nameof(DespawnZombie));
    }

    // Vuelve el zombie a estar 100% vivo. Pensado para ser llamado
    // SOLO desde sistemas externos como el respawn del tutorial.
    public void ResetForRespawn(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;

        IsDead = false;

        if (IsServer)
        {
            currentHealthNetwork.Value = maxHealth;
            ApagarQuemadura();

            if (zombieAI != null)
                zombieAI.QuitarAturdimiento();
        }

        ConfigureRagdollInitialState();

        for (int i = 0; i < ragdollColliders.Length; i++)
            if (ragdollColliders[i] != null)
                ragdollColliders[i].gameObject.layer = ragdollOriginalLayers[i];

        foreach (Collider rootCollider in rootColliders)
            if (rootCollider != null)
                rootCollider.enabled = true;

        if (agent != null) agent.enabled = true;
        if (zombieAI != null) zombieAI.enabled = true;
        if (zombieHeadshot != null)
            zombieHeadshot.ActivarHitbox();
            
        if (animator != null)
        {
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
        }

        if (healthBarCanvas != null)
            healthBarCanvas.SetActive(true);
        else if (healthSlider != null)
            healthSlider.gameObject.SetActive(true);

        ActualizarBarraDeVida();
    }
    private void SumarHeadshotAlJugador(ulong shooterClientId)
{
    if (!IsServer)
        return;

    if (NetworkManager.Singleton == null)
        return;

    if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
        shooterClientId,
        out NetworkClient shooterClient))
    {
        return;
    }

    if (shooterClient.PlayerObject == null)
        return;

    PlayerScore playerScore =
        shooterClient.PlayerObject.GetComponentInChildren<PlayerScore>();

    if (playerScore != null)
        playerScore.SumarHeadshot();
}
}