using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using Unity.Netcode.Components;

public class ZombieAI : NetworkBehaviour
{
    [Header("Ataque")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private int attackDamage = 10;

    [Tooltip("Activado = DifficultySettings.zombieDamage puede reemplazar el daño del prefab. Para enemigos con daño propio, como el perro zombie, desactivarlo.")]
    [SerializeField] private bool useDifficultyDamageOverride = true;

    [SerializeField] private float attackCooldown = 0.9f;

    [SerializeField] private float attackHitDelay = 0.35f;
    [Tooltip("Tiempo máximo de seguridad para mantener bloqueado el movimiento durante una animación de ataque.")]
    [SerializeField] private float attackAnimationMaxDuration = 3f;

    [Header("Velocidad")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float runSpeed = 3f;
    [Range(1f, 1.5f)]
    [Tooltip("Aumento visual/base que se conserva para caminar. Al correr se garantiza una velocidad base mínima de 3 m/s.")]
    [SerializeField] private float baseSpeedBoost = 1.10f;

    [Header("Animación zombie mejorada")]
    [Tooltip("Suavizado de los parámetros direccionales del Animator.")]
    [SerializeField] private float locomotionDampTime = 0.12f;
    [Range(0f, 1f)]
    [Tooltip("Entre los zombies que corren, porcentaje que visualmente usa Sprint.")]
    [SerializeField] private float sprintChance = 0.25f;
    [Tooltip("Al empezar a moverse vuelve a elegir una variante Walk/Run para que no todos caminen igual.")]
    [SerializeField] private bool rerollLocomotionOnMoveStart = true;
    [SerializeField] private float idleVariantChangeInterval = 5f;

    [Header("Reacción al daño")]
    [Tooltip("Solo afecta al zombie humano legacy (no al perro). Si está activo, el NavMeshAgent se queda quieto hasta que termina el estado HitReaction.")]
    [SerializeField] private bool freezeDuringHitReaction = true;
    [SerializeField] private float hitReactionEnterTimeout = 0.35f;
    [SerializeField] private float hitReactionMaxDuration = 3f;

    [Header("Aparición desde la tierra")]
    [Tooltip("Solo se aplica al zombie humano legacy. Al aparecer reproduce OutOfGround -> Stand antes de empezar a perseguir.")]
    [SerializeField] private bool emergeFromGround = true;
    [Tooltip("Opcional. Si queda vacío se genera automáticamente una ráfaga simple de tierra/polvo.")]
    [SerializeField] private GameObject groundSpawnEffectPrefab;
    [SerializeField] private float spawnStateEnterTimeout = 0.5f;
    [SerializeField] private float spawnMaxDuration = 8f;

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

    // Se usa para que todos los clientes arranquen la presentación de salida de la tierra a la vez.
    private NetworkVariable<bool> spawningNetwork =
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
    private NetworkAnimator networkAnimator;
    private ZombieAnimationDriver animationDriver;

    private readonly HashSet<int> animatorParameters = new HashSet<int>();
    private Coroutine hitReactionRoutine;
    private Coroutine attackLockRoutine;
    private Coroutine spawnRoutine;
    private bool spawnGameplayLock;

    // Durante OutOfGround/CrawlToStand consumimos únicamente el root motion X/Z
    // del clip para que el zombie no vuelva de golpe a la posición original al
    // cambiar de animación. El NavMeshAgent se desacopla temporalmente y se
    // resincroniza al finalizar el spawn.
    private bool spawnRootMotionActive;
    private bool spawnAgentSettingsCaptured;
    private bool spawnPreviousUpdatePosition = true;

    private bool visualSprint;
    private bool wasMoving;
    private bool lastRunningState;
    private float nextIdleVariantChange;
    private int currentIdleVariant;
    private int currentWalkVariant;
    private int currentRunVariant;
    private int currentSprintVariant;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int RunningHash = Animator.StringToHash("Running");
    private static readonly int SprintHash = Animator.StringToHash("Sprint");
    private static readonly int IdleVariantHash = Animator.StringToHash("IdleVariant");
    private static readonly int WalkVariantHash = Animator.StringToHash("WalkVariant");
    private static readonly int RunVariantHash = Animator.StringToHash("RunVariant");
    private static readonly int SprintVariantHash = Animator.StringToHash("SprintVariant");
    private static readonly int AttackIndexHash = Animator.StringToHash("AttackIndex");
    private static readonly int HitIndexHash = Animator.StringToHash("HitIndex");
    private static readonly int SpawningHash = Animator.StringToHash("Spawning");
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
        networkAnimator = GetComponent<NetworkAnimator>();
        if (networkAnimator == null)
            networkAnimator = GetComponentInChildren<NetworkAnimator>(true);

        CacheAnimatorParameters();

        animationDriver = GetComponent<ZombieAnimationDriver>();
        if (animationDriver == null)
            animationDriver = GetComponentInChildren<ZombieAnimationDriver>(true);

        // La navegación manda sobre el movimiento real. Las animaciones del pack se usan
        // como presentación visual para evitar que root motion y NavMeshAgent se peleen.
        if (animationDriver == null && animator != null)
            animator.applyRootMotion = false;

        zombieHealth = GetComponent<ZombieHealth>();
        zombieAudio = GetComponent<ZombieAudio>();
    }

    public override void OnNetworkSpawn()
    {
        aturdidoNetwork.OnValueChanged += OnAturdidoChanged;
        spawningNetwork.OnValueChanged += OnSpawningChanged;

        ActualizarEfectoAturdimiento(aturdidoNetwork.Value);
        AplicarPresentacionSpawn(spawningNetwork.Value);

        // A diferencia de la versión anterior, el componente queda habilitado en clientes:
        // Update sale inmediatamente si no somos servidor, pero así las NetworkVariables
        // y la presentación visual del spawn siguen funcionando de forma segura.
        if (!IsServer)
            return;

        if (agent != null)
            agent.speed = walkSpeed;

        InicializarVariantesVisuales();
        FindClosestPlayer();

        if (emergeFromGround && animationDriver == null)
            IniciarAparicionDesdeTierra();
    }

    public override void OnNetworkDespawn()
    {
        aturdidoNetwork.OnValueChanged -= OnAturdidoChanged;
        spawningNetwork.OnValueChanged -= OnSpawningChanged;

        ActualizarEfectoAturdimiento(false);

        if (hitReactionRoutine != null)
        {
            StopCoroutine(hitReactionRoutine);
            hitReactionRoutine = null;
        }

        if (attackLockRoutine != null)
        {
            StopCoroutine(attackLockRoutine);
            attackLockRoutine = null;
        }

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        FinalizarRootMotionSpawn();
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

        if (attackLockRoutine != null)
        {
            StopCoroutine(attackLockRoutine);
            attackLockRoutine = null;
        }

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
        bool changed = IsRunning != isRunning;
        IsRunning = isRunning;

        if (changed)
        {
            if (IsRunning)
            {
                currentRunVariant = Random.Range(0, 2);
                currentSprintVariant = Random.Range(0, 2);
                visualSprint = Random.value < sprintChance;
            }
            else
            {
                currentWalkVariant = Random.Range(0, 5);
                visualSprint = false;
            }

            AplicarVariantesLocomocionAnimator();
        }

        lastRunningState = IsRunning;
        SetAnimatorBool(RunningHash, IsRunning);
        SetAnimatorBool(SprintHash, visualSprint);

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
            // Al correr queremos 3 m/s reales como base, aunque el prefab conserve
            // un valor serializado antiguo menor. Los multiplicadores de ronda y terreno
            // siguen aplicándose después, como antes.
            float baseMovementSpeed = IsRunning
                ? Mathf.Max(3f, runSpeed)
                : walkSpeed * Mathf.Max(1f, baseSpeedBoost);

            agent.speed =
                baseMovementSpeed *
                speedMultiplier *
                multiplicadorTerreno;
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

        if (spawnGameplayLock)
        {
            QuedarseQuieto();
            return;
        }

        if (hitReactionRoutine != null)
        {
            QuedarseQuieto();
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
            int attackIndex = ElegirAtaque();
            SetAnimatorInt(AttackIndexHash, attackIndex);
            SetNetworkedTrigger("Attack");
        }

        if (zombieAudio != null)
        {
            zombieAudio.PlayAttackSound();
        }

        StartCoroutine(
            ApplyAttackDamage()
        );

        if (animationDriver == null && animator != null)
        {
            if (attackLockRoutine != null)
                StopCoroutine(attackLockRoutine);

            attackLockRoutine = StartCoroutine(
                FinalizarAtaqueAlTerminarAnimacion()
            );
        }
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
            if (animationDriver != null || animator == null)
                isAttacking = false;
            yield break;
        }

        // Aturdido durante la animación del golpe: el golpe no llega.
        if (aturdidoNetwork.Value)
        {
            if (animationDriver != null || animator == null)
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
                        if (animationDriver != null || animator == null)
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


        if (animationDriver != null || animator == null)
            isAttacking = false;
    }

    private IEnumerator FinalizarAtaqueAlTerminarAnimacion()
    {
        float enterElapsed = 0f;
        bool entered = false;

        while (enterElapsed < 0.4f)
        {
            if (animator == null || !animator.enabled)
                break;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsTag("Attack"))
            {
                entered = true;
                break;
            }

            enterElapsed += Time.deltaTime;
            yield return null;
        }

        if (entered)
        {
            float elapsed = 0f;
            while (elapsed < Mathf.Max(0.5f, attackAnimationMaxDuration))
            {
                if (animator == null || !animator.enabled)
                    break;

                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                bool attacking = state.IsTag("Attack");

                if (!attacking && !animator.IsInTransition(0))
                    break;

                QuedarseQuieto();
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        isAttacking = false;
        attackLockRoutine = null;
    }

    // =========================================================
    // ANIMACIÓN
    // =========================================================

    private void UpdateAnimator()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        float speed = agent.velocity.magnitude;

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

        SetAnimatorFloat(SpeedHash, speed, locomotionDampTime);

        Vector3 localVelocity = transform.InverseTransformDirection(agent.velocity);
        Vector2 direction = new Vector2(localVelocity.x, localVelocity.z);
        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();

        SetAnimatorFloat(MoveXHash, direction.x, locomotionDampTime);
        SetAnimatorFloat(MoveYHash, direction.y, locomotionDampTime);
        SetAnimatorBool(RunningHash, IsRunning);
        SetAnimatorBool(SprintHash, visualSprint);

        bool moving = speed > 0.05f;

        if (moving && !wasMoving && rerollLocomotionOnMoveStart)
        {
            RerollLocomotionVariants();
        }

        if (lastRunningState != IsRunning)
        {
            lastRunningState = IsRunning;
            RerollLocomotionVariants();
        }

        if (!moving)
        {
            if (wasMoving || Time.time >= nextIdleVariantChange)
            {
                currentIdleVariant = (currentIdleVariant + 1 + DeterministicInt(3, 41)) % 4;
                nextIdleVariantChange = Time.time + Mathf.Max(1f, idleVariantChangeInterval);
                SetAnimatorFloat(IdleVariantHash, currentIdleVariant, 0f);
            }
        }

        wasMoving = moving;
    }

    // =========================================================
    // HIT REACTION
    // =========================================================

    /// <summary>
    /// Solo servidor. Dispara una reacción de impacto del zombie humano y, si está
    /// habilitado, mantiene quieto el NavMeshAgent hasta que el Animator termina
    /// el estado etiquetado como HitReaction. Los AnimationDriver (por ejemplo
    /// DemonDog) conservan su comportamiento anterior.
    /// </summary>
    public void ReaccionarAlImpacto(int variante)
    {
        if (!IsServer || animationDriver != null)
            return;

        if (zombieHealth != null && zombieHealth.IsDead)
            return;

        if (spawnGameplayLock)
            return;

        if (attackLockRoutine != null)
        {
            StopCoroutine(attackLockRoutine);
            attackLockRoutine = null;
        }
        isAttacking = false;

        SetAnimatorInt(HitIndexHash, Mathf.Clamp(variante, 0, 2));

        int hitHash = Animator.StringToHash("Hit");
        if (animator != null && TieneParametro(hitHash))
            animator.SetTrigger(hitHash);

        if (!freezeDuringHitReaction)
            return;

        if (hitReactionRoutine != null)
            StopCoroutine(hitReactionRoutine);

        hitReactionRoutine = StartCoroutine(BloquearDuranteHitReaction());
    }

    private IEnumerator BloquearDuranteHitReaction()
    {
        QuedarseQuieto();

        float enterElapsed = 0f;
        bool entered = false;

        while (enterElapsed < Mathf.Max(0.05f, hitReactionEnterTimeout))
        {
            if (animator == null || !animator.enabled)
                break;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsTag("HitReaction"))
            {
                entered = true;
                break;
            }

            enterElapsed += Time.deltaTime;
            yield return null;
        }

        if (entered)
        {
            float elapsed = 0f;

            while (elapsed < Mathf.Max(0.25f, hitReactionMaxDuration))
            {
                if (animator == null || !animator.enabled)
                    break;

                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                bool stillInHit = state.IsTag("HitReaction");

                if (!stillInHit && !animator.IsInTransition(0))
                    break;

                QuedarseQuieto();
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        hitReactionRoutine = null;
    }

    // =========================================================
    // APARICIÓN DESDE LA TIERRA
    // =========================================================

    /// <summary>
    /// Permite volver a reproducir la aparición en sistemas que reutilizan el mismo
    /// NetworkObject, por ejemplo el respawn del tutorial.
    /// </summary>
    public void ReproducirAparicionDesdeTierra()
    {
        if (!IsServer || !emergeFromGround || animationDriver != null)
            return;

        IniciarAparicionDesdeTierra();
    }

    private void IniciarAparicionDesdeTierra()
    {
        if (!IsServer || spawnRoutine != null)
            return;

        spawnGameplayLock = true;
        PrepararRootMotionSpawn();

        // Asegura que el cambio false -> true vuelva a disparar presentación
        // incluso si el mismo zombie se reutiliza.
        if (spawningNetwork.Value)
            spawningNetwork.Value = false;

        spawningNetwork.Value = true;
        spawnRoutine = StartCoroutine(RutinaAparicionDesdeTierra());
    }

    private IEnumerator RutinaAparicionDesdeTierra()
    {
        QuedarseQuieto();

        float enterElapsed = 0f;
        bool entered = false;

        while (enterElapsed < Mathf.Max(0.05f, spawnStateEnterTimeout))
        {
            if (animator == null || !animator.enabled)
                break;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsTag("Spawning"))
            {
                entered = true;
                break;
            }

            enterElapsed += Time.deltaTime;
            yield return null;
        }

        float elapsed = 0f;

        if (entered)
        {
            while (elapsed < Mathf.Max(1f, spawnMaxDuration))
            {
                if (animator == null || !animator.enabled)
                    break;

                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                bool spawning = state.IsTag("Spawning");

                if (!spawning && !animator.IsInTransition(0))
                    break;

                QuedarseQuieto();
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            // Fallback: si por cualquier motivo el controller aún no fue actualizado,
            // no dejamos al zombie bloqueado indefinidamente.
            yield return new WaitForSeconds(0.2f);
        }

        spawningNetwork.Value = false;
        FinalizarRootMotionSpawn();
        spawnGameplayLock = false;
        spawnRoutine = null;
    }

    /// <summary>
    /// Desacopla temporalmente el NavMeshAgent para que el desplazamiento X/Z
    /// authored en los clips de aparición pueda mover el GameObject real.
    /// </summary>
    private void PrepararRootMotionSpawn()
    {
        spawnRootMotionActive = true;

        if (animator != null)
            animator.applyRootMotion = true;

        if (agent == null || !agent.isActiveAndEnabled)
            return;

        spawnPreviousUpdatePosition = agent.updatePosition;
        spawnAgentSettingsCaptured = true;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        // Evita que el NavMeshAgent vuelva a escribir su posición mientras
        // OnAnimatorMove desplaza el zombie durante la salida de la tierra.
        agent.updatePosition = false;
    }

    /// <summary>
    /// Unity llama a este método después de evaluar el Animator. Solo durante
    /// el spawn consumimos deltaPosition. El eje Y se mantiene horneado en la
    /// pose para conservar correctamente el efecto de emerger del suelo.
    /// </summary>
    private void OnAnimatorMove()
    {
        if (!spawnRootMotionActive || !IsServer || animator == null)
            return;

        Vector3 delta = animator.deltaPosition;
        delta.y = 0f;

        if (
            float.IsNaN(delta.x) || float.IsNaN(delta.z) ||
            float.IsInfinity(delta.x) || float.IsInfinity(delta.z)
        )
        {
            return;
        }

        transform.position += delta;
    }

    /// <summary>
    /// Vuelve a enganchar el agente a la posición final alcanzada por la
    /// animación. Se proyecta sobre el NavMesh para mantener al zombie a nivel
    /// del suelo y evitar cualquier salto vertical al empezar a perseguir.
    /// </summary>
    private void FinalizarRootMotionSpawn()
{
    if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
    {
        agent.isStopped = true;
        agent.ResetPath();
    }
        if (!spawnRootMotionActive && !spawnAgentSettingsCaptured)
            return;

        spawnRootMotionActive = false;

        if (animator != null)
            animator.applyRootMotion = false;

        if (agent == null || !agent.isActiveAndEnabled)
        {
            spawnAgentSettingsCaptured = false;
            return;
        }

        Vector3 finalPosition = transform.position;

        if (
            NavMesh.SamplePosition(
                finalPosition,
                out NavMeshHit navHit,
                2f,
                agent.areaMask
            )
        )
        {
            finalPosition = navHit.position;
        }

        // Activamos updatePosition solo para resincronizar internamente el agente.
        // Warp actualiza tanto su posición interna como el Transform en el mismo frame.
        agent.updatePosition = true;

        if (agent.isOnNavMesh)
        {
            agent.Warp(finalPosition);
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        else
        {
            transform.position = finalPosition;
            agent.nextPosition = finalPosition;
        }

        if (spawnAgentSettingsCaptured)
            agent.updatePosition = spawnPreviousUpdatePosition;

        spawnAgentSettingsCaptured = false;
    }

    private void OnSpawningChanged(bool anterior, bool actual)
    {
        AplicarPresentacionSpawn(actual);
    }

    private void AplicarPresentacionSpawn(bool spawning)
    {
        if (animationDriver != null)
            return;

        SetAnimatorBool(SpawningHash, spawning);

        if (!spawning)
            return;

        if (animator != null && TieneParametro(Animator.StringToHash("Spawn")))
            animator.SetTrigger("Spawn");

        CrearEfectoSalidaTierra();
    }

    private void CrearEfectoSalidaTierra()
    {
        if (groundSpawnEffectPrefab != null)
        {
            GameObject fx = Instantiate(
                groundSpawnEffectPrefab,
                transform.position + Vector3.up * 0.03f,
                Quaternion.identity
            );
            Destroy(fx, 5f);
            return;
        }

        // Fallback sin assets externos: una ráfaga corta de polvo/tierra.
        GameObject go = new GameObject("ZombieSpawnGroundFX");
        go.transform.position = transform.position + Vector3.up * 0.04f;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.65f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.24f, 0.12f, 0.045f, 0.95f),
            new Color(0.48f, 0.30f, 0.14f, 0.75f)
        );
        main.gravityModifier = 0.65f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 48;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 30),
            new ParticleSystem.Burst(0.18f, 12)
        });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 70f;
        shape.radius = 0.55f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");

        if (shader != null)
        {
            Material material = new Material(shader);
            material.color = new Color(0.36f, 0.20f, 0.08f, 1f);
            renderer.material = material;
            Destroy(material, 3f);
        }

        ps.Play();
        Destroy(go, 3f);
    }

    // =========================================================
    // VARIANTES / PARÁMETROS DEL ANIMATOR
    // =========================================================

    private void InicializarVariantesVisuales()
    {
        currentWalkVariant = DeterministicInt(5, 13);
        currentRunVariant = DeterministicInt(2, 17);
        currentSprintVariant = DeterministicInt(2, 19);
        visualSprint = IsRunning && Deterministic01(29) < sprintChance;
        currentIdleVariant = DeterministicInt(4, 7);
        nextIdleVariantChange = Time.time + Mathf.Max(1f, idleVariantChangeInterval);
        lastRunningState = IsRunning;

        SetAnimatorBool(RunningHash, IsRunning);
        SetAnimatorBool(SprintHash, visualSprint);
        SetAnimatorFloat(IdleVariantHash, currentIdleVariant, 0f);
        AplicarVariantesLocomocionAnimator();
    }

    private void RerollLocomotionVariants()
    {
        currentWalkVariant = Random.Range(0, 5);
        currentRunVariant = Random.Range(0, 2);
        currentSprintVariant = Random.Range(0, 2);

        if (IsRunning)
            visualSprint = Random.value < sprintChance;
        else
            visualSprint = false;

        SetAnimatorBool(SprintHash, visualSprint);
        AplicarVariantesLocomocionAnimator();
    }

    private void AplicarVariantesLocomocionAnimator()
    {
        SetAnimatorFloat(WalkVariantHash, currentWalkVariant, 0f);
        SetAnimatorFloat(RunVariantHash, currentRunVariant, 0f);
        SetAnimatorFloat(SprintVariantHash, currentSprintVariant, 0f);
    }

    private int ElegirAtaque()
    {
        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.001f)
            {
                float angle = Vector3.SignedAngle(transform.forward, toTarget.normalized, Vector3.up);

                if (Mathf.Abs(angle) > 135f)
                    return 3; // Attack 180

                if (angle > 55f)
                    return 4; // Attack 90 Left

                if (angle < -55f)
                    return 5; // Attack 90 Right
            }
        }

        // 0/1/2 = ataques base, 6 = forward, 7 = forward jump.
        int[] frontAttacks = IsRunning
            ? new[] { 0, 1, 2, 6, 7 }
            : new[] { 0, 1, 2, 6 };

        int index = DeterministicInt(frontAttacks.Length, Mathf.FloorToInt(Time.time * 10f) + 53);
        return frontAttacks[index];
    }

    private void CacheAnimatorParameters()
    {
        animatorParameters.Clear();

        if (animator == null)
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
            animatorParameters.Add(parameter.nameHash);
    }

    private bool TieneParametro(int hash)
    {
        return animator != null && animatorParameters.Contains(hash);
    }

    private void SetAnimatorFloat(int hash, float value, float dampTime)
    {
        if (!TieneParametro(hash))
            return;

        if (dampTime > 0f)
            animator.SetFloat(hash, value, dampTime, Time.deltaTime);
        else
            animator.SetFloat(hash, value);
    }

    private void SetAnimatorBool(int hash, bool value)
    {
        if (TieneParametro(hash))
            animator.SetBool(hash, value);
    }

    private void SetAnimatorInt(int hash, int value)
    {
        if (TieneParametro(hash))
            animator.SetInteger(hash, value);
    }

    private void SetNetworkedTrigger(string triggerName)
    {
        if (animator == null)
            return;

        int hash = Animator.StringToHash(triggerName);
        if (!TieneParametro(hash))
            return;

        if (networkAnimator != null && networkAnimator.isActiveAndEnabled)
            networkAnimator.SetTrigger(triggerName);
        else
            animator.SetTrigger(hash);
    }

    private float Deterministic01(int salt)
    {
        uint id;

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            id = (uint)(NetworkObject.NetworkObjectId & 0xffffffffUL);
        }
        else
        {
            // Fallback local para elegir variantes antes de que exista NetworkObjectId.
            // Evita Object.GetInstanceID(), que en versiones nuevas de Unity está obsoleto.
            unchecked
            {
                Vector3 p = transform.position;
                id = (uint)(gameObject.name != null ? gameObject.name.GetHashCode() : 0);
                id ^= (uint)Mathf.RoundToInt(p.x * 100f) * 73856093u;
                id ^= (uint)Mathf.RoundToInt(p.y * 100f) * 19349663u;
                id ^= (uint)Mathf.RoundToInt(p.z * 100f) * 83492791u;
            }
        }

        uint x = id ^ (uint)(salt * 747796405);
        x ^= x >> 16;
        x *= 2246822519u;
        x ^= x >> 13;
        return (x & 0x00ffffffu) / 16777215f;
    }

    private int DeterministicInt(int maxExclusive, int salt)
    {
        if (maxExclusive <= 1)
            return 0;

        return Mathf.Clamp(
            Mathf.FloorToInt(Deterministic01(salt) * maxExclusive),
            0,
            maxExclusive - 1
        );
    }

}