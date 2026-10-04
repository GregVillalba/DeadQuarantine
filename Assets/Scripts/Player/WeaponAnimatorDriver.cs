using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Le manda al Animator de FPS_Arms los parámetros de movimiento que usa el
/// animator del proyecto original (Infima LPSP): Crouching, Movement,
/// Horizontal, Vertical, Turning y Play Rate Locomotion.
///
/// Va en el MISMO objeto que el Animator (FPS_Arms), junto a WeaponSway,
/// WeaponRecoil, WeaponJumpMotion y WeaponMotionApplier.
///
/// No pisa nada de lo que ya hace Weapon.cs (Speed, IsAiming, Aiming, Running,
/// Fire, Reload...). Solo agrega parámetros nuevos.
///
/// IMPORTANTE: solo escribe los parámetros que EXISTEN en el controller. Podés
/// agregar este script antes de terminar los cambios del Animator y no hay
/// errores ni warnings: cada parámetro empieza a funcionar cuando lo creás.
/// Funciona también con los AnimatorOverrideController de cada arma.
/// </summary>
public class WeaponAnimatorDriver : MonoBehaviour
{
    [Header("Referencias (si están vacías se buscan solas)")]
    [SerializeField] private Animator weaponAnimator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Caminar / correr")]
    [Tooltip("Segundos de suavizado de Movement / Horizontal / Vertical. Más alto = transiciones de dirección más lentas.")]
    [SerializeField] private float locomotionDamp = 0.12f;
    [Tooltip("Velocidad (m/s) a la que el ciclo de caminar se reproduce a velocidad normal. = moveSpeed de PlayerMovement.")]
    [SerializeField] private float walkReferenceSpeed = 5f;
    [Tooltip("Velocidad (m/s) a la que el ciclo de correr se reproduce a velocidad normal. = sprintSpeed de PlayerMovement.")]
    [SerializeField] private float sprintReferenceSpeed = 8f;
    [SerializeField] private float minPlayRate = 0.6f;
    [SerializeField] private float maxPlayRate = 1.3f;
    [SerializeField] private float playRateDamp = 0.15f;

    [Header("Girar el mouse (Turning)")]
    [Tooltip("Cuánto del clip de 'turning' se mezcla como máximo al girar rápido. 0 = desactivado.")]
    [SerializeField, Range(0f, 1f)] private float maxTurning = 0.3f;
    [Tooltip("Píxeles de mouse por frame con los que Turning llega al máximo.")]
    [SerializeField] private float turningFullScaleMouseDelta = 25f;
    [SerializeField] private float turningDamp = 0.1f;

    private static readonly int CrouchingHash = Animator.StringToHash("Crouching");
    private static readonly int MovementHash = Animator.StringToHash("Movement");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");
    private static readonly int TurningHash = Animator.StringToHash("Turning");
    private static readonly int PlayRateLocomotionHash = Animator.StringToHash("Play Rate Locomotion");

    private readonly AirTracker air = new AirTracker();

    // Parámetros float/bool que existen en el controller actual (cambia al cambiar de arma).
    private readonly HashSet<int> existingParams = new HashSet<int>();
    private RuntimeAnimatorController paramsCheckedFor;

    private void Awake()
    {
        if (weaponAnimator == null)
            weaponAnimator = GetComponent<Animator>();

        if (weaponAnimator == null)
            weaponAnimator = GetComponentInChildren<Animator>();

        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();

        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();
    }

    private void Update()
    {
        if (weaponAnimator == null || !weaponAnimator.isActiveAndEnabled)
            return;

        RefreshParamCache();

        float dt = Time.deltaTime;

        if (characterController != null)
            air.Tick(characterController, dt);

        // ---------- Crouching ----------
        if (Has(CrouchingHash))
            weaponAnimator.SetBool(CrouchingHash, playerMovement != null && playerMovement.IsCrouching);

        // ---------- Movimiento (input filtrado por lo que REALMENTE te movés) ----------
        // Si caminás contra una pared o estás en el aire, el arma no debe seguir "caminando".
        Vector3 horizontalVelocity = characterController != null
            ? new Vector3(characterController.velocity.x, 0f, characterController.velocity.z)
            : Vector3.zero;

        bool isSprinting = playerMovement != null && playerMovement.IsSprinting;

        // Corriendo, NO se corta la pose al saltar o al tocar algo: la locomoción se
        // mantiene aunque estés en el aire (como el original, donde Running manda).
        // Caminando en el aire sí vuelve a idle, para que no "camine" flotando.
        bool actuallyMoving = air.IsAirborne
            ? isSprinting
            : horizontalVelocity.magnitude > 0.1f;

        Vector2 input = Vector2.zero;
        if (actuallyMoving && playerMovement != null)
            input = Vector2.ClampMagnitude(playerMovement.MoveInput, 1f);

        if (Has(MovementHash))
            weaponAnimator.SetFloat(MovementHash, Mathf.Clamp01(input.magnitude), locomotionDamp, dt);

        if (Has(HorizontalHash))
            weaponAnimator.SetFloat(HorizontalHash, input.x, locomotionDamp, dt);

        if (Has(VerticalHash))
            weaponAnimator.SetFloat(VerticalHash, input.y, locomotionDamp, dt);

        // ---------- Velocidad del ciclo de caminar / correr ----------
        if (Has(PlayRateLocomotionHash))
        {
            float rate = 1f;

            if (actuallyMoving)
            {
                float reference = isSprinting ? sprintReferenceSpeed : walkReferenceSpeed;
                rate = Mathf.Clamp(horizontalVelocity.magnitude / Mathf.Max(reference, 0.01f), minPlayRate, maxPlayRate);
            }

            weaponAnimator.SetFloat(PlayRateLocomotionHash, rate, playRateDamp, dt);
        }

        // ---------- Turning (girar el mouse parado o caminando) ----------
        if (Has(TurningHash))
        {
            float turning = 0f;

            // En pausa el cursor se mueve libre por el menú: no debe girar el arma.
            if (maxTurning > 0f && Mouse.current != null && !PlayerInputGate.MouseBlocked)
            {
                float mouseX = Mathf.Abs(Mouse.current.delta.ReadValue().x);
                turning = Mathf.Clamp01(mouseX / Mathf.Max(turningFullScaleMouseDelta, 0.01f)) * maxTurning;
            }

            weaponAnimator.SetFloat(TurningHash, turning, turningDamp, dt);
        }
    }

    private bool Has(int hash) => existingParams.Contains(hash);

    private void RefreshParamCache()
    {
        RuntimeAnimatorController controller = weaponAnimator.runtimeAnimatorController;

        if (controller == paramsCheckedFor)
            return;

        paramsCheckedFor = controller;
        existingParams.Clear();

        if (controller == null)
            return;

        foreach (AnimatorControllerParameter p in weaponAnimator.parameters)
            existingParams.Add(p.nameHash);
    }
}