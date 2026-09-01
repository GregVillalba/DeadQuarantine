using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movimiento")]
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
    private PlayerHealth playerHealth;

    private Vector2 moveInput;
    private float velocityY;
    private bool isExhausted;

    private float standingHeight;
    private Vector3 standingCenter;
    private Vector3 crouchCenter;

    private void Awake()
    {
        characterController =
            GetComponent<CharacterController>();

        controls =
            new PlayerControls();

        playerHealth =
            GetComponent<PlayerHealth>();

        CurrentStamina =
            maxStamina;

        standingHeight =
            characterController.height;

        standingCenter =
            characterController.center;

        crouchCenter =
            new Vector3(
                standingCenter.x,
                crouchHeight / 2f,
                standingCenter.z
            );
    }

    private void OnEnable()
    {
        if (IsSpawned && !IsOwner)
            return;

        controls.Player.Enable();

        controls.Player.Move.performed +=
            OnMove;

        controls.Player.Move.canceled +=
            OnMove;

        controls.Player.Jump.performed +=
            OnJump;
    }

    private void OnDisable()
    {
        if (controls == null)
            return;

        controls.Player.Move.performed -=
            OnMove;

        controls.Player.Move.canceled -=
            OnMove;

        controls.Player.Jump.performed -=
            OnJump;

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

    private void OnMove(
        InputAction.CallbackContext context)
    {
        moveInput =
            context.ReadValue<Vector2>();
    }

    private void OnJump(
        InputAction.CallbackContext context)
    {
        if (!IsOwner)
            return;

        if (playerHealth != null &&
            !playerHealth.IsAlive)
        {
            return;
        }

        if (characterController.isGrounded &&
            !IsCrouching)
        {
            velocityY =
                jumpForce;
        }
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        // Mientras está abatido no procesa
        // movimiento ni crouch.
        if (playerHealth != null &&
            playerHealth.IsDowned)
        {
            HandleDowned();

            return;
        }

        HandleCrouch();
        HandleStamina();
        ApplyGravity();

        float currentSpeed =
            moveSpeed;

        if (IsCrouching)
        {
            currentSpeed =
                crouchSpeed;
        }
        else if (IsSprinting)
        {
            currentSpeed =
                sprintSpeed;
        }

        Vector3 horizontalMovement =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        Vector3 fullMovement =
            horizontalMovement * currentSpeed +
            Vector3.up * velocityY;

        characterController.Move(
            fullMovement *
            Time.deltaTime
        );
    }

    private void HandleDowned()
    {
        // Bloquear movimiento.
        moveInput =
            Vector2.zero;

        IsSprinting =
            false;

        IsCrouching =
            false;

        isExhausted =
            false;

        // Evitar que quede saltando.
        velocityY =
            groundedVelocity;

        // Mantener el CharacterController
        // en su configuración normal.
        characterController.height =
            Mathf.Lerp(
                characterController.height,
                standingHeight,
                crouchTransitionSpeed *
                Time.deltaTime
            );

        characterController.center =
            Vector3.Lerp(
                characterController.center,
                standingCenter,
                crouchTransitionSpeed *
                Time.deltaTime
            );

        // No tocamos la cámara acá.
        // PlayerDownedEffects se ocupa de ella.
    }

    private void HandleCrouch()
    {
        if (!IsOwner)
            return;

        if (playerHealth != null &&
            !playerHealth.IsAlive)
        {
            return;
        }

        bool wantsToCrouch =
            controls.Player.Crouch.IsPressed();

        if (wantsToCrouch &&
            !IsCrouching)
        {
            IsCrouching =
                true;
        }
        else if (
            !wantsToCrouch &&
            IsCrouching &&
            CanStandUp()
        )
        {
            IsCrouching =
                false;
        }

        float targetHeight =
            IsCrouching
                ? crouchHeight
                : standingHeight;

        Vector3 targetCenter =
            IsCrouching
                ? crouchCenter
                : standingCenter;

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
    }

    private bool CanStandUp()
    {
        float checkDistance =
            standingHeight -
            crouchHeight;

        Vector3 origin =
            transform.position +
            Vector3.up *
            crouchHeight;

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

        if (playerHealth != null &&
            !playerHealth.IsAlive)
        {
            IsSprinting =
                false;

            return;
        }

        bool wantsToSprint =
            controls.Player.Sprint.IsPressed() &&
            moveInput.magnitude > 0.1f &&
            !isExhausted &&
            !IsCrouching;

        if (wantsToSprint &&
            CurrentStamina > 0f)
        {
            IsSprinting =
                true;

            CurrentStamina -=
                staminaDrainRate *
                Time.deltaTime;

            if (CurrentStamina <= 0f)
            {
                CurrentStamina =
                    0f;

                isExhausted =
                    true;

                IsSprinting =
                    false;
            }
        }
        else
        {
            IsSprinting =
                false;

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
                isExhausted =
                    false;
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
            velocityY =
                groundedVelocity;
        }
        else
        {
            velocityY +=
                gravity *
                Time.deltaTime;
        }
    }

    public void ResetMovementState()
    {
        if (!IsOwner)
            return;

        moveInput =
            Vector2.zero;

        IsSprinting =
            false;

        IsCrouching =
            false;

        isExhausted =
            false;

        CurrentStamina =
            maxStamina;

        velocityY =
            groundedVelocity;

        characterController.height =
            standingHeight;

        characterController.center =
            standingCenter;
    }
}