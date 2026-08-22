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

    [Header("Aim (ADS)")]
    [SerializeField] private Vector3 aimPosition = new Vector3(0f, -0.05f, 0.2f);
    [SerializeField] private float aimFOV = 50f;
    [SerializeField] private float aimTransitionSpeed = 10f;

    [Header("Weapon Bob")]
    [SerializeField] private float bobFrequency = 8f;
    [SerializeField] private float bobAmplitude = 0.03f;

    [Header("Dispersión")]
    [SerializeField] private float spreadIdle = 0.5f;
    [SerializeField] private float spreadMoving = 3f;
    [SerializeField] private float spreadCrouching = 0.2f;
    [SerializeField] private float spreadAiming = 0f;
    [SerializeField] private float spreadChangeSpeed = 5f;

    [Header("Retroceso")]
    [SerializeField] private Vector3 recoilHipOffset = new Vector3(0f, 0.05f, -0.18f);
    [SerializeField] private Vector3 recoilAimOffset = new Vector3(0f, 0.08f, 0f);
    [SerializeField] private float recoilRecoverySpeed = 6f;

    public bool IsAiming { get; private set; }
    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;
    public float CurrentSpreadNormalized => currentSpread / spreadMoving;

    private PlayerControls controls;
    private int currentAmmo;
    private float nextFireTime;
    private bool isReloading;

    private Vector3 hipPosition;
    private float defaultWorldFOV;
    private float bobTimer;
    private Vector3 smoothedBasePosition;

    private float currentSpread;
    private Vector3 recoilOffset;

    private void Awake()
    {
        controls = new PlayerControls();
        currentAmmo = maxAmmo;

        hipPosition = transform.localPosition;
        smoothedBasePosition = hipPosition;
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
        RecoverRecoil();
    }

    private void HandleAim()
    {
        IsAiming = controls.Player.Aim.IsPressed();

        Vector3 basePosition = IsAiming ? aimPosition : hipPosition + CalculateBobOffset();
        smoothedBasePosition = Vector3.Lerp(smoothedBasePosition, basePosition, aimTransitionSpeed * Time.deltaTime);

        transform.localPosition = smoothedBasePosition + recoilOffset;

        float targetFOV = IsAiming ? aimFOV : defaultWorldFOV;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, aimTransitionSpeed * Time.deltaTime);
    }
    private Vector3 CalculateBobOffset()
    {
        bool isMoving = IsMovingOnGround();

        if (!isMoving)
        {
            bobTimer = 0f;
            return Vector3.zero;
        }

        bobTimer += Time.deltaTime * bobFrequency;

        float verticalOffset = Mathf.Sin(bobTimer) * bobAmplitude;
        float horizontalOffset = Mathf.Cos(bobTimer * 0.5f) * bobAmplitude * 0.5f;

        return new Vector3(horizontalOffset, verticalOffset, 0f);
    }

    private bool IsMovingOnGround()
    {
        if (!characterController.isGrounded) return false;

        Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
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

        currentSpread = Mathf.Lerp(currentSpread, targetSpread, spreadChangeSpeed * Time.deltaTime);
    }

    private void ApplyRecoil()
    {
        recoilOffset = IsAiming ? recoilAimOffset : recoilHipOffset;
    }

    private void RecoverRecoil()
    {
        recoilOffset = Vector3.Lerp(recoilOffset, Vector3.zero, recoilRecoverySpeed * Time.deltaTime);
    }

    private void OnFire(InputAction.CallbackContext context)
    {
        // 1. Si el juego está en pausa, no hace nada
        if (Time.timeScale == 0f) return;

        // 2. Si el mouse está sobre cualquier botón/UI, no dispara
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null) return;

        if (isReloading) return;
        if (Time.time < nextFireTime) return;
        if (currentAmmo <= 0) return;

        nextFireTime = Time.time + fireRate;
        currentAmmo--;

        ApplyRecoil();
        Shoot();
    }

    private void OnReload(InputAction.CallbackContext context)
    {
        if (isReloading) return;
        if (currentAmmo == maxAmmo) return;

        StartCoroutine(ReloadRoutine());
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
        Vector3 spreadDirection = ApplySpreadToDirection(playerCamera.transform.forward, currentSpread);
        Ray ray = new Ray(playerCamera.transform.position, spreadDirection);

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            Debug.Log("Impacto en: " + hit.collider.name);
            Debug.DrawLine(firePoint.position, hit.point, Color.red, 1f);
        }
        else
        {
            Debug.DrawRay(firePoint.position, spreadDirection * range, Color.yellow, 1f);
        }
    }

    private Vector3 ApplySpreadToDirection(Vector3 baseDirection, float spreadDegrees)
    {
        if (spreadDegrees <= 0f) return baseDirection;

        float randomX = Random.Range(-spreadDegrees, spreadDegrees);
        float randomY = Random.Range(-spreadDegrees, spreadDegrees);

        Quaternion spreadRotation = Quaternion.Euler(randomY, randomX, 0f);
        return spreadRotation * baseDirection;
    }
}