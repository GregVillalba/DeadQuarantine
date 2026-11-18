using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(ZombieAI), typeof(NavMeshAgent), typeof(Animator))]
public sealed class ZombieBossCombat : NetworkBehaviour
{
    private enum Move { None, Left45, Right45, Right45Alt, Right90, Leap, Shockwave, Acid, Rock }

    [System.Serializable]
    private sealed class Attack
    {
        [Min(0)] public int damage;
        [Min(0)] public float minRange;
        [Min(0)] public float maxRange;
        [Min(0)] public float cooldown;
        [Tooltip("Peso relativo al elegir entre ataques disponibles; no provoca turnos vacíos.")]
        [Range(0, 1)] public float chance;

        public Attack(float minRange, float maxRange, float cooldown, float chance)
        {
            this.minRange = minRange;
            this.maxRange = maxRange;
            this.cooldown = cooldown;
            this.chance = chance;
        }
    }

    [Header("Daño 0 = daño normal del jefe")]
    [SerializeField] private Attack leap = new(3.5f, 13, 7, .85f) { damage = 40 };
    [SerializeField] private Attack shockwave = new(0, 12, 8, .65f) { damage = 32 };
    [SerializeField] private Attack acid = new(1.2f, 11f, 9, .8f) { damage = 8 };
    [SerializeField] private Attack rock = new(8, 24, 6.5f, .8f) { damage = 42 };
    [SerializeField, Min(0)] private float timeBetweenSpecials = 1.1f;
    [SerializeField, Min(.1f)] private float leapRadius = 2.7f;
    [SerializeField, Min(.1f)] private float leapHeight = 3.8f;
    [SerializeField, Min(.1f)] private float waveRadius = 18;
    [SerializeField, Min(.1f)] private float waveSpeed = 20f;
    [SerializeField, Min(.05f)] private float waveHeight = .32f;
    [SerializeField, Min(.05f)] private float acidTickInterval = .4f;
    [SerializeField, Range(12, 48)] private float acidSpread = 42;
    [SerializeField, Min(.1f)] private float rockSpeed = 20;
    [SerializeField, Min(.1f)] private float rockLifetime = 4;
    [SerializeField, Min(.05f)] private float rockRadius = 1.15f;
    [SerializeField, Min(.1f)] private float impactEffectDuration = 2;
    [Header("Puntos y efectos")]
    [SerializeField] private Transform mouth;
    [SerializeField] private Transform throwingHand;
    [SerializeField] private GameObject impactEffect;
    [SerializeField] private GameObject rockPrefab;
    [SerializeField] private AudioClip impactSound;
    [SerializeField] private Material waveMaterial;
    [SerializeField] private Material acidMaterial;

    private const float LeapTakeoff = .17f;
    private const float LeapLanding = .53f;
    private const float WaveLanding = .60f;
    private const float AcidStart = .26f;
    private const float AcidEnd = .85f;
    private const float RockRelease = .45f;
    private const float TurnStart = .16f;
    private const float TurnEnd = .8f;

    private static readonly string[] StateNames =
    {
        "", "BossTurnLeft45", "BossTurnRight45", "BossTurnRight45Alt",
        "BossTurnRight90", "BossLeap", "BossShockwave", "BossAcid", "BossRock"
    };
    private static readonly int[] States = System.Array.ConvertAll(StateNames, Animator.StringToHash);
    private static readonly Move[] Specials = { Move.Leap, Move.Shockwave, Move.Acid, Move.Rock };
    private const float GroundLift = .06f;
    private const int RingPoints = 48;

    private readonly float[] cooldownUntil = new float[StateNames.Length];
    private readonly HashSet<PlayerHealth> waveChecked = new();
    private readonly Dictionary<PlayerHealth, float> acidNextHit = new();
    private ZombieBossAcidSpray acidSpray;
    private Transform head;
    private Quaternion mouthDirection;
    private System.Action<PlayerHealth, Vector3> acidHit;
    private readonly List<Move> candidates = new();
    private ZombieAI ai;
    private ZombieHealth health;
    private ZombieAudio audioEffects;
    private Animator animator;
    private NetworkAnimator networkAnimator;
    private NavMeshAgent agent;
    private AudioSource voice;
    private Move move;
    private float nextSpecial, nextDecision, started;
    private bool entered, actionDone, airborne, falling, interrupted;
    private Vector3 aim, leapOrigin, leapTarget;
    private Quaternion startRotation;
    private float turnAngle, fallSpeed;

