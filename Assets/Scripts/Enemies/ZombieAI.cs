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
    [Tooltip("Al aparecer, cada zombie decide si su estilo de carrera usa Sprint. Esa elección se conserva hasta que muere.")]
    [SerializeField] private float sprintChance = 0.25f;

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

    [Header("Alcance del golpe")]
    [Tooltip("Diferencia de altura máxima (m) con el objetivo para poder pegarle. Evita golpes a otro piso.")]
    [SerializeField] private float alturaMaximaAtaque = 1.6f;
    [Tooltip("Capas que tapan el golpe (paredes, puertas...). Los zombies, los jugadores y la torreta objetivo nunca tapan.")]
    [SerializeField] private LayerMask capasBloqueanAtaque = ~(1 << 2);
    [Tooltip("Altura (m) desde los pies de la línea que se revisa entre el zombie y el objetivo.")]
    [SerializeField] private float alturaLineaAtaque = 1.1f;

    [Header("Estabilidad en espacios cerrados")]
    [Tooltip("Distancia a la que el zombie intenta colocarse respecto del jugador. Debe ser menor que Attack Range.")]
    [SerializeField] private float attackApproachDistance = 1.35f;
    [Tooltip("Cada cuánto se recalcula el destino. Evita que varios zombies cambien de ruta violentamente cada frame.")]
    [SerializeField] private float repathInterval = 0.15f;
    [Tooltip("Radio usado para buscar un punto válido de NavMesh alrededor de la posición de aproximación.")]
    [SerializeField] private float approachSampleRadius = 0.75f;
    [Tooltip("Velocidad con la que el zombie se orienta hacia el jugador antes de atacar.")]
    [SerializeField] private float attackFacingSpeed = 540f;
    [Tooltip("Ángulo máximo permitido para iniciar un ataque. Si no está mirando al jugador, primero gira en el lugar.")]
    [Range(10f, 90f)]
    [SerializeField] private float attackFacingTolerance = 55f;
    [Tooltip("El ForwardJump tiene mucho desplazamiento visual. Desactivado evita que se use en espacios cerrados.")]
    [SerializeField] private bool allowForwardJumpAttack = false;

    [Header("Aturdimiento (granada aturdidora)")]
    [Tooltip("Efecto que se muestra encima del zombie mientras está aturdido. Vacío = sin efecto visual.")]
    [SerializeField] private GameObject efectoAturdido;
    [SerializeField] private float alturaEfectoAturdido = 1.8f;

    public bool IsRunning { get; private set; }
    readonly NetworkVariable<byte> pvpveKind=new();
    public DeadQuarantine.PvPvE.PvPvEZombieKind PvPvEKind=>(DeadQuarantine.PvPvE.PvPvEZombieKind)pvpveKind.Value;
    bool InPvPvE=>DeadQuarantine.PvPvE.PvPvEZombieDirector.Active||(zombieHealth&&zombieHealth.IsPvPvEZombie);
    public void SetPvPvEKindServer(DeadQuarantine.PvPvE.PvPvEZombieKind kind){if(!IsServer||!InPvPvE)return;pvpveKind.Value=(byte)kind;ApplyPvPvEKind(0,pvpveKind.Value);}
    void ApplyPvPvEKind(byte oldValue,byte value){if(InPvPvE)SetRunning(value!=(byte)DeadQuarantine.PvPvE.PvPvEZombieKind.Walker);}


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

    /// <summary>true mientras hace la animación de salir de la tierra (en el servidor y en los clientes).</summary>
    public bool EstaApareciendo => spawningNetwork.Value;

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

    // Estas variantes se eligen UNA SOLA VEZ cuando aparece el zombie y
    // se conservan durante toda su vida.
    private bool assignedSprintVisual;
    private bool visualSprint;
    private int currentIdleVariant;
    private int currentWalkVariant;
    private int currentRunVariant;
    private int currentSprintVariant;
    private int currentFrontAttackVariant;
    private int currentRunningFrontAttackVariant;

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
    private float nextPathUpdateTime;
    private Transform target;
    // Si el objetivo es una torreta (Supervivencia) en vez de un jugador.
    private TorretaAutomatica targetTorreta;
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
        pvpveKind.OnValueChanged+=ApplyPvPvEKind;
        if(DeadQuarantine.PvPvE.PvPvEZombieDirector.Active){ApplyPvPvEKind(0,pvpveKind.Value);if(!IsServer&&agent)agent.enabled=false;}
        aturdidoNetwork.OnValueChanged += OnAturdidoChanged;
        spawningNetwork.OnValueChanged += OnSpawningChanged;

        ActualizarEfectoAturdimiento(aturdidoNetwork.Value);
        AplicarPresentacionSpawn(spawningNetwork.Value);

        // Cada instancia elige sus variantes una sola vez al aparecer. Como la
        // selección usa NetworkObjectId, todos los clientes obtienen la misma
        // combinación para ese zombie sin volver a sortear durante su vida.
        InicializarVariantesVisuales();

        // A diferencia de la versión anterior, el componente queda habilitado en clientes:
        // Update sale inmediatamente si no somos servidor, pero así las NetworkVariables
        // y la presentación visual del spawn siguen funcionando de forma segura.
        if (!IsServer)
            return;

        if (agent != null)
        {
            agent.speed = walkSpeed;
            // El destino ya se calcula a una distancia del jugador, por eso no
            // necesitamos que el agente intente meterse dentro de su centro.
            agent.stoppingDistance = 0.05f;
            agent.autoBraking = true;
        }

        FindClosestPlayer();

        if (emergeFromGround && animationDriver == null)
            IniciarAparicionDesdeTierra();
    }

    public override void OnNetworkDespawn()
    {
        pvpveKind.OnValueChanged-=ApplyPvPvEKind;
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
        IsRunning = isRunning;

        // No volvemos a elegir ninguna animación acá. El zombie conserva las
        // variantes que recibió al aparecer; solo activamos visualmente Sprint
        // si este zombie fue asignado como "sprint" y actualmente está corriendo.
        visualSprint = IsRunning && assignedSprintVisual;

        SetAnimatorBool(RunningHash, IsRunning);
        SetAnimatorBool(SprintHash, visualSprint);
        AplicarVariantesLocomocionAnimator();

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

        if(InPvPvE&&(!DeadQuarantine.PvPvE.Networking.PvPvECombat.Active||!DeadQuarantine.PvPvE.Networking.PvPvECombat.Active.IsPlaying)){QuedarseQuieto();return;}
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
            if (InPvPvE || hayJugadorEnZonaSegura)
                QuedarseQuieto();

            return;
        }

        // Plana (sin altura) hasta el jugador, o hasta el borde de la torreta.
        float distanceToTarget = DistanciaAlObjetivo();

        if (distanceToTarget <= attackRange && PuedeGolpearObjetivo())
        {
            if (AgenteSobreNavMesh())
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }

            // En espacios cerrados primero gira sobre su propio eje. Evita que
            // el agente intente rodear al jugador para "buscar" el ángulo.
            RotarHaciaObjetivo();

            if (EstaMirandoObjetivoParaAtacar())
                TryAttack();
        }
        else if (!isAttacking)
        {
            // En vez de mandar a todos los zombies al centro exacto del jugador,
            // cada uno busca un punto de aproximación desde el lado por el que llega.
            // Esto reduce mucho los círculos, empujones y cambios de ruta en pasillos.
            if (AgenteSobreNavMesh())
            {
                agent.isStopped = false;

                if (Time.time >= nextPathUpdateTime)
                {
                    nextPathUpdateTime =
                        Time.time + Mathf.Max(0.05f, repathInterval);

                    Vector3 destination =
                        CalcularDestinoAproximacion();

                    if (
                        !agent.hasPath ||
                        (agent.destination - destination).sqrMagnitude > 0.04f
                    )
                    {
                        agent.SetDestination(destination);
                    }
                }
            }
        }

        UpdateAnimator();
    }

    // =========================================================
    // BUSCAR JUGADOR VIVO (O TORRETA SANA)
    // =========================================================

    private void FindClosestPlayer()
    {
        if(DeadQuarantine.PvPvE.PvPvEZombieDirector.Active){targetTorreta=null;target=DeadQuarantine.PvPvE.PvPvEZombieDirector.Active.Closest(transform.position);return;}

        if (NetworkManager.Singleton == null)
            return;

        hayJugadorEnZonaSegura = false;

        float closestDistance =
            Mathf.Infinity;

        Transform closestPlayer = null;
        int jugadoresVivos = 0;
        int jugadoresEnMinijuego = 0;

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

            jugadoresVivos++;

            // El servidor omite al jugador que está usando el arcade.
            PauseController pauseController =
                client.PlayerObject.GetComponent<PauseController>();
            if (pauseController != null && pauseController.EnMinijuegoNet.Value)
            {
                jugadoresEnMinijuego++;
                continue;
            }

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

        // Si todos los jugadores vivos están en el minijuego, no se elige tampoco
        // una torreta como objetivo: los zombis quedan quietos hasta que alguien salga.
        if (jugadoresVivos > 0 && jugadoresEnMinijuego == jugadoresVivos)
        {
            hayJugadorEnZonaSegura = true;
            targetTorreta = null;
            target = null;
            return;
        }

        // Torretas sanas: compiten con los jugadores por cercanía (se mide hasta su borde, no hasta el centro).
        TorretaAutomatica closestTorreta = null;

        foreach (TorretaAutomatica torreta in TorretaAutomatica.Todas)
        {
            if (torreta == null || !torreta.IsSpawned || torreta.EstaRota)
                continue;

            float distance =
                Vector3.Distance(
                    transform.position,
                    torreta.PuntoMasCercano(transform.position)
                );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTorreta = torreta;
            }
        }

        targetTorreta = closestTorreta;
        target = closestTorreta != null ? closestTorreta.transform : closestPlayer;
    }

    // Distancia plana (sin altura) hasta el jugador, o hasta el borde de la torreta
    // (el NavMeshObstacle no deja llegar a su centro).
    private float DistanciaAlObjetivo()
    {
        if (target == null)
            return Mathf.Infinity;

        return DistanciaPlana(transform.position, PuntoObjetivo());
    }

    // La distancia de ataque es plana: sin estos chequeos un zombie del otro lado de una pared
    // (o en otro piso) a menos de attackRange le pegaría igual.
    private readonly RaycastHit[] impactosAtaque = new RaycastHit[16];

    private bool PuedeGolpearObjetivo()
    {
        if (target == null)
            return false;

        Vector3 punto = PuntoObjetivo();

        float pieObjetivo = targetTorreta != null ? targetTorreta.transform.position.y : target.position.y;

        if (Mathf.Abs(pieObjetivo - transform.position.y) > alturaMaximaAtaque)
            return false;

        Vector3 origen = transform.position + Vector3.up * alturaLineaAtaque;
        Vector3 destino = new Vector3(punto.x, pieObjetivo + alturaLineaAtaque, punto.z);
        Vector3 haciaDestino = destino - origen;
        float distancia = haciaDestino.magnitude;

        if (distancia < 0.01f)
            return true;

        int cantidad = Physics.RaycastNonAlloc(
            origen,
            haciaDestino / distancia,
            impactosAtaque,
            distancia,
            capasBloqueanAtaque,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < cantidad; i++)
        {
            Collider impacto = impactosAtaque[i].collider;

            if (impacto == null ||
                impacto.transform.IsChildOf(transform) ||
                impacto.transform.IsChildOf(target))
                continue;

            if (impacto.GetComponentInParent<ZombieHealth>() != null ||
                impacto.GetComponentInParent<PlayerHealth>() != null)
                continue;

            return false;
        }

        return true;
    }

    // Centro del jugador, o el punto de la torreta más cercano al zombie.
    private Vector3 PuntoObjetivo()
    {
        if (targetTorreta != null)
            return targetTorreta.PuntoMasCercano(transform.position);

        return target != null ? target.position : transform.position;
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

    private static float DistanciaPlana(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    /// <summary>
    /// Devuelve un punto cercano al jugador, pero no su centro. El zombie conserva
    /// aproximadamente el lado desde el cual se está acercando, por lo que varios
    /// enemigos dejan de intentar cruzarse entre sí para ocupar el mismo punto.
    /// </summary>
    private Vector3 CalcularDestinoAproximacion()
    {
        if (target == null)
            return transform.position;

        // Con la torreta se mide desde su borde: su centro queda dentro del NavMeshObstacle.
        Vector3 objetivo = PuntoObjetivo();
        objetivo.y = target.position.y;

        Vector3 awayFromTarget =
            transform.position - objetivo;
        awayFromTarget.y = 0f;

        if (awayFromTarget.sqrMagnitude < 0.0001f)
        {
            awayFromTarget = -target.forward;
            awayFromTarget.y = 0f;
        }

        awayFromTarget.Normalize();

        float desiredDistance =
            Mathf.Clamp(
                attackApproachDistance,
                0.25f,
                Mathf.Max(0.3f, attackRange - 0.15f)
            );

        Vector3 desired =
            objetivo +
            awayFromTarget * desiredDistance;

        if (
            AgenteSobreNavMesh() &&
            NavMesh.SamplePosition(
                desired,
                out NavMeshHit navHit,
                Mathf.Max(0.2f, approachSampleRadius),
                agent.areaMask
            )
        )
        {
            // El punto tiene que estar del mismo lado que el objetivo: si hay una pared en el medio
            // (el zombie viene de otra habitación) se va derecho al objetivo y la ruta rodea por la puerta.
            if (
                !NavMesh.SamplePosition(
                    objetivo,
                    out NavMeshHit objetivoEnNavMesh,
                    1.5f,
                    agent.areaMask
                )
            )
            {
                return navHit.position;
            }

            bool paredEnElMedio = NavMesh.Raycast(
                objetivoEnNavMesh.position,
                navHit.position,
                out _,
                agent.areaMask
            );

            return paredEnElMedio ? objetivoEnNavMesh.position : navHit.position;
        }

        return desired;
    }

    private void RotarHaciaObjetivo()
    {
        if (target == null)
            return;

        Vector3 direction =
            target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion desired =
            Quaternion.LookRotation(direction.normalized, Vector3.up);

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                desired,
                Mathf.Max(90f, attackFacingSpeed) * Time.deltaTime
            );
    }

    private bool EstaMirandoObjetivoParaAtacar()
    {
        if (target == null)
            return false;

        Vector3 direction =
            target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return true;

        float angle =
            Vector3.Angle(
                transform.forward,
                direction.normalized
            );

        return angle <= Mathf.Clamp(attackFacingTolerance, 10f, 90f);
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

        if (!EstaMirandoObjetivoParaAtacar())
            return;

        nextAttackTime =
            Time.time +
            attackCooldown;

        isAttacking = true;

        if (AgenteSobreNavMesh())
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
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

        // Torreta: le saca vida (la misma que a un jugador). Rota en medio del golpe = no le pasa nada.
        if (targetTorreta != null)
        {
            if (!targetTorreta.EstaRota && DistanciaAlObjetivo() <= attackRange && PuedeGolpearObjetivo())
                targetTorreta.RecibirDanoZombie(DanoDeAtaque());

            if (animationDriver != null || animator == null)
                isAttacking = false;
            yield break;
        }

        if (target != null)
        {
            float distanceToTarget =
                DistanciaPlana(
                    transform.position,
                    target.position
                );

            if (distanceToTarget <= attackRange && PuedeGolpearObjetivo())
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

                    int damage = DanoDeAtaque();

                    if(InPvPvE){if(Vector3.Distance(transform.position,playerHealth.transform.position)<=attackRange)DeadQuarantine.PvPvE.Networking.PvPvECombat.Active?.ApplyZombieDamageServer(this,playerHealth,damage);}
                    else playerHealth.TakeDamage(damage);

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

    // Daño de la dificultad (si está activo y configurado) o el del prefab.
    private int DanoDeAtaque()
    {
        DifficultySettings settings =
            RoundManager.Instance != null
                ? RoundManager.Instance.ActiveSettings
                : null;

        int damage =
            (useDifficultyDamageOverride && settings != null && settings.zombieDamage > 0)
                ? settings.zombieDamage
                : attackDamage;

        Debug.Log($"[ZombieAI] settings={(settings != null ? settings.name : "NULL")} | zombieDamage={(settings != null ? settings.zombieDamage : -1)} | prefab={attackDamage}");

        return damage;
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

        // Las variantes visuales NO cambian durante la vida del zombie.
        // Idle, Walk, Run, Sprint y el estilo frontal de ataque fueron elegidos
        // una sola vez en InicializarVariantesVisuales().
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

        bool puntoNavMeshValido = NavMesh.SamplePosition(
            finalPosition, out NavMeshHit navHit, 5f, agent.areaMask);
        if (puntoNavMeshValido)
            finalPosition = navHit.position;

        // Activamos updatePosition solo para resincronizar internamente el agente.
        // Warp actualiza tanto su posición interna como el Transform en el mismo frame.
        agent.updatePosition = true;

        if (puntoNavMeshValido && agent.Warp(finalPosition) && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        else
        {
            transform.position = finalPosition;
            Debug.LogWarning(
                "[ZombieAI] El zombie terminó de aparecer fuera del NavMesh.", this);
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

        // AddComponent<ParticleSystem>() puede arrancar el sistema inmediatamente
        // porque Play On Awake viene activo por defecto. Unity no permite cambiar
        // duration mientras el sistema esta reproduciendose, asi que primero se
        // detiene y limpia, despues se configura y al final se hace Play().
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
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
        // IMPORTANTE:
        // Estas elecciones se hacen una sola vez por spawn y nunca se vuelven a
        // sortear mientras el zombie siga vivo.
        currentWalkVariant = DeterministicInt(5, 13);
        currentRunVariant = DeterministicInt(2, 17);
        currentSprintVariant = DeterministicInt(2, 19);
        currentIdleVariant = DeterministicInt(4, 7);

        assignedSprintVisual = Deterministic01(29) < sprintChance;
        visualSprint = IsRunning && assignedSprintVisual;

        // Ataques frontales fijos para este zombie.
        // 0/1/2 = ataques base, 6 = forward, 7 = forward jump.
        int[] frontAttacks = { 0, 1, 2, 6 };
        int[] runningFrontAttacks = allowForwardJumpAttack
            ? new[] { 0, 1, 2, 6, 7 }
            : new[] { 0, 1, 2, 6 };

        currentFrontAttackVariant =
            frontAttacks[DeterministicInt(frontAttacks.Length, 53)];

        currentRunningFrontAttackVariant =
            runningFrontAttacks[DeterministicInt(runningFrontAttacks.Length, 59)];

        SetAnimatorBool(RunningHash, IsRunning);
        SetAnimatorBool(SprintHash, visualSprint);
        SetAnimatorFloat(IdleVariantHash, currentIdleVariant, 0f);
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
                float angle = Vector3.SignedAngle(
                    transform.forward,
                    toTarget.normalized,
                    Vector3.up
                );

                // Los ataques direccionales siguen dependiendo de dónde esté el
                // jugador, porque no son una variante aleatoria sino una respuesta
                // a la posición del objetivo.
                if (Mathf.Abs(angle) > 135f)
                    return 3; // Attack 180

                if (angle > 55f)
                    return 4; // Attack 90 Left

                if (angle < -55f)
                    return 5; // Attack 90 Right
            }
        }

        // Para los ataques frontales usamos siempre la variante que este zombie
        // recibió al aparecer. No cambia entre ataques.
        return IsRunning
            ? currentRunningFrontAttackVariant
            : currentFrontAttackVariant;
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
