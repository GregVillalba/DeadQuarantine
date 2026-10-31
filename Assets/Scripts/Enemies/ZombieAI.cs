using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class ZombieAI : NetworkBehaviour
{
    [Header("Ataque")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private int attackDamage = 10;

    [Tooltip("Activado = DifficultySettings.zombieDamage puede reemplazar el daño del prefab. Para enemigos con daño propio, como el perro zombie, desactivarlo.")]
    [SerializeField] private bool useDifficultyDamageOverride = true;

    [SerializeField] private float attackCooldown = 0.9f;

    [SerializeField] private float attackHitDelay = 0.35f;

    [Header("Velocidad")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float runSpeed = 3f;

    [Header("Aturdimiento (granada aturdidora)")]
    [Tooltip("Efecto que se muestra encima del zombie mientras está aturdido. Vacío = sin efecto visual.")]
    [SerializeField] private GameObject efectoAturdido;
    [SerializeField] private float alturaEfectoAturdido = 1.8f;

    public bool IsRunning { get; private set; }

    // true mientras está aturdido: no se mueve ni ataca, y los clientes congelan la animación.
    private NetworkVariable<bool> aturdidoNetwork =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public bool EstaAturdido => aturdidoNetwork.Value;

    private float finAturdimiento;
    private Coroutine rutinaAturdimiento;
    private GameObject efectoAturdidoActivo;
    private bool animacionCongelada;

    // Multiplicador de velocidad por ronda (modo Supervivencia). 1 = sin cambios.
    private float speedMultiplier = 1f;     //

    // Ralentización por obstáculos (charco de barro). 1 = sin obstáculo. Se revisa cada tanto, no cada cuadro.
    private float multiplicadorTerreno = 1f;
    private float proximaRevisionTerreno;

    private NavMeshAgent agent;
    private Animator animator;
    private ZombieAnimationDriver animationDriver;
    private ZombieHealth zombieHealth;
    private ZombieAudio zombieAudio;

    private float nextAttackTime;
    private Transform target;
    private bool hayJugadorEnZonaSegura;

    private bool isAttacking;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        animationDriver = GetComponent<ZombieAnimationDriver>();
        if (animationDriver == null)
            animationDriver = GetComponentInChildren<ZombieAnimationDriver>(true);

        zombieHealth = GetComponent<ZombieHealth>();
        zombieAudio = GetComponent<ZombieAudio>();
    }

    public override void OnNetworkSpawn()
    {
        // El efecto del aturdimiento se ve en todos los clientes, aunque la IA corra solo en el servidor.
        aturdidoNetwork.OnValueChanged += OnAturdidoChanged;
        ActualizarEfectoAturdimiento(aturdidoNetwork.Value);

        // La IA solamente funciona en el servidor.
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        if (agent != null)
        {
            agent.speed = walkSpeed;
        }

        FindClosestPlayer();
    }

    public override void OnNetworkDespawn()
    {
        aturdidoNetwork.OnValueChanged -= OnAturdidoChanged;
        ActualizarEfectoAturdimiento(false);
    }

    // =========================================================
    // ATURDIMIENTO
    // =========================================================

    /// <summary>
    /// Solo servidor. Deja al zombie inmóvil y sin atacar durante "duracion" segundos.
    /// Si ya estaba aturdido se queda con la duración que termine más tarde.
    /// </summary>
    public void Aturdir(float duracion)
    {
        if (!IsServer || duracion <= 0f)
            return;

        if (zombieHealth != null && zombieHealth.IsDead)
            return;

        finAturdimiento =
            Mathf.Max(
                finAturdimiento,
                Time.time + duracion
            );

        // Corta el golpe que estaba por pegar.
        isAttacking = false;

        if (AgenteSobreNavMesh())
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (rutinaAturdimiento == null)
            rutinaAturdimiento = StartCoroutine(RutinaAturdimiento());
    }

    /// <summary>Solo servidor. Termina el aturdimiento ya (por ejemplo, al morir).</summary>
    public void QuitarAturdimiento()
    {
        if (!IsServer)
            return;

        if (rutinaAturdimiento != null)
        {
            StopCoroutine(rutinaAturdimiento);
            rutinaAturdimiento = null;
        }

        finAturdimiento = 0f;
        aturdidoNetwork.Value = false;
    }

    // Esta rutina sigue corriendo aunque ZombieAI quede deshabilitado: solo se corta al morir o despawnear.
    private IEnumerator RutinaAturdimiento()
    {
        aturdidoNetwork.Value = true;

        while (Time.time < finAturdimiento)
            yield return null;

        rutinaAturdimiento = null;
        aturdidoNetwork.Value = false;
    }

    private void OnAturdidoChanged(bool anterior, bool actual)
    {
        ActualizarEfectoAturdimiento(actual);
    }

    /// <summary>
    /// Local. Saca el efecto del aturdimiento y descongela la animación sin esperar a la red.
    /// Lo usa ZombieHealth al morir para que la animación de muerte se reproduzca.
    /// </summary>
    public void QuitarEfectoAturdimiento()
    {
        ActualizarEfectoAturdimiento(false);
    }

    private void ActualizarEfectoAturdimiento(bool aturdido)
    {
        // Congela la animación (incluye Animators en hijos, como el del perro zombie).
        // Solo descongela si la congeló el aturdimiento: el perro congela su propio cadáver al morir
        // y el aviso de "ya no está aturdido" puede llegar después de la muerte.
        if (aturdido != animacionCongelada)
        {
            foreach (Animator anim in GetComponentsInChildren<Animator>(true))
                anim.speed = aturdido ? 0f : 1f;

            animacionCongelada = aturdido;
        }

        if (aturdido)
        {
            if (efectoAturdidoActivo == null && efectoAturdido != null)
            {
                efectoAturdidoActivo =
                    Instantiate(
                        efectoAturdido,
                        transform.position + Vector3.up * alturaEfectoAturdido,
                        Quaternion.identity,
                        transform
                    );
            }

            return;
        }

        if (efectoAturdidoActivo != null)
        {
            Destroy(efectoAturdidoActivo);
            efectoAturdidoActivo = null;
        }
    }

    public void SetRunning(bool isRunning)
    {
        IsRunning = isRunning;
        AplicarVelocidad();
    }

    public void SetSpeedMultiplier(float multiplier)            //
    {
        speedMultiplier = Mathf.Max(0.1f, multiplier);
        AplicarVelocidad();
    }

    // Velocidad del agente: caminar/correr × ronda × obstáculo que esté pisando.
    private void AplicarVelocidad()
    {
        if (agent != null)
        {
            agent.speed =
                (IsRunning
                    ? runSpeed
                    : walkSpeed) * speedMultiplier * multiplicadorTerreno;
        }
    }

    private void ActualizarTerreno()
    {
        if (Time.time < proximaRevisionTerreno)
            return;

        proximaRevisionTerreno = Time.time + 0.2f;

        float multiplicador = ObstaculoTerreno.MultiplicadorZombieEn(transform.position, IsRunning);

        if (Mathf.Approximately(multiplicador, multiplicadorTerreno))
            return;

        multiplicadorTerreno = multiplicador;
        AplicarVelocidad();
    }

    public bool IsBehindTarget()
    {
        if (target == null)
            return false;

        Vector3 directionToZombie =
            (
                transform.position -
                target.position
            ).normalized;

        float dot =
            Vector3.Dot(
                target.forward,
                directionToZombie
            );

        return dot < -0.3f;
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (
            zombieHealth != null &&
            zombieHealth.IsDead
        )
        {
            return;
        }

        if (aturdidoNetwork.Value)
        {
            QuedarseQuieto();
            return;
        }

        ActualizarTerreno();
        FindClosestPlayer();

        if (target == null)
        {
            if (hayJugadorEnZonaSegura)
                QuedarseQuieto();

            return;
        }

        float distanceToTarget =
            Vector3.Distance(
                transform.position,
                target.position
            );

        if (distanceToTarget <= attackRange)
        {
            if (AgenteSobreNavMesh())
            {
                agent.isStopped = true;
            }

            TryAttack();
        }
        else
        {
            if (!isAttacking)
            {
                // Sin NavMesh (por ejemplo, escenario oculto) isStopped lanza un error.
                if (AgenteSobreNavMesh())
                {
                    agent.isStopped = false;
                    agent.SetDestination(
                        target.position
                    );
                }
            }
        }

        UpdateAnimator();
    }

    // =========================================================
    // BUSCAR JUGADOR VIVO
    // =========================================================

    private void FindClosestPlayer()
    {
        if (NetworkManager.Singleton == null)
            return;

        hayJugadorEnZonaSegura = false;

        float closestDistance =
            Mathf.Infinity;

        Transform closestPlayer = null;

        foreach (
            NetworkClient client
            in NetworkManager.Singleton
                .ConnectedClientsList
        )
        {
            if (client.PlayerObject == null)
                continue;

            PlayerHealth playerHealth =
                client.PlayerObject.GetComponent<PlayerHealth>();

            if (playerHealth == null)
                continue;

            // Solamente jugadores vivos.
            if (!playerHealth.IsAlive)
                continue;

            // Modo Historia: igual que a un abatido, se ignora al que está en la zona segura.
            if (EstaEnZonaSegura(client.ClientId))
            {
                hayJugadorEnZonaSegura = true;
                continue;
            }

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

    private static bool EstaEnZonaSegura(ulong clientId)
    {
        return GestorHistoriaPartida.Instance != null &&
            GestorHistoriaPartida.Instance.JugadorEnZonaSegura(clientId);
    }

    // Sin esto seguiría caminando hasta la última posición del jugador
    // y se juntarían todos en la puerta de la zona segura.
    private void QuedarseQuieto()
    {
        if (AgenteSobreNavMesh())
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        UpdateAnimator();
    }

    private bool AgenteSobreNavMesh()
    {
        return agent != null &&
            agent.isActiveAndEnabled &&
            agent.isOnNavMesh;
    }

    // =========================================================
    // ATAQUE
    // =========================================================

    private void TryAttack()
    {
        if (isAttacking)
            return;

        if (Time.time < nextAttackTime)
            return;

        if (target == null)
            return;

        nextAttackTime =
            Time.time +
            attackCooldown;

        isAttacking = true;

        if (AgenteSobreNavMesh())
        {
            agent.isStopped = true;
        }

        if (animationDriver != null)
        {
            animationDriver.PlayAttack();
        }
        else if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        if (zombieAudio != null)
        {
            zombieAudio.PlayAttackSound();
        }

        StartCoroutine(
            ApplyAttackDamage()
        );
    }

    private IEnumerator ApplyAttackDamage()
    {
        yield return new WaitForSeconds(
            attackHitDelay
        );

        if (
            zombieHealth != null &&
            zombieHealth.IsDead
        )
        {
            isAttacking = false;
            yield break;
        }

        // Aturdido durante la animación del golpe: el golpe no llega.
        if (aturdidoNetwork.Value)
        {
            isAttacking = false;
            yield break;
        }

        if (target != null)
        {
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
                    // Puede haber muerto o quedado abatido
                    // durante la animación del golpe.
                    if (!playerHealth.IsAlive)
                    {
                        isAttacking = false;
                        yield break;
                    }

                    /*playerHealth.TakeDamage(
                        attackDamage
                    );*/

                    DifficultySettings settings =
                        RoundManager.Instance != null
                            ? RoundManager.Instance.ActiveSettings
                            : null;

                    int damage =
                        (useDifficultyDamageOverride && settings != null && settings.zombieDamage > 0)
                            ? settings.zombieDamage
                            : attackDamage;

                    Debug.Log($"[ZombieAI] settings={(settings != null ? settings.name : "NULL")} | zombieDamage={(settings != null ? settings.zombieDamage : -1)} | prefab={attackDamage}");

                    playerHealth.TakeDamage(damage);

                    Debug.Log(
                        "[ZombieAI] Zombie atacó a Player " +
                        playerHealth.name
                    );
                }
            }
        }

        isAttacking = false;
    }

    // =========================================================
    // ANIMACIÓN
    // =========================================================

    private void UpdateAnimator()
    {
        if (
            agent == null ||
            !agent.isOnNavMesh
        )
        {
            return;
        }

        float speed =
            agent.velocity.magnitude;

        // Un enemigo con AnimationDriver puede tener el Animator en un hijo
        // (como DemonDog), por eso se evalúa antes del Animator legacy de la raíz.
        if (animationDriver != null)
        {
            animationDriver.SetMovement(
                speed,
                Mathf.Max(0.01f, agent.speed)
            );
            return;
        }

        if (animator == null)
            return;

        animator.SetFloat(
            "Speed",
            speed
        );
    }
}