    private Vector3 waveOrigin;
    private float waveStarted = -1, previousWaveRadius;
    [SerializeField, Min(.1f)] private float leapWaveRadius = 5.5f;
    [SerializeField, Range(0, 1)] private float leapWaveDamageScale = .6f;
    private float activeWaveRadius, visualWaveRadius;
    private Vector3 rockOrigin, rockVelocity, lastRock;
    private float rockStarted = -1;
    private int attackBaseDamage, waveBaseDamage, rockBaseDamage;
    private readonly NetworkVariable<bool> acidActive = new();

    private LineRenderer waveLine;
    private GameObject heldRock, flyingRock;
    private double waveVisualStart = -1, rockVisualStart = -1;
    private Vector3 visualWaveOrigin, visualRockOrigin, visualRockVelocity;

    [Header("Resistencia de encuentro (se fija al aparecer, no se cura al cambiar el grupo)")]
    [SerializeField, Min(1)] private float soloHealthMultiplier = 2.4f;
    [SerializeField, Min(1)] private int minimumSoloHealth = 4000;
    [SerializeField, Range(0, 1.5f)] private float healthPerExtraPlayer = .8f;
    [Header("Presión: segunda fase al 50%, furia al 25% de vida")]
    [SerializeField, Range(.4f, 1f)] private float woundedCooldownMultiplier = .75f;
    [SerializeField, Range(.4f, 1f)] private float enragedCooldownMultiplier = .6f;
    [SerializeField, Min(0)] private float rockImpactRadius = 1.6f;
    private Transform focus;
    private Move lastSpecial;
    private Transform preferredTarget, lastPressuredTarget;
    private float nextRetarget;
    private readonly HashSet<PlayerHealth> rockHit = new();

    public int EncounterHealth(int originalHealth, int participants) => Mathf.Max(1,
        Mathf.RoundToInt(Mathf.Max(minimumSoloHealth, originalHealth * soloHealthMultiplier) *
            (1f + (Mathf.Clamp(participants, 1, 4) - 1) * healthPerExtraPlayer)));

    private float HealthFraction => health && health.MaximumHealth > 0 ?
        health.CurrentHealth / (float)health.MaximumHealth : 1f;
    private float Pace => HealthFraction <= .25f ? enragedCooldownMultiplier :
        HealthFraction <= .5f ? woundedCooldownMultiplier : 1f;
    internal void UpdatePursuit(float distance)
    {
        // Preview requested: retain running movement speed while the controller uses Walking.
        if (!ai.IsRunning) ai.SetRunning(true);
    }

    internal float PursuitMultiplier => HealthFraction <= .25f ? 1.2f : HealthFraction <= .5f ? 1.1f : 1f;

    // Share pressure between exposed survivors instead of chasing the same kiter forever.
    // Keep the target for several seconds so the boss does not twitch between players.
    internal Transform PreferredTarget(Transform nearest)
    {
        if (Time.time < nextRetarget && preferredTarget &&
            preferredTarget.TryGetComponent<PlayerHealth>(out var current) && ai.CanReceiveBossDamage(current))
            return preferredTarget;
        nextRetarget = Time.time + 4f;
        preferredTarget = null;
        float best = float.PositiveInfinity;
        foreach (var player in Players())
        {
            Vector3 point = player.transform.position;
            float distance = Vector3.Distance(transform.position, point);
            if (distance > rock.maxRange || Mathf.Abs(point.y - transform.position.y) > 1.6f ||
                TryFirstSolid(transform.position + Vector3.up, point + Vector3.up, 0, out _)) continue;
            float score = distance + (player.transform == lastPressuredTarget ? 8f : 0f);
            if (score < best) { best = score; preferredTarget = player.transform; }
        }
        lastPressuredTarget = preferredTarget;
        return preferredTarget ? preferredTarget : nearest;
    }

