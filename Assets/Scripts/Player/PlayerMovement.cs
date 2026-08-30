using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float groundedVelocity = -2f;

    [Header("Estamina")]
    [SerializeField] private float maxStamina = 5f;
    [SerializeField] private float staminaDrainRate = 1.5f;
    [SerializeField] private float staminaRegenRate = 1f;
    [SerializeField] private float exhaustedRecoverThreshold = 0.25f;

    [Header("Crouch")]
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float crouchTransitionSpeed = 8f;

    public float CurrentStamina { get; private set; }
    public float MaxStamina => maxStamina;
    public bool IsSprinting { get; private set; }
    public bool IsCrouching { get; private set; }

    private CharacterController characterController;
    private PlayerControls controls;
    private Vector2 moveInput;
    private float velocityY;
    private bool isExhausted;

    private float standingHeight;
    private Vector3 standingCenter;
    private Vector3 standingCameraPosition;
    private Vector3 crouchCenter;
    private Vector3 crouchCameraPosition;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        controls = new PlayerControls();
        CurrentStamina = maxStamina;

        standingHeight = characterController.height;
        standingCenter = characterController.center;
        standingCameraPosition = cameraTransform.localPosition;

        crouchCenter = new Vector3(
            standingCenter.x,
            crouchHeight / 2f,
            standingCenter.z
        );

        float heightDifference =
            standingHeight - crouchHeight;

        crouchCameraPosition =
            standingCameraPosition -
            new Vector3(0f, heightDifference, 0f);
    }

    private void Start()
    {
        if (IsSpawned && !IsOwner)
            return;
    }

    private void OnEnable()
    {
        // El Player remoto NO procesa input.
        if (IsSpawned && !IsOwner)
            return;

        controls.Player.Enable();

        controls.Player.Move.performed += OnMove;
        controls.Player.Move.canceled += OnMove;
        controls.Player.Jump.performed += OnJump;
    }

    private void OnDisable()
    {
        if (controls == null)
            return;

        controls.Player.Move.performed -= OnMove;
        controls.Player.Move.canceled -= OnMove;
        controls.Player.Jump.performed -= OnJump;

        controls.Player.Disable();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        controls.Player.Enable();

        Debug.Log(
            "[PlayerMovement] Player local inicializado. ClientId: " +
            OwnerClientId
        );
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();

        Debug.Log(
            "[MOVEMENT] " +
            gameObject.name +
            " input = " +
            moveInput
        );
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (!IsOwner)
            return;

        if (
            characterController.isGrounded &&
            !IsCrouching
        )
        {
            velocityY = jumpForce;
        }
    }

    private void Update()
    {
        HandleCrouch();
        HandleStamina();
        ApplyGravity();

        float currentSpeed = moveSpeed;

        if (IsCrouching)
            currentSpeed = crouchSpeed;
        else if (IsSprinting)
            currentSpeed = sprintSpeed;

        Vector3 horizontalMovement =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        Vector3 fullMovement =
            horizontalMovement * currentSpeed +
            Vector3.up * velocityY;

        Vector3 beforePosition = transform.position;

        CollisionFlags flags =
            characterController.Move(
                fullMovement * Time.deltaTime
            );

        Vector3 afterPosition = transform.position;

        if (moveInput != Vector2.zero)
        {
            Debug.Log(
                "[MOVEMENT DEBUG] " +
                gameObject.name +
                " | MoveInput=" + moveInput +
                " | Speed=" + currentSpeed +
                " | CC Enabled=" + characterController.enabled +
                " | Before=" + beforePosition +
                " | After=" + afterPosition +
                " | CollisionFlags=" + flags
            );
        }
    }

    private void HandleCrouch()
    {
        if (!IsOwner)
            return;

        bool wantsToCrouch =
            controls.Player.Crouch.IsPressed();

        if (wantsToCrouch && !IsCrouching)
        {
            IsCrouching = true;
        }
        else if (
            !wantsToCrouch &&
            IsCrouching &&
            CanStandUp()
        )
        {
            IsCrouching = false;
        }

        float targetHeight =
            IsCrouching
                ? crouchHeight
                : standingHeight;

        Vector3 targetCenter =
            IsCrouching
                ? crouchCenter
                : standingCenter;

        Vector3 targetCameraPosition =
            IsCrouching
                ? crouchCameraPosition
                : standingCameraPosition;

        characterController.height =
            Mathf.Lerp(
                characterController.height,
                targetHeight,
                crouchTransitionSpeed *
                Time.deltaTime
            );

        characterController.center =
            Vector3.Lerp(
                characterController.center,
                targetCenter,
                crouchTransitionSpeed *
                Time.deltaTime
            );

        cameraTransform.localPosition =
            Vector3.Lerp(
                cameraTransform.localPosition,
                targetCameraPosition,
                crouchTransitionSpeed *
                Time.deltaTime
            );
    }

    private bool CanStandUp()
    {
        float checkDistance =
            standingHeight - crouchHeight;

        Vector3 origin =
            transform.position +
            Vector3.up * crouchHeight;

        return !Physics.Raycast(
            origin,
            Vector3.up,
            checkDistance
        );
    }

    private void HandleStamina()
    {
        if (!IsOwner)
            return;

        bool wantsToSprint =
            controls.Player.Sprint.IsPressed() &&
            moveInput.magnitude > 0.1f &&
            !isExhausted &&
            !IsCrouching;

        if (
            wantsToSprint &&
            CurrentStamina > 0f
        )
        {
            IsSprinting = true;

            CurrentStamina -=
                staminaDrainRate *
                Time.deltaTime;

            if (CurrentStamina <= 0f)
            {
                CurrentStamina = 0f;
                isExhausted = true;
                IsSprinting = false;
            }
        }
        else
        {
            IsSprinting = false;

            CurrentStamina +=
                staminaRegenRate *
                Time.deltaTime;

            CurrentStamina =
                Mathf.Clamp(
                    CurrentStamina,
                    0f,
                    maxStamina
                );

            if (
                isExhausted &&
                CurrentStamina >=
                maxStamina *
                exhaustedRecoverThreshold
            )
            {
                isExhausted = false;
            }
        }
    }

    private void ApplyGravity()
    {
        if (!IsOwner)
            return;

        if (
            characterController.isGrounded &&
            velocityY < 0f
        )
        {
            velocityY = groundedVelocity;
        }
        else
        {
            velocityY +=
                gravity *
                Time.deltaTime;
        }
    }
}