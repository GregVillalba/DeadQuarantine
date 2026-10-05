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
    [SerializeField] private float recoilIntensityMultiplier = 1f; // <- subir en la escopeta
    [Tooltip("Qué curvas de recoil del original usa esta arma: AR (rifles), SMG (subfusiles) o Handgun (pistolas, casi sin recoil). " +
             "Al apuntar, el arma baja a 35% y la cámara queda igual: lo maneja cada script con los valores del original.")]
    [SerializeField] private RecoilPreset recoilPreset = RecoilPreset.SMG;

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
    [Tooltip("Balas de reserva de ESTA arma cuando la dificultad limita la reserva (ej.: rifle 120, pistola 100). " +
             "La dificultad la puede multiplicar (Reserve Ammo Multiplier). En Normal (recargas ilimitadas) no se usa.")]
    [SerializeField] private int reserveAmmoCapacity = 120;
    [SerializeField] private string weaponName = "Pistola";

    [Header("Escopeta (perdigones)")] // <- NUEVO
    [SerializeField] private bool firesMultiplePellets = false; // <- NUEVO: tildar solo en la Shotgun
    [SerializeField] private int pelletsPerShot = 8; // <- NUEVO
    [SerializeField] private float pelletSpreadAngle = 6f; // <- NUEVO: dispersión propia del perdigón, se suma a currentSpread

    [Header("Cerrojo (Sniper de cerrojo manual)")] // <- NUEVO
    [SerializeField] private bool requiresBoltActionAfterFire = false; // <- NUEVO: tildar solo en el Sniper de cerrojo
    [SerializeField] private float boltActionDuration = 1.3f; // <- NUEVO: debe igualar la duración real del clip Reload_Bolt
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

    [SerializeField] private Animator partsAnimator;   // vacío = usa el Animator de este mismo objeto

    private Animator Parts
    {
        get
        {
            if (partsAnimator == null) partsAnimator = GetComponent<Animator>();
            return (partsAnimator != null && partsAnimator.runtimeAnimatorController != null) ? partsAnimator : null;
        }
    }
    private void PartsTrigger(string n) { if (Parts != null) Parts.SetTrigger(n); }
    private void PartsBool(string n, bool v) { if (Parts != null) Parts.SetBool(n, v); }

    public bool IsAiming { get; private set; }

    public int CurrentAmmo => currentAmmo;
    // ----- CAMBIO COMBINADO: PROPIEDADES DE DIFICULTAD (DEL COMPAÑERO) -----
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
    public bool ShellLoading => shellLoading; // <- NUEVO
    public bool IsChambering => isChambering; // <- NUEVO: true durante el ciclo de cerrojo post-disparo

    private PlayerControls controls;

    private int currentAmmo;
    private float nextFireTime;
    private bool isReloading;
    private bool shellLoading; // <- NUEVO
    private bool isChambering; // <- NUEVO
    private float chamberReadyTime; // <- NUEVO

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

        if (playerCamera != null)
            defaultWorldFOV = playerCamera.fieldOfView;

        currentSpread = spreadIdle;

        // Si faltan en el Inspector, se buscan: sin playerMovement IsSprinting siempre daba false.
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

        // Semiautomático: dispara una sola vez por cada apretada de botón.
        // Automático (Rifle): el disparo se maneja en Update() mientras se mantiene el botón.
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

    // Misma fuente que usa PlayerHealth: en multiplayer los clientes ven la dificultad del host.
    private DifficultySettings ActiveDifficulty =>
        RoundManager.Instance != null ? RoundManager.Instance.ActiveSettings : null;

    /// <summary>true = en esta dificultad recargar gasta la reserva (Difícil). false = recargas ilimitadas.</summary>
    public bool UsesLimitedReserve => ActiveDifficulty != null && ActiveDifficulty.limitedReserveAmmo;

    /// <summary>Reserva máxima de esta arma con la dificultad actual.</summary>
    public int ReserveCapacity
    {
        get
        {
            float multiplier = ActiveDifficulty != null ? ActiveDifficulty.reserveAmmoMultiplier : 1f;
            return Mathf.Max(0, Mathf.RoundToInt(reserveAmmoCapacity * multiplier));
        }
    }

    /// <summary>Balas que quedan en la reserva. Solo es relevante si UsesLimitedReserve es true.</summary>
    public int ReserveAmmo
    {
        get
        {
            EnsureReserveInitialized();
            return reserveAmmo;
        }
    }

    /// <summary>
    /// Suma balas a la reserva (para tienda, cajas de munición...). No pasa de la capacidad.
    /// Con reserva ilimitada no hace nada.
    /// </summary>
    public void AddReserveAmmo(int amount)
    {
        if (!UsesLimitedReserve || amount <= 0)
            return;

        EnsureReserveInitialized();
        reserveAmmo = Mathf.Min(reserveAmmo + amount, ReserveCapacity);
    }

    /// <summary>Deja la reserva llena (por ejemplo al comprar munición completa).</summary>
    public void RefillReserveAmmo()
    {
        if (!UsesLimitedReserve)
            return;

        reserveAmmo = ReserveCapacity;
        reserveInitialized = true;
    }

    // Se inicializa al primer uso y no en Awake: las armas sin comprar nunca se activan, y en un cliente
    // la dificultad del host puede llegar unos frames después de que nace el jugador.
    private void EnsureReserveInitialized()
    {
        if (reserveInitialized || !UsesLimitedReserve)
            return;

        reserveAmmo = ReserveCapacity;
        reserveInitialized = true;
    }

    // Saca 'amount' balas de la reserva. Con reserva ilimitada siempre se puede.
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

    // Pasa balas de la reserva al cargador hasta llenarlo (o hasta quedarse sin reserva).
    // Se puede llamar más de una vez por recarga: la segunda vez el cargador ya está lleno
    // (o la reserva vacía) y no mueve nada, así que no descuenta de más.
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

    // NUEVO: la llama un Animation Event en cada vuelta del clip Reload_Insert.
    // Mientras falten balas, deja ShellLoading en true para que el Animator
    // vuelva a reproducir el mismo clip (loop bala por bala).
    public void AnimationInsertOneShell()
    {
        // Cada bala insertada sale de la reserva (con reserva ilimitada siempre hay).
        if (currentAmmo < maxAmmo && TryConsumeReserve(1))
            currentAmmo++;

        // Si se acaba la reserva el loop termina aunque el cargador no esté lleno.
        shellLoading = currentAmmo < maxAmmo && (!UsesLimitedReserve || reserveAmmo > 0);

        PlaySound(reloadInsertSound); // <- nuevo

        Debug.Log("[Weapon] Insertada bala. currentAmmo=" + currentAmmo + "/" + maxAmmo + " | ShellLoading=" + shellLoading);

        if (weaponAnimator != null)
            weaponAnimator.SetBool("ShellLoading", shellLoading);
    }

    public void AnimationReloadFinished()
    {
        if (!usesMultiPartReload)
        {
            FillMagazine();
        }
        else if (currentAmmo != maxAmmo && (!UsesLimitedReserve || ReserveAmmo > 0))
        {
            Debug.LogWarning(
                "[Weapon] AnimationReloadFinished: el loop bala por bala terminó con " +
                currentAmmo + "/" + maxAmmo +
                " — revisá las condiciones ShellLoading en Insert A/B, se cortó antes de tiempo."
            );
            FillMagazine(); // red de seguridad para no dejar el arma rota
        }

        isReloading = false;
        shellLoading = false;

        if (weaponAnimator != null)
            weaponAnimator.SetBool("ShellLoading", false);
    }

    // Llamado por Animation Event, al abrir la recámara/tubo al empezar a recargar.
    public void AnimationReloadOpen()
    {
        PlaySound(reloadOpenSound);
    }

    // Llamado por Animation Event, al cerrar después de insertar todas las balas.
    public void AnimationReloadClose()
    {
        PlaySound(reloadCloseSound);
    }

    // Llamado por Animation Event, en el tirón hacia atrás del bombeo
    // (tanto después de disparar como al final de la recarga).
    public void AnimationBoltOpen()
    {
        PlaySound(reloadBoltOpenSound);
    }

    // Llamado por Animation Event, en el golpe hacia adelante del bombeo.
    // Si este bombeo es el último paso de la recarga, acá es donde
    // realmente termina — por eso cierra isReloading.
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

        // NUEVO: libera el disparo cuando termina el ciclo de cerrojo.
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
        Debug.Log("[Weapon] OnAimStarted llamado. InputLocked=" + InputLocked +
            " | isReloading=" + isReloading +
            " | IsSprinting=" + (playerMovement != null && playerMovement.IsSprinting));

        if (PauseController.LocalPlayerPaused)
            return;

        if (InputLocked)
            return;

        if (isReloading)
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

        currentSpread = Mathf.MoveTowards(currentSpread, targetSpread, recoverySpeed * Time.deltaTime);
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
            currentSpread = Mathf.Clamp(currentSpread + jumpSpread, 0f, maxSpread);

        if (!wasGrounded && isGrounded)
            landingRecoveryTimer = landingRecoveryDuration;

        wasGrounded = isGrounded;
    }

    private bool IsMovingOnGround()
    {
        if (characterController == null)
            return false;

        if (!characterController.isGrounded)
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

    private static readonly int RunningHash = Animator.StringToHash("Running");
    private RuntimeAnimatorController runningParamCheckedFor;
    private bool hasRunningParam;
    private float nextRunningParamCheck;
    private bool lastLoggedSprint;

    [Header("Diagnóstico")]
    [Tooltip("Escribe en la consola qué controller está usando el arma, si tiene el parámetro Running y cuándo cambia IsSprinting.")]
    [SerializeField] private bool debugRunning = false;

    // Mira si el controller actual tiene el bool "Running". Un resultado positivo se guarda;
    // uno negativo se vuelve a comprobar cada 0.5 s, porque justo después de cambiar de arma
    // el Animator puede no estar listo todavía y devolver una lista de parámetros vacía.
    private bool AnimatorHasRunningParam()
    {
        RuntimeAnimatorController controller = weaponAnimator.runtimeAnimatorController;

        if (hasRunningParam && controller == runningParamCheckedFor)
            return true;

        if (controller == runningParamCheckedFor && Time.unscaledTime < nextRunningParamCheck)
            return false;

        runningParamCheckedFor = controller;
        nextRunningParamCheck = Time.unscaledTime + 0.5f;
        hasRunningParam = false;

        if (controller != null && weaponAnimator.isInitialized)
        {
            foreach (AnimatorControllerParameter p in weaponAnimator.parameters)
            {
                if (p.nameHash == RunningHash && p.type == AnimatorControllerParameterType.Bool)
                {
                    hasRunningParam = true;
                    break;
                }
            }

            if (debugRunning)
            {
                Debug.Log("[Weapon] Controller del arma: '" + controller.name + "' | parámetro Running (bool): " +
                          (hasRunningParam ? "SÍ" : "NO, este no es el controller modificado"));
            }
        }

        return hasRunningParam;
    }

    private void UpdateAnimatorParams()
    {
        if (weaponAnimator == null)
            return;

        // IsEmpty refleja SIEMPRE si el cargador está vacío, no solo al apretar recargar.
        // Antes solo se escribía en OnReload, así que la capa NoAmmoADD (Slide_Back) nunca se
        // activaba al gastar la última bala. Además, al cambiar de arma el Animator resetea sus
        // parámetros, y esto lo vuelve a poner bien en el siguiente frame.
        // Cuando AnimationAmmunitionFill carga el cargador, pasa a false y la corredera vuelve sola.
        weaponAnimator.SetBool("IsEmpty", currentAmmo <= 0);

        if (characterController == null)
            return;

        // Solo velocidad horizontal — la caída (eje Y) no debe contar como "caminar".
        Vector3 horizontalVelocity = new Vector3(
            characterController.velocity.x,
            0f,
            characterController.velocity.z
        );

        float speed = horizontalVelocity.magnitude;

        // El Animator ahora tiene un bool "Running" (igual que el original de Infima):
        // la pose de correr depende de que estés corriendo (IsSprinting), ya no de
        // Speed > 7. Así saltar no te saca de Running y aterrizar no obliga a esperar
        // 0.5 s para volver. Si el controller no tiene el parámetro, todo queda como antes.
        airTracker.Tick(characterController, Time.deltaTime);

        if (AnimatorHasRunningParam())
        {
            weaponAnimator.SetBool(RunningHash, playerMovement != null && playerMovement.IsSprinting);

            if (debugRunning)
            {
                bool sprintNow = playerMovement != null && playerMovement.IsSprinting;

                if (sprintNow != lastLoggedSprint)
                {
                    lastLoggedSprint = sprintNow;
                    Debug.Log("[Weapon] IsSprinting=" + sprintNow + " | Running en el Animator=" + weaponAnimator.GetBool(RunningHash));
                }
            }

            // En el aire, caminar vuelve suave a idle (sin balanceo de caminata flotando).
            // OJO: corriendo NO se pone en 0, si no la pose de correr se corta al saltar.
            if (airTracker.IsAirborne && !(playerMovement != null && playerMovement.IsSprinting))
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

        aimingBlend = Mathf.MoveTowards(aimingBlend, target, aimingBlendSpeed * Time.deltaTime);

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

        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null)
        {
            Debug.Log("[Weapon] TryFire bloqueado: hay un objeto de UI seleccionado -> " +
                EventSystem.current.currentSelectedGameObject.name);
            return;
        }

        if (isReloading)
        {
            Debug.Log("[Weapon] TryFire bloqueado: isReloading=true");
            return;
        }

        // NUEVO: mientras se acciona el cerrojo tras el disparo anterior, no se puede volver a disparar.
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
            return; // este es normal (cadencia), no hace falta loguearlo

        if (currentAmmo <= 0)
        {
            PlaySound(emptySound);

            if (weaponAnimator != null)
                weaponAnimator.SetTrigger("FireEmpty");

            // Actualizamos nextFireTime igual: si no, con el botón
            // mantenido en un arma automática spamea el sonido de vacío cada frame.
            nextFireTime = Time.time + fireRate;
            return;
        }

        nextFireTime = Time.time + fireRate;
        currentAmmo--;

        if (playerScore != null)
            playerScore.RegistrarDisparoServerRpc();

        currentSpread = Mathf.Clamp(currentSpread + spreadIncreasePerShot, 0f, maxSpread);

        if (cameraRecoil != null)
            cameraRecoil.Fire(recoilPreset, IsAiming, recoilIntensityMultiplier);

        if (weaponRecoil != null)
            weaponRecoil.Fire(recoilPreset, IsAiming, recoilIntensityMultiplier);

        PlaySound(shootSound);

        if (weaponAnimator != null)
            weaponAnimator.SetTrigger("Fire");

        muzzle?.PlayEffect();

        Shoot();

        // NUEVO: arma de cerrojo manual (Sniper) — hay que accionarlo antes de volver a disparar.
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
            Debug.Log("[Weapon] Enviando BoltAction después de " + boltActionDelay + " segundos.");
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

        if (InputLocked)
            return;

        if (isReloading)
            return;

        if (currentAmmo == maxAmmo)
            return;

        // Reserva limitada (Difícil): sin balas de reserva no hay nada que recargar.
        if (UsesLimitedReserve && ReserveAmmo <= 0)
            return;

        // NO RECARGAR MIENTRAS CORRE.
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
        // NUEVO: la escopeta dispara varios perdigones por vez, cada uno con
        // su propia dispersión aleatoria. El resto de las armas siguen
        // disparando un solo proyectil, como antes.
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

            PlayerMovement movementHit = hit.collider.GetComponentInParent<PlayerMovement>();

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
                DecalManager.Instance.SpawnBulletHole(
                    hit.point,
                    hit.normal,
                    hit.collider
                );
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
        if (bulletTrailPrefab == null || firePoint == null)
            return;

        GameObject trailObject = Instantiate(bulletTrailPrefab, firePoint.position, Quaternion.identity);

        BulletTrail trail = trailObject.GetComponent<BulletTrail>();

        if (trail != null)
            trail.Init(targetPoint);
    }

    // =========================================================
    // DIRECCIÓN CON DISPERSIÓN
    // =========================================================

    private Vector3 ApplySpreadToDirection(Vector3 baseDirection, float spreadDegrees)
    {
        if (spreadDegrees <= 0f)
            return baseDirection;

        float randomX = Random.Range(-spreadDegrees, spreadDegrees);
        float randomY = Random.Range(-spreadDegrees, spreadDegrees);

        Quaternion spreadRotation = Quaternion.Euler(randomY, randomX, 0f);

        return spreadRotation * baseDirection;
    }
}