    internal bool BlocksMovement => move != Move.None || falling;

    internal bool MeleeReachedImpact =>
        animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack") &&
        animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= .42f;

    private void Awake()
    {
        ai = GetComponent<ZombieAI>();
        health = GetComponent<ZombieHealth>();
        audioEffects = GetComponent<ZombieAudio>();
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        agent = GetComponent<NavMeshAgent>();
        voice = GetComponent<AudioSource>();
        head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head) mouthDirection = Quaternion.Inverse(head.rotation) * transform.rotation;
        acidHit = ApplyAcidHit;
    }

    public override void OnNetworkSpawn()
    {
        // El cliente recibe la trayectoria del NetworkTransform durante el salto.
        if (!IsServer) agent.enabled = false;
    }

    public override void OnNetworkDespawn() => ClearVisuals();
    public override void OnDestroy()
    {
        ClearVisuals();
        acidSpray?.Dispose();
        base.OnDestroy();
        if (waveLine) Destroy(waveLine.gameObject);
    }

    internal bool TrySpecial(Vector3 target, float distance)
    {
        if (!agent.isOnNavMesh || Time.time < nextSpecial || Time.time < nextDecision)
            return false;

        nextDecision = Time.time + .22f;
        var direction = target - transform.position;
        direction.y = 0;
        if (Mathf.Abs(target.y - transform.position.y) > 1.6f ||
            Vector3.Angle(transform.forward, direction) > 60)
            return false;
        Vector3 targetCenter = target + Vector3.up;
        bool sightBlocked = TryFirstSolid(transform.position + Vector3.up, targetCenter, 0, out _);

        focus = null;
        foreach (var player in Players())
            if ((player.transform.position - target).sqrMagnitude < 1f) { focus = player.transform; break; }
        candidates.Clear();
        foreach (Move candidate in Specials)
        {
            if ((candidate == Move.Shockwave && waveStarted >= 0) || (candidate == Move.Rock && rockStarted >= 0))
                continue;
            Attack settings = Settings(candidate);
            if (distance >= settings.minRange && distance <= settings.maxRange &&
                Time.time >= cooldownUntil[(int)candidate])
                candidates.Add(candidate);
        }

        // Chance is a selection weight, not another chance to do nothing.
        // Alternate attacks when another valid option exists.
        if (candidates.Count > 1) candidates.Remove(lastSpecial);
        while (candidates.Count > 0)
        {
            float total = 0;
            foreach (var option in candidates) total += Mathf.Max(.01f, Settings(option).chance);
            float roll = Random.value * total;
            int index = candidates.Count - 1;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= Mathf.Max(.01f, Settings(candidates[i]).chance);
                if (roll <= 0) { index = i; break; }
            }
            Move candidate = candidates[index];
            candidates.RemoveAt(index);
            if (candidate == Move.Leap ? !PlanLeap(target) : sightBlocked) continue;

            aim = targetCenter;
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.LookRotation(direction);
            lastSpecial = candidate;
            cooldownUntil[(int)candidate] = Time.time + Settings(candidate).cooldown * Pace;
            attackBaseDamage = ai.DanoDeAtaque();
            StartMove(candidate);
            return true;
        }
        return false;
    }

    internal bool TryTurn(Vector3 target)
    {
        Vector3 direction = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
        if (direction.sqrMagnitude < .001f) return false;
        float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
        if (Mathf.Abs(angle) < 38) return false;

        Move selected = angle < 0 ? Move.Left45 : angle >= 73 ? Move.Right90 :
            Random.value < .5f ? Move.Right45 : Move.Right45Alt;
        startRotation = transform.rotation;
        turnAngle = selected == Move.Right90 ? 90 : angle < 0 ? -45 : 45;
        StartMove(selected);
        return true;
    }

    private Attack Settings(Move action) => action switch
    {
        Move.Leap => leap, Move.Shockwave => shockwave, Move.Acid => acid, _ => rock
    };

    private void StartMove(Move action)
    {
        move = action;
        started = Time.time;
        actionDone = entered = interrupted = false;
        if (action == Move.Acid) acidNextHit.Clear();
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
        networkAnimator.SetTrigger(StateNames[(int)move]);
        if (move >= Move.Leap)
            AttackCueClientRpc(move == Move.Rock);
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsServer)
        {
            if (health.IsDead)
            {
                StopMove(false);
                waveStarted = rockStarted = -1;
            }
            else
            {
                if (ai.EstaAturdido && move != Move.None && !interrupted)
                    InterruptMove();
                if (falling) UpdateFall();
                if (move != Move.None) UpdateMove();
                if (waveStarted >= 0) UpdateWave();
                if (rockStarted >= 0) UpdateRock();
            }
        }

        if (health.IsDead)
            ClearVisuals();
        else
        {
            if (IsClient) UpdateVisuals();

        }
    }

    private void LateUpdate()
    {
        if (!IsSpawned || health.IsDead) return;
        // Use the mouth and head AFTER the Animator and its head IK have updated.
        if (acidActive.Value && mouth && head)
        {
            AlignAcidHead();
            acidSpray ??= new ZombieBossAcidSpray();
            acidSpray.Step(mouth, head.rotation * mouthDirection, acid.maxRange, acidSpread,
                acidMaterial, IsClient, IsServer ? acidHit : null);
        }
        else acidSpray?.Clear();
    }

    private void AlignAcidHead()
    {
        // Avatar look-at axes differ from the anatomical mouth axis. Restrict
        // the actual head pitch after IK, keeping its animated yaw and jaw pose.
        Vector3 forward = head.rotation * mouthDirection * Vector3.forward;
        Vector3 horizontal = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (horizontal.sqrMagnitude < .001f) return;
        float pitch = Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        float limited = Mathf.Clamp(pitch, -8f, 0f) * Mathf.Deg2Rad;
        Vector3 desired = horizontal.normalized * Mathf.Cos(limited) + Vector3.up * Mathf.Sin(limited);
        head.rotation = Quaternion.FromToRotation(forward, desired) * head.rotation;
    }

    private void OnAnimatorIK(int layer)
    {
        if (!animator || !head) return;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (health.IsDead || !state.IsName("BossAcid")) { animator.SetLookAtWeight(0); return; }
        float phase = state.normalizedTime;
        float blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.16f, .28f, phase)) *
            (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.84f, .98f, phase)));
        // The Roaring clip bends its head down. Correct the actual head pose,
        // rather than firing in a direction disconnected from the animated mouth.
        animator.SetLookAtWeight(blend, .25f, 1f, 0, .25f);
        animator.SetLookAtPosition(head.position + transform.forward * 6f - Vector3.up * .2f);
    }

    private void UpdateMove()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash != States[(int)move])
        {
            if (entered || Time.time - started > .7f) StopMove(true);
            return;
        }
        entered = true;
        if (interrupted) return;
        float phase = state.normalizedTime;
        // Retarget only during the visible preparation. Once airborne / released,
        // the leap and boulder remain committed and can be dodged.
        if (focus && focus.TryGetComponent<PlayerHealth>(out var focused) && ai.CanReceiveBossDamage(focused))
        {
            if (move == Move.Rock && phase < RockRelease)
            {
                Vector3 velocity = focus.TryGetComponent<CharacterController>(out var body) ?
                    Vector3.ClampMagnitude(body.velocity, 6f) : Vector3.zero;
                float lead = Mathf.Clamp(Vector3.Distance(throwingHand.position, focus.position + Vector3.up) / rockSpeed * .55f, .12f, .6f);
                aim = focus.position + Vector3.up + Vector3.ProjectOnPlane(velocity, Vector3.up) * lead;
            }
            if ((move == Move.Acid && phase < AcidStart) || (move == Move.Leap && phase < LeapTakeoff))
            {
                Vector3 facing = Vector3.ProjectOnPlane(focus.position - transform.position, Vector3.up);
                if (facing.sqrMagnitude > .01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(facing), 100f * Time.deltaTime);
            }
        }
        if (move < Move.Leap)
        {
            float t = Mathf.InverseLerp(TurnStart, TurnEnd, phase);
            transform.rotation = startRotation * Quaternion.Euler(0, turnAngle * Mathf.SmoothStep(0, 1, t), 0);
        }
        else if (move == Move.Leap) UpdateLeap(phase);
        else if (move == Move.Shockwave && phase >= WaveLanding && !actionDone)
        {
            actionDone = true;
            BeginWave(transform.position, waveRadius, Mathf.Max(shockwave.damage, attackBaseDamage));
        }
        else if (move == Move.Acid) UpdateAcid(phase);
        else if (move == Move.Rock && phase >= RockRelease && !actionDone)
        {
            actionDone = true;
            rockOrigin = lastRock = throwingHand.position;
            if (HasRockReleaseObstruction())
            {
                DropHeldRockClientRpc();
                ImpactClientRpc(rockOrigin, false, 0);
                return;
            }
            Vector3 offset = aim - rockOrigin;
            float flightTime = Mathf.Max(.15f, offset.magnitude / rockSpeed);
            rockVelocity = offset / flightTime - .5f * Physics.gravity * flightTime;
            rockStarted = Time.time;
            rockBaseDamage = attackBaseDamage;
            LaunchRockClientRpc(rockOrigin, rockVelocity, NetworkManager.ServerTime.Time);
        }
    }

    private bool PlanLeap(Vector3 target)
    {
        leapOrigin = transform.position;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        // A direct NavMesh ray rejects gaps and low props even if the air path is clear.
        // Try the intended landing first and shorten only when the capsule cannot clear it.
        for (int candidate = 0; candidate < 5; candidate++)
        {
            Vector3 sample = Vector3.Lerp(target, leapOrigin, candidate * .13f);
            if (!NavMesh.SamplePosition(sample, out NavMeshHit landing, 1.25f, filter) ||
                Mathf.Abs(landing.position.y - leapOrigin.y) > 1.35f ||
                Vector3.Distance(landing.position, leapOrigin) < 2.5f)
                continue;

            leapTarget = landing.position;
            Vector3 previous = leapOrigin;
            bool clear = true;
            for (int i = 1; i <= 12; i++)
            {
                Vector3 point = LeapPoint(i / 12f);
                if (SweepBody(previous, point, out _))
                {
                    clear = false;
                    break;
                }
                previous = point;
            }
            if (clear) return true;
        }
        return false;
    }

    private Vector3 LeapPoint(float t) =>
        Vector3.Lerp(leapOrigin, leapTarget, t) + Vector3.up * (4f * leapHeight * t * (1f - t));

    private bool SweepBody(Vector3 from, Vector3 to, out RaycastHit obstacle)
    {
        Vector3 step = to - from;
        float radius = agent.radius * .9f;
        obstacle = default;
        if (step.sqrMagnitude < .000001f) return false;
        float closest = step.magnitude;
        bool blocked = false;
        foreach (RaycastHit hit in Physics.CapsuleCastAll(
            from + Vector3.up * (radius + .19f),
            from + Vector3.up * Mathf.Max(radius + .19f, agent.height - radius),
            radius, step.normalized, step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.distance > closest || IsActor(hit.collider) || hit.normal.y > .7f)
                continue;
            closest = hit.distance;
            obstacle = hit;
            blocked = true;
        }
        return blocked;
    }

    private void UpdateLeap(float phase)
    {
        if (phase < LeapTakeoff || actionDone) return;
        if (!airborne)
        {
            if (focus)
            {
                Vector3 velocity = focus.TryGetComponent<CharacterController>(out var body) ?
                    Vector3.ProjectOnPlane(Vector3.ClampMagnitude(body.velocity, 6f), Vector3.up) : Vector3.zero;
                // Keep the validated original arc if the newer landing is obstructed.
                Vector3 oldOrigin = leapOrigin, oldTarget = leapTarget;
                if (!PlanLeap(focus.position + velocity * .2f))
                { leapOrigin = oldOrigin; leapTarget = oldTarget; }
            }
            airborne = true;
            agent.updatePosition = false;
        }
        Vector3 desired = LeapPoint(Mathf.InverseLerp(LeapTakeoff, LeapLanding, phase));
        if (SweepBody(transform.position, desired, out _))
        {
            InterruptMove();
            return;
        }
        transform.position = desired;
        if (phase < LeapLanding) return;

        actionDone = true;
        ReattachAgent();
        BeginWave(transform.position, leapWaveRadius,
            Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(leap.damage, attackBaseDamage) * leapWaveDamageScale)));
        foreach (var player in Players())
        {
            Vector3 point = player.transform.position;
            if (Vector3.Distance(point, transform.position) <= leapRadius &&
                !TryFirstSolid(transform.position + Vector3.up * .6f, point + Vector3.up * .6f, 0, out _))
            {
                Damage(player, leap, attackBaseDamage, transform.position);
                waveChecked.Add(player); // The direct impact and its ring must not double-hit.
            }
        }
    }

    private void ReattachAgent()
    {
        airborne = falling = false;
        if (!agent.isOnNavMesh) return;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit ground, .65f, filter))
        {
            agent.nextPosition = ground.position;
            // Bind the agent at the finished arc before it resumes writing the Transform.
            agent.Warp(ground.position);
        }
        agent.updatePosition = true;
    }

    private void UpdateFall()
    {
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit ground, leapHeight + 1f, agent.areaMask))
        {
            ReattachAgent();
            return;
        }
        fallSpeed += 18 * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, ground.position, fallSpeed * Time.deltaTime);
        if ((transform.position - ground.position).sqrMagnitude < .001f)
            ReattachAgent();
    }

    private void InterruptMove()
    {
        interrupted = true;
        if (move == Move.Rock && !actionDone) DropHeldRockClientRpc();
        acidActive.Value = false;
        if (airborne)
        {
            airborne = false;
            falling = true;
            fallSpeed = 0;
        }
    }

    private void StopMove(bool survive)
    {
        if (move == Move.None) return;
        if (move >= Move.Leap) nextSpecial = Time.time + timeBetweenSpecials * Pace;
        if (move == Move.Rock && !actionDone && survive && !interrupted) DropHeldRockClientRpc();
        if (acidActive.Value) acidActive.Value = false;
        if (airborne && survive)
        {
            falling = true;
            fallSpeed = 0;
        }
        move = Move.None;
    }

    private void BeginWave(Vector3 origin, float radius, int damage)
    {
        waveOrigin = origin; activeWaveRadius = radius; waveBaseDamage = damage;
        waveStarted = Time.time; previousWaveRadius = 0; waveChecked.Clear();
        ImpactClientRpc(origin, true, NetworkManager.ServerTime.Time, radius);
    }

    private void UpdateWave()
    {
        float radius = Mathf.Min((Time.time - waveStarted) * waveSpeed, activeWaveRadius);
        foreach (var player in Players())
        {
            Vector3 feet = player.transform.position;
            float distance = Vector2.Distance(new(waveOrigin.x, waveOrigin.z), new(feet.x, feet.z));
            if (distance < previousWaveRadius - .2f || distance > radius + .2f || !waveChecked.Add(player))
                continue;
            if (TryWaveGround(feet, out Vector3 ground) && feet.y - ground.y <= waveHeight &&
                feet.y >= ground.y - .2f && !TryFirstSolid(waveOrigin + Vector3.up * .75f, ground + Vector3.up * .75f, 0, out _))
                player.TakeDamage(waveBaseDamage, source: waveOrigin);
        }
        previousWaveRadius = radius;
        if (radius >= activeWaveRadius) waveStarted = -1;
    }

    private void UpdateAcid(float phase)
    {
        bool active = phase >= AcidStart && phase <= AcidEnd;
        if (acidActive.Value != active) acidActive.Value = active;
    }

    private void ApplyAcidHit(PlayerHealth player, Vector3 origin)
    {
        if (acidNextHit.TryGetValue(player, out float next) && Time.time < next) return;
        if (!ai.CanReceiveBossDamage(player)) return;
        acidNextHit[player] = Time.time + acidTickInterval;
        Damage(player, acid, Mathf.CeilToInt(attackBaseDamage * .25f), origin);
    }

    private bool HasRockReleaseObstruction()
    {
        foreach (Collider shape in Physics.OverlapSphere(rockOrigin, rockRadius,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (!IsActor(shape)) return true;
        return false;
    }

    private void UpdateRock()
    {
        float age = Time.time - rockStarted;
        Vector3 next = Ballistic(rockOrigin, rockVelocity, age);
        Vector3 step = next - lastRock;
        if (step.sqrMagnitude > .000001f)
        {
            RaycastHit first = default;
            float closest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.SphereCastAll(lastRock, rockRadius, step.normalized,
                step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= closest || hit.collider.GetComponentInParent<ZombieHealth>()) continue;
                first = hit;
                closest = hit.distance;
            }
            if (first.collider)
            {
                PlayerHealth player = first.collider.GetComponentInParent<PlayerHealth>();
                rockHit.Clear();
                if (player && ai.CanReceiveBossDamage(player) && rockHit.Add(player))
                {
                    Damage(player, rock, rockBaseDamage, rockOrigin);
                    player.ApplyBossRockStunServer();
                }
                // A small, visible impact zone prevents an effortless near miss,
                // but the first wall still stops both the projectile and blast.
                foreach (var nearby in Players())
                    if (Vector3.Distance(nearby.transform.position + Vector3.up * .6f, first.point) <= rockImpactRadius &&
                        !TryFirstSolid(first.point + first.normal * .1f,
                            nearby.transform.position + Vector3.up * .6f, 0, out _) && rockHit.Add(nearby))
                        Damage(nearby, rock, rockBaseDamage, first.point, .65f);
                rockStarted = -1;
                StopRockClientRpc(first.point, true);
                return;
            }
        }
        lastRock = next;
        if (age >= rockLifetime)
        {
            rockStarted = -1;
            StopRockClientRpc(next, false);
        }
    }

    private static Vector3 Ballistic(Vector3 start, Vector3 velocity, float time) =>
        start + velocity * time + .5f * Physics.gravity * (time * time);

    private static bool IsActor(Collider hit) =>
        hit.GetComponentInParent<ZombieHealth>() || hit.GetComponentInParent<PlayerHealth>();

    private static bool TryFirstSolid(Vector3 from, Vector3 to, float radius, out RaycastHit first)
    {
        Vector3 step = to - from;
        first = default;
        if (step.sqrMagnitude < .000001f) return false;

        var hits = radius > 0
            ? Physics.SphereCastAll(from, radius, step.normalized, step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            : Physics.RaycastAll(from, step.normalized, step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float closest = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.distance >= closest || IsActor(hit.collider)) continue;
            first = hit;
            closest = hit.distance;
        }
        return first.collider != null;
    }

    private IEnumerable<PlayerHealth> Players()
    {
        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            if (client.PlayerObject && client.PlayerObject.TryGetComponent(out PlayerHealth player) &&
                ai.CanReceiveBossDamage(player))
                yield return player;
        }
    }

    private static void Damage(PlayerHealth player, Attack settings, int baseDamage, Vector3 source, float scale = 1)
    {
        int damage = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(settings.damage, baseDamage) * scale));
        player.TakeDamage(damage, source: source);
    }

    private bool TryWaveGround(Vector3 position, out Vector3 ground)
    {
        position.y = waveOrigin.y + 1.1f;
        bool found = TryFirstSolid(position, position - Vector3.up * 2.2f, 0, out RaycastHit hit);
        ground = hit.point;
        return found && hit.normal.y > .5f;
    }

    [ClientRpc]
    private void AttackCueClientRpc(bool showRock)
    {
        if (health.IsDead) return;
        audioEffects?.PlayAttackSound();
        if (showRock && rockPrefab && throwingHand)
        {
            Destroy(heldRock);
            heldRock = MakeRock();
            // Retain world size: the animated hand inherits the giant boss scale.
            heldRock.transform.SetParent(throwingHand, true);
            heldRock.transform.localPosition = Vector3.zero;
            heldRock.transform.localRotation = Quaternion.identity;
        }
    }

    [ClientRpc]
    private void DropHeldRockClientRpc() => Destroy(heldRock);

    [ClientRpc]
    private void LaunchRockClientRpc(Vector3 origin, Vector3 velocity, double time)
    {
        Destroy(flyingRock);
        if (health.IsDead || !rockPrefab) { Destroy(heldRock); return; }
        // Release the exact held object, preserving its mesh and world scale.
        flyingRock = heldRock ? heldRock : MakeRock();
        heldRock = null;
        flyingRock.transform.SetParent(null, true);
        flyingRock.transform.position = origin;
        visualRockOrigin = origin;
        visualRockVelocity = velocity;
        rockVisualStart = time;
    }

    [ClientRpc]
    private void StopRockClientRpc(Vector3 point, bool impact)
    {
        Destroy(flyingRock);
        if (impact && !health.IsDead) PlayImpact(point);
    }

    [ClientRpc]
    private void ImpactClientRpc(Vector3 point, bool wave, double time, float radius = 0)
    {
        if (health.IsDead) return;
        PlayImpact(point);
        if (!wave) return;
        visualWaveOrigin = point;
        visualWaveRadius = radius > 0 ? radius : waveRadius;
        waveVisualStart = time;
    }

    private GameObject MakeRock()
    {
        GameObject instance = Instantiate(rockPrefab);
        foreach (Collider shape in instance.GetComponentsInChildren<Collider>()) shape.enabled = false;
        foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return instance;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer part in renderers) bounds.Encapsulate(part.bounds);

        var pivot = new GameObject("Zombie boss boulder");
        Vector3 extents = bounds.extents;
        float scale = rockRadius / Mathf.Max(.01f, extents.magnitude);
        instance.transform.SetParent(pivot.transform, true);
        instance.transform.localScale *= scale;
        instance.transform.localPosition = (instance.transform.position - bounds.center) * scale;
        return pivot;
    }

    private void PlayImpact(Vector3 point)
    {
        if (impactEffect) Destroy(Instantiate(impactEffect, point, Quaternion.identity), impactEffectDuration);
        if (impactSound) AudioSource.PlayClipAtPoint(impactSound, point, voice ? voice.volume : 1);
    }

    private void UpdateVisuals()
    {
        double time = NetworkManager.ServerTime.Time;
        if (flyingRock)
        {
            float elapsed = Mathf.Max(0, (float)(time - rockVisualStart));
            if (elapsed >= rockLifetime) Destroy(flyingRock);
            else
            {
                flyingRock.transform.position = Ballistic(visualRockOrigin, visualRockVelocity, elapsed);
                flyingRock.transform.Rotate(125 * Time.deltaTime, 50 * Time.deltaTime, 0);
            }
        }
        if (waveVisualStart >= 0)
        {
            float radius = Mathf.Max(0, (float)(time - waveVisualStart)) * waveSpeed;
            if (radius >= visualWaveRadius)
            {
                waveVisualStart = -1;
                if (waveLine) waveLine.enabled = false;
            }
            else if (waveMaterial)
            {
                if (!waveLine) waveLine = CreateWave();
                float fade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.70f, 1f, radius / visualWaveRadius));
                waveLine.widthMultiplier = Mathf.Lerp(.06f, .27f, fade);
                Color tint = new(1f, .87f, .70f, fade);
                waveLine.startColor = waveLine.endColor = tint;
                waveLine.enabled = true;
                for (int i = 0; i < RingPoints; i++)
                {
                    float angle = i * Mathf.PI * 2 / RingPoints;
                    Vector3 point = visualWaveOrigin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                    if (TryFirstSolid(point + Vector3.up * 1.1f, point - Vector3.up * 1.1f, 0, out RaycastHit ground))
                        point = ground.point;
                    waveLine.SetPosition(i, point + Vector3.up * GroundLift);
                }
            }
        }
    }

    private LineRenderer CreateWave()
    {
        GameObject effect = new("Zombie boss shockwave");
        effect.transform.SetParent(transform, false);
        var line = effect.AddComponent<LineRenderer>();
        line.sharedMaterial = waveMaterial;
        line.positionCount = RingPoints;
        line.loop = true;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void ClearVisuals()
    {
        Destroy(heldRock);
        Destroy(flyingRock);
        if (waveLine) waveLine.enabled = false;
        acidSpray?.Clear();
        waveVisualStart = -1;
    }
}