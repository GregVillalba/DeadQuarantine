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

    [Header("Dispersión")]
    [SerializeField] private float spreadIdle = 0.5f;
    [SerializeField] private float spreadMoving = 3f;
    [SerializeField] private float spreadCrouching = 0.2f;
    [SerializeField] private float spreadAiming = 0f;
    [SerializeField] private float spreadChangeSpeed = 5f;

    [Header("Estabilización de mira al apuntar")]
    [SerializeField] private Transform armsRoot; // FPS_Arms
    [SerializeField] private Vector3 aimStablePosition = new Vector3(0f, -0.05f, 0.2f);
    [SerializeField] private float aimStabilizeSpeed = 15f;

    public bool IsAiming { get; private set; }

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;
    public float CurrentSpreadNormalized => currentSpread / spreadMoving;
    public string WeaponName => weaponName;

    private PlayerControls controls;

    private int currentAmmo;
    private float nextFireTime;
    private bool isReloading;
    public bool IsReloading => isReloading;

    private float defaultWorldFOV;
    private float currentSpread;

    private void Awake()
    {
        controls = new PlayerControls();

        currentAmmo = maxAmmo;

        defaultWorldFOV = playerCamera.fieldOfView;
    }

    private void OnEnable()
    {
        controls.Player.Enable();

        controls.Player.Fire.performed += OnFire;
        controls.Player.Reload.performed += OnReload;

        controls.Player.Aim.started += OnAimStarted;
        controls.Player.Aim.canceled += OnAimCanceled;
    }

    private void OnDisable()
    {
        controls.Player.Fire.performed -= OnFire;
        controls.Player.Reload.performed -= OnReload;

        controls.Player.Aim.started -= OnAimStarted;
        controls.Player.Aim.canceled -= OnAimCanceled;

        controls.Player.Disable();

        IsAiming = false;
    }

    private void Update()
    {
        UpdateAimFOV();
        UpdateSpread();
        UpdateAnimatorParams();
        StabilizeAimPosition();
    }

    private void StabilizeAimPosition()
    {
        if (armsRoot == null) return;

        if (IsAiming)
        {
            armsRoot.localPosition = Vector3.Lerp(
                armsRoot.localPosition,
                aimStablePosition,
                aimStabilizeSpeed * Time.deltaTime
            );
        }
    }

    private void OnAimStarted(InputAction.CallbackContext context)
    {
        // No permite apuntar mientras recarga.
        if (isReloading)
            return;

        IsAiming = true;
    }

    private void OnAimCanceled(InputAction.CallbackContext context)
    {
        IsAiming = false;
    }

    private void UpdateAimFOV()
    {
        float targetFOV = IsAiming
            ? aimFOV
            : defaultWorldFOV;

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            aimTransitionSpeed * Time.deltaTime
        );
    }

    private void UpdateSpread()
    {
        float targetSpread;

        if (IsAiming)
        {
            targetSpread = spreadAiming;
        }
        else if (playerMovement.IsCrouching)
        {
            targetSpread = spreadCrouching;
        }
        else if (IsMovingOnGround())
        {
            targetSpread = spreadMoving;
        }
        else
        {
            targetSpread = spreadIdle;
        }

        currentSpread = Mathf.Lerp(
            currentSpread,
            targetSpread,
            spreadChangeSpeed * Time.deltaTime
        );
    }

    private void SpawnBulletTrail(Vector3 targetPoint)
    {
        if (bulletTrailPrefab == null || firePoint == null)
            return;

        GameObject trailObject = Instantiate(
            bulletTrailPrefab,
            firePoint.position,
            Quaternion.identity
        );

        BulletTrail trail = trailObject.GetComponent<BulletTrail>();

        if (trail != null)
        {
            trail.Init(targetPoint);
        }
    }


    private bool IsMovingOnGround()
    {
        if (!characterController.isGrounded)
            return false;

        Vector3 horizontalVelocity = new Vector3(
            characterController.velocity.x,
            0f,
            characterController.velocity.z
        );

        return horizontalVelocity.magnitude > 0.1f;
    }

    private void UpdateAnimatorParams()
    {
        if (weaponAnimator == null)
            return;

        float speed = characterController.velocity.magnitude;

        weaponAnimator.SetFloat("Speed", speed);
        weaponAnimator.SetBool("IsAiming", IsAiming);
    }

    private void OnFire(InputAction.CallbackContext context)
    {
        if (Time.timeScale == 0f)
            return;

        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null)
            return;

        if (isReloading)
            return;

        if (Time.time < nextFireTime)
            return;

        if (currentAmmo <= 0)
        {
            PlaySound(emptySound);

            if (weaponAnimator != null)
            {
                weaponAnimator.SetTrigger("FireEmpty");
            }

            return;
        }

        nextFireTime = Time.time + fireRate;

        currentAmmo--;

        // Sonido de disparo.
        PlaySound(shootSound);

        // Animación de disparo.
        if (weaponAnimator != null)
        {
            weaponAnimator.SetTrigger("Fire");
        }
        
        muzzle?.PlayEffect();

        // Disparo real.
        Shoot();
        
    }

    private void OnReload(InputAction.CallbackContext context)
    {
        if (isReloading)
            return;

        if (currentAmmo == maxAmmo)
            return;

        // Si estaba apuntando, deja de apuntar.
        IsAiming = false;

        bool wasEmpty = currentAmmo == 0;

        // Sonido de recarga.
        if (wasEmpty)
        {
            PlaySound(reloadEmptySound);
        }
        else
        {
            PlaySound(reloadSound);
        }

        // Animación de recarga.
        if (weaponAnimator != null)
        {
            weaponAnimator.SetBool(
                "IsEmpty",
                wasEmpty
            );

            weaponAnimator.SetTrigger("Reload");
        }

        StartCoroutine(ReloadRoutine());
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private System.Collections.IEnumerator ReloadRoutine()
    {
        isReloading = true;

        yield return new WaitForSeconds(reloadTime);

        currentAmmo = maxAmmo;

        isReloading = false;
    }

    private void Shoot()
    {
        Vector3 spreadDirection =
            ApplySpreadToDirection(
                playerCamera.transform.forward,
                currentSpread
            );

        Ray ray = new Ray(
            playerCamera.transform.position,
            spreadDirection
        );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            range))
        {
            Debug.Log(
                "Impacto en: " + hit.collider.name
            );

            Debug.DrawLine(
                firePoint.position,
                hit.point,
                Color.red,
                1f
            );

            SpawnBulletTrail(hit.point);

            ZombieHealth zombieHealth =
                hit.collider.GetComponentInParent<ZombieHealth>();

            if (zombieHealth != null)
            {
                zombieHealth.TakeDamage(damage);

                Debug.Log(
                    "Daño realizado: " + damage
                );
            }
            else if (DecalManager.Instance != null)
            {
                DecalManager.Instance.SpawnBulletHole(
                    hit.point,
                    hit.normal
                );
            }
        }
        else
        {
            Vector3 missPoint = firePoint.position + spreadDirection * range;

            SpawnBulletTrail(missPoint);

            Debug.DrawRay(
                firePoint.position,
                spreadDirection * range,
                Color.yellow,
                1f
            );
        }
    }

    private Vector3 ApplySpreadToDirection(
        Vector3 baseDirection,
        float spreadDegrees
    )
    {
        if (spreadDegrees <= 0f)
            return baseDirection;

        float randomX = Random.Range(
            -spreadDegrees,
            spreadDegrees
        );

        float randomY = Random.Range(
            -spreadDegrees,
            spreadDegrees
        );

        Quaternion spreadRotation =
            Quaternion.Euler(
                randomY,
                randomX,
                0f
            );

        return spreadRotation * baseDirection;
    }
}