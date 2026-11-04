using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Personaje decorativo del menú.
/// Usa el AnimatorOverrideController del RIFLE que ya usa NetworkWeaponPresentation.
/// Alterna Idle relajado <-> Aim sin activar gameplay.
/// </summary>
public class MainMenuCharacterPose : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("Rifle")]
    [SerializeField] private int weaponId = 2;
    [SerializeField] private Transform weaponRoot;
    [SerializeField] private Transform leftHandGrip;

    [Header("Idle / Aim")]
    [SerializeField] private Vector2 relaxedDuration = new Vector2(7f, 11f);
    [SerializeField] private Vector2 aimDuration = new Vector2(4.5f, 7f);
    [Range(0f, 1f)]
    [SerializeField] private float relaxedAimWeight = 0f;
    [Range(0f, 1f)]
    [SerializeField] private float aimedAimWeight = 1f;
    [SerializeField] private float aimBlendSpeed = 1.15f;

    [Header("Animación")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float animatorSpeed = 0.90f;
    [SerializeField] private float lookSwayAmount = 0.08f;
    [SerializeField] private float lookSwaySpeed = 0.12f;

    private readonly HashSet<int> floatParameters = new HashSet<int>();
    private readonly HashSet<int> boolParameters = new HashSet<int>();

    private bool aiming;
    private float currentAimWeight;
    private float nextChangeTime;

    private static readonly int TP_Speed = Animator.StringToHash("TP_Speed");
    private static readonly int TP_IsAiming = Animator.StringToHash("TP_IsAiming");
    private static readonly int TP_MoveZ = Animator.StringToHash("TP_MoveZ");
    private static readonly int TP_MoveX = Animator.StringToHash("TP_MoveX");
    private static readonly int TP_Look = Animator.StringToHash("TP_Look");
    private static readonly int TP_IsAlive = Animator.StringToHash("TP_IsAlive");
    private static readonly int TP_IsGrounded = Animator.StringToHash("TP_IsGrounded");
    private static readonly int TP_IsReloading = Animator.StringToHash("TP_IsReloading");
    private static readonly int TP_IsCrouching = Animator.StringToHash("TP_IsCrouching");
    private static readonly int TP_IsSprinting = Animator.StringToHash("TP_IsSprinting");
    private static readonly int TP_WeaponID = Animator.StringToHash("TP_WeaponID");
    private static readonly int TP_MoveAmount = Animator.StringToHash("TP_MoveAmount");
    private static readonly int TP_AimWeight = Animator.StringToHash("TP_AimWeight");
    private static readonly int TP_ReloadSpeed = Animator.StringToHash("TP_ReloadSpeed");
    private static readonly int TP_VerticalSpeed = Animator.StringToHash("TP_VerticalSpeed");

    public void Configure(
        Animator targetAnimator,
        int newWeaponId,
        Transform newWeaponRoot)
    {
        animator = targetAnimator;
        weaponId = newWeaponId;
        weaponRoot = newWeaponRoot;
        FindLeftHandGrip();
        CacheParameters();
        ApplyBaseParameters();
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        CacheParameters();
        FindLeftHandGrip();
    }

    private void OnEnable()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator == null)
            return;

        CacheParameters();
        FindLeftHandGrip();

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.speed = animatorSpeed;

        aiming = false;
        currentAimWeight = relaxedAimWeight;

        nextChangeTime =
            Time.unscaledTime +
            Random.Range(relaxedDuration.x, relaxedDuration.y);

        ApplyBaseParameters();
    }

    private void Update()
    {
        if (animator == null)
            return;

        ApplyBaseParameters();

        if (Time.unscaledTime >= nextChangeTime)
        {
            aiming = !aiming;

            Vector2 range =
                aiming ? aimDuration : relaxedDuration;

            nextChangeTime =
                Time.unscaledTime +
                Random.Range(
                    Mathf.Min(range.x, range.y),
                    Mathf.Max(range.x, range.y)
                );
        }

        float target =
            aiming ? aimedAimWeight : relaxedAimWeight;

        currentAimWeight =
            Mathf.MoveTowards(
                currentAimWeight,
                target,
                aimBlendSpeed * Time.unscaledDeltaTime
            );

        SetBool(
            TP_IsAiming,
            aiming || currentAimWeight > 0.08f
        );

        SetFloat(TP_AimWeight, currentAimWeight);

        SetFloat(
            TP_Look,
            Mathf.Sin(
                Time.unscaledTime *
                lookSwaySpeed *
                Mathf.PI * 2f
            ) * lookSwayAmount
        );
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null ||
            !animator.isHuman ||
            leftHandGrip == null)
        {
            return;
        }

        // Misma idea que ThirdPersonWeaponGrip del Player real:
        // la mano izquierda se mantiene en TP_LeftHandGrip del rifle.
        const float weight = 1f;

        animator.SetIKPositionWeight(
            AvatarIKGoal.LeftHand,
            weight
        );

        animator.SetIKRotationWeight(
            AvatarIKGoal.LeftHand,
            weight
        );

        animator.SetIKPosition(
            AvatarIKGoal.LeftHand,
            leftHandGrip.position
        );

        animator.SetIKRotation(
            AvatarIKGoal.LeftHand,
            leftHandGrip.rotation
        );
    }

    private void FindLeftHandGrip()
    {
        leftHandGrip = null;

        if (weaponRoot == null)
            return;

        Transform[] all =
            weaponRoot.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in all)
        {
            if (child.name == "TP_LeftHandGrip")
            {
                leftHandGrip = child;
                break;
            }
        }
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

    private void ApplyBaseParameters()
    {
        SetBool(TP_IsAlive, true);
        SetBool(TP_IsGrounded, true);
        SetBool(TP_IsReloading, false);
        SetBool(TP_IsCrouching, false);
        SetBool(TP_IsSprinting, false);

        SetFloat(TP_Speed, 0f);
        SetFloat(TP_MoveX, 0f);
        SetFloat(TP_MoveZ, 0f);
        SetFloat(TP_MoveAmount, 0f);
        SetFloat(TP_WeaponID, weaponId);
        SetFloat(TP_ReloadSpeed, 1f);
        SetFloat(TP_VerticalSpeed, 0f);
    }

    private void SetBool(int hash, bool value)
    {
        if (boolParameters.Contains(hash))
            animator.SetBool(hash, value);
    }

    private void SetFloat(int hash, float value)
    {
        if (floatParameters.Contains(hash))
            animator.SetFloat(hash, value);
    }
}
