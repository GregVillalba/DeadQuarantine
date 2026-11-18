using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(ZombieAI), typeof(NavMeshAgent), typeof(Animator))]
public sealed partial class ZombieStalker : NetworkBehaviour
{
    enum Behaviour { Searching, Repositioning, Hiding, Peeking, Attacking, Escaping, Traversing }
    [Header("Distancias y decisiones")]
    [SerializeField] float preferredNear = 15, preferredFar = 25, retreatDistance = 9, resumeAttackDistance = 14, attackRange = 32;
    [SerializeField] float attackCooldown = 3, coverPause = 1.8f, decisionInterval = 1.5f, maxPositionTime = 6;
    [SerializeField, Range(8, 32)] int positionSamples = 20;
    [SerializeField, Range(0, 12)] float maxNavigableHeight = 8;
    [Header("Proyectil y ácido")]
    [SerializeField] float acidBallSpeed = 40, acidBallRadius = .3f, acidGravity = 1.5f;
    [SerializeField, Range(0, 1)] float acidReleasePhase = .45f;
    [SerializeField, Range(0, 1)] float acidAimLockPhase = .42f;
    [SerializeField, Range(0, 1)] float acidLeadFraction = .9f;
    [SerializeField, Min(0)] float acidMaxLead = 4.5f;
    [SerializeField] float acidDuration = 4;
    [SerializeField] int acidDamagePerSecond = 4;
    [SerializeField, Min(0)] int acidImpactDamage = 15;
    [SerializeField] Material acidMaterial;
    [SerializeField] Transform mouth;
    [Header("Salto: sólo enlaces existentes del NavMesh")]
    [SerializeField] float jumpDuration = 1.5f, jumpArc = .65f, maxLinkDistance = 4, maxLinkHeight = 2;
    readonly NetworkVariable<Vector3> aimPoint = new();
    ZombieAI ai; ZombieHealth health; Animator animator; NetworkAnimator networkAnimator; NavMeshAgent agent;
    Transform head; Quaternion headBasis; PlayerHealth target;
    Behaviour behaviour;
    bool enteredCast, released, pendingRelease, escapeLatched, hasCover;
    float castStarted, nextAttack, nextPlan, nextAcquire, waitUntil, positionStarted, progressAt, lastRemaining;
    Vector3 coverPoint, peekPoint, goal, targetAtPlan, previousGoal;
    PlayerHealth observedTarget;
    Vector3 observedPosition, observedVelocity;
    float observedAt;
    NavMeshPath path;
    bool traversing, returningFromLink;
    Vector3 linkFrom, linkTo;
    float linkElapsed;
    Vector3 rejectedLinkEnd;
    float rejectLinkUntil;
    public bool IsTraversing => traversing;
    public bool CanUseRangedMode => !TutorialEscena.EsActiva &&
        !DeadQuarantine.PvPvE.PvPvEZombieDirector.Active && !health.IsPvPvEZombie && RoundManager.Instance &&
        (RoundManager.Instance.EsModoHistoria || AllowsSurvival(RoundManager.Instance.StalkerRulesMode, RoundManager.Instance.CurrentRoundNetwork.Value));
    public static bool AllowsSurvival(GameModeConfig mode, int round) => mode && mode.infiniteRounds && round >= 5;
    public static int StoryBudget(int players) => Mathf.Clamp(players, 1, 3);
    public static int HealthFor(int normalHealth, DifficultyLevel difficulty) =>
        Mathf.Max(140 + (int)difficulty * 50, Mathf.RoundToInt(normalHealth * 1.75f));

