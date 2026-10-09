
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
    [SerializeField] private CameraRecoil cameraRecoil;
    [SerializeField] private WeaponRecoil weaponRecoil;
    [SerializeField] private float recoilIntensityMultiplier = 1f;

    [Tooltip("Qué curvas de recoil del original usa esta arma: AR, SMG o Handgun.")]
    [SerializeField] private RecoilPreset recoilPreset = RecoilPreset.SMG;

    private HUDController hudController;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip emptySound;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private AudioClip reloadEmptySound;

    [Header("Audio - Recarga por partes")]
    [SerializeField] private AudioClip reloadOpenSound;
    [SerializeField] private AudioClip reloadInsertSound;
    [SerializeField] private AudioClip reloadCloseSound;
    [SerializeField] private AudioClip reloadBoltOpenSound;
    [SerializeField] private AudioClip reloadBoltCloseSound;

    [Header("Pistola")]
    [SerializeField] private bool isAutomatic = false;
    [SerializeField] private float fireRate = 0.25f;
    [SerializeField] private float reloadTime = 1.5f;
    [SerializeField] private float range = 100f;
    [SerializeField] private int damage = 25;
    [SerializeField] private int maxAmmo = 12;

    [Tooltip("Balas de reserva de esta arma cuando la dificultad limita la reserva.")]
    [SerializeField] private int reserveAmmoCapacity = 120;

    [SerializeField] private string weaponName = "Pistola";

    [Header("Escopeta (perdigones)")]
    [SerializeField] private bool firesMultiplePellets = false;
    [SerializeField] private int pelletsPerShot = 8;
    [SerializeField] private float pelletSpreadAngle = 6f;

    [Header("Cerrojo")]
    [SerializeField] private bool requiresBoltActionAfterFire = false;
    [SerializeField] private float boltActionDuration = 1.3f;
    [SerializeField] private float boltActionDelay = 0.12f;

    private bool boltActionActive = false;

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

    [SerializeField] private Animator partsAnimator;

    private Animator Parts
    {
        get
        {
            if (partsAnimator == null)
                partsAnimator = GetComponent<Animator>();

            return partsAnimator != null &&
                   partsAnimator.runtimeAnimatorController != null
                ? partsAnimator
                : null;
        }
    }

    private void PartsTrigger(string n)
    {
        if (Parts != null)
            Parts.SetTrigger(n);
    }

    private void PartsBool(string n, bool v)
    {
        if (Parts != null)
            Parts.SetBool(n, v);
    }

    public bool IsAiming { get; private set; }

    public int CurrentAmmo => currentAmmo;

    /// <summary>Balas que entran en el cargador. Lo usa el HUD.</summary>
    public int CapacidadCargador => maxAmmo;

    // Propiedades usadas por la dificultad y el HUD.
    public int MaxAmmo => EffectiveMaxAmmo;

    private int appliedMaxAmmo;

    public int EffectiveMaxAmmo;

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

        currentAmmo = maxAmmo;
        EffectiveMaxAmmo = maxAmmo;

        if (playerCamera != null)
            defaultWorldFOV = playerCamera.fieldOfView;

        currentSpread = spreadIdle;

        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();

        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();

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

    // =========================================================
    // MUNICIÓN DE RESERVA
    // =========================================================

    private int reserveAmmo;
    private bool reserveInitialized;

    private DifficultySettings ActiveDifficulty =>
        RoundManager.Instance != null
            ? RoundManager.Instance.ActiveSettings
            : null;

    public bool UsesLimitedReserve =>
        ActiveDifficulty != null && ActiveDifficulty.limitedReserveAmmo;

    public int ReserveCapacity
    {
        get
        {
            float multiplier = ActiveDifficulty != null
                ? ActiveDifficulty.reserveAmmoMultiplier
                : 1f;

            return Mathf.Max(
                0,
                Mathf.RoundToInt(reserveAmmoCapacity * multiplier)
            );
        }
    }

    public int ReserveAmmo
    {
        get
        {
            EnsureReserveInitialized();
            return reserveAmmo;
        }
    }

    public void AddReserveAmmo(int amount)
    {
        if (!UsesLimitedReserve || amount <= 0)
            return;

        EnsureReserveInitialized();
        reserveAmmo = Mathf.Min(reserveAmmo + amount, ReserveCapacity);
    }

    public void RefillReserveAmmo()
    {
        if (!UsesLimitedReserve)
            return;

        reserveAmmo = ReserveCapacity;
        reserveInitialized = true;
    }

    public bool MunicionCompleta =>
        currentAmmo >= maxAmmo &&
        (!UsesLimitedReserve || ReserveAmmo >= ReserveCapacity);

    public void RestaurarMunicion()
    {
        currentAmmo = maxAmmo;
        RefillReserveAmmo();
    }

    private void EnsureReserveInitialized()
    {
        if (reserveInitialized || !UsesLimitedReserve)
            return;

        reserveAmmo = ReserveCapacity;
        reserveInitialized = true;
    }

    private bool TryConsumeReserve(int amount)
    {
        if (!UsesLimitedReserve)
            return true;

        EnsureReserveInitialized();

        if (reserveAmmo < amount)
            return false;

        reserveAmmo -= amount;
        return true;
    }

    private void FillMagazine()
    {
        int needed = maxAmmo - currentAmmo;

        if (needed <= 0)
            return;

        if (!UsesLimitedReserve)
        {
            currentAmmo = maxAmmo;
            return;
        }

        EnsureReserveInitialized();

        int loaded = Mathf.Min(needed, reserveAmmo);
        currentAmmo += loaded;
        reserveAmmo -= loaded;
    }

    public void AnimationAmmunitionFill()
    {
        FillMagazine();
    }

    public void AnimationInsertOneShell()
    {
        if (currentAmmo < maxAmmo && TryConsumeReserve(1))
            currentAmmo++;

        shellLoading = currentAmmo < maxAmmo &&
                       (!UsesLimitedReserve || reserveAmmo > 0);

        PlaySound(reloadInsertSound);

        Debug.Log(
            "[Weapon] Insertada bala. currentAmmo=" +
            currentAmmo + "/" + maxAmmo +
            " | ShellLoading=" + shellLoading
        );

        if (weaponAnimator != null)
            weaponAnimator.SetBool("ShellLoading", shellLoading);
    }

    public void AnimationReloadFinished()
    {
        if (!usesMultiPartReload)
        {
            FillMagazine();
        }
        else if (
            currentAmmo != maxAmmo &&
            (!UsesLimitedReserve || ReserveAmmo > 0)
        )
        {
            Debug.LogWarning(
                "[Weapon] AnimationReloadFinished: el loop bala por bala terminó con " +
                currentAmmo + "/" + maxAmmo +
                " — revisá las condiciones ShellLoading en Insert A/B."
            );

            FillMagazine();
        }

        isReloading = false;
        shellLoading = false;

        if (weaponAnimator != null)
            weaponAnimator.SetBool("ShellLoading", false);
    }

    public void AnimationReloadOpen()
    {
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
        if (IsAiming && playerMovement != null && playerMovement.IsSprinting)
            IsAiming = false;

        if (isChambering && Time.time >= chamberReadyTime)
        {
            isChambering = false;
            boltActionActive = false;

            if (weaponAnimator != null)
                weaponAnimator.SetBool("BoltAction", false);
        }

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
        Debug.Log(
            "[Weapon] OnAimStarted llamado. InputLocked=" + InputLocked +
            " | isReloading=" + isReloading +
            " | IsSprinting=" +
            (playerMovement != null && playerMovement.IsSprinting)
        );

        if (PauseController.LocalPlayerPaused)
            return;

        if (InputLocked || isReloading)
            return;

        if (playerMovement != null && playerMovement.IsSprinting)
            return;

        IsAiming = true;
    }

    private void OnAimCanceled(InputAction.CallbackContext context)
    {
        IsAiming = false;
    }

    private void UpdateAimFOV()
    {
        if (playerCamera == null)
            return;

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
        if (characterController == null)
            return;

        if (IsAiming)
        {
            currentSpread = 0f;
            return;
        }

        if (!characterController.isGrounded)
            return;

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

        currentSpread = Mathf.MoveTowards(
            currentSpread,
            targetSpread,
            recoverySpeed * Time.deltaTime
        );

        currentSpread = Mathf.Clamp(currentSpread, 0f, maxSpread);

        if (landingRecoveryTimer > 0f)
            landingRecoveryTimer -= Time.deltaTime;
    }

    private void UpdateJumpSpread()
    {
        if (characterController == null)
            return;

        bool isGrounded = characterController.isGrounded;

        if (wasGrounded && !isGrounded)
        {
            currentSpread = Mathf.Clamp(
                currentSpread + jumpSpread,
                0f,
                maxSpread
            );
        }

        if (!wasGrounded && isGrounded)
            landingRecoveryTimer = landingRecoveryDuration;

        wasGrounded = isGrounded;
    }

    private bool IsMovingOnGround()
    {
        if (characterController == null || !characterController.isGrounded)
            return false;

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

    private readonly AirTracker airTracker = new AirTracker();

    private static readonly int RunningHash =
        Animator.StringToHash("Running");

    private RuntimeAnimatorController runningParamCheckedFor;
    private bool hasRunningParam;
    private float nextRunningParamCheck;
    private bool lastLoggedSprint;

    [Header("Diagnóstico")]
    [Tooltip("Muestra información del parámetro Running en la consola.")]
    [SerializeField] private bool debugRunning = false;

    private bool AnimatorHasRunningParam()
    {
        if (weaponAnimator == null)
            return false;

        RuntimeAnimatorController controller =
            weaponAnimator.runtimeAnimatorController;

        if (hasRunningParam && controller == runningParamCheckedFor)
            return true;

        if (
            controller == runningParamCheckedFor &&
            Time.unscaledTime < nextRunningParamCheck
        )
        {
            return false;
        }

        runningParamCheckedFor = controller;
        nextRunningParamCheck = Time.unscaledTime + 0.5f;
        hasRunningParam = false;

        if (controller != null && weaponAnimator.isInitialized)
        {
            foreach (AnimatorControllerParameter p in weaponAnimator.parameters)
            {
                if (
                    p.nameHash == RunningHash &&
                    p.type == AnimatorControllerParameterType.Bool
                )
                {
                    hasRunningParam = true;
                    break;
                }
            }

            if (debugRunning)
            {
                Debug.Log(
                    "[Weapon] Controller del arma: '" + controller.name +
                    "' | parámetro Running (bool): " +
                    (hasRunningParam
                        ? "SÍ"
                        : "NO, este no es el controller modificado")
                );
            }
        }

        return hasRunningParam;
    }

    private void UpdateAnimatorParams()
    {
        if (weaponAnimator == null)
            return;

        weaponAnimator.SetBool("IsEmpty", currentAmmo <= 0);

        if (characterController == null)
            return;

        Vector3 horizontalVelocity = new Vector3(
            characterController.velocity.x,
            0f,
            characterController.velocity.z
        );

        float speed = horizontalVelocity.magnitude;

        airTracker.Tick(characterController, Time.deltaTime);

        if (AnimatorHasRunningParam())
        {
            bool sprintNow =
                playerMovement != null && playerMovement.IsSprinting;

            weaponAnimator.SetBool(RunningHash, sprintNow);

            if (debugRunning && sprintNow != lastLoggedSprint)
            {
                lastLoggedSprint = sprintNow;

                Debug.Log(
                    "[Weapon] IsSprinting=" + sprintNow +
                    " | Running en el Animator=" +
                    weaponAnimator.GetBool(RunningHash)
                );
            }

            if (airTracker.IsAirborne && !sprintNow)
                speed = 0f;
        }

        weaponAnimator.SetFloat("Speed", speed, 0.15f, Time.deltaTime);
        weaponAnimator.SetBool("IsAiming", IsAiming);

        UpdateAimingBlend();
    }

    private void UpdateAimingBlend()
    {
        if (weaponAnimator == null)
            return;

        float target = IsAiming ? 1f : 0f;

        aimingBlend = Mathf.MoveTowards(
            aimingBlend,
            target,
            aimingBlendSpeed * Time.deltaTime
        );

        weaponAnimator.SetFloat("Aiming", aimingBlend);
    }

    // =========================================================
    // DISPARO
    // =========================================================

    private void OnFireSemiAuto(InputAction.CallbackContext context)
    {
        Debug.Log("[Weapon] OnFireSemiAuto llamado. InputLocked=" + InputLocked);
        TryFire();
    }

    private void TryFire()
    {
        if (PauseController.LocalPlayerPaused)
            return;

        if (InputLocked)
        {
            Debug.Log("[Weapon] TryFire bloqueado: InputLocked=true");
            return;
        }

        if (Time.timeScale == 0f)
        {
            Debug.Log("[Weapon] TryFire bloqueado: Time.timeScale == 0");
            return;
        }

        if (
            EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null
        )
        {
            Debug.Log(
                "[Weapon] TryFire bloqueado: hay un objeto de UI seleccionado -> " +
                EventSystem.current.currentSelectedGameObject.name
            );

            return;
        }

        if (isReloading)
        {
            Debug.Log("[Weapon] TryFire bloqueado: isReloading=true");
            return;
        }

        if (isChambering)
        {
            Debug.Log("[Weapon] TryFire bloqueado: isChambering=true");
            return;
        }

        if (playerMovement != null && playerMovement.IsSprinting)
        {
            Debug.Log("[Weapon] TryFire bloqueado: IsSprinting=true");
            return;
        }

        if (Time.time < nextFireTime)
            return;

        if (currentAmmo <= 0)
        {
            PlaySound(emptySound);

            if (weaponAnimator != null)
                weaponAnimator.SetTrigger("FireEmpty");

            nextFireTime = Time.time + fireRate;
            return;
        }

        nextFireTime = Time.time + fireRate;
        currentAmmo--;

        if (playerScore != null)
            playerScore.RegistrarDisparoServerRpc();

        currentSpread = Mathf.Clamp(
            currentSpread + spreadIncreasePerShot,
            0f,
            maxSpread
        );

        if (cameraRecoil != null)
            cameraRecoil.Fire(recoilPreset, IsAiming, recoilIntensityMultiplier);

        if (weaponRecoil != null)
            weaponRecoil.Fire(recoilPreset, IsAiming, recoilIntensityMultiplier);

        PlaySound(shootSound);

        if (weaponAnimator != null)
            weaponAnimator.SetTrigger("Fire");

        muzzle?.PlayEffect();

        Shoot();

        if (requiresBoltActionAfterFire)
        {
            isChambering = true;
            chamberReadyTime = Time.time + boltActionDuration;
            boltActionActive = true;

            if (weaponAnimator != null)
                weaponAnimator.SetBool("BoltAction", true);
        }
    }

    private IEnumerator PlayBoltActionAfterFire()
    {
        yield return new WaitForSeconds(boltActionDelay);

        if (weaponAnimator != null)
        {
            Debug.Log(
                "[Weapon] Enviando BoltAction después de " +
                boltActionDelay + " segundos."
            );

            weaponAnimator.SetTrigger("BoltAction");
        }
    }

    // =========================================================
    // RECARGA
    // =========================================================

    private void OnReload(InputAction.CallbackContext context)
    {
        if (PauseController.LocalPlayerPaused)
            return;

        if (InputLocked || isReloading)
            return;

        if (currentAmmo == maxAmmo)
            return;

        if (UsesLimitedReserve && ReserveAmmo <= 0)
            return;

        if (playerMovement != null && playerMovement.IsSprinting)
            return;

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

            shellLoading = usesMultiPartReload && currentAmmo < maxAmmo;
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
        if (playerCamera == null)
            return;

        Vector3 spreadDirection = ApplySpreadToDirection(
            playerCamera.transform.forward,
            spreadDegrees
        );

        Ray ray = new Ray(
            playerCamera.transform.position,
            spreadDirection
        );

        RaycastHit[] hits = Physics.RaycastAll(ray, range);

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        bool validHitFound = false;
        RaycastHit validHit = default;

        foreach (RaycastHit hit in hits)
        {
            PlayerHealth playerHit =
                hit.collider.GetComponentInParent<PlayerHealth>();

            if (playerHit != null)
                continue;

            if (hit.collider.transform.root == transform.root)
                continue;

            if (
                (bulletIgnoredLayers.value &
                 (1 << hit.collider.gameObject.layer)) != 0
            )
            {
                continue;
            }

            PlayerMovement movementHit =
                hit.collider.GetComponentInParent<PlayerMovement>();

            if (movementHit != null)
                continue;

            validHit = hit;
            validHitFound = true;
            break;
        }

        if (validHitFound)
        {
            RaycastHit hit = validHit;

            Debug.Log("Impacto en: " + hit.collider.name);

            if (firePoint != null)
                Debug.DrawLine(firePoint.position, hit.point, Color.red, 1f);

            SpawnBulletTrail(hit.point);

            // Daño a zombies.
            ZombieHealth zombieHealth =
                hit.collider.GetComponentInParent<ZombieHealth>();

            if (zombieHealth != null)
            {
                int finalDamage = ApplyDistanceFalloff(damage, hit.distance);

                zombieHealth.TakeDamage(
                    finalDamage,
                    hit.point,
                    hit.normal
                );

                if (playerScore != null)
                    playerScore.RegistrarImpactoServerRpc();

                return;
            }

            // EASTER EGG: permite que los disparos dañen los objetos
            // que tengan el componente EasterEgg en el collider o sus padres.
            EasterEgg easterEgg =
                hit.collider.GetComponentInParent<EasterEgg>();

            if (easterEgg != null)
            {
                easterEgg.TakeDamage(
                    damage,
                    hit.point,
                    hit.normal
                );

                if (playerScore != null)
                    playerScore.RegistrarImpactoServerRpc();

                return;
            }

            // Impacto normal contra el escenario.
            if (DecalManager.Instance != null)
            {
                DecalManager.Instance.SpawnBulletHole(
                    hit.point,
                    hit.normal,
                    hit.collider
                );
            }
        }
        else
        {
            Vector3 missPoint =
                (firePoint != null ? firePoint.position : ray.origin)
                + spreadDirection * range;

            SpawnBulletTrail(missPoint);

            Debug.DrawRay(
                ray.origin,
                spreadDirection * range,
                Color.yellow,
                1f
            );
        }
    }

    // =========================================================
    // BULLET TRAIL
    // =========================================================

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
            trail.Init(targetPoint);
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

        float randomX = Random.Range(-spreadDegrees, spreadDegrees);
        float randomY = Random.Range(-spreadDegrees, spreadDegrees);

        Quaternion spreadRotation = Quaternion.Euler(
            randomY,
            randomX,
            0f
        );

        return spreadRotation * baseDirection;
    }
}