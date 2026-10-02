using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections;

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

    [Header("Recoil de cámara")]
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private float cameraRecoilPerShot = 0.4f;

    private HUDController hudController;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip emptySound;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private AudioClip reloadEmptySound;

    [Header("Audio - Recarga por partes (Escopeta/Sniper)")]
    [SerializeField] private AudioClip reloadOpenSound;
    [SerializeField] private AudioClip reloadInsertSound;
    [SerializeField] private AudioClip reloadCloseSound;
    [SerializeField] private AudioClip reloadBoltOpenSound;
    [SerializeField] private AudioClip reloadBoltCloseSound;

    [Header("Pistola")]
    [SerializeField] private bool isAutomatic = false; // Tildar solo en el Rifle
    [SerializeField] private float fireRate = 0.25f;
    [SerializeField] private float reloadTime = 1.5f;
    [SerializeField] private float range = 100f;
    [SerializeField] private int damage = 25;
    [SerializeField] private int maxAmmo = 12;
    [SerializeField] private string weaponName = "Pistola";

    [Header("Escopeta (perdigones)")]
    [SerializeField] private bool firesMultiplePellets = false; // tildar solo en la Shotgun
    [SerializeField] private int pelletsPerShot = 8;
    [SerializeField] private float pelletSpreadAngle = 6f; // dispersión propia del perdigón, se suma a currentSpread

    [Header("Cerrojo (Sniper de cerrojo manual)")]
    [SerializeField] private bool requiresBoltActionAfterFire = false; // tildar solo en el Sniper de cerrojo
    [SerializeField] private float boltActionDuration = 1.3f; // debe igualar la duración real del clip Reload_Bolt

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

    [Header("Impactos")]
    [SerializeField] private LayerMask bulletIgnoredLayers;

    [Header("Animación de recarga (tercera persona)")]
    [SerializeField] private bool usesMultiPartReload = false;
    public bool UsesMultiPartReload => usesMultiPartReload;

    [Header("Daño por distancia")]
    [SerializeField] private float damageFalloffStart = 8f;
    [SerializeField] private float damageFalloffEnd = 25f;
    [SerializeField] private float minDamageMultiplier = 0.25f;

    public bool IsAiming { get; private set; }

    public int CurrentAmmo => currentAmmo;

    // ----- CAMBIO COMBINADO: PROPIEDADES DE DIFICULTAD (DEL COMPAÑERO) -----
    public int MaxAmmo => EffectiveMaxAmmo; 
    
    private int appliedMaxAmmo;
    public int EffectiveMaxAmmo
    {
        get
        {
            DifficultySettings s =
                RoundManager.Instance != null
                    ? RoundManager.Instance.ActiveSettings
                    : null;

            return (s != null && s.magazineSize > 0)
                ? s.magazineSize
                : maxAmmo;
        }
    }

    private void RefreshMaxAmmo()
    {
        int max = EffectiveMaxAmmo;

        if (max == appliedMaxAmmo)
            return;

        bool wasFull = currentAmmo >= appliedMaxAmmo;
        appliedMaxAmmo = max;
        currentAmmo = wasFull ? max : Mathf.Min(currentAmmo, max);
    }
    // ------------------------------------------------------------------------

    public float CurrentSpreadNormalized
    {
        get
        {
            float minimumSpread = IsAiming ? spreadAiming : spreadIdle;

            if (maxSpread <= minimumSpread)
                return 0f;

            return Mathf.Clamp01(
                Mathf.InverseLerp(minimumSpread, maxSpread, currentSpread)
            );
        }
    }

    public string WeaponName => weaponName;
    public bool IsReloading => isReloading;
    public bool ShellLoading => shellLoading;
    public bool IsChambering => isChambering; 

    private PlayerControls controls;

    private int currentAmmo;
    private float nextFireTime;
    private bool isReloading;
    private bool shellLoading; 
    private bool isChambering; 
    private float chamberReadyTime; 

    private float defaultWorldFOV;
    private float currentSpread;
    private float aimingBlend;

    private bool wasGrounded = true;
    private float landingRecoveryTimer;

    private PlayerScore playerScore;

    public bool InputLocked { get; set; }

    private void Awake()
    {
        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);

        // Inicializamos con el sistema de dificultad del compañero
        appliedMaxAmmo = EffectiveMaxAmmo;
        currentAmmo = EffectiveMaxAmmo; 

        if (playerCamera != null)
            defaultWorldFOV = playerCamera.fieldOfView;

        currentSpread = spreadIdle;

        hudController = GetComponentInParent<HUDController>();

        if (hudController == null)
            hudController = transform.root.GetComponentInChildren<HUDController>(true);

        playerScore = GetComponentInParent<PlayerScore>();

        if (playerScore == null)
            playerScore = transform.root.GetComponentInChildren<PlayerScore>(true);
    }

    private void OnEnable()
    {
        controls.Player.Enable();

        if (!isAutomatic)
            controls.Player.Fire.performed += OnFireSemiAuto;

        controls.Player.Reload.performed += OnReload;
        controls.Player.Aim.started += OnAimStarted;
        controls.Player.Aim.canceled += OnAimCanceled;
    }

    private void OnDisable()
    {
        if (!isAutomatic)
            controls.Player.Fire.performed -= OnFireSemiAuto;

        controls.Player.Reload.performed -= OnReload;
        controls.Player.Aim.started -= OnAimStarted;
        controls.Player.Aim.canceled -= OnAimCanceled;

        controls.Player.Disable();

        IsAiming = false;
    }

    private int ApplyDistanceFalloff(int baseDamage, float distance)
    {
        if (distance <= damageFalloffStart)
            return baseDamage;

        if (distance >= damageFalloffEnd)
            return Mathf.RoundToInt(baseDamage * minDamageMultiplier);

        float t = Mathf.InverseLerp(damageFalloffStart, damageFalloffEnd, distance);
        float multiplier = Mathf.Lerp(1f, minDamageMultiplier, t);

        return Mathf.RoundToInt(baseDamage * multiplier);
    }

    public void AnimationAmmunitionFill()
    {
        currentAmmo = EffectiveMaxAmmo; // Adaptado para la dificultad
    }

    // Adaptado para usar el EffectiveMaxAmmo del compañero
    public void AnimationInsertOneShell()
    {
        int max = EffectiveMaxAmmo;
        if (currentAmmo < max)
            currentAmmo++;

        shellLoading = currentAmmo < max;

        PlaySound(reloadInsertSound);

        Debug.Log("[Weapon] Insertada bala. currentAmmo=" + currentAmmo + "/" + max + " | ShellLoading=" + shellLoading);

        if (weaponAnimator != null)
            weaponAnimator.SetBool("ShellLoading", shellLoading);
    }

    public void AnimationReloadFinished()
    {
        int max = EffectiveMaxAmmo; // Adaptado para la dificultad

        if (!usesMultiPartReload)
        {
            currentAmmo = max;
        }
        else if (currentAmmo != max)
        {
            Debug.LogWarning(
                "[Weapon] AnimationReloadFinished: el loop bala por bala terminó con " +
                currentAmmo + "/" + max +
                " — revisá las condiciones ShellLoading en Insert A/B, se cortó antes de tiempo."
            );
            currentAmmo = max; // red de seguridad para no dejar el arma rota
        }

        isReloading = false;
        shellLoading = false;

        if (weaponAnimator != null)
            weaponAnimator.SetBool("ShellLoading", false);
    }

    public void AnimationReloadOpen()
    {
        Debug.Log("[Weapon] AnimationReloadOpen en Time.time=" + Time.time);
        PlaySound(reloadOpenSound);
    }

    public void AnimationReloadClose()
    {
        PlaySound(reloadCloseSound);
    }

    public void AnimationBoltOpen()
    {
        PlaySound(reloadBoltOpenSound);
    }

    public void AnimationBoltClose()
    {
        PlaySound(reloadBoltCloseSound);

        if (isReloading)
            AnimationReloadFinished();
    }

    public void ForceStopAiming()
    {
        IsAiming = false;
    }

    private void Update()
    {
        RefreshMaxAmmo(); // Checkeamos la lógica de munición dinámica de tu compañero

        if (IsAiming && playerMovement != null && playerMovement.IsSprinting)
            IsAiming = false;

        if (isChambering && Time.time >= chamberReadyTime)
            isChambering = false;

        UpdateAimFOV();
        UpdateSpread();
        UpdateJumpSpread();
        UpdateAnimatorParams();

        if (isAutomatic)
        {
            bool tieneBalas = currentAmmo > 0;

            if (tieneBalas && controls.Player.Fire.IsPressed())
                TryFire();
            else if (!tieneBalas && controls.Player.Fire.WasPressedThisFrame())
                TryFire();
        }
    }

    // =========================================================
    // AIM
    // =========================================================

    private void OnAimStarted(InputAction.CallbackContext context)
    {
        if (InputLocked) return;
        if (isReloading) return;
        if (playerMovement != null && playerMovement.IsSprinting) return;

        IsAiming = true;
    }

    private void OnAimCanceled(InputAction.CallbackContext context)
    {
        IsAiming = false;
    }

    private void UpdateAimFOV()
    {
        if (playerCamera == null) return;

        float targetFOV = IsAiming ? aimFOV : defaultWorldFOV;

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            aimTransitionSpeed * Time.deltaTime
        );
    }

    // =========================================================
    // SPREAD
    // =========================================================

    private void UpdateSpread()
    {
        if (characterController == null) return;

        if (IsAiming)
        {
            currentSpread = 0f;
            return;
        }

        if (!characterController.isGrounded) return;

        float targetSpread;

        if (playerMovement != null && playerMovement.IsCrouching)
            targetSpread = spreadCrouching;
        else if (IsMovingOnGround())
            targetSpread = spreadMoving;
        else
            targetSpread = spreadIdle;

        float recoverySpeed = landingRecoveryTimer > 0f
            ? landingRecoverySpeed
            : spreadRecoverySpeed;

        currentSpread = Mathf.MoveTowards(currentSpread, targetSpread, recoverySpeed * Time.deltaTime);
        currentSpread = Mathf.Clamp(currentSpread, 0f, maxSpread);

        if (landingRecoveryTimer > 0f)
            landingRecoveryTimer -= Time.deltaTime;
    }

    private void UpdateJumpSpread()
    {
        if (characterController == null) return;

        bool isGrounded = characterController.isGrounded;

        if (wasGrounded && !isGrounded)
            currentSpread = Mathf.Clamp(currentSpread + jumpSpread, 0f, maxSpread);

        if (!wasGrounded && isGrounded)
            landingRecoveryTimer = landingRecoveryDuration;

        wasGrounded = isGrounded;
    }

    private bool IsMovingOnGround()
    {
        if (characterController == null) return false;
        if (!characterController.isGrounded) return false;

        Vector3 horizontalVelocity = new Vector3(
            characterController.velocity.x,
            0f,
            characterController.velocity.z
        );

        return horizontalVelocity.magnitude > 0.1f;
    }

    // =========================================================
    // ANIMATOR
    // =========================================================

    private void UpdateAnimatorParams()
    {
        if (weaponAnimator == null || characterController == null) return;

        Vector3 horizontalVelocity = new Vector3(
            characterController.velocity.x,
            0f,
            characterController.velocity.z
        );

        float speed = horizontalVelocity.magnitude;

        weaponAnimator.SetFloat("Speed", speed, 0.15f, Time.deltaTime);
        weaponAnimator.SetBool("IsAiming", IsAiming);

        UpdateAimingBlend();
    }

    private void UpdateAimingBlend()
    {
        if (weaponAnimator == null) return;

        float target = IsAiming ? 1f : 0f;
        aimingBlend = Mathf.MoveTowards(aimingBlend, target, aimingBlendSpeed * Time.deltaTime);
        weaponAnimator.SetFloat("Aiming", aimingBlend);
    }

    // =========================================================
    // DISPARO
    // =========================================================

    private void OnFireSemiAuto(InputAction.CallbackContext context)
    {
        TryFire();
    }

    private void TryFire()
    {
        if (InputLocked) return;
        if (Time.timeScale == 0f) return;

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null) return;

        if (isReloading) return;
        if (isChambering) return;
        if (playerMovement != null && playerMovement.IsSprinting) return;

        if (Time.time < nextFireTime) return;

        if (currentAmmo <= 0)
        {
            PlaySound(emptySound);
            if (weaponAnimator != null) weaponAnimator.SetTrigger("FireEmpty");
            nextFireTime = Time.time + fireRate;
            return;
        }

        nextFireTime = Time.time + fireRate;
        currentAmmo--;

        if (playerScore != null) playerScore.RegistrarDisparoServerRpc();

        currentSpread = Mathf.Clamp(currentSpread + spreadIncreasePerShot, 0f, maxSpread);

        if (playerLook != null) playerLook.AddRecoil(cameraRecoilPerShot);

        PlaySound(shootSound);

        if (weaponAnimator != null) weaponAnimator.SetTrigger("Fire");

        muzzle?.PlayEffect();

        Shoot();

        if (requiresBoltActionAfterFire)
        {
            isChambering = true;
            chamberReadyTime = Time.time + boltActionDuration;
            if (weaponAnimator != null) weaponAnimator.SetTrigger("BoltAction");
        }
    }

    // =========================================================
    // RECARGA
    // =========================================================

    private void OnReload(InputAction.CallbackContext context)
    {
        if (InputLocked) return;
        if (isReloading) return;

        // Comprobación de dificultad añadida por tu compañero
        DifficultySettings reloadSettings =
            RoundManager.Instance != null
                ? RoundManager.Instance.ActiveSettings
                : null;

        if (reloadSettings != null && !reloadSettings.allowReload)
            return;

        if (currentAmmo >= EffectiveMaxAmmo) // Modificado con la lógica de tu compañero
            return;

        if (playerMovement != null && playerMovement.IsSprinting) return;

        IsAiming = false;
        isReloading = true;

        bool wasEmpty = currentAmmo == 0;

        if (wasEmpty)
            PlaySound(reloadEmptySound);
        else
            PlaySound(reloadSound);

        if (weaponAnimator != null)
        {
            weaponAnimator.SetBool("IsEmpty", wasEmpty);
            weaponAnimator.SetBool("MultiPartReload", usesMultiPartReload);

            shellLoading = usesMultiPartReload && currentAmmo < EffectiveMaxAmmo;
            weaponAnimator.SetBool("ShellLoading", shellLoading);

            weaponAnimator.SetTrigger("Reload");
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    // =========================================================
    // DISPARO REAL
    // =========================================================

    private void Shoot()
    {
        if (firesMultiplePellets)
        {
            for (int i = 0; i < pelletsPerShot; i++)
                FirePellet(currentSpread + pelletSpreadAngle);
        }
        else
        {
            FirePellet(currentSpread);
        }
    }

    private void FirePellet(float spreadDegrees)
    {
        Vector3 spreadDirection = ApplySpreadToDirection(
            playerCamera.transform.forward,
            spreadDegrees
        );

        Ray ray = new Ray(playerCamera.transform.position, spreadDirection);

        RaycastHit[] hits = Physics.RaycastAll(ray, range);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool validHitFound = false;
        RaycastHit validHit = default;

        foreach (RaycastHit hit in hits)
        {
            PlayerHealth playerHit = hit.collider.GetComponentInParent<PlayerHealth>();
            if (playerHit != null) continue;

            if (hit.collider.transform.root == transform.root) continue;

            if ((bulletIgnoredLayers.value & (1 << hit.collider.gameObject.layer)) != 0) continue;

            PlayerMovement movementHit = hit.collider.GetComponentInParent<PlayerMovement>();
            if (movementHit != null) continue;

            validHit = hit;
            validHitFound = true;
            break;
        }

        if (validHitFound)
        {
            RaycastHit hit = validHit;

            Debug.DrawLine(firePoint.position, hit.point, Color.red, 1f);
            SpawnBulletTrail(hit.point);

            ZombieHealth zombieHealth = hit.collider.GetComponentInParent<ZombieHealth>();

            if (zombieHealth != null)
            {
                int finalDamage = ApplyDistanceFalloff(damage, hit.distance);
                zombieHealth.TakeDamage(finalDamage, hit.point, hit.normal);

                if (playerScore != null)
                    playerScore.RegistrarImpactoServerRpc();

                return;
            }

            if (DecalManager.Instance != null)
            {
                DecalManager.Instance.SpawnBulletHole(hit.point, hit.normal, hit.collider);
            }
        }
        else
        {
            Vector3 missPoint = firePoint.position + spreadDirection * range;
            SpawnBulletTrail(missPoint);
            Debug.DrawRay(firePoint.position, spreadDirection * range, Color.yellow, 1f);
        }
    }

    // =========================================================
    // BULLET TRAIL
    // =========================================================

    private void SpawnBulletTrail(Vector3 targetPoint)
    {
        if (bulletTrailPrefab == null || firePoint == null) return;
        GameObject trailObject = Instantiate(bulletTrailPrefab, firePoint.position, Quaternion.identity);
        BulletTrail trail = trailObject.GetComponent<BulletTrail>();
        if (trail != null) trail.Init(targetPoint);
    }

    // =========================================================
    // DIRECCIÓN CON DISPERSIÓN
    // =========================================================

    private Vector3 ApplySpreadToDirection(Vector3 baseDirection, float spreadDegrees)
    {
        if (spreadDegrees <= 0f) return baseDirection;
        float randomX = Random.Range(-spreadDegrees, spreadDegrees);
        float randomY = Random.Range(-spreadDegrees, spreadDegrees);
        Quaternion spreadRotation = Quaternion.Euler(randomY, randomX, 0f);
        return spreadRotation * baseDirection;
    }
}