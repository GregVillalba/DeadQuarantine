using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sway del arma al MIRAR y al MOVERSE: port de SwayMotion del original, con los
/// SwayData reales (SO_SD_Default / SO_SD_Aiming, SO_ST_Look, SO_ST_Movement, ...).
///
///  - Input de mirada: delta del mouse (limitado a magnitud 1) evalúa las curvas
///    "Look" (horizontal con X, vertical con Y).
///  - Input de movimiento (strafe / adelante-atrás, magnitud 1) evalúa las curvas "Movement".
///  - Al apuntar se usa el SwayData de apuntado (el sway baja a 10%).
///  - Un resorte (damping 12 / stiffness 165, o 16 / 165 apuntando) persigue el resultado.
///
/// El bob al caminar se QUITÓ: lo hace la animación del arma (el original tampoco
/// tiene bob por código).
///
/// Es una fuente más para WeaponMotionApplier (no escribe el Transform). Los valores
/// salen en los ejes del original; el Applier los convierte a los de FPS_Arms.
/// </summary>
public class WeaponSway : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Weapon weapon; // para saber si está apuntando (lo asigna WeaponSwitcher)

    [Header("Sway del original")]
    [SerializeField] private SwayData swayDefault = MotionData.SwayDefault();
    [SerializeField] private SwayData swayAiming = MotionData.SwayAiming();

    [Header("Input de mirada")]
    [Tooltip("El delta del mouse (píxeles por frame) se multiplica por esto y se limita a magnitud 1, " +
             "como el original. Más alto = el sway llega al máximo con movimientos más lentos del mouse.")]
    [SerializeField] private float lookInputScale = 0.1f;

    private PlayerLook playerLook;
    private PauseController pauseController;

    private readonly SpringVector3 locationSpring = new SpringVector3();
    private readonly SpringVector3 rotationSpring = new SpringVector3();

    private int lastComputedFrame = -1;

    /// <summary>Lo lee WeaponMotionApplier / otros scripts.</summary>
    public bool IsAiming => weapon != null && weapon.IsAiming;
    public bool IsSprinting => playerMovement != null && playerMovement.IsSprinting;

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();

        playerLook = GetComponentInParent<PlayerLook>(true);

        // Misma búsqueda que usa PlayerSpectator.
        pauseController = GetComponentInParent<PauseController>();
    }

    /// <summary>
    /// true cuando el personaje no debe reaccionar al input: pausa, o PlayerLook /
    /// PlayerMovement desactivados (el modo pausa los apaga).
    /// </summary>
    private bool InputBlocked()
    {
        if (Time.timeScale == 0f)
            return true;

        if (pauseController != null && pauseController.EstaPausado)
            return true;

        if (playerLook != null && !playerLook.enabled)
            return true;

        if (playerMovement != null && !playerMovement.enabled)
            return true;

        return false;
    }

    [ContextMenu("Restaurar valores del original")]
    private void RestoreOriginal()
    {
        swayDefault = MotionData.SwayDefault();
        swayAiming = MotionData.SwayAiming();
        lookInputScale = 0.1f;
    }

    private void ComputeIfNeeded()
    {
        if (lastComputedFrame == Time.frameCount)
            return;

        lastComputedFrame = Time.frameCount;

        SwayData data = IsAiming ? swayAiming : swayDefault;

        // En pausa no se lee el mouse ni el movimiento: el arma se asienta en vez de seguir al cursor.
        bool blocked = InputBlocked();

        Vector2 mouseDelta = (!blocked && Mouse.current != null) ? Mouse.current.delta.ReadValue() : Vector2.zero;
        Vector2 inputLook = Vector2.ClampMagnitude(mouseDelta * lookInputScale, 1f);
        Vector2 movement = (!blocked && playerMovement != null) ? Vector2.ClampMagnitude(playerMovement.MoveInput, 1f) : Vector2.zero;

        // Horizontal (X del input)
        Vector3 horizontalLocation =
            data.look.horizontal.EvaluateLocation(inputLook.x) * data.look.horizontal.locationMultiplier +
            data.movement.horizontal.EvaluateLocation(movement.x) * data.movement.horizontal.locationMultiplier;

        Vector3 horizontalRotation =
            data.look.horizontal.EvaluateRotation(inputLook.x) * data.look.horizontal.rotationMultiplier +
            data.movement.horizontal.EvaluateRotation(movement.x) * data.movement.horizontal.rotationMultiplier;

        // Vertical (Y del input)
        Vector3 verticalLocation =
            data.look.vertical.EvaluateLocation(inputLook.y) * data.look.vertical.locationMultiplier +
            data.movement.vertical.EvaluateLocation(movement.y) * data.movement.vertical.locationMultiplier;

        Vector3 verticalRotation =
            data.look.vertical.EvaluateRotation(inputLook.y) * data.look.vertical.rotationMultiplier +
            data.movement.vertical.EvaluateRotation(movement.y) * data.movement.vertical.rotationMultiplier;

        locationSpring.Apply(data.spring);
        rotationSpring.Apply(data.spring);
        locationSpring.SetTarget(horizontalLocation + verticalLocation);
        rotationSpring.SetTarget(horizontalRotation + verticalRotation);
    }

    public Vector3 TickPosition()
    {
        ComputeIfNeeded();
        return locationSpring.Evaluate(Time.deltaTime);
    }

    public Vector3 TickRotation()
    {
        ComputeIfNeeded();
        return rotationSpring.Evaluate(Time.deltaTime);
    }

    // Llamado por WeaponSwitcher al cambiar de arma.
    public void SetWeapon(Weapon newWeapon)
    {
        weapon = newWeapon;
    }
}