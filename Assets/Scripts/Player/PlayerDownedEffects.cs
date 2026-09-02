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
    [SerializeField] private float normalSaturation = 0f;
    [SerializeField] private float downedSaturation = -100f;

    private ColorAdjustments colorAdjustments;

    private Volume localVolume;
    private VolumeProfile localVolumeProfile;

    private Vector3 normalCameraLocalPosition;

    private bool isDowned;
    private bool knockdownStarted;

    private float knockdownStartTime;

    // =========================================================
    // AWAKE
    // =========================================================

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
                GetComponentInChildren<Camera>(true);

            if (playerCamera != null)
            {
                cameraTransform =
                    playerCamera.transform;
            }
        }
    }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

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

        // -----------------------------------------------------
        // VOLUME LOCAL DEL JUGADOR
        // -----------------------------------------------------

        if (IsOwner)
        {
            CreateLocalVolume();
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

    // =========================================================
    // CREAR VOLUME LOCAL
    // =========================================================

    private void CreateLocalVolume()
    {
        if (cameraTransform == null)
            return;

        Camera playerCamera =
            cameraTransform.GetComponent<Camera>();

        if (playerCamera == null)
        {
            Debug.LogError(
                "[PlayerDownedEffects] " +
                "cameraTransform no tiene Camera."
            );

            return;
        }

        // -----------------------------------------------------
        // CREAR OBJETO PARA EL VOLUME
        // -----------------------------------------------------

        GameObject volumeObject =
            new GameObject(
                "Local Downed Volume"
            );

        volumeObject.transform.SetParent(
            cameraTransform,
            false
        );

        volumeObject.transform.localPosition =
            Vector3.zero;

        volumeObject.transform.localRotation =
            Quaternion.identity;

        // Usamos la misma layer de la cámara.
        // De esta forma nos aseguramos de que la cámara
        // pueda detectar este Volume.
        volumeObject.layer =
            cameraTransform.gameObject.layer;

        // -----------------------------------------------------
        // VOLUME
        // -----------------------------------------------------

        localVolume =
            volumeObject.AddComponent<Volume>();

        localVolume.isGlobal = true;
        localVolume.priority = 100f;
        localVolume.weight = 1f;

        // -----------------------------------------------------
        // PERFIL NUEVO EXCLUSIVO PARA ESTE JUGADOR
        // -----------------------------------------------------

        localVolumeProfile =
            ScriptableObject.CreateInstance<
                VolumeProfile
            >();

        colorAdjustments =
            localVolumeProfile.Add<
                ColorAdjustments
            >();

        colorAdjustments.saturation.overrideState =
            true;

        colorAdjustments.saturation.value =
            normalSaturation;

        localVolume.profile =
            localVolumeProfile;

        // -----------------------------------------------------
        // ASEGURAR QUE LA CÁMARA DETECTE LA LAYER DEL VOLUME
        // -----------------------------------------------------

        UniversalAdditionalCameraData cameraData =
            playerCamera.GetComponent<
                UniversalAdditionalCameraData
            >();

        if (cameraData != null)
        {
            int volumeLayer =
                1 << volumeObject.layer;

            cameraData.volumeLayerMask |=
                volumeLayer;
        }

        Debug.Log(
            "[PlayerDownedEffects] " +
            "Volume local creado para " +
            gameObject.name
        );
    }

    // =========================================================
    // NETWORK DESPAWN
    // =========================================================

    public override void OnNetworkDespawn()
    {
        if (playerHealth != null)
        {
            playerHealth.State.OnValueChanged -=
                OnStateChanged;
        }

        if (localVolumeProfile != null)
        {
            Destroy(localVolumeProfile);
            localVolumeProfile = null;
        }

        if (localVolume != null)
        {
            Destroy(
                localVolume.gameObject
            );

            localVolume = null;
        }
    }

    // =========================================================
    // CAMBIO DE ESTADO
    // =========================================================

    private void OnStateChanged(
        PlayerHealth.PlayerState previousState,
        PlayerHealth.PlayerState newState
    )
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

    // =========================================================
    // APLICAR ESTADO
    // =========================================================

    private void ApplyState(
        PlayerHealth.PlayerState state
    )
    {
        isDowned =
            state ==
            PlayerHealth.PlayerState.Downed;

        if (isDowned)
        {
            StartDowned();
        }
        else
        {
            StopDowned();
        }
    }

    // =========================================================
    // INICIAR DOWNED
    // =========================================================

    private void StartDowned()
    {
        knockdownStarted = true;

        knockdownStartTime =
            Time.time;

        // -----------------------------------------------------
        // ANIMACIÓN
        // -----------------------------------------------------

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

        // -----------------------------------------------------
        // BLANCO Y NEGRO
        // -----------------------------------------------------

        if (
            IsOwner &&
            colorAdjustments != null
        )
        {
            colorAdjustments.saturation.overrideState =
                true;

            colorAdjustments.saturation.value =
                downedSaturation;
        }
    }

    // =========================================================
    // TERMINAR DOWNED
    // =========================================================

    private void StopDowned()
    {
        isDowned = false;
        knockdownStarted = false;

        if (
            IsOwner &&
            colorAdjustments != null
        )
        {
            colorAdjustments.saturation.overrideState =
                true;

            colorAdjustments.saturation.value =
                normalSaturation;
        }

        if (cameraTransform != null)
        {
            cameraTransform.localPosition =
                normalCameraLocalPosition;
        }
    }

    // =========================================================
    // LATE UPDATE
    // =========================================================

    private void LateUpdate()
    {
        if (!isDowned)
            return;

        HandleDownedCamera();
        KeepKnockdownAnimation();
    }

    // =========================================================
    // CÁMARA DOWNED
    // =========================================================

    private void HandleDownedCamera()
    {
        if (!IsOwner)
            return;

        if (cameraTransform == null)
            return;

        Vector3 targetPosition =
            normalCameraLocalPosition;

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

    // =========================================================
    // MANTENER ANIMACIÓN
    // =========================================================

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

        if (
            stateInfo.fullPathHash !=
            Animator.StringToHash(
                "Base Layer." +
                knockdownStateName
            )
        )
        {
            float elapsed =
                Time.time -
                knockdownStartTime;

            float normalizedTime =
                stateInfo.length > 0f
                    ? elapsed /
                      stateInfo.length
                    : 0f;

            animator.Play(
                stateHash,
                0,
                normalizedTime
            );
        }
    }
}