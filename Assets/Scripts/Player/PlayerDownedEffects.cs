using UnityEngine;
using Unity.Netcode;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlayerDownedEffects : NetworkBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform cameraTransform;

    [Header("Knockdown")]
    [SerializeField] private string knockdownStateName = "Knockdown";

    [Header("Cámara Downed")]
    [SerializeField] private float downedCameraOffsetY = -1.20f;
    [SerializeField] private float downedCameraTransitionSpeed = 8f;

    [Header("Filtro blanco y negro")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private float normalSaturation = 0f;
    [SerializeField] private float downedSaturation = -100f;

    private ColorAdjustments colorAdjustments;

    private Vector3 normalCameraLocalPosition;

    private bool isDowned;
    private bool knockdownStarted;

    private float knockdownStartTime;

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth =
                GetComponent<PlayerHealth>();
        }

        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>();
        }

        if (cameraTransform == null)
        {
            Camera playerCamera =
                GetComponentInChildren<Camera>(
                    true
                );

            if (playerCamera != null)
            {
                cameraTransform =
                    playerCamera.transform;
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        if (playerHealth == null)
        {
            Debug.LogError(
                "[PlayerDownedEffects] " +
                "No se encontró PlayerHealth."
            );

            return;
        }

        if (animator == null)
        {
            Debug.LogError(
                "[PlayerDownedEffects] " +
                "No se encontró Animator."
            );
        }

        if (cameraTransform != null)
        {
            normalCameraLocalPosition =
                cameraTransform.localPosition;
        }
        else
        {
            Debug.LogError(
                "[PlayerDownedEffects] " +
                "No se encontró Camera."
            );
        }

        if (postProcessVolume == null)
        {
            postProcessVolume =
                FindFirstObjectByType<Volume>();
        }

        if (IsOwner &&
            postProcessVolume != null)
        {
            if (
                postProcessVolume.profile != null &&
                postProcessVolume.profile.TryGet(
                    out colorAdjustments
                )
            )
            {
                colorAdjustments.saturation.value =
                    normalSaturation;
            }
            else
            {
                Debug.LogError(
                    "[PlayerDownedEffects] " +
                    "El Volume no contiene " +
                    "Color Adjustments."
                );
            }
        }

        playerHealth.State.OnValueChanged +=
            OnStateChanged;

        ApplyState(
            playerHealth.State.Value
        );

        Debug.Log(
            "[PlayerDownedEffects] Inicializado en " +
            gameObject.name +
            " | Estado = " +
            playerHealth.State.Value
        );
    }

    public override void OnNetworkDespawn()
    {
        if (playerHealth != null)
        {
            playerHealth.State.OnValueChanged -=
                OnStateChanged;
        }
    }

    private void OnStateChanged(
        PlayerHealth.PlayerState previousState,
        PlayerHealth.PlayerState newState)
    {
        Debug.Log(
            "[PlayerDownedEffects] " +
            gameObject.name +
            " | " +
            previousState +
            " -> " +
            newState
        );

        ApplyState(newState);
    }

    private void ApplyState(
        PlayerHealth.PlayerState state)
    {
        isDowned =
            state == PlayerHealth.PlayerState.Downed;

        if (isDowned)
        {
            StartDowned();
        }
        else
        {
            StopDowned();
        }
    }

    private void StartDowned()
    {
        knockdownStarted = true;
        knockdownStartTime =
            Time.time;

        if (animator != null)
        {
            int stateHash =
                Animator.StringToHash(
                    knockdownStateName
                );

            Debug.Log(
                "[PlayerDownedEffects] " +
                "Iniciando Knockdown en " +
                animator.gameObject.name
            );

            animator.ResetTrigger(
                "Knockdown"
            );

            animator.Play(
                stateHash,
                0,
                0f
            );
        }

        if (
            IsOwner &&
            colorAdjustments != null
        )
        {
            colorAdjustments.saturation.value =
                downedSaturation;
        }
    }

    private void StopDowned()
    {
        isDowned = false;
        knockdownStarted = false;

        if (
            IsOwner &&
            colorAdjustments != null
        )
        {
            colorAdjustments.saturation.value =
                normalSaturation;
        }

        if (cameraTransform != null)
        {
            cameraTransform.localPosition =
                normalCameraLocalPosition;
        }
    }

    private void LateUpdate()
    {
        if (!isDowned)
            return;

        HandleDownedCamera();
        KeepKnockdownAnimation();
    }

    private void HandleDownedCamera()
    {
        if (!IsOwner)
            return;

        if (cameraTransform == null)
            return;

        Vector3 targetPosition =
            normalCameraLocalPosition;

        // Siempre baja respecto de la posición
        // normal, nunca sube.
        targetPosition.y =
            normalCameraLocalPosition.y +
            downedCameraOffsetY;

        cameraTransform.localPosition =
            Vector3.Lerp(
                cameraTransform.localPosition,
                targetPosition,
                downedCameraTransitionSpeed *
                Time.deltaTime
            );
    }

    private void KeepKnockdownAnimation()
    {
        if (!knockdownStarted)
            return;

        if (animator == null)
            return;

        int stateHash =
            Animator.StringToHash(
                knockdownStateName
            );

        AnimatorStateInfo stateInfo =
            animator.GetCurrentAnimatorStateInfo(0);

        // Si otro sistema del personaje cambió
        // el estado, volvemos a Knockdown.
        if (stateInfo.fullPathHash !=
            Animator.StringToHash(
                "Base Layer." +
                knockdownStateName
            ))
        {
            float elapsed =
                Time.time -
                knockdownStartTime;

            float normalizedTime =
                stateInfo.length > 0f
                    ? elapsed / stateInfo.length
                    : 0f;

            animator.Play(
                stateHash,
                0,
                normalizedTime
            );
        }
    }
}