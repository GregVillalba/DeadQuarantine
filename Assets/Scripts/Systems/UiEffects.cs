using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Efectos del HUD: sway con el mouse, inclinación (tilt) y movimiento al saltar / aterrizar.
/// Adaptado del UIEffects de cowsins a este proyecto: ya no depende de cowsins, de
/// InputManager, de IPlayerControlProvider ni de eventos OnJump / OnLand.
///
/// CÓMO USARLO
///  1. Creá un objeto hijo del HUD_Canvas (ej. "HUD_Effects") con un RectTransform que ocupe todo
///     el canvas (anchors en stretch, offsets en 0) y poné ahí los elementos que querés que se muevan
///     (vida, estamina, munición...). Lo que NO querés que se mueva (hit marker, crosshair) dejalo afuera.
///  2. Agregá este script a ese objeto. Busca solo PlayerMovement y CharacterController en el jugador.
///
/// El sway usa el delta del mouse (como WeaponSway). Salto y aterrizaje se detectan con AirTracker,
/// igual que el movimiento del arma y la cámara.
/// </summary>
public class UIEffects : MonoBehaviour
{
    [Header("Referencias (si están vacías se buscan solas)")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private CharacterController characterController;

    [Header("Sway (posición)")]
    [SerializeField] private float amount = 0.02f;
    [SerializeField] private float maxAmount = 0.06f;
    [SerializeField] private float smoothAmount = 6f;

    [Header("Tilt (inclinación)")]
    [SerializeField] private float tiltAmount = 4f;
    [SerializeField] private float maxTiltAmount = 5f;
    [SerializeField] private float smoothTiltAmount = 12f;

    [Header("Input")]
    [Tooltip("Multiplica el delta del mouse (cowsins lo divide por 10 = 0.1).")]
    [SerializeField] private float mouseScale = 0.1f;

    [Header("Salto / aterrizaje")]
    [SerializeField] private AnimationCurve jumpMotion = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f));
    [SerializeField] private AnimationCurve groundedMotion = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.25f, -1f), new Keyframe(0.6f, 0.25f), new Keyframe(1f, 0f));
    [Tooltip("Cuánto se mueve el HUD en vertical (en unidades del canvas, ver Overlay Units Scale).")]
    [SerializeField] private float distance = 0.03f;
    [Tooltip("Cuánto se inclina el HUD al saltar / aterrizar, en grados.")]
    [SerializeField] private float rotationAmount = 2f;
    [SerializeField, Min(1f)] private float evaluationSpeed = 3f;
    [Tooltip("Aterrizar de una caída más lenta que esto (m/s) no mueve el HUD (bajar un escalón).")]
    [SerializeField] private float landMinFallSpeed = 1.5f;

    [Header("Canvas Screen Space - Overlay")]
    [Tooltip("En Overlay las unidades son píxeles, y 0.02 se vería quieto. Si el canvas es Overlay, el sway y el salto " +
             "se multiplican por esto (1000 = 0.02 -> 20 px). En World Space / Screen Space - Camera no se usa.")]
    [SerializeField] private float overlayUnitsScale = 1000f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private float inputX;
    private float inputY;

    private Vector3 swayPositionOffset;
    private Quaternion swayRotationOffset = Quaternion.identity;

    private Vector3 jumpPositionOffset;
    private Quaternion jumpRotationOffset = Quaternion.identity;

    private Coroutine jumpMotionCoroutine;

    private readonly AirTracker air = new AirTracker();
    private bool jumped;
    private float unitsScale = 1f;

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();

        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();

        Canvas canvas = GetComponentInParent<Canvas>();
        Canvas root = canvas != null ? canvas.rootCanvas : null;

        unitsScale = (root != null && root.renderMode == RenderMode.ScreenSpaceOverlay) ? overlayUnitsScale : 1f;
    }

    private void Start()
    {
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;
    }

    private void OnDisable()
    {
        // Si se apaga mientras se mueve, no queda torcido.
        jumpMotionCoroutine = null;
        jumpPositionOffset = Vector3.zero;
        jumpRotationOffset = Quaternion.identity;
    }

    private void Update()
    {
        DetectJumpAndLand();

        // Igual que el original: el sway solo se actualiza si el jugador es controlable.
        if (IsControllable())
        {
            CalculateSway();
            CalculateSwayOffsets();
        }

        ApplyCombinedTransform();
    }

    private bool IsControllable()
    {
        if (Time.timeScale == 0f)
            return false;

        return playerMovement == null || !playerMovement.MovementLocked;
    }

    private void DetectJumpAndLand()
    {
        air.Tick(characterController, Mathf.Min(Time.deltaTime, 0.05f));

        if (air.JustTookOff)
        {
            jumped = air.VerticalSpeed > 0.5f;

            if (jumped)
                StartMotion(jumpMotion);
        }

        if (air.JustLanded && air.LandImpactSpeed >= landMinFallSpeed)
            StartMotion(groundedMotion);
    }

    private void CalculateSway()
    {
        Vector2 delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        Vector2 stick = Gamepad.current != null ? Gamepad.current.rightStick.ReadValue() : Vector2.zero;

        // Igual que el original: -mouse / 10 - 2 * stick. El HUD va al revés de la mirada.
        inputX = -delta.x * mouseScale - 2f * stick.x;
        inputY = -delta.y * mouseScale - 2f * stick.y;
    }

    private void CalculateSwayOffsets()
    {
        float moveX = Mathf.Clamp(inputX * amount, -maxAmount, maxAmount);
        // Igual que el original: Y se limita a +-1 (X usa maxAmount).
        float moveY = Mathf.Clamp(inputY * amount, -1f, 1f);
        Vector3 targetPos = new Vector3(moveX, moveY, 0f) * unitsScale;

        swayPositionOffset = Vector3.Lerp(swayPositionOffset, targetPos, Time.deltaTime * smoothAmount);

        float tiltX = Mathf.Clamp(inputX * tiltAmount, -maxTiltAmount, maxTiltAmount);
        Quaternion targetRot = Quaternion.Euler(0f, 0f, tiltX);

        swayRotationOffset = Quaternion.Slerp(swayRotationOffset, targetRot, Time.deltaTime * smoothTiltAmount);
    }

    private void ApplyCombinedTransform()
    {
        transform.localPosition = initialPosition + swayPositionOffset + jumpPositionOffset;
        transform.localRotation = initialRotation * swayRotationOffset * jumpRotationOffset;
    }

    private void StartMotion(AnimationCurve curve)
    {
        if (!isActiveAndEnabled)
            return;

        if (jumpMotionCoroutine != null)
            StopCoroutine(jumpMotionCoroutine);

        jumpMotionCoroutine = StartCoroutine(ApplyMotion(curve));
    }

    private IEnumerator ApplyMotion(AnimationCurve motionCurve)
    {
        float motion = 0f;

        while (motion < 1f)
        {
            motion += Time.deltaTime * evaluationSpeed;
            float evaluated = motionCurve.Evaluate(motion);

            jumpPositionOffset = new Vector3(0f, evaluated * distance * unitsScale, 0f);
            jumpRotationOffset = Quaternion.Euler(evaluated * rotationAmount, 0f, 0f);

            yield return null;
        }

        // Al terminar vuelve a cero para no quedar trabado.
        jumpPositionOffset = Vector3.zero;
        jumpRotationOffset = Quaternion.identity;
        jumpMotionCoroutine = null;
    }
}