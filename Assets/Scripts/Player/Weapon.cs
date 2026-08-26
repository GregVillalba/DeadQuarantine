using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class Weapon : MonoBehaviour
{
    [SerializeField] private Transform firePoint;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera weaponCamera;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private float fireRate = 0.4f;
    [SerializeField] private float reloadTime = 1.5f;
    [SerializeField] private float range = 100f;
    [SerializeField] private int damage = 25;
    [SerializeField] private int maxAmmo = 6;
    [SerializeField] private string weaponName = "Revolver";
    [SerializeField] private Animator weaponAnimator;

    [Header("Aim (ADS)")]
    [SerializeField] private float aimFOV = 50f;
    [SerializeField] private float aimTransitionSpeed = 10f;

    [Header("Dispersión")]
    [SerializeField] private float spreadIdle = 0.5f;
    [SerializeField] private float spreadMoving = 3f;
    [SerializeField] private float spreadCrouching = 0.2f;
    [SerializeField] private float spreadAiming = 0f;
    [SerializeField] private float spreadChangeSpeed = 5f;

    public bool IsAiming { get; private set; }
    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;
    public float CurrentSpreadNormalized => currentSpread / spreadMoving;
    public string WeaponName => weaponName;

    private PlayerControls controls;
    private int currentAmmo;
    private float nextFireTime;
    private bool isReloading;

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
    }

    private void OnDisable()
    {
        controls.Player.Fire.performed -= OnFire;
        controls.Player.Reload.performed -= OnReload;

        controls.Player.Disable();
    }

    private void Update()
    {
        HandleAim();
        UpdateSpread();
        UpdateAnimatorParams();
    }

    private void HandleAim()
    {
        // El Animator se encarga de mover los brazos/arma.
        // Acá solamente obtenemos el estado de apuntado.
        IsAiming = controls.Player.Aim.IsPressed();

        // El FOV sí se controla desde código porque
        // no depende de la animación del arma.
        float targetFOV = IsAiming ? aimFOV : defaultWorldFOV;

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            aimTransitionSpeed * Time.deltaTime
        );
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

    private void OnFire(InputAction.CallbackContext context)
    {
        // Si el juego está en pausa, no hace nada.
        if (Time.timeScale == 0f)
            return;

        // Si el mouse está sobre UI, no dispara.
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null)
            return;

        // No dispara mientras recarga.
        if (isReloading)
            return;

        // Respeta la cadencia del arma.
        if (Time.time < nextFireTime)
            return;

        // No dispara si no hay munición.
        if (currentAmmo <= 0)
            return;

        nextFireTime = Time.time + fireRate;
        currentAmmo--;

        // Le decimos al Animator que hubo un disparo.
        if (weaponAnimator != null)
        {
            weaponAnimator.SetTrigger("Fire");
        }

        // Realiza el disparo real mediante Raycast.
        Shoot();
    }

    private void OnReload(InputAction.CallbackContext context)
    {
        // No recargar si ya está recargando.
        if (isReloading)
            return;

        // No recargar si el cargador ya está lleno.
        if (currentAmmo == maxAmmo)
            return;

        if (weaponAnimator != null)
        {
            // Permite diferenciar Reload y Reload_Empty
            // desde el Animator.
            weaponAnimator.SetBool("IsEmpty", currentAmmo == 0);

            weaponAnimator.SetTrigger("Reload");
        }

        StartCoroutine(ReloadRoutine());
    }

    private void UpdateAnimatorParams()
    {
        if (weaponAnimator == null)
            return;

        // Velocidad del jugador.
        float speed = characterController.velocity.magnitude;

        weaponAnimator.SetFloat("Speed", speed);

        // Estado de apuntado.
        weaponAnimator.SetBool("IsAiming", IsAiming);
    }

    private System.Collections.IEnumerator ReloadRoutine()
    {
        isReloading = true;

        Debug.Log("Recargando...");

        yield return new WaitForSeconds(reloadTime);

        currentAmmo = maxAmmo;
        isReloading = false;

        Debug.Log("Recarga completa. Munición: " + currentAmmo);
    }

    private void Shoot()
    {
        Vector3 spreadDirection = ApplySpreadToDirection(
            playerCamera.transform.forward,
            currentSpread
        );

        Ray ray = new Ray(
            playerCamera.transform.position,
            spreadDirection
        );

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            Debug.Log("Impacto en: " + hit.collider.name);

            Debug.DrawLine(
                firePoint.position,
                hit.point,
                Color.red,
                1f
            );

            ZombieHealth zombieHealth =
                hit.collider.GetComponent<ZombieHealth>();

            if (zombieHealth != null)
            {
                zombieHealth.TakeDamage(damage);
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
            Quaternion.Euler(randomY, randomX, 0f);

        return spreadRotation * baseDirection;
    }
}