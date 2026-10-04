using UnityEngine;
using UnityEngine.InputSystem;

// Sway por mirar/moverse + bobbing al caminar. Nada de recoil ni de salto
// acá — eso vive en WeaponRecoil.cs y WeaponJumpMotion.cs, cada uno con su
// propia responsabilidad. WeaponMotionApplier suma los tres resultados y
// escribe el Transform una sola vez por frame.
public class WeaponSway : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Weapon weapon; // solo para saber si está apuntando (reduce el sway)

    [Header("Sway por mouse (look)")]
    [SerializeField] private float lookSwayAmount = 0.003f;        // bajado de nuevo — se sentía exagerado
    [SerializeField] private float maxLookSwayAmount = 0.012f;
    [SerializeField] private float lookRotationSwayAmount = 0.6f;  // bajado de nuevo
    [SerializeField] private float maxLookRotationSway = 1.5f;

    [Header("Sway por movimiento (strafe/adelante-atrás)")]
    [SerializeField] private Vector2 moveSwayAmount = new Vector2(0.01f, 0.006f);
    [SerializeField] private float moveRotationSwayAmount = 2.5f;

    [Header("Reduce el sway al apuntar")]
    [SerializeField, Range(0f, 1f)] private float aimingSwayMultiplier = 0.35f;

    [Header("Bobbing al caminar")]
    [SerializeField] private float bobFrequency = 8f;
    [SerializeField] private float bobAmount = 0.012f;
    [SerializeField] private float bobFrequencySprintMultiplier = 1.5f;
    [Tooltip("0 = sin bob por código (el balanceo al caminar lo hacen los clips del Animator, como en el original). " +
             "Subilo solo si algún arma no tiene balanceo propio en su animación.")]
    [SerializeField, Range(0f, 1f)] private float proceduralBobScale = 0f;
    [Tooltip("Segundos que tarda el bob en apagarse al despegar o al frenar (fundido, no corte).")]
    [SerializeField] private float bobFadeOutTime = 0.15f;
    [Tooltip("Segundos que tarda el bob en volver al aterrizar o al arrancar a caminar.")]
    [SerializeField] private float bobFadeInTime = 0.20f;

    [Header("Resortes")]
    [SerializeField] private SpringVector3 positionSpring = new SpringVector3 { stiffness = 120f, damping = 18f, mass = 1f };
    [SerializeField] private SpringVector3 rotationSpring = new SpringVector3 { stiffness = 120f, damping = 18f, mass = 1f };

    private float bobTimer;
    private float bobWeight;
    private readonly AirTracker air = new AirTracker();

    // Se recalcula una sola vez por frame, la primera vez que lo pide
    // TickPosition() o TickRotation() (no importa cuál llamen primero).
    private int lastComputedFrame = -1;
    private Vector3 cachedTargetPosition;
    private Vector3 cachedTargetRotation;

    /// <summary>Lo lee WeaponMotionApplier para escalar la pose de salto.</summary>
    public bool IsAiming => weapon != null && weapon.IsAiming;
    public bool IsSprinting => playerMovement != null && playerMovement.IsSprinting;

    private void ComputeTargetsIfNeeded()
    {
        if (lastComputedFrame == Time.frameCount)
            return;

        lastComputedFrame = Time.frameCount;

        float aimMultiplier = (weapon != null && weapon.IsAiming) ? aimingSwayMultiplier : 1f;

        Vector3 lookPos = CalculateLookSway(out Vector3 lookRot);
        Vector3 movePos = CalculateMoveSway(out Vector3 moveRot);
        Vector3 bobPos = CalculateWalkBob();

        cachedTargetPosition = (lookPos + movePos + bobPos) * aimMultiplier;
        cachedTargetRotation = (lookRot + moveRot) * aimMultiplier;

        positionSpring.SetTarget(cachedTargetPosition);
        rotationSpring.SetTarget(cachedTargetRotation);
    }

    public Vector3 TickPosition()
    {
        ComputeTargetsIfNeeded();
        return positionSpring.Evaluate(Time.deltaTime);
    }

    public Vector3 TickRotation()
    {
        ComputeTargetsIfNeeded();
        return rotationSpring.Evaluate(Time.deltaTime);
    }

    // =========================================================
    // SWAY POR MOUSE (look)
    // =========================================================

    private Vector3 CalculateLookSway(out Vector3 rotationOffset)
    {
        // NUEVO: en pausa (y los 2 frames siguientes a reanudar) el cursor está
        // desbloqueado y se mueve para clickear botones — eso NO es mirar. Sin
        // este chequeo, ese delta quedaba guardado como objetivo del resorte y
        // se notaba como la cámara/arma "deslizándose sola" un instante después
        // de despausar.
        Vector2 mouseDelta = (Mouse.current != null && !PlayerInputGate.MouseBlocked)
            ? Mouse.current.delta.ReadValue()
            : Vector2.zero;

        float swayX = Mathf.Clamp(-mouseDelta.x * lookSwayAmount * 0.01f, -maxLookSwayAmount, maxLookSwayAmount);
        float swayY = Mathf.Clamp(-mouseDelta.y * lookSwayAmount * 0.01f, -maxLookSwayAmount, maxLookSwayAmount);

        float rotY = Mathf.Clamp(mouseDelta.x * lookRotationSwayAmount * 0.01f, -maxLookRotationSway, maxLookRotationSway);
        float rotX = Mathf.Clamp(-mouseDelta.y * lookRotationSwayAmount * 0.01f, -maxLookRotationSway, maxLookRotationSway);

        rotationOffset = new Vector3(rotX, rotY, 0f);
        return new Vector3(swayX, swayY, 0f);
    }

    // =========================================================
    // SWAY POR MOVIMIENTO (strafe / adelante-atrás)
    // =========================================================

    private Vector3 CalculateMoveSway(out Vector3 rotationOffset)
    {
        if (playerMovement == null)
        {
            rotationOffset = Vector3.zero;
            return Vector3.zero;
        }

        Vector2 move = playerMovement.MoveInput;

        float posX = -move.x * moveSwayAmount.x;
        float posY = -Mathf.Abs(move.y) * moveSwayAmount.y * 0.5f;

        // Al strafear, el arma se "atrasa" un toque en el roll, como si pesara.
        float rollZ = move.x * moveRotationSwayAmount;

        rotationOffset = new Vector3(0f, 0f, -rollZ);
        return new Vector3(posX, posY, 0f);
    }

    // =========================================================
    // BOBBING AL CAMINAR — con fundido, nunca un corte
    // =========================================================

    private Vector3 CalculateWalkBob()
    {
        float dt = Time.deltaTime;

        if (proceduralBobScale <= 0f)
        {
            bobWeight = 0f;
            bobTimer = 0f;
            return Vector3.zero;
        }

        // AirTracker ignora el parpadeo de isGrounded (escalones, rampas).
        air.Tick(characterController, dt);

        bool walking = false;

        if (characterController != null && !air.IsAirborne)
        {
            Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
            walking = horizontalVelocity.magnitude > 0.1f;
        }

        // El peso del bob sube/baja gradualmente. En el aire baja a 0 mientras la
        // pose de WeaponJumpMotion sube a 1: el arma CAMBIA de pose en vez de
        // simplemente dejar de balancearse.
        float targetWeight = walking ? 1f : 0f;
        float fadeTime = walking ? bobFadeInTime : bobFadeOutTime;
        bobWeight = Mathf.MoveTowards(bobWeight, targetWeight, dt / Mathf.Max(fadeTime, 0.01f));

        if (bobWeight <= 0.0001f)
        {
            bobTimer = 0f;
            return Vector3.zero;
        }

        // La fase solo avanza mientras camina; durante el fundido de salida queda
        // congelada, así el arma se "asienta" en vez de seguir oscilando en el aire.
        if (walking)
        {
            float freq = bobFrequency * (IsSprinting ? bobFrequencySprintMultiplier : 1f);
            bobTimer += dt * freq;
        }

        float vertical = Mathf.Sin(bobTimer) * bobAmount;
        float horizontal = Mathf.Cos(bobTimer * 0.5f) * bobAmount * 0.5f;

        return new Vector3(horizontal, vertical, 0f) * (bobWeight * proceduralBobScale);
    }

    // Llamado por WeaponSwitcher al cambiar de arma (igual que HUDController/WeaponAnimationEvents).
    public void SetWeapon(Weapon newWeapon)
    {
        weapon = newWeapon;
    }
}