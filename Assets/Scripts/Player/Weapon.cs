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

    private void OnDisable()
    {
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

    private void Update()
    {
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
        if (isReloading)
            return;

        IsAiming =
            true;
    }

    private void OnAimCanceled(
        InputAction.CallbackContext context)
    {
        IsAiming =
            false;
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

        // Al apuntar, la dispersión es siempre 0.
        if (IsAiming)
        {
            currentSpread = 0f;
            return;
        }

        // Mientras está en el aire,
        // mantiene la dispersión del salto.
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

        // Empieza el salto.
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

        // Acaba de tocar el suelo.
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
            characterController.velocity.magnitude;

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
            return;

        if (isReloading)
            return;

        // NO DISPARAR MIENTRAS CORRE.
        if (playerMovement != null &&
            playerMovement.IsSprinting)
        {
            return;
        }

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

        Ray ray =
            new Ray(
                playerCamera.transform.position,
                spreadDirection
            );

        // Obtener TODOS los colliders atravesados
        // por el disparo.
        RaycastHit[] hits =
            Physics.RaycastAll(
                ray,
                range
            );

        // Ordenarlos desde el más cercano
        // al más lejano.
        System.Array.Sort(
            hits,
            (a, b) =>
                a.distance.CompareTo(
                    b.distance
                )
        );

        bool validHitFound = false;
        RaycastHit validHit = default;

        foreach (RaycastHit hit in hits)
        {
            // =================================================
            // IGNORAR JUGADORES
            // =================================================

            PlayerHealth playerHit =
                hit.collider.GetComponentInParent<
                    PlayerHealth
                >();

            if (playerHit != null)
            {
                continue;
            }

            // =================================================
            // IGNORAR PROPIO PLAYER
            // =================================================

            if (hit.collider.transform.root ==
                transform.root)
            {
                continue;
            }

            // =================================================
            // IGNORAR CUALQUIER COMPONENTE DEL PLAYER
            // =================================================

            PlayerMovement movementHit =
                hit.collider.GetComponentInParent<
                    PlayerMovement
                >();

            if (movementHit != null)
            {
                continue;
            }

            // Este es el primer objeto válido.
            validHit =
                hit;

            validHitFound =
                true;

            break;
        }

        // =====================================================
        // IMPACTO VÁLIDO
        // =====================================================

        if (validHitFound)
        {
            RaycastHit hit =
                validHit;

            Debug.Log(
                "Impacto en: " +
                hit.collider.name
            );

            Debug.DrawLine(
                firePoint.position,
                hit.point,
                Color.red,
                1f
            );

            SpawnBulletTrail(
                hit.point
            );

            // =================================================
            // ZOMBIE
            // =================================================

            ZombieHealth zombieHealth =
                hit.collider
                    .GetComponentInParent<
                        ZombieHealth
                    >();

            if (zombieHealth != null)
            {
                zombieHealth.TakeDamage(
                    damage,
                    hit.point,
                    hit.normal
                );

                return;
            }

            // =================================================
            // PARED / OBJETO
            // =================================================

            if (DecalManager.Instance != null)
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
            // =================================================
            // NO IMPACTÓ CONTRA NADA VÁLIDO
            // =================================================

            Vector3 missPoint =
                firePoint.position +
                spreadDirection *
                range;

            SpawnBulletTrail(
                missPoint
            );

            Debug.DrawRay(
                firePoint.position,
                spreadDirection *
                range,
                Color.yellow,
                1f
            );
        }
    }

    // =========================================================
    // BULLET TRAIL
    // =========================================================

    private void SpawnBulletTrail(
        Vector3 targetPoint
    )
    {
        if (bulletTrailPrefab == null ||
            firePoint == null)
            return;

        GameObject trailObject =
            Instantiate(
                bulletTrailPrefab,
                firePoint.position,
                Quaternion.identity
            );

        BulletTrail trail =
            trailObject.GetComponent<
                BulletTrail
            >();

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
        float spreadDegrees
    )
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