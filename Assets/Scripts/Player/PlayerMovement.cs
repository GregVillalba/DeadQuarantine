using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float downedMoveSpeed = 2f;
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

    [Header("Parkour")]
    [SerializeField] private LayerMask parkourLayer;
    [SerializeField] private float parkourCheckDistance = 2.2f;
    [SerializeField] private float parkourCheckRadius = 0.35f;
    [SerializeField] private float parkourMinHeight = 0.4f;
    [SerializeField] private float parkourMaxHeight = 1.4f;
    [SerializeField] private float parkourLandingDistance = 0.8f;
    [SerializeField] private float parkourDuration = 0.45f;
    [SerializeField] private float parkourArcHeight = 0.7f;
    [SerializeField] private float parkourLowerCheckHeight = 0.55f;
    [SerializeField] private float parkourUpperCheckHeight = 1.8f;

    [Header("Ventana")]
    [SerializeField] private float windowTraversalDistance = 1.3f;

    /*
     * Altura a la que se mueve el jugador durante el Window Vault.
     *
     * 0.45 - 0.65 suele funcionar bien dependiendo del modelo
     * y de la altura del hueco.
     */
    [SerializeField] private float windowHeightOffset = 0.55f;

    /*
     * Pequeño arco vertical durante el paso.
     * No debe ser muy grande porque el jugador ya estará elevado.
     */
    [SerializeField] private float windowArcHeight = 0.08f;

    /*
     * Distancia adicional después del trigger para asegurarnos
     * de haber atravesado completamente la pared.
     */
    [SerializeField] private float windowExitDistance = 1.1f;

    [Header("Modelo del personaje")]
    [SerializeField] private Transform characterModel;

    /*
     * Offset opcional del modelo durante Window Vault.
     *
     * Si polygon_fps ya se mueve correctamente con el Player,
     * dejalo en 0.
     *
     * Si visualmente queda demasiado bajo, probá 0.1 - 0.2.
     */
    [SerializeField] private float windowModelVerticalOffset = 0f;

    [Header("Animación Parkour")]
    [SerializeField] private Animator parkourAnimator;
    [SerializeField] private string vaultTriggerName = "Vault";
    [SerializeField] private string windowVaultTriggerName = "WindowVault";

    [Header("Cámara Parkour")]
    [SerializeField] private float windowCameraDown = 0.10f;
    [SerializeField] private float windowCameraForward = 0.10f;
    [SerializeField] private float cameraTilt = -8f;
    [SerializeField] private float cameraRoll = 2f;

    [SerializeField] private float cameraVerticalOffset = 0.10f;
    [SerializeField] private float cameraForwardOffset = 0.08f;

    [SerializeField] private float cameraEffectSpeed = 12f;
    [SerializeField] private float cameraReturnDuration = 0.20f;

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

    private Vector3 standingCameraPosition;
    private Quaternion standingCameraRotation;

    private Vector3 crouchCenter;
    private Vector3 crouchCameraPosition;

    private bool isParkouring;
    private bool forceCrouchForParkour;

    private Coroutine parkourCoroutine;
    private Coroutine cameraReturnCoroutine;

    private Vector3 originalCharacterModelLocalPosition;

    // =========================================================
    // AWAKE
    // =========================================================

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

        if (cameraTransform != null)
        {
            standingCameraPosition =
                cameraTransform.localPosition;

            standingCameraRotation =
                cameraTransform.localRotation;
        }
        else
        {
            standingCameraPosition =
                Vector3.zero;

            standingCameraRotation =
                Quaternion.identity;
        }

        crouchCenter =
            new Vector3(
                standingCenter.x,
                crouchHeight / 2f,
                standingCenter.z
            );

        float heightDifference =
            standingHeight -
            crouchHeight;

        crouchCameraPosition =
            standingCameraPosition -
            new Vector3(
                0f,
                heightDifference,
                0f
            );

        if (characterModel != null)
        {
            originalCharacterModelLocalPosition =
                characterModel.localPosition;
        }
    }

    // =========================================================
    // ENABLE / DISABLE
    // =========================================================

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

    // =========================================================
    // NETWORK
    // =========================================================

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        controls.Player.Enable();
    }

    // =========================================================
    // INPUT
    // =========================================================

    private void OnMove(
        InputAction.CallbackContext context
    )
    {
        if (!IsOwner)
            return;

        moveInput =
            context.ReadValue<Vector2>();
    }

    private void OnJump(
        InputAction.CallbackContext context
    )
    {
        if (!IsOwner)
            return;

        if (isParkouring)
            return;

        if (
            IsCrouching &&
            !forceCrouchForParkour
        )
        {
            return;
        }

        /*
         * IMPORTANTE:
         *
         * La ventana solamente se comprueba cuando se pulsa Space.
         * Nunca se inicia automáticamente al acercarse.
         */

        if (TryStartWindowParkour())
            return;

        if (TryStartNormalParkour())
            return;

        if (characterController.isGrounded)
        {
            velocityY =
                jumpForce;
        }
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (!IsOwner)
            return;

        HandleStamina();

        if (isParkouring)
        {
            HandleCrouch();
            return;
        }

        HandleCrouch();
        ApplyGravity();

        float currentSpeed =
            moveSpeed;

        if (playerHealth != null &&
            playerHealth.IsDowned)
        {
            currentSpeed =
                downedMoveSpeed;
        }
        else if (IsCrouching)
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
            horizontalMovement *
            currentSpeed +
            Vector3.up *
            velocityY;

        characterController.Move(
            fullMovement *
            Time.deltaTime
        );
    }

    // =========================================================
    // WINDOW PARKOUR
    // =========================================================

    private bool TryStartWindowParkour()
    {
        if (!characterController.isGrounded)
            return false;

        if (moveInput.y <= 0.1f)
            return false;

        /*
         * Hacemos varias comprobaciones alrededor del cuerpo
         * en vez de depender de un único punto.
         *
         * Esto hace que el trigger de la ventana se detecte
         * de forma mucho más consistente.
         */

        Vector3 forward =
            transform.forward;

        Vector3[] origins =
        {
            transform.position + Vector3.up * 0.45f,
            transform.position + Vector3.up * 0.8f,
            transform.position + Vector3.up * 1.1f,
            transform.position + Vector3.up * 1.4f
        };

        RaycastHit closestWindowHit =
            default;

        bool foundWindow =
            false;

        float closestDistance =
            float.MaxValue;

        foreach (Vector3 origin in origins)
        {
            Debug.DrawRay(
                origin,
                forward * parkourCheckDistance,
                Color.cyan,
                1f
            );

            RaycastHit[] hits =
                Physics.SphereCastAll(
                    origin,
                    parkourCheckRadius,
                    forward,
                    parkourCheckDistance,
                    parkourLayer,
                    QueryTriggerInteraction.Collide
                );

            if (
                hits == null ||
                hits.Length == 0
            )
            {
                continue;
            }

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                if (
                    hit.collider.transform.root ==
                    transform.root
                )
                {
                    continue;
                }

                /*
                 * La ventana es el collider separado que está
                 * configurado como Trigger.
                 */
                if (!hit.collider.isTrigger)
                    continue;

                if (
                    hit.distance <
                    closestDistance
                )
                {
                    closestDistance =
                        hit.distance;

                    closestWindowHit =
                        hit;

                    foundWindow =
                        true;
                }
            }
        }

        if (!foundWindow)
            return false;

        Debug.Log(
            "[PARKOUR] Window trigger detectado: " +
            closestWindowHit.collider.name
        );

        /*
         * -----------------------------------------------------
         * POSICIÓN DE SALIDA
         * -----------------------------------------------------
         *
         * En lugar de usar bounds.center, usamos el punto
         * real donde el SphereCast encontró la ventana.
         *
         * Después avanzamos una distancia adicional para
         * asegurarnos de pasar completamente la pared.
         */

        Vector3 hitPoint =
            closestWindowHit.point;

        Vector3 horizontalHitPoint =
            new Vector3(
                hitPoint.x,
                transform.position.y,
                hitPoint.z
            );

        Vector3 landingPosition =
            horizontalHitPoint +
            forward *
            windowExitDistance;

        /*
         * Conservamos la orientación horizontal del jugador.
         */

        landingPosition.y =
            transform.position.y;

        /*
         * La elevación se hará durante el tránsito.
         * Por eso NO ponemos aquí directamente el offset vertical.
         */

        if (parkourCoroutine != null)
        {
            StopCoroutine(
                parkourCoroutine
            );
        }

        parkourCoroutine =
            StartCoroutine(
                PerformWindowParkour(
                    landingPosition
                )
            );

        return true;
    }

    private IEnumerator PerformWindowParkour(
        Vector3 landingPosition
    )
    {
        isParkouring =
            true;

        IsSprinting =
            false;

        velocityY =
            0f;

        /*
         * =====================================================
         * CROUCH FORZADO
         * =====================================================
         */

        forceCrouchForParkour =
            true;

        IsCrouching =
            true;

        characterController.height =
            crouchHeight;

        characterController.center =
            crouchCenter;

        /*
         * =====================================================
         * ANIMACIÓN
         * =====================================================
         */

        PlayParkourAnimation(true);

        /*
         * =====================================================
         * CÁMARA
         * =====================================================
         */

        if (cameraReturnCoroutine != null)
        {
            StopCoroutine(
                cameraReturnCoroutine
            );

            cameraReturnCoroutine =
                null;
        }

        Vector3 originalCameraPosition =
            cameraTransform != null
                ? cameraTransform.localPosition
                : Vector3.zero;

        Quaternion originalCameraRotation =
            cameraTransform != null
                ? cameraTransform.localRotation
                : Quaternion.identity;

        /*
         * =====================================================
         * MODELO
         * =====================================================
         */

        if (characterModel != null)
        {
            originalCharacterModelLocalPosition =
                characterModel.localPosition;
        }

        /*
         * =====================================================
         * POSICIONES
         * =====================================================
         */

        Vector3 originalPosition =
            transform.position;

        /*
         * El personaje comienza a subir hasta la altura de la
         * ventana antes de cruzarla.
         */

        Vector3 elevatedStartPosition =
            originalPosition +
            Vector3.up *
            windowHeightOffset;

        Vector3 elevatedLandingPosition =
            landingPosition +
            Vector3.up *
            windowHeightOffset;

        /*
         * =====================================================
         * DESACTIVAR CHARACTER CONTROLLER
         * =====================================================
         *
         * Esto permite atravesar directamente la abertura sin
         * que los colliders de la pared bloqueen el movimiento.
         */

        characterController.enabled =
            false;

        /*
         * =====================================================
         * FASE 1: SUBIR Y ENTRAR
         * =====================================================
         */

        float elapsed =
            0f;

        float enterDuration =
            parkourDuration *
            0.25f;

        while (
            elapsed <
            enterDuration
        )
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    enterDuration
                );

            float smoothT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            transform.position =
                Vector3.Lerp(
                    originalPosition,
                    elevatedStartPosition,
                    smoothT
                );

            UpdateWindowCamera(
                smoothT,
                0f
            );

            UpdateWindowModel(
                smoothT
            );

            yield return null;
        }

        /*
         * =====================================================
         * FASE 2: ATRAVESAR LA VENTANA
         * =====================================================
         */

        elapsed =
            0f;

        float traversalDuration =
            parkourDuration *
            0.55f;

        while (
            elapsed <
            traversalDuration
        )
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    traversalDuration
                );

            float smoothT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            Vector3 horizontalPosition =
                Vector3.Lerp(
                    elevatedStartPosition,
                    elevatedLandingPosition,
                    smoothT
                );

            float arc =
                Mathf.Sin(
                    smoothT *
                    Mathf.PI
                );

            float verticalArc =
                arc *
                windowArcHeight;

            transform.position =
                new Vector3(
                    horizontalPosition.x,
                    horizontalPosition.y +
                    verticalArc,
                    horizontalPosition.z
                );

            UpdateWindowCamera(
                smoothT,
                1f
            );

            UpdateWindowModel(
                smoothT
            );

            yield return null;
        }

        /*
         * =====================================================
         * FASE 3: BAJAR
         * =====================================================
         */

        elapsed =
            0f;

        float exitDuration =
            parkourDuration *
            0.20f;

        Vector3 elevatedEnd =
            elevatedLandingPosition;

        while (
            elapsed <
            exitDuration
        )
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    exitDuration
                );

            float smoothT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            transform.position =
                Vector3.Lerp(
                    elevatedEnd,
                    landingPosition,
                    smoothT
                );

            UpdateWindowCamera(
                1f - smoothT,
                0f
            );

            UpdateWindowModel(
                1f - smoothT
            );

            yield return null;
        }

        /*
         * Posición final exacta.
         */

        transform.position =
            landingPosition;

        /*
         * =====================================================
         * RESTAURAR CHARACTER CONTROLLER
         * =====================================================
         */

        characterController.enabled =
            true;

        /*
         * Mantener crouch unos instantes al salir.
         */

        IsCrouching =
            true;

        yield return new WaitForSeconds(
            0.03f
        );

        /*
         * Levantarse automáticamente.
         */

        forceCrouchForParkour =
            false;

        IsCrouching =
            false;

        characterController.height =
            standingHeight;

        characterController.center =
            standingCenter;

        /*
         * =====================================================
         * RESTAURAR MODELO
         * =====================================================
         */

        RestoreCharacterModel();

        /*
         * =====================================================
         * RESTAURAR CÁMARA
         * =====================================================
         */

        cameraReturnCoroutine =
            StartCoroutine(
                RestoreParkourCamera(
                    originalCameraPosition,
                    originalCameraRotation
                )
            );

        velocityY =
            groundedVelocity;

        isParkouring =
            false;

        parkourCoroutine =
            null;

        Debug.Log(
            "[PARKOUR] Window Vault terminado."
        );
    }

    // =========================================================
    // WINDOW MODEL
    // =========================================================

    private void UpdateWindowModel(
        float t
    )
    {
        if (characterModel == null)
            return;

        /*
         * El modelo puede recibir un pequeño offset adicional.
         *
         * No sustituye el movimiento del Player.
         * Solo sirve para ajustar visualmente polygon_fps.
         */

        float effect =
            Mathf.Sin(
                t *
                Mathf.PI
            );

        Vector3 desired =
            originalCharacterModelLocalPosition;

        desired.y +=
            windowModelVerticalOffset *
            effect;

        characterModel.localPosition =
            Vector3.Lerp(
                characterModel.localPosition,
                desired,
                12f *
                Time.deltaTime
            );
    }

    private void RestoreCharacterModel()
    {
        if (characterModel == null)
            return;

        characterModel.localPosition =
            originalCharacterModelLocalPosition;
    }

    // =========================================================
    // WINDOW CAMERA
    // =========================================================

    private void UpdateWindowCamera(
        float t,
        float traversalEffect
    )
    {
        if (cameraTransform == null)
            return;

        float effect;

        if (traversalEffect > 0f)
        {
            effect =
                Mathf.Sin(
                    t *
                    Mathf.PI
                );
        }
        else
        {
            effect =
                Mathf.Sin(
                    t *
                    Mathf.PI *
                    0.5f
                );
        }

        Vector3 desiredPosition =
            standingCameraPosition;

        desiredPosition.y -=
            windowCameraDown *
            effect;

        desiredPosition.z +=
            windowCameraForward *
            effect;

        Quaternion desiredRotation =
            standingCameraRotation *
            Quaternion.Euler(
                cameraTilt *
                effect,
                0f,
                cameraRoll *
                effect
            );

        cameraTransform.localPosition =
            Vector3.Lerp(
                cameraTransform.localPosition,
                desiredPosition,
                cameraEffectSpeed *
                Time.deltaTime
            );

        cameraTransform.localRotation =
            Quaternion.Slerp(
                cameraTransform.localRotation,
                desiredRotation,
                cameraEffectSpeed *
                Time.deltaTime
            );
    }

    // =========================================================
    // NORMAL PARKOUR
    // =========================================================

    private bool TryStartNormalParkour()
    {
        if (!characterController.isGrounded)
            return false;

        if (moveInput.y <= 0.1f)
            return false;

        Vector3 feetPosition =
            transform.position;

        Vector3 lowerOrigin =
            feetPosition +
            Vector3.up *
            parkourLowerCheckHeight;

        bool lowerHit =
            Physics.Raycast(
                lowerOrigin,
                transform.forward,
                out RaycastHit lowerHitInfo,
                parkourCheckDistance,
                parkourLayer,
                QueryTriggerInteraction.Ignore
            );

        if (!lowerHit)
            return false;

        if (lowerHitInfo.collider == null)
            return false;

        /*
         * Los triggers son únicamente para detectar ventanas.
         */
        if (lowerHitInfo.collider.isTrigger)
            return false;

        if (
            lowerHitInfo.collider.transform.root ==
            transform.root
        )
        {
            return false;
        }

        Vector3 upperOrigin =
            feetPosition +
            Vector3.up *
            parkourUpperCheckHeight;

        bool upperBlocked =
            Physics.Raycast(
                upperOrigin,
                transform.forward,
                parkourCheckDistance,
                parkourLayer,
                QueryTriggerInteraction.Ignore
            );

        if (upperBlocked)
            return false;

        Bounds bounds =
            lowerHitInfo.collider.bounds;

        float obstacleTop =
            bounds.max.y;

        float obstacleHeight =
            obstacleTop -
            feetPosition.y;

        if (
            obstacleHeight <
            parkourMinHeight
        )
        {
            return false;
        }

        if (
            obstacleHeight >
            parkourMaxHeight
        )
        {
            return false;
        }

        Vector3 landingPosition =
            transform.position +
            transform.forward *
            (
                lowerHitInfo.distance +
                parkourLandingDistance
            );

        Vector3 landingRayOrigin =
            landingPosition +
            Vector3.up *
            2f;

        if (!Physics.Raycast(
            landingRayOrigin,
            Vector3.down,
            out RaycastHit landingHit,
            5f,
            ~0,
            QueryTriggerInteraction.Ignore
        ))
        {
            return false;
        }

        landingPosition =
            landingHit.point;

        if (!IsLandingPositionClear(
            landingPosition,
            null
        ))
        {
            return false;
        }

        if (parkourCoroutine != null)
        {
            StopCoroutine(
                parkourCoroutine
            );
        }

        parkourCoroutine =
            StartCoroutine(
                PerformNormalParkour(
                    landingPosition,
                    obstacleTop
                )
            );

        return true;
    }

    private IEnumerator PerformNormalParkour(
        Vector3 landingPosition,
        float obstacleTop
    )
    {
        isParkouring =
            true;

        IsSprinting =
            false;

        IsCrouching =
            false;

        velocityY =
            0f;

        PlayParkourAnimation(false);

        if (cameraReturnCoroutine != null)
        {
            StopCoroutine(
                cameraReturnCoroutine
            );

            cameraReturnCoroutine =
                null;
        }

        Vector3 originalCameraPosition =
            cameraTransform != null
                ? cameraTransform.localPosition
                : Vector3.zero;

        Quaternion originalCameraRotation =
            cameraTransform != null
                ? cameraTransform.localRotation
                : Quaternion.identity;

        characterController.enabled =
            false;

        Vector3 startPosition =
            transform.position;

        float elapsed =
            0f;

        while (
            elapsed <
            parkourDuration
        )
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    parkourDuration
                );

            float smoothT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            Vector3 horizontalPosition =
                Vector3.Lerp(
                    startPosition,
                    landingPosition,
                    smoothT
                );

            float baseHeight =
                Mathf.Lerp(
                    startPosition.y,
                    landingPosition.y,
                    smoothT
                );

            float arc =
                Mathf.Sin(
                    smoothT *
                    Mathf.PI
                );

            float highestPoint =
                Mathf.Max(
                    obstacleTop +
                    parkourArcHeight,
                    startPosition.y +
                    parkourArcHeight
                );

            float middleBase =
                Mathf.Lerp(
                    startPosition.y,
                    landingPosition.y,
                    0.5f
                );

            float additionalHeight =
                Mathf.Max(
                    0f,
                    highestPoint -
                    middleBase
                );

            float finalY =
                baseHeight +
                arc *
                additionalHeight;

            transform.position =
                new Vector3(
                    horizontalPosition.x,
                    finalY,
                    horizontalPosition.z
                );

            ApplyParkourCameraKick(
                smoothT
            );

            yield return null;
        }

        transform.position =
            landingPosition;

        characterController.enabled =
            true;

        cameraReturnCoroutine =
            StartCoroutine(
                RestoreParkourCamera(
                    originalCameraPosition,
                    originalCameraRotation
                )
            );

        FinishParkour();
    }

    // =========================================================
    // ANIMACIÓN
    // =========================================================

    private void PlayParkourAnimation(
        bool isWindow
    )
    {
        if (parkourAnimator == null)
            return;

        if (
            !string.IsNullOrEmpty(
                vaultTriggerName
            )
        )
        {
            parkourAnimator.ResetTrigger(
                vaultTriggerName
            );
        }

        if (
            !string.IsNullOrEmpty(
                windowVaultTriggerName
            )
        )
        {
            parkourAnimator.ResetTrigger(
                windowVaultTriggerName
            );
        }

        if (isWindow)
        {
            if (
                !string.IsNullOrEmpty(
                    windowVaultTriggerName
                )
            )
            {
                parkourAnimator.SetTrigger(
                    windowVaultTriggerName
                );
            }
        }
        else
        {
            if (
                !string.IsNullOrEmpty(
                    vaultTriggerName
                )
            )
            {
                parkourAnimator.SetTrigger(
                    vaultTriggerName
                );
            }
        }
    }

    // =========================================================
    // CÁMARA NORMAL PARKOUR
    // =========================================================

    private void ApplyParkourCameraKick(
        float t
    )
    {
        if (cameraTransform == null)
            return;

        float kick =
            Mathf.Sin(
                t *
                Mathf.PI
            );

        Vector3 basePosition =
            IsCrouching
                ? crouchCameraPosition
                : standingCameraPosition;

        Vector3 offset =
            new Vector3(
                0f,
                -cameraVerticalOffset *
                kick,
                cameraForwardOffset *
                kick
            );

        Vector3 desired =
            basePosition +
            offset;

        cameraTransform.localPosition =
            Vector3.Lerp(
                cameraTransform.localPosition,
                desired,
                cameraEffectSpeed *
                Time.deltaTime
            );

        Quaternion desiredRotation =
            standingCameraRotation *
            Quaternion.Euler(
                cameraTilt *
                kick,
                0f,
                cameraRoll *
                kick
            );

        cameraTransform.localRotation =
            Quaternion.Slerp(
                cameraTransform.localRotation,
                desiredRotation,
                cameraEffectSpeed *
                Time.deltaTime
            );
    }

    // =========================================================
    // RESTAURAR CÁMARA
    // =========================================================

    private IEnumerator RestoreParkourCamera(
        Vector3 originalPosition,
        Quaternion originalRotation
    )
    {
        if (cameraTransform == null)
            yield break;

        Vector3 startPosition =
            cameraTransform.localPosition;

        Quaternion startRotation =
            cameraTransform.localRotation;

        float elapsed =
            0f;

        while (
            elapsed <
            cameraReturnDuration
        )
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    cameraReturnDuration
                );

            float smoothT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            cameraTransform.localPosition =
                Vector3.Lerp(
                    startPosition,
                    standingCameraPosition,
                    smoothT
                );

            cameraTransform.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    standingCameraRotation,
                    smoothT
                );

            yield return null;
        }

        cameraTransform.localPosition =
            standingCameraPosition;

        cameraTransform.localRotation =
            standingCameraRotation;

        cameraReturnCoroutine =
            null;
    }

    // =========================================================
    // ZONA DE ATERRIZAJE
    // =========================================================

    private bool IsLandingPositionClear(
        Vector3 position,
        Transform windowRoot
    )
    {
        float radius =
            characterController.radius *
            0.9f;

        float halfHeight =
            standingHeight /
            2f;

        Vector3 center =
            position +
            standingCenter;

        Vector3 bottom =
            center +
            Vector3.down *
            Mathf.Max(
                0f,
                halfHeight -
                radius
            );

        Vector3 top =
            center +
            Vector3.up *
            Mathf.Max(
                0f,
                halfHeight -
                radius
            );

        Collider[] colliders =
            Physics.OverlapCapsule(
                bottom,
                top,
                radius,
                ~0,
                QueryTriggerInteraction.Ignore
            );

        foreach (
            Collider collider
            in colliders
        )
        {
            if (collider == null)
                continue;

            if (
                collider.transform.root ==
                transform.root
            )
            {
                continue;
            }

            if (
                windowRoot != null &&
                collider.transform.IsChildOf(
                    windowRoot
                )
            )
            {
                continue;
            }

            return false;
        }

        return true;
    }

    // =========================================================
    // CROUCH
    // =========================================================

    private void HandleCrouch()
    {
        if (!IsOwner)
            return;

        bool wantsToCrouch;

        if (forceCrouchForParkour)
        {
            wantsToCrouch =
                true;
        }
        else
        {
            wantsToCrouch =
                controls.Player.Crouch.IsPressed();
        }

        if (
            wantsToCrouch &&
            !IsCrouching
        )
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

        if (isParkouring)
        {
            float targetHeightDuringParkour =
                IsCrouching
                    ? crouchHeight
                    : standingHeight;

            Vector3 targetCenterDuringParkour =
                IsCrouching
                    ? crouchCenter
                    : standingCenter;

            if (characterController.enabled)
            {
                characterController.height =
                    Mathf.Lerp(
                        characterController.height,
                        targetHeightDuringParkour,
                        crouchTransitionSpeed *
                        Time.deltaTime
                    );

                characterController.center =
                    Vector3.Lerp(
                        characterController.center,
                        targetCenterDuringParkour,
                        crouchTransitionSpeed *
                        Time.deltaTime
                    );
            }

            return;
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

        if (cameraTransform != null)
        {
            cameraTransform.localPosition =
                Vector3.Lerp(
                    cameraTransform.localPosition,
                    targetCameraPosition,
                    crouchTransitionSpeed *
                    Time.deltaTime
                );
        }
    }

    // =========================================================
    // CAN STAND
    // =========================================================

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

    // =========================================================
    // STAMINA
    // =========================================================

    private void HandleStamina()
    {
        if (!IsOwner)
            return;

        bool wantsToSprint =
            controls.Player.Sprint.IsPressed() &&
            moveInput.magnitude > 0.1f &&
            !isExhausted &&
            !IsCrouching &&
            !isParkouring &&
            (playerHealth == null ||
             !playerHealth.IsDowned);

        if (
            wantsToSprint &&
            CurrentStamina > 0f
        )
        {
            IsSprinting =
                true;

            CurrentStamina -=
                staminaDrainRate *
                Time.deltaTime;

            if (
                CurrentStamina <= 0f
            )
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

    // =========================================================
    // GRAVEDAD
    // =========================================================

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

    // =========================================================
    // FINALIZAR PARKOUR
    // =========================================================

    private void FinishParkour()
    {
        velocityY =
            groundedVelocity;

        isParkouring =
            false;

        parkourCoroutine =
            null;
    }

    // =========================================================
    // RESET
    // =========================================================

    public void ResetMovementState()
    {
        if (parkourCoroutine != null)
        {
            StopCoroutine(
                parkourCoroutine
            );

            parkourCoroutine =
                null;
        }

        if (cameraReturnCoroutine != null)
        {
            StopCoroutine(
                cameraReturnCoroutine
            );

            cameraReturnCoroutine =
                null;
        }

        forceCrouchForParkour =
            false;

        moveInput =
            Vector2.zero;

        velocityY =
            groundedVelocity;

        IsSprinting =
            false;

        IsCrouching =
            false;

        isParkouring =
            false;

        if (characterController != null)
        {
            characterController.enabled =
                true;

            characterController.height =
                standingHeight;

            characterController.center =
                standingCenter;
        }

        RestoreCharacterModel();

        if (cameraTransform != null)
        {
            cameraTransform.localPosition =
                standingCameraPosition;

            cameraTransform.localRotation =
                standingCameraRotation;
        }
    }
}