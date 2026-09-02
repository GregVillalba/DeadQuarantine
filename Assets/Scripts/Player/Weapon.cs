using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class Weapon : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera weaponCamera;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Animator weaponAnimator;
    [SerializeField] private Muzzle muzzle;
    [SerializeField] private GameObject bulletTrailPrefab;

    private HUDController hudController;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip emptySound;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private AudioClip reloadEmptySound;

    [Header("Pistola")]
    [SerializeField] private float fireRate = 0.25f;
    [SerializeField] private float reloadTime = 1.5f;
    [SerializeField] private float range = 100f;
    [SerializeField] private int damage = 25;
    [SerializeField] private int maxAmmo = 12;
    [SerializeField] private string weaponName = "Pistola";

    [Header("Aim")]
    [SerializeField] private float aimFOV = 50f;
    [SerializeField] private float aimTransitionSpeed = 10f;

    [Header("Animator - Blend Aiming")]
    [SerializeField] private float aimingBlendSpeed = 8f;

    [Header("Dispersión")]
    [SerializeField] private float spreadIdle = 5f;
    [SerializeField] private float spreadMoving = 8f;
    [SerializeField] private float spreadCrouching = 3f;
    [SerializeField] private float spreadAiming = 0f;

    [Header("Dispersión por disparo")]
    [SerializeField] private float spreadIncreasePerShot = 4f;
    [SerializeField] private float maxSpread = 35f;
    [SerializeField] private float spreadRecoverySpeed = 3f;

    [Header("Dispersión al saltar")]
    [SerializeField] private float jumpSpread = 15f;
    [SerializeField] private float landingRecoverySpeed = 25f;
    [SerializeField] private float landingRecoveryDuration = 0.25f;

    public bool IsAiming { get; private set; }

    public int CurrentAmmo =>
        currentAmmo;

    public int MaxAmmo =>
        maxAmmo;

    public float CurrentSpreadNormalized
    {
        get
        {
            float minimumSpread =
                IsAiming
                    ? spreadAiming
                    : spreadIdle;

            if (maxSpread <= minimumSpread)
                return 0f;

            return Mathf.Clamp01(
                Mathf.InverseLerp(
                    minimumSpread,
                    maxSpread,
                    currentSpread
                )
            );
        }
    }

    public string WeaponName =>
        weaponName;

    public bool IsReloading =>
        isReloading;

    private PlayerControls controls;

    private int currentAmmo;
    private float nextFireTime;
    private bool isReloading;

    private float defaultWorldFOV;
    private float currentSpread;
    private float aimingBlend;

    private bool wasGrounded = true;

    private float landingRecoveryTimer;

    private void Awake()
    {
        controls =
            new PlayerControls();

        currentAmmo =
            maxAmmo;

        if (playerCamera != null)
        {
            defaultWorldFOV =
                playerCamera.fieldOfView;
        }

        currentSpread =
            spreadIdle;

        hudController =
            GetComponentInParent<
                HUDController
            >();

        if (hudController == null)
        {
            hudController =
                transform.root
                    .GetComponentInChildren<
                        HUDController
                    >(true);
        }

        // Intentar encontrar PlayerMovement
        // automáticamente si no está asignado.
        if (playerMovement == null)
        {
            playerMovement =
                GetComponentInParent<
                    PlayerMovement
                >();
        }

        // Intentar encontrar CharacterController
        // automáticamente si no está asignado.
        if (characterController == null)
        {
            characterController =
                GetComponentInParent<
                    CharacterController
                >();
        }
    }

    private void OnEnable()
    {
        controls.Player.Enable();

        controls.Player.Fire.performed +=
            OnFire;

        controls.Player.Reload.performed +=
            OnReload;

        controls.Player.Aim.started +=
            OnAimStarted;

        controls.Player.Aim.canceled +=
            OnAimCanceled;
    }

    private void OnDisable()
    {
        if (controls == null)
            return;

        controls.Player.Fire.performed -=
            OnFire;

        controls.Player.Reload.performed -=
            OnReload;

        controls.Player.Aim.started -=
            OnAimStarted;

        controls.Player.Aim.canceled -=
            OnAimCanceled;

        controls.Player.Disable();

        IsAiming =
            false;
    }

    public void AnimationAmmunitionFill()
    {
        currentAmmo =
            maxAmmo;
    }

    public void AnimationReloadFinished()
    {
        currentAmmo =
            maxAmmo;

        isReloading =
            false;
    }

    private void Update()
    {
        UpdateSprintAimRestriction();
        UpdateAimFOV();
        UpdateSpread();
        UpdateJumpSpread();
        UpdateAnimatorParams();
    }

    // =========================================================
    // AIM
    // =========================================================

    private void OnAimStarted(
        InputAction.CallbackContext context)
    {
        // No se puede apuntar durante la recarga.
        if (isReloading)
            return;

        // No se puede apuntar mientras se corre.
        if (playerMovement != null &&
            playerMovement.IsSprinting)
        {
            IsAiming =
                false;

            return;
        }

        IsAiming =
            true;
    }

    private void OnAimCanceled(
        InputAction.CallbackContext context)
    {
        IsAiming =
            false;
    }

    private void UpdateSprintAimRestriction()
    {
        if (!IsAiming)
            return;

        if (playerMovement == null)
            return;

        // Si empieza a correr mientras apuntaba,
        // cancelar inmediatamente el apuntado.
        if (playerMovement.IsSprinting)
        {
            IsAiming =
                false;
        }
    }

    private void UpdateAimFOV()
    {
        if (playerCamera == null)
            return;

        float targetFOV =
            IsAiming
                ? aimFOV
                : defaultWorldFOV;

        playerCamera.fieldOfView =
            Mathf.Lerp(
                playerCamera.fieldOfView,
                targetFOV,
                aimTransitionSpeed *
                Time.deltaTime
            );
    }

    // =========================================================
    // SPREAD
    // =========================================================

    private void UpdateSpread()
    {
        if (characterController == null)
            return;

        if (IsAiming)
        {
            currentSpread =
                spreadAiming;

            return;
        }

        if (!characterController.isGrounded)
            return;

        float targetSpread;

        if (playerMovement != null &&
            playerMovement.IsCrouching)
        {
            targetSpread =
                spreadCrouching;
        }
        else if (IsMovingOnGround())
        {
            targetSpread =
                spreadMoving;
        }
        else
        {
            targetSpread =
                spreadIdle;
        }

        float recoverySpeed =
            landingRecoveryTimer > 0f
                ? landingRecoverySpeed
                : spreadRecoverySpeed;

        currentSpread =
            Mathf.MoveTowards(
                currentSpread,
                targetSpread,
                recoverySpeed *
                Time.deltaTime
            );

        currentSpread =
            Mathf.Clamp(
                currentSpread,
                0f,
                maxSpread
            );

        if (landingRecoveryTimer > 0f)
        {
            landingRecoveryTimer -=
                Time.deltaTime;
        }
    }

    private void UpdateJumpSpread()
    {
        if (characterController == null)
            return;

        bool isGrounded =
            characterController.isGrounded;

        if (wasGrounded && !isGrounded)
        {
            currentSpread =
                Mathf.Clamp(
                    currentSpread +
                    jumpSpread,
                    0f,
                    maxSpread
                );
        }

        if (!wasGrounded && isGrounded)
        {
            landingRecoveryTimer =
                landingRecoveryDuration;
        }

        wasGrounded =
            isGrounded;
    }

    private bool IsMovingOnGround()
    {
        if (characterController == null)
            return false;

        if (!characterController.isGrounded)
            return false;

        Vector3 horizontalVelocity =
            new Vector3(
                characterController.velocity.x,
                0f,
                characterController.velocity.z
            );

        return horizontalVelocity.magnitude >
               0.1f;
    }

    // =========================================================
    // ANIMATOR
    // =========================================================

    private void UpdateAnimatorParams()
    {
        if (weaponAnimator == null ||
            characterController == null)
            return;

        float speed =
            characterController.velocity
                .magnitude;

        weaponAnimator.SetFloat(
            "Speed",
            speed,
            0.15f,
            Time.deltaTime
        );

        weaponAnimator.SetBool(
            "IsAiming",
            IsAiming
        );

        UpdateAimingBlend();
    }

    private void UpdateAimingBlend()
    {
        if (weaponAnimator == null)
            return;

        float target =
            IsAiming
                ? 1f
                : 0f;

        aimingBlend =
            Mathf.MoveTowards(
                aimingBlend,
                target,
                aimingBlendSpeed *
                Time.deltaTime
            );

        weaponAnimator.SetFloat(
            "Aiming",
            aimingBlend
        );
    }

    // =========================================================
    // DISPARO
    // =========================================================

    private void OnFire(
        InputAction.CallbackContext context)
    {
        if (Time.timeScale == 0f)
            return;

        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null)
        {
            return;
        }

        if (isReloading)
            return;

        if (Time.time < nextFireTime)
            return;

        if (currentAmmo <= 0)
        {
            PlaySound(
                emptySound
            );

            if (weaponAnimator != null)
            {
                weaponAnimator.SetTrigger(
                    "FireEmpty"
                );
            }

            return;
        }

        nextFireTime =
            Time.time + fireRate;

        currentAmmo--;

        currentSpread =
            Mathf.Clamp(
                currentSpread +
                spreadIncreasePerShot,
                0f,
                maxSpread
            );

        PlaySound(
            shootSound
        );

        if (weaponAnimator != null)
        {
            weaponAnimator.SetTrigger(
                "Fire"
            );
        }

        muzzle?.PlayEffect();

        Shoot();
    }

    // =========================================================
    // RECARGA
    // =========================================================

    private void OnReload(
        InputAction.CallbackContext context)
    {
        if (isReloading)
            return;

        if (currentAmmo == maxAmmo)
            return;

        // Recargar cancela el apuntado.
        IsAiming =
            false;

        isReloading =
            true;

        bool wasEmpty =
            currentAmmo == 0;

        if (wasEmpty)
        {
            PlaySound(
                reloadEmptySound
            );
        }
        else
        {
            PlaySound(
                reloadSound
            );
        }

        if (weaponAnimator != null)
        {
            weaponAnimator.SetBool(
                "IsEmpty",
                wasEmpty
            );

            weaponAnimator.SetTrigger(
                "Reload"
            );
        }
    }

    private void PlaySound(
        AudioClip clip)
    {
        if (audioSource != null &&
            clip != null)
        {
            audioSource.PlayOneShot(
                clip
            );
        }
    }

    // =========================================================
    // DISPARO REAL
    // =========================================================

    private void Shoot()
    {
        if (GameplayPopupsController.Instance != null)
        {
            GameplayPopupsController.Instance
                .OcultarPanelRonda();
        }

        Vector3 spreadDirection =
            ApplySpreadToDirection(
                playerCamera.transform.forward,
                currentSpread
            );

        Vector3 bulletStartPosition =
            firePoint.position;

        PlayerHealth playerHealth =
            GetComponentInParent<PlayerHealth>();

        if (
            playerHealth != null &&
            playerHealth.IsDowned
        )
        {
            bulletStartPosition =
                playerCamera.transform.position;
        }

        Ray ray =
            new Ray(
                playerCamera.transform.position,
                spreadDirection
            );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            range))
        {
            Debug.Log(
                "Impacto en: " +
                hit.collider.name
            );

            Debug.DrawLine(
                bulletStartPosition,
                hit.point,
                Color.red,
                1f
            );

            SpawnBulletTrail(
                hit.point,
                bulletStartPosition
            );

            ZombieHealth zombieHealth =
                hit.collider
                    .GetComponentInParent<ZombieHealth>();

            if (zombieHealth != null)
            {
                zombieHealth.TakeDamage(
                    damage,
                    hit.point,
                    hit.normal
                );
            }
            else if (
                DecalManager.Instance != null)
            {
                DecalManager.Instance
                    .SpawnBulletHole(
                        hit.point,
                        hit.normal
                    );
            }
        }
        else
        {
            Vector3 missPoint =
                playerCamera.transform.position +
                spreadDirection *
                range;

            SpawnBulletTrail(
                missPoint,
                bulletStartPosition
            );

            Debug.DrawRay(
                bulletStartPosition,
                spreadDirection * range,
                Color.yellow,
                1f
            );
        }
    }

    // =========================================================
    // BULLET TRAIL
    // =========================================================

    private void SpawnBulletTrail(
        Vector3 targetPoint,
        Vector3 startPoint
    )
    {
        if (
            bulletTrailPrefab == null
        )
        {
            return;
        }

        GameObject trailObject =
            Instantiate(
                bulletTrailPrefab,
                startPoint,
                Quaternion.identity
            );

        BulletTrail trail =
            trailObject.GetComponent<BulletTrail>();

        if (trail != null)
        {
            trail.Init(
                targetPoint
            );
        }
    }

    // =========================================================
    // DIRECCIÓN CON DISPERSIÓN
    // =========================================================

    private Vector3 ApplySpreadToDirection(
        Vector3 baseDirection,
        float spreadDegrees)
    {
        if (spreadDegrees <= 0f)
            return baseDirection;

        float randomX =
            Random.Range(
                -spreadDegrees,
                spreadDegrees
            );

        float randomY =
            Random.Range(
                -spreadDegrees,
                spreadDegrees
            );

        Quaternion spreadRotation =
            Quaternion.Euler(
                randomY,
                randomX,
                0f
            );

        return spreadRotation *
               baseDirection;
    }
}