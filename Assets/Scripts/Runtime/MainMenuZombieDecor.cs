using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zombie puramente decorativo para el menú.
/// No usa ZombieAI ni NavMesh. Puede quedar en Idle o caminar lentamente
/// entre dos puntos, manteniendo sus variantes fijas.
/// </summary>
public class MainMenuZombieDecor : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private bool walking;
    [SerializeField] private Vector3 pointA;
    [SerializeField] private Vector3 pointB;
    [SerializeField] private float worldSpeed = 0.45f;
    [SerializeField] private int idleVariant;
    [SerializeField] private int walkVariant;

    [Header("Movimiento")]
    [SerializeField] private float turnSpeed = 90f;
    [SerializeField] private Vector2 pauseAtEnds = new Vector2(1.5f, 4f);
    [SerializeField] private float animatorSpeed = 0.82f;

    private readonly HashSet<int> floatParameters = new HashSet<int>();
    private readonly HashSet<int> boolParameters = new HashSet<int>();

    private Vector3 currentTarget;
    private bool goingToB = true;
    private float pauseUntil;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int IdleVariant = Animator.StringToHash("IdleVariant");
    private static readonly int WalkVariant = Animator.StringToHash("WalkVariant");
    private static readonly int RunVariant = Animator.StringToHash("RunVariant");
    private static readonly int SprintVariant = Animator.StringToHash("SprintVariant");
    private static readonly int Running = Animator.StringToHash("Running");
    private static readonly int Sprint = Animator.StringToHash("Sprint");
    private static readonly int Spawning = Animator.StringToHash("Spawning");

    public void Configure(
        Animator targetAnimator,
        bool shouldWalk,
        Vector3 start,
        Vector3 end,
        float speed,
        int fixedIdleVariant,
        int fixedWalkVariant)
    {
        animator = targetAnimator;
        walking = shouldWalk;
        pointA = start;
        pointB = end;
        worldSpeed = speed;
        idleVariant = fixedIdleVariant;
        walkVariant = fixedWalkVariant;

        currentTarget = pointB;
        CacheParameters();
        ApplyAnimatorState();
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        CacheParameters();
    }

    private void OnEnable()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator == null)
            return;

        CacheParameters();

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.speed = animatorSpeed;

        currentTarget = pointB;
        pauseUntil = 0f;

        ApplyAnimatorState();
    }

    private void Update()
    {
        if (animator == null)
            return;

        if (!walking)
        {
            SetFloat(Speed, 0f);
            SetFloat(MoveX, 0f);
            SetFloat(MoveY, 0f);
            return;
        }

        if (Time.unscaledTime < pauseUntil)
        {
            SetFloat(Speed, 0f);
            return;
        }

        Vector3 delta = currentTarget - transform.position;
        delta.y = 0f;

        if (delta.magnitude <= 0.15f)
        {
            goingToB = !goingToB;
            currentTarget = goingToB ? pointB : pointA;

            pauseUntil =
                Time.unscaledTime +
                Random.Range(
                    Mathf.Min(pauseAtEnds.x, pauseAtEnds.y),
                    Mathf.Max(pauseAtEnds.x, pauseAtEnds.y)
                );

            SetFloat(Speed, 0f);
            return;
        }

        Vector3 direction = delta.normalized;

        Quaternion desiredRotation =
            Quaternion.LookRotation(direction, Vector3.up);

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                desiredRotation,
                turnSpeed * Time.unscaledDeltaTime
            );

        transform.position +=
            direction *
            worldSpeed *
            Time.unscaledDeltaTime;

        SetFloat(Speed, 0.65f);
        SetFloat(MoveX, 0f);
        SetFloat(MoveY, 1f);
    }

    private void ApplyAnimatorState()
    {
        SetBool(Spawning, false);
        SetBool(Running, false);
        SetBool(Sprint, false);

        SetFloat(IdleVariant, idleVariant);
        SetFloat(WalkVariant, walkVariant);
        SetFloat(RunVariant, 0f);
        SetFloat(SprintVariant, 0f);

        SetFloat(MoveX, 0f);
        SetFloat(MoveY, walking ? 1f : 0f);
        SetFloat(Speed, walking ? 0.65f : 0f);

        if (!walking && animator != null)
            animator.CrossFade("Idle", 0.05f);
    }

    private void CacheParameters()
    {
        floatParameters.Clear();
        boolParameters.Clear();

        if (animator == null)
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Float)
                floatParameters.Add(parameter.nameHash);
            else if (parameter.type == AnimatorControllerParameterType.Bool)
                boolParameters.Add(parameter.nameHash);
        }
    }

    private void SetFloat(int hash, float value)
    {
        if (floatParameters.Contains(hash))
            animator.SetFloat(hash, value);
    }

    private void SetBool(int hash, bool value)
    {
        if (boolParameters.Contains(hash))
            animator.SetBool(hash, value);
    }
}