    void Awake()
    {
        path = new NavMeshPath();
        ai = GetComponent<ZombieAI>(); health = GetComponent<ZombieHealth>(); animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>(); agent = GetComponent<NavMeshAgent>();
        head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head) headBasis = Quaternion.Inverse(head.rotation) * transform.rotation;
    }
    public override void OnNetworkSpawn()
    {
        if (!IsServer) agent.enabled = false;
        else { agent.autoTraverseOffMeshLink = false; agent.acceleration = 18; agent.angularSpeed = 240; }
    }
    public override void OnNetworkDespawn() { ClearAcidProjectiles(); }
    public override void OnDestroy() { ClearAcidProjectiles(); base.OnDestroy(); }

    internal void Tick()
    {
        if (!IsServer || !IsSpawned || health.IsDead || !CanUseRangedMode || !agent.isOnNavMesh) return;
        if (agent.isOnOffMeshLink) { BeginTraversal(); return; }
        AcquireTarget();
        UpdateTargetMotion();
        if (!target) { Stop(); behaviour = Behaviour.Searching; return; }
        float threatDistance = NearestThreatDistance();
        if (threatDistance < retreatDistance && !escapeLatched)
        {
            escapeLatched = true;
            CancelCast(); hasCover = false; behaviour = Behaviour.Escaping; nextPlan = 0;
        }
        if (escapeLatched)
        {
            if (threatDistance < resumeAttackDistance)
            {
                if (Time.time >= nextPlan && (behaviour != Behaviour.Escaping || RouteFailed() ||
                    (targetAtPlan - target.transform.position).sqrMagnitude > 9 || Time.time - positionStarted > 3))
                    PlanPosition(true);
                else if (Time.time >= nextPlan && !agent.hasPath) PlanPosition(true);
                if (behaviour == Behaviour.Escaping) Move(goal);
                return;
            }
            escapeLatched = false; behaviour = Behaviour.Searching; nextPlan = 0;
        }
        if (behaviour == Behaviour.Attacking) return;
        // Any valid firing opportunity can interrupt repositioning; cover is optional.
        if (TryBeginAttack()) return;
        if ((behaviour == Behaviour.Repositioning || behaviour == Behaviour.Peeking) &&
            (RouteFailed() || Time.time - positionStarted > maxPositionTime || (targetAtPlan - target.transform.position).sqrMagnitude > 36))
        { behaviour = Behaviour.Searching; nextPlan = 0; }

        if (behaviour == Behaviour.Hiding)
        {
            Stop();
            if (Time.time < waitUntil) return;
            if (hasCover && ClearShot(peekPoint + Vector3.up * 1.4f, TargetPoint(target)))
            { SetGoal(peekPoint, Behaviour.Peeking); }
            else { behaviour = Behaviour.Searching; nextPlan = 0; }
        }
        if (behaviour == Behaviour.Repositioning || behaviour == Behaviour.Peeking)
        {
            Move(goal);
            if (Vector3.Distance(transform.position, goal) > .7f) return;
            Stop();
            if (behaviour == Behaviour.Repositioning && hasCover)
            { behaviour = Behaviour.Hiding; waitUntil = Time.time + coverPause; return; }
            // A peek lasts briefly. An unsafe firing lane is never used just because the route ended.
            if (TryBeginAttack()) return;
            behaviour = Behaviour.Searching; nextPlan = Time.time + .5f;
        }
        if (Time.time >= nextPlan) PlanPosition(false);
    }
    void AcquireTarget()
    {
        if (Time.time < nextAcquire && ValidTarget(target)) return;
        nextAcquire = Time.time + .5f;
        PlayerHealth candidateTarget = null; float closest = float.PositiveInfinity;
        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var candidate = client.PlayerObject ? client.PlayerObject.GetComponent<PlayerHealth>() : null;
            if (!ValidTarget(candidate)) continue;
            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distance < closest) { closest = distance; candidateTarget = candidate; }
        }
        if (target != candidateTarget) { target = candidateTarget; nextPlan = 0; }
    }
    float NearestThreatDistance()
    {
        float nearest = float.PositiveInfinity;
        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject ? client.PlayerObject.GetComponent<PlayerHealth>() : null;
            if (!ValidTarget(player)) continue;
            // A player on another storey is not a close pursuer through the ceiling.
            if (Mathf.Abs(player.transform.position.y - transform.position.y) < 3 &&
                !SolidBetween(transform.position + Vector3.up, TargetPoint(player)))
                nearest = Mathf.Min(nearest, FlatDistance(transform.position, player.transform.position));
        }
        return nearest;
    }
    bool ValidTarget(PlayerHealth player) => player && player.IsSpawned && player.NetworkManager == NetworkManager && ai.CanReceiveBossDamage(player);
    static Vector3 TargetPoint(PlayerHealth player) => player.transform.position + Vector3.up * 1.1f;
    bool TryBeginAttack()
    {
        float range = Vector3.Distance(transform.position, target.transform.position);
        if (escapeLatched || Time.time < nextAttack || range < resumeAttackDistance || range > attackRange ||
            !ClearShot(mouth ? mouth.position : transform.position + Vector3.up * 1.4f, TargetPoint(target))) return false;
        Stop(); Face(target.transform.position);
        // Keep this tick for aiming instead of resuming a path that would turn away again.
        if (Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up)) > 12) return true;
        behaviour = Behaviour.Attacking; enteredCast = released = pendingRelease = false; castStarted = Time.time;
        aimPoint.Value = PredictAcidAim(); networkAnimator.ResetTrigger("Escape"); networkAnimator.SetTrigger("Acid");
        GetComponent<ZombieAudio>()?.PlayAttackSound();
        return true;
    }
    void UpdateTargetMotion()
    {
        if (!target) { observedTarget = null; observedVelocity = Vector3.zero; return; }
        if (observedTarget != target)
        {
            observedTarget = target; observedPosition = target.transform.position;
            observedAt = Time.time; observedVelocity = Vector3.zero; return;
        }
        float dt = Time.time - observedAt;
        if (dt < .06f) return;
        Vector3 delta = target.transform.position - observedPosition;
        Vector3 velocity = delta.magnitude > 8 || dt > .4f ? Vector3.zero : Vector3.ProjectOnPlane(delta / dt, Vector3.up);
        observedVelocity = Vector3.Lerp(observedVelocity, Vector3.ClampMagnitude(velocity, 8), .7f);
        observedAt = Time.time; observedPosition = target.transform.position;
    }
    Vector3 PredictAcidAim(float windupRemaining = 0)
    {
        Vector3 origin = mouth ? mouth.position : transform.position + Vector3.up * 1.4f;
        Vector3 point = TargetPoint(target);
        float travel = Vector3.Distance(origin, point) / Mathf.Max(1, acidBallSpeed) + Mathf.Max(0, windupRemaining);
        point += Vector3.ClampMagnitude(observedVelocity * travel * acidLeadFraction, acidMaxLead);
        return origin + Vector3.ClampMagnitude(point - origin, Mathf.Max(1, attackRange - .1f));
    }
    void Stop()
    {
        if (agent.enabled && agent.isOnNavMesh && !agent.isOnOffMeshLink)
        { agent.isStopped = true; if (agent.hasPath) agent.ResetPath(); agent.velocity = Vector3.zero; }
    }
    void Move(Vector3 point)
    {
        if (!ai.IsRunning) ai.SetRunning(true);
        agent.updateRotation = true; agent.isStopped = false;
        if (!agent.pathPending && (!agent.hasPath || (agent.destination - point).sqrMagnitude > .2f))
            if (!agent.SetDestination(point)) { behaviour = Behaviour.Searching; nextPlan = Time.time + decisionInterval; }
    }
    void Face(Vector3 point)
    {
        Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
        if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 220 * Time.deltaTime);
    }
    bool RouteFailed()
    {
        if (agent.pathPending) return false;
        if (agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete) return true;
        float distance = Vector3.Distance(transform.position, goal);
        if (distance < .7f) return false;
        if (distance < lastRemaining - .25f) { lastRemaining = distance; progressAt = Time.time; }
        return Time.time - progressAt > 2;
    }
    void SetGoal(Vector3 point, Behaviour next)
    {
        goal = point; behaviour = next; positionStarted = progressAt = Time.time;
        lastRemaining = Vector3.Distance(transform.position, point); Move(point);
    }
    void PlanPosition(bool retreat)
    {
        nextPlan = Time.time + decisionInterval;
        if (!target) return;
        Vector3 player = target.transform.position;
        targetAtPlan = player;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        Vector3 away = Vector3.ProjectOnPlane(transform.position - player, Vector3.up).normalized;
        if (away.sqrMagnitude < .1f) away = -transform.forward;
        float best = float.PositiveInfinity; Vector3 chosen = transform.position, chosenPeek = transform.position;
        bool covered = false;
        for (int i = 0; i < positionSamples; i++)
        {
            Vector3 radial = Quaternion.Euler(0, Random.Range(-110f, 110f), 0) * away;
            Vector3 desired = player + radial * Random.Range(preferredNear, preferredFar);
            // Search both the current level and sampled heights; only complete routes are accepted.
            desired.y = i % 3 == 0 ? player.y + Random.Range(1.5f, maxNavigableHeight) : transform.position.y;
            if (!NavMesh.SamplePosition(desired, out var sample, 3, filter) ||
                Mathf.Abs(sample.position.y - player.y) > maxNavigableHeight ||
                Vector3.Distance(sample.position, transform.position) < 2 || !SafeRoute(sample.position, filter, retreat)) continue;
            float routeLength = PathLength(path);
            bool cover = TryCover(sample.position, player, filter, out Vector3 peek);
            if (!cover && !ClearShot(sample.position + Vector3.up * 1.4f, TargetPoint(target))) continue;
            float score = routeLength * .35f + Mathf.Abs(FlatDistance(sample.position, player) - (preferredNear + preferredFar) * .5f) * .4f;
            score -= cover ? 10 : 0;
            score += Exposure(sample.position + Vector3.up * 1.4f) * 7;
            score -= Mathf.Clamp(sample.position.y - player.y, 0, 5) * .9f;
            if (FlatDistance(sample.position, previousGoal) < 3) score += 8;
            if (score >= best) continue;
            best = score; chosen = sample.position; chosenPeek = peek; covered = cover;
        }
        if (float.IsPositiveInfinity(best))
        {
            // A short lateral/away route is a fallback, never a teleport or an unreachable destination.
            for (int i = 0; i < 8; i++)
            {
                Vector3 desired = transform.position + Quaternion.Euler(0, (i % 2 == 0 ? 1 : -1) * (25 + i * 10), 0) * away * 5;
                if (!NavMesh.SamplePosition(desired, out var sample, 2, filter) || !SafeRoute(sample.position, filter, retreat) ||
                    FlatDistance(sample.position, player) < FlatDistance(transform.position, player) + 1) continue;
                chosen = chosenPeek = sample.position; best = 0; covered = false; break;
            }
        }
        if (float.IsPositiveInfinity(best)) { Stop(); behaviour = Behaviour.Searching; return; }
        previousGoal = goal; coverPoint = chosen; peekPoint = chosenPeek; hasCover = covered;
        SetGoal(chosen, retreat ? Behaviour.Escaping : Behaviour.Repositioning);
    }
    bool SafeRoute(Vector3 destination, NavMeshQueryFilter filter, bool retreat)
    {
        if (!NavMesh.CalculatePath(transform.position, destination, filter, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        var corners = path.corners;
        if (Time.time < rejectLinkUntil)
            foreach (var corner in corners) if (Vector3.Distance(corner, rejectedLinkEnd) < .8f) return false;
        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject ? client.PlayerObject.GetComponent<PlayerHealth>() : null;
            if (!ValidTarget(player)) continue;
            Vector3 p = player.transform.position;
            float safe = Mathf.Min(retreat ? retreatDistance : 6, FlatDistance(transform.position, p) - .5f);
            for (int i = 1; i < corners.Length; i++)
                if (Mathf.Abs(corners[i].y - p.y) < 2.5f && SegmentDistance(p, corners[i - 1], corners[i]) < safe) return false;
            if (retreat && FlatDistance(destination, p) < Mathf.Min(resumeAttackDistance, FlatDistance(transform.position, p) + 2)) return false;
        }
        return true;
    }
    bool TryCover(Vector3 point, Vector3 player, NavMeshQueryFilter filter, out Vector3 peek)
    {
        peek = point;
        if (!SolidBetween(player + Vector3.up, point + Vector3.up * 1.4f)) return false;
        Vector3 tangent = Vector3.Cross(Vector3.up, (point - player).normalized);
        for (int i = 0; i < 4; i++)
        {
            float side = i % 2 == 0 ? -1 : 1, distance = i < 2 ? 2.2f : 3.8f;
            if (!NavMesh.SamplePosition(point + tangent * side * distance, out var sample, 1.2f, filter) ||
                Vector3.Distance(sample.position, player) > attackRange ||
                !ClearShot(sample.position + Vector3.up * 1.4f, player + Vector3.up * 1.1f) ||
                !NavMesh.CalculatePath(point, sample.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            peek = sample.position; return true;
        }
        return false;
    }
    float Exposure(Vector3 point)
    {
        float visible = 0;
        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject ? client.PlayerObject.GetComponent<PlayerHealth>() : null;
            if (!ValidTarget(player)) continue;
            // Root yaw is replicated for remote players; do not read a remote client's local mouse pitch.
            Vector3 direction = Vector3.ProjectOnPlane(point - player.transform.position, Vector3.up).normalized;
            if (Vector3.Dot(player.transform.forward, direction) > .45f && !SolidBetween(TargetPoint(player), point)) visible++;
        }
        return visible;
    }
    static float PathLength(NavMeshPath route) { float length = 0; var c = route.corners; for (int i = 1; i < c.Length; i++) length += Vector3.Distance(c[i - 1], c[i]); return length; }
    static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.x, a.z), new(b.x, b.z));
    static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = Vector3.ProjectOnPlane(b - a, Vector3.up);
        float t = ab.sqrMagnitude > .001f ? Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(p - a, Vector3.up), ab) / ab.sqrMagnitude) : 0;
        return FlatDistance(p, a + ab * t);
    }
    static bool SolidBetween(Vector3 a, Vector3 b) => FirstContact(a, b, 0, out _, true);
    bool ClearShot(Vector3 a, Vector3 b) => !FirstContact(a, b, acidBallRadius, out var contact) || contact.collider.GetComponentInParent<PlayerHealth>();

    void Update()
    {
        if (!IsSpawned) return;
        if (IsServer) UpdateAcidProjectiles();
        if (!IsServer || health.IsDead || behaviour != Behaviour.Attacking) return;
        if (ai.EstaAturdido || !CanUseRangedMode || !ValidTarget(target) || NearestThreatDistance() < retreatDistance)
        { CancelCast(); escapeLatched = true; behaviour = Behaviour.Escaping; nextPlan = 0; return; }
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (!state.IsName("StalkerAcid"))
        {
            if (enteredCast || Time.time - castStarted > .7f) FinishCast();
            return;
        }
        enteredCast = true;
        UpdateTargetMotion();
        if (!released && state.normalizedTime < Mathf.Min(acidAimLockPhase, acidReleasePhase))
        {
            float remaining = Mathf.Max(0, acidReleasePhase - state.normalizedTime) * state.length /
                Mathf.Max(.01f, Mathf.Abs(state.speed * state.speedMultiplier * animator.speed));
            aimPoint.Value = PredictAcidAim(remaining); Face(target.transform.position);
        }
        if (!released && state.normalizedTime >= acidReleasePhase) pendingRelease = true;
    }
    void FinishCast()
    {
        behaviour = Behaviour.Searching; pendingRelease = false; nextAttack = Time.time + attackCooldown; nextPlan = 0;
    }
    void CancelCast()
    {
        if (behaviour != Behaviour.Attacking) return;
        pendingRelease = false; nextAttack = Time.time + attackCooldown;
        networkAnimator.ResetTrigger("Acid"); networkAnimator.SetTrigger("Escape");
    }
    void OnAnimatorIK(int layer)
    {
        if (!head || !animator) return;
        if (!animator.GetCurrentAnimatorStateInfo(0).IsName("StalkerAcid") || health.IsDead) { animator.SetLookAtWeight(0); return; }
        animator.SetLookAtWeight(1, .2f, 1, 0, .25f); animator.SetLookAtPosition(aimPoint.Value);
    }
    void LateUpdate()
    {
        if (!IsSpawned) return;
        UpdateAcidVisuals();
        if (!health.IsDead && head && mouth && animator.GetCurrentAnimatorStateInfo(0).IsName("StalkerAcid"))
        {
            Vector3 forward = head.rotation * headBasis * Vector3.forward;
            Vector3 horizontal = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            Vector3 intended = (aimPoint.Value - mouth.position).normalized;
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(intended.y, -1, 1)) * Mathf.Rad2Deg, -45, 45) * Mathf.Deg2Rad;
            if (horizontal.sqrMagnitude > .1f) head.rotation = Quaternion.FromToRotation(forward,
                horizontal * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)) * head.rotation;
        }
        if (IsServer && pendingRelease && behaviour == Behaviour.Attacking && !health.IsDead)
        { pendingRelease = false; released = true; FireAcidBall(); }
        if (IsServer && !traversing && agent.enabled && agent.isOnNavMesh)
            animator.SetFloat("LocomotionRate", agent.velocity.magnitude < .15f ? 1 : Mathf.Clamp(agent.velocity.magnitude / (ai.IsRunning ? 6 : 1.7f), .4f, 2.5f));
    }
    void BeginTraversal()
    {
        if (traversing) return;
        var link = agent.currentOffMeshLinkData;
        if (!link.valid || Vector3.Distance(link.startPos, link.endPos) > maxLinkDistance || Mathf.Abs(link.endPos.y - link.startPos.y) > maxLinkHeight)
        {
            rejectedLinkEnd = link.endPos; rejectLinkUntil = Time.time + 10;
            agent.Warp(transform.position); agent.isStopped = true;
            behaviour = Behaviour.Searching; nextPlan = 0; return;
        }
        linkFrom = transform.position; linkTo = link.endPos + Vector3.up * agent.baseOffset;
        linkElapsed = 0; traversing = true; returningFromLink = false; behaviour = Behaviour.Traversing;
        agent.updatePosition = false; agent.updateRotation = false;
        networkAnimator.SetTrigger("Traverse"); animator.SetBool("Traversing", true);
        Face(linkTo);
    }
    internal void TickTraversal()
    {
        if (!IsServer || !traversing || health.IsDead || ai.EstaAturdido) return;
        linkElapsed += returningFromLink ? -Time.deltaTime : Time.deltaTime;
        float progress = Mathf.Clamp01(linkElapsed / Mathf.Max(.5f, jumpDuration));
        float flight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, .8f, progress));
        Vector3 next = Vector3.Lerp(linkFrom, linkTo, flight) + Vector3.up * (4 * flight * (1 - flight) * jumpArc);
        // Existing navigation links provide the route; ceilings and newly closed doors still block movement.
        if (!returningFromLink && FirstContact(transform.position + Vector3.up, next + Vector3.up, .25f, out _))
        { returningFromLink = true; return; }
        transform.position = next; agent.nextPosition = next; Face(linkTo);
        if (returningFromLink ? linkElapsed > 0 : progress < 1) return;
        if (returningFromLink) agent.Warp(linkFrom); // Reattach at the point physically reached, with no position jump.
        else agent.CompleteOffMeshLink();
        agent.updatePosition = true; agent.updateRotation = true;
        animator.SetBool("Traversing", false); traversing = false; behaviour = Behaviour.Searching; nextPlan = 0;
    }
}
