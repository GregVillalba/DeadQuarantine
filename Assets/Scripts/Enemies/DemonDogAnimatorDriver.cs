using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

/// <summary>
/// Adapta la IA existente del proyecto al Animator Controller DemonDog.
///
/// Parámetros usados por el controller del asset:
/// - Locomotion (float): 0 = idle, 1 = avance.
/// - WalkSpeed (float): 0..1 selecciona Stalk/Stroll/WalkFast/Trot/Canter/Sprint.
/// - Attack1 / Attack2 (trigger).
/// - GotHit (trigger) + GotHitX (float 0..1).
/// - DeathLeft / DeathRight (trigger).
///
/// No modifica el Animator Controller: solamente conduce sus parámetros.
/// </summary>
public class DemonDogAnimatorDriver : ZombieAnimationDriver
{
    [Header("Referencias")]
    [SerializeField] private Animator animator;
    [Tooltip("Opcional. Si el prefab tiene NetworkAnimator, se usa para sincronizar triggers.")]
    [SerializeField] private NetworkAnimator networkAnimator;

    [Header("Locomoción")]
    [SerializeField] private float movementThreshold = 0.05f;
    [SerializeField] private float locomotionDampTime = 0.08f;
    [SerializeField] private float walkSpeedDampTime = 0.08f;

    [Header("Ataque")]
    [SerializeField] private bool alternateAttackAnimations = true;

    [Header("Muerte")]
    [Tooltip("Congela al perro en la pose final de muerte para impedir que el controller vuelva a Locomotion antes del despawn.")]
    [SerializeField] private bool freezeDeathPose = true;

    [Tooltip("El controller original sale de DiesLeft/DiesRight cerca de 0.85. Se congela un poco antes.")]
    [Range(0.5f, 0.84f)]
    [SerializeField] private float freezeDeathAtNormalizedTime = 0.82f;

    [Tooltip("Tiempo máximo para esperar a que el Animator entre al estado de muerte después del trigger.")]
    [SerializeField] private float deathStateEnterTimeout = 1f;

    private static readonly int LocomotionHash = Animator.StringToHash("Locomotion");
    private static readonly int WalkSpeedHash = Animator.StringToHash("WalkSpeed");
    private static readonly int StrafeHash = Animator.StringToHash("Strafe");
    private static readonly int IdleStyleHash = Animator.StringToHash("IdleStyle");
    private static readonly int GotHitXHash = Animator.StringToHash("GotHitX");

    private bool useAttack1Next = true;
    private bool dead;
    private NetworkObject networkObject;
    private Coroutine freezeDeathRoutine;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (networkAnimator == null)
            networkAnimator = GetComponent<NetworkAnimator>();

        if (networkAnimator == null)
            networkAnimator = GetComponentInParent<NetworkAnimator>();

        networkObject = GetComponentInParent<NetworkObject>();

        if (animator == null)
        {
            Debug.LogError(
                "[DemonDogAnimatorDriver] No se encontró Animator en " + name + ".",
                this
            );
            return;
        }

        // El perro persigue hacia adelante; no necesitamos strafe para esta IA.
        animator.SetFloat(StrafeHash, 0f);
        animator.SetFloat(IdleStyleHash, 0f);
    }

    public override void SetMovement(float worldSpeed, float maxSpeed)
    {
        if (animator == null || dead)
            return;

        bool moving = worldSpeed > movementThreshold;

        // El blend tree principal del asset usa Locomotion = 0 para Idle y 1 para Forward.
        animator.SetFloat(
            LocomotionHash,
            moving ? 1f : 0f,
            locomotionDampTime,
            Time.deltaTime
        );

        float normalizedSpeed =
            maxSpeed > 0.01f
                ? Mathf.Clamp01(worldSpeed / maxSpeed)
                : 0f;

        animator.SetFloat(
            WalkSpeedHash,
            normalizedSpeed,
            walkSpeedDampTime,
            Time.deltaTime
        );
    }

    public override void PlayAttack()
    {
        if (animator == null || dead)
            return;

        string trigger;

        if (alternateAttackAnimations)
        {
            trigger = useAttack1Next ? "Attack1" : "Attack2";
            useAttack1Next = !useAttack1Next;
        }
        else
        {
            trigger = Random.value < 0.5f ? "Attack1" : "Attack2";
        }

        SetNetworkedAttackTrigger(trigger);
    }

    public override void PlayHit()
    {
        if (animator == null || dead)
            return;

        // El controller tiene un blend de impactos direccionales basado en GotHitX.
        // Como ZombieHealth todavía no conserva la dirección del atacante, elegimos
        // una reacción de impacto válida de forma aleatoria.
        animator.SetFloat(GotHitXHash, Random.value);

        // ZombieHealth ya llama este método mediante ClientRpc en cada cliente.
        // Se dispara localmente para no duplicar el evento a través de NetworkAnimator.
        animator.SetTrigger("GotHit");
    }

    public override void PlayDeath()
    {
        if (animator == null || dead)
            return;

        dead = true;

        animator.SetFloat(LocomotionHash, 0f);
        animator.SetFloat(WalkSpeedHash, 0f);

        // ZombieHealth reproduce la muerte mediante ClientRpc en todos los clientes.
        // Elegimos la misma variante en todos usando el NetworkObjectId.
        bool useLeftDeath =
            networkObject != null && networkObject.IsSpawned
                ? networkObject.NetworkObjectId % 2UL == 0UL
                : Random.value < 0.5f;

        string deathTrigger = useLeftDeath ? "DeathLeft" : "DeathRight";
        string deathState = useLeftDeath ? "DiesLeft" : "DiesRight";

        animator.SetTrigger(deathTrigger);

        if (freezeDeathPose)
        {
            if (freezeDeathRoutine != null)
                StopCoroutine(freezeDeathRoutine);

            freezeDeathRoutine = StartCoroutine(
                FreezeDeathPoseRoutine(deathState)
            );
        }
    }

    private System.Collections.IEnumerator FreezeDeathPoseRoutine(string deathStateName)
    {
        if (animator == null)
            yield break;

        // Espera a que el trigger realmente lleve al Animator a DiesLeft/DiesRight.
        float elapsed = 0f;
        bool enteredDeathState = false;

        while (elapsed < deathStateEnterTimeout)
        {
            if (animator == null || !animator.enabled)
                yield break;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

            if (state.IsName(deathStateName))
            {
                enteredDeathState = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!enteredDeathState)
            yield break;

        // El controller del asset tiene una transición automática desde las muertes
        // hacia Locomotion con Exit Time ~= 0.85. Frenamos antes de ese punto para
        // conservar la pose de cadáver hasta que ZombieHealth haga el despawn.
        while (animator != null && animator.enabled)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

            if (!state.IsName(deathStateName))
                yield break;

            if (state.normalizedTime >= freezeDeathAtNormalizedTime)
            {
                animator.speed = 0f;
                yield break;
            }

            yield return null;
        }
    }

    private void SetNetworkedAttackTrigger(string triggerName)
    {
        // El ataque nace en ZombieAI, que solo corre en el servidor.
        // Para ese caso NetworkAnimator.SetTrigger mantiene el ataque sincronizado.
        if (networkAnimator != null && networkAnimator.isActiveAndEnabled)
        {
            networkAnimator.SetTrigger(triggerName);
        }
        else if (animator != null)
        {
            animator.SetTrigger(triggerName);
        }
    }
}
