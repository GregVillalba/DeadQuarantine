using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections;

// El raycast de ADS se resuelve despues de que CameraRecoil y el rig FPS
// hayan actualizado la pose FINAL que se renderiza en pantalla.
[DefaultExecutionOrder(1000)]
public partial class Weapon : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera weaponCamera;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerCharacter playerCharacter;
    [SerializeField] private Animator weaponAnimator;
    [SerializeField] private Muzzle muzzle;
    [SerializeField] private GameObject bulletTrailPrefab;
    private bool headshotRegistradoEnDisparo;

    [Header("Recoil de cámara")]
    [SerializeField] private CameraRecoil cameraRecoil;
    [SerializeField] private WeaponRecoil weaponRecoil;
    [SerializeField] private float recoilIntensityMultiplier = 1f;

    [Tooltip("Qué curvas de recoil del original usa esta arma: AR, SMG o Handgun.")]
    [SerializeField] private RecoilPreset recoilPreset = RecoilPreset.SMG;

    private HUDController hudController;
    private WeaponSwitcher weaponSwitcher;

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

    [Header("Ráfaga")]
    [Tooltip("Si está activo, una pulsación dispara Burst Count proyectiles y no usa fuego automático por mantener pulsado.")]
    [SerializeField] private bool usesBurstFire = false;
    [Min(1)] [SerializeField] private int burstCount = 3;
    [Min(0.01f)] [SerializeField] private float burstShotInterval = 0.07f;
    [Min(0.01f)] [SerializeField] private float burstCooldown = 0.25f;

    [Header("Recarga exacta por arma")]
    [Tooltip("Solo para armas nuevas. Hace que Reload Time sea la duración base real de la recarga (antes del modificador de clase).")]
    [SerializeField] private bool enforceReloadDuration = false;
    [Tooltip("Multiplicador del clip de recarga normal calculado por el integrador para que visualmente dure Reload Time.")]
    [Min(0.01f)] [SerializeField] private float reloadClipSpeedMultiplier = 1f;
    [Tooltip("Multiplicador del clip de recarga desde vacío calculado por el integrador.")]
    [Min(0.01f)] [SerializeField] private float reloadEmptyClipSpeedMultiplier = 1f;
    [Tooltip("Multiplicador del Animator de PARTES del arma en primera persona durante una recarga normal.")]
    [Min(0.01f)] [SerializeField] private float partsReloadClipSpeedMultiplier = 1f;
    [Tooltip("Multiplicador del Animator de PARTES del arma en primera persona durante una recarga desde vacío.")]
    [Min(0.01f)] [SerializeField] private float partsReloadEmptyClipSpeedMultiplier = 1f;
    [Tooltip("Multiplicador de la animación corporal TP de recarga normal. Separado del FP para que ambos terminen al mismo tiempo.")]
    [Min(0.01f)] [SerializeField] private float tpReloadClipSpeedMultiplier = 1f;
    [Tooltip("Multiplicador de la animación corporal TP de recarga desde vacío.")]
    [Min(0.01f)] [SerializeField] private float tpReloadEmptyClipSpeedMultiplier = 1f;
    [Tooltip("Multiplicador del Animator de PARTES del arma en tercera persona durante una recarga normal.")]
    [Min(0.01f)] [SerializeField] private float tpPartsReloadClipSpeedMultiplier = 1f;
    [Tooltip("Multiplicador del Animator de PARTES del arma en tercera persona durante una recarga desde vacío.")]
    [Min(0.01f)] [SerializeField] private float tpPartsReloadEmptyClipSpeedMultiplier = 1f;
    [Tooltip("LEGACY: la duración real de recarga la controla el clip del Animator. Se conserva para no romper prefabs existentes.")]
    [SerializeField] private float reloadTime = 1.5f;
    [SerializeField] private float range = 100f;
    [SerializeField] private int damage = 25;
    [SerializeField] private int maxAmmo = 12;

    [Tooltip("Balas máximas de reserva de esta arma.")]
    [SerializeField] private int reserveAmmoCapacity = 120;
    [Tooltip("Las armas nuevas usan siempre este límite exacto, independientemente de la dificultad.")]
    [SerializeField] private bool forceReserveLimit = false;

    [SerializeField] private string weaponName = "Pistola";

    [Header("Escopeta (perdigones)")]
    [SerializeField] private bool firesMultiplePellets = false;
    [SerializeField] private int pelletsPerShot = 8;
    [SerializeField] private float pelletSpreadAngle = 6f;
    [Tooltip("0 = perdigones repartidos parejo dentro del círculo. 1 = muy concentrados hacia el centro (más como una escopeta real).")]
    [Range(0f, 1f)] [SerializeField] private float pelletCenterBias = 0.4f;

    [Header("Cerrojo")]
    [SerializeField] private bool requiresBoltActionAfterFire = false;
    [SerializeField] private float boltActionDuration = 1.3f;
    [SerializeField] private float boltActionDelay = 0.12f;

    private bool boltActionActive = false;
    private bool pendingReload;
    private Coroutine burstRoutine;
    private Coroutine reloadFallbackRoutine;
    private bool burstActive;
    private bool reloadStartedEmpty;
    private float reloadCanFillAt;
    private float reloadCanFinishAt;
    private float lastShellInsert = float.NegativeInfinity;
    private readonly System.Collections.Generic.List<Vector3> presentationTargets = new();
    private NetworkWeaponPresentation presentation;

    [Header("Aim")]
    [SerializeField] private float aimFOV = 50f;
    [SerializeField] private float aimTransitionSpeed = 10f;

    [Header("ADS - precisión de mirilla (armas nuevas)")]
    [Tooltip("Al apuntar, proyecta la dirección del cañón visible de WeaponCamera sobre la cámara jugable. " +
             "Esto vincula la trayectoria con la mirilla y con el recoil visual. Si el socket está mal orientado, usa el centro de la cámara.")]
    [SerializeField] private bool adsFireAlongVisualSight = false;

    [Tooltip("Solo para las armas cuyo ADS todavía queda descentrado. Compensa la proyección de WeaponCamera " +
             "SIN mover el arma, las manos ni sus AnimationClips. Afecta únicamente a la cámara de primera persona.")]
    [SerializeField] private bool adsCenterVisualSight = false;
    [Tooltip("Referencia de la mirilla del modelo (SOCKET_Default). Se usa cuando el eje del SOCKET_Muzzle no es válido.")]
    [SerializeField] private Transform adsSightReference;
    [Tooltip("Máxima corrección visual en unidades de viewport (0.10 = 10% del ancho/alto).")]
    [Range(0.01f, 0.45f)] [SerializeField] private float adsMaxViewportCorrection = 0.12f;
    [Range(0f, 0.25f)]
    [Tooltip("Solo Vector: cuanto baja el aro de la mira en ADS. 0.116 equivale al desfasaje de 85 px observado a 734 px de altura. No cambia las balas ni mueve brazos.")]
    [SerializeField] private float vectorAdsVerticalSightDown = 0.116f;
    private bool adsProjectionWasModified;

    // Solo los disparos ADS se posponen hasta LateUpdate. Fuera de ADS,
    // el comportamiento de disparo original permanece totalmente igual.
    private int pendingAdsRounds;


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
    // Read-only context for remote presentation; inactive FP models may not have run Awake.
    public float PresentationCharacterReloadSpeed => ClassReloadSpeedMultiplier;
    private WeaponAttachments attachmentRuntime;
    private WeaponAttachments RuntimeAttachments => attachmentRuntime ? attachmentRuntime : attachmentRuntime = GetComponent<WeaponAttachments>();
    public float AttachmentDamageMultiplier => RuntimeAttachments != null ? RuntimeAttachments.DamageMultiplier : 1f;
    public float AttachmentSpreadMultiplier => RuntimeAttachments != null ? RuntimeAttachments.SpreadMultiplier : 1f;
    public float AttachmentRecoilMultiplier => RuntimeAttachments != null ? RuntimeAttachments.RecoilMultiplier : 1f;
    public bool ShotIsSuppressed => RuntimeAttachments != null && RuntimeAttachments.Suppressed;
    public AudioClip PresentationShotSound => RuntimeAttachments != null && RuntimeAttachments.ShotSound ? RuntimeAttachments.ShotSound : shootSound;
    public float AttachmentAimFov => RuntimeAttachments != null ? RuntimeAttachments.AimFov(aimFOV) : aimFOV;
    public void SetAttachmentSight(Transform sight) { if(RuntimeAttachments!=null&&RuntimeAttachments.HasRuntimeCatalog)SetPackAttachmentSight(sight); }
    public UnityEngine.Audio.AudioMixerGroup PresentationMixer => audioSource ? audioSource.outputAudioMixerGroup : null;
    public GameObject PresentationTrail => bulletTrailPrefab;
    public float PresentationRange => range;
    public float EffectiveRange=>range*(RuntimeAttachments?RuntimeAttachments.RangeMultiplier:1);
    public float AttachmentAdsDuration=>RuntimeAttachments?RuntimeAttachments.AdsDurationMultiplier:1;
    public float AttachmentSway=>RuntimeAttachments?RuntimeAttachments.AdsSwayMultiplier:1;
    public float AttachmentIncomingShake=>IsAiming&&RuntimeAttachments?RuntimeAttachments.IncomingShakeMultiplier:1;
    public float AttachmentMovement=>(RuntimeAttachments?RuntimeAttachments.MobilityMultiplier:1)*(IsAiming&&RuntimeAttachments?RuntimeAttachments.AdsMovementMultiplier:1);
    public Vector2 AttachmentRecoilAxes=>RuntimeAttachments?RuntimeAttachments.RecoilAxes:Vector2.one;
    public float PresentationFireInterval => usesBurstFire ? burstShotInterval : fireRate;
    public float PresentationReloadSpeed(bool empty) =>
        ClassReloadSpeedMultiplier * (empty ? reloadEmptyClipSpeedMultiplier : reloadClipSpeedMultiplier);
    public float PresentationPartsReloadSpeed(bool empty) =>
        ClassReloadSpeedMultiplier * (empty ? partsReloadEmptyClipSpeedMultiplier : partsReloadClipSpeedMultiplier);
    public float PresentationThirdPersonReloadSpeed(bool empty) =>
        ClassReloadSpeedMultiplier * (empty ? tpReloadEmptyClipSpeedMultiplier : tpReloadClipSpeedMultiplier);
    public float PresentationThirdPersonPartsReloadSpeed(bool empty) =>
        ClassReloadSpeedMultiplier * (empty ? tpPartsReloadEmptyClipSpeedMultiplier : tpPartsReloadClipSpeedMultiplier);
    public uint PresentationShotSequence { get; private set; }
    public bool UsesBurstFire => usesBurstFire;
    // Keep the empty-reload clip and its speed until the animation completes.
    public bool PresentationEmpty => isReloading ? reloadStartedEmpty : currentAmmo == 0;
    public Camera AimCamera => playerCamera;
    public float HipFieldOfView=>defaultWorldFOV>0?defaultWorldFOV:playerCamera?playerCamera.fieldOfView:60;
    public Vector3 AimOrigin => playerCamera ? playerCamera.transform.position : transform.position;
    public AudioClip PresentationReloadSound(bool empty) => empty ? reloadEmptySound : reloadSound;
    public AudioClip PresentationEventSound(NetworkWeaponPresentation.WeaponEvent kind) => kind switch {
        NetworkWeaponPresentation.WeaponEvent.ReloadOpen=>reloadOpenSound,
        NetworkWeaponPresentation.WeaponEvent.Insert=>reloadInsertSound,
        NetworkWeaponPresentation.WeaponEvent.ReloadClose=>reloadCloseSound,
        NetworkWeaponPresentation.WeaponEvent.BoltOpen=>reloadBoltOpenSound,
        NetworkWeaponPresentation.WeaponEvent.BoltClose=>reloadBoltCloseSound,_=>null };
    public void PresentationEvent(NetworkWeaponPresentation.WeaponEvent kind)
    { if(!presentation)presentation=GetComponentInParent<NetworkWeaponPresentation>();presentation?.PublishEvent(this,kind); }
    // Overlay-camera coordinates must be mapped to the world camera before spawning physics or trails.
    public void PresentationPose(Transform socket,out Vector3 position,out Quaternion rotation)
    {
        position=socket.position;rotation=socket.rotation;
        if(playerCamera && weaponCamera && playerCamera!=weaponCamera) {
            position=playerCamera.ViewportToWorldPoint(weaponCamera.WorldToViewportPoint(socket.position));
            rotation=playerCamera.transform.rotation*Quaternion.Inverse(weaponCamera.transform.rotation)*socket.rotation;
        }
    }

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

    /// <summary>
    /// Dispersión real del arma en grados: la del momento (quieto, moviéndose, disparando, saltando)
    /// más el abanico de los perdigones si es una escopeta. Lo usa el HUD para el tamaño de la mira.
    /// </summary>
    public float CurrentSpreadAngle => (currentSpread + (firesMultiplePellets ? pelletSpreadAngle : 0f)) * AttachmentSpreadMultiplier;

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

    private bool inputLocked;

    public bool InputLocked
    {
        get => inputLocked;

        set
        {
            if (inputLocked == value)
                return;

            inputLocked = value;

            Debug.LogWarning(
                "[INPUT LOCK] " +
                gameObject.name +
                " -> " +
                value +
                "\n" +
                System.Environment.StackTrace,
                this
            );
        }
    }

    // =========================================================
    // VELOCIDAD DE RECARGA POR CLASE
    // =========================================================

    private bool reloadAnimatorBaseCaptured;
    private float baseWeaponAnimatorSpeed = 1f;
    private float basePartsAnimatorSpeed = 1f;

    /// <summary>
    /// 1 = Normal. 1.2 = 20% más rápido. 0.9 = 10% más lento.
    /// El dato viene del personaje seleccionado.
    /// </summary>
    private float ClassReloadSpeedMultiplier =>
        playerCharacter != null
            ? Mathf.Max(0.01f, playerCharacter.ReloadSpeedMultiplier)
            : 1f;

    private void CaptureReloadAnimatorBaseSpeeds()
    {
        if (reloadAnimatorBaseCaptured)
            return;

        reloadAnimatorBaseCaptured = true;

        if (weaponAnimator != null)
            baseWeaponAnimatorSpeed = Mathf.Max(0.01f, weaponAnimator.speed);

        Animator parts = Parts;

        if (parts != null && parts != weaponAnimator)
            basePartsAnimatorSpeed = Mathf.Max(0.01f, parts.speed);
    }

    private void ApplyReloadAnimationSpeed(bool empty)
    {
        CaptureReloadAnimatorBaseSpeeds();

        float armsMultiplier = empty ? reloadEmptyClipSpeedMultiplier : reloadClipSpeedMultiplier;
        float partsMultiplier = empty ? partsReloadEmptyClipSpeedMultiplier : partsReloadClipSpeedMultiplier;
        float classMultiplier = ClassReloadSpeedMultiplier;

        if (weaponAnimator != null)
            weaponAnimator.speed = baseWeaponAnimatorSpeed * classMultiplier * Mathf.Max(0.01f, armsMultiplier);

        Animator parts = Parts;

        if (parts != null && parts != weaponAnimator)
            parts.speed = basePartsAnimatorSpeed * classMultiplier * Mathf.Max(0.01f, partsMultiplier);
    }

    private void ResetReloadAnimationSpeed()
    {
        if (!reloadAnimatorBaseCaptured)
            return;

        if (weaponAnimator != null)
            weaponAnimator.speed = baseWeaponAnimatorSpeed;

        Animator parts = Parts;

        if (parts != null && parts != weaponAnimator)
            parts.speed = basePartsAnimatorSpeed;
    }

    private void Awake()
    {
        packPresentation=GetComponent<PackWeaponPresentation>();
        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);

        currentAmmo = maxAmmo;
        EffectiveMaxAmmo = maxAmmo;

        if (playerCamera != null)
            defaultWorldFOV = playerCamera.fieldOfView;

        currentSpread = spreadIdle;

        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();

        if (playerCharacter == null)
            playerCharacter = GetComponentInParent<PlayerCharacter>();

        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();

        hudController = GetComponentInParent<HUDController>();

        if (hudController == null)
            hudController = transform.root.GetComponentInChildren<HUDController>(true);

        weaponSwitcher = GetComponentInParent<WeaponSwitcher>();

        if (weaponSwitcher == null)
            weaponSwitcher = transform.root.GetComponentInChildren<WeaponSwitcher>(true);

        playerScore = GetComponentInParent<PlayerScore>();

        if (playerScore == null)
            playerScore = transform.root.GetComponentInChildren<PlayerScore>(true);

        if(!UseAuthoredRecoil)CaptureReloadAnimatorBaseSpeeds();
    }

    // La WeaponCamera es una cámara overlay; su raíz TAMBIÉN contiene las manos.
    // La cámara queda fija. Los perfiles DQ_OpticalSight alinean su rig visual de manos y arma.
    // Los demás perfiles conservan la corrección original por proyección y sus offsets manuales.
    private void LateUpdate()
    {
        // La correccion de proyeccion debe estar aplicada antes de calcular
        // en que pixel se ve la mirilla. La cámara conserva su transform.
        UpdateAdsVisualProjection();
        SyncIntegratedReloadParts();
        FlushPendingAdsShots();
    }

    private void SyncIntegratedReloadParts()
    {
        if (!UsesIntegratedVisual || !isReloading || usesMultiPartReload || weaponAnimator == null || Parts == null) return;
        int layer = weaponAnimator.GetLayerIndex("Reload");
        if (layer < 0) return;
        var state = weaponAnimator.GetCurrentAnimatorStateInfo(layer);
        string name = state.IsName("Reload Empty") ? "Reload Empty" : state.IsName("Reload") ? "Reload" : null;
        if (name != null && Parts.HasState(0, Animator.StringToHash(name)))
        {
            Parts.Play(name, 0, Mathf.Clamp01(state.normalizedTime));
            Parts.Update(0);
        }
    }

    private Vector3 integratedAimRigOffset, integratedAimFullOffset;
    private float integratedAimVelocity;
    private bool integratedAimHeld;
    private bool UsesIntegratedVisual => adsSightReference != null && adsSightReference.name == "DQ_OpticalSight";

    private void RestoreIntegratedAimPose()
    {
        RestorePackAim();
        if (weaponAnimator != null && integratedAimRigOffset != Vector3.zero)
            weaponAnimator.transform.localPosition -= integratedAimRigOffset;
        integratedAimRigOffset = Vector3.zero;
    }

    private void AlignIntegratedAimPose()
    {
        if(UsePackAim){ApplyPackAim();return;}
        RestoreIntegratedAimPose();
        if (adsSightReference == null || adsSightReference.name != "DQ_OpticalSight" ||
            weaponAnimator == null || weaponCamera == null || !weaponCamera.enabled ||
            aimingBlend <= 0.001f) return;
        Transform rig = weaponAnimator.transform;
        // Shift hands and gun together. The camera must remain outside this visual rig.
        if (weaponCamera.transform == rig || weaponCamera.transform.IsChildOf(rig)) return;
        if (IsAiming && !isReloading && !isChambering)
        {
            Vector3 sight = weaponCamera.transform.InverseTransformPoint(adsSightReference.position);
            Vector3 delta = weaponCamera.transform.TransformVector(new Vector3(-sight.x, -sight.y, 0f));
            integratedAimFullOffset = rig.parent != null ? rig.parent.InverseTransformVector(delta) : delta;
        }
        // Fade the last correction out; following the sight during a reload would fight the authored motion.
        integratedAimRigOffset = integratedAimFullOffset * Mathf.Clamp01(aimingBlend);
        rig.localPosition += integratedAimRigOffset;
    }

    private void UpdateAdsVisualProjection()
    {
        AlignIntegratedAimPose();
        if (UsesIntegratedVisual||UsePackAim)
        {
            // Physical alignment replaces the off-axis projection, including while leaving ADS.
            if (weaponCamera != null) weaponCamera.ResetProjectionMatrix();
            adsProjectionWasModified = false;
            return;
        }
        if (!adsCenterVisualSight || weaponCamera == null || weaponCamera == playerCamera)
        {
            RestoreAdsProjection();
            return;
        }

        // Remote players / arma no apuntando: proyección normal y sin efectos residuales.
        if (!weaponCamera.enabled || !IsAiming || aimingBlend <= 0.001f)
        {
            RestoreAdsProjection();
            return;
        }

        // La Vector no usa SOCKET_Scope como centro de mira: ese socket
        // es el punto de montaje de DQ_DefaultScope, no el centro optico.
        // Usar el centro de su renderer SOLO para Vector evita cambiar P90/MAC10
        // y evita mover FPS_Arms o alterar el disparo fisico.
        if (weaponName == "Vector" && (adsSightReference == null || adsSightReference.name != "DQ_OpticalSight"))
        {
            UpdateVectorScopeProjection();
            return;
        }

        weaponCamera.ResetProjectionMatrix();
        adsProjectionWasModified = false;
        if (!TryGetSightViewport(out Vector3 viewport)) return;

        float correctionX = Mathf.Clamp(0.5f - viewport.x, -adsMaxViewportCorrection, adsMaxViewportCorrection);
        float correctionY = Mathf.Clamp(0.5f - viewport.y, -adsMaxViewportCorrection, adsMaxViewportCorrection);
        float blend = Mathf.Clamp01(aimingBlend);
        if (Mathf.Abs(correctionX) < 0.0001f && Mathf.Abs(correctionY) < 0.0001f) return;

        // La convención de m02/m12 varía entre proyección y backend de render.
        // Probar ambas direcciones y conservar la que realmente acerque la mira
        // al centro. No mover la cámara ni el rig del jugador.
        Matrix4x4 original = weaponCamera.projectionMatrix;
        Matrix4x4 candidate = original;
        candidate.m02 -= 2f * correctionX * blend;
        candidate.m12 -= 2f * correctionY * blend;
        weaponCamera.projectionMatrix = candidate;
        Vector3 measured;
        if (adsSightReference != null)
            measured = weaponCamera.WorldToViewportPoint(adsSightReference.position);
        else
            measured = viewport; // fallback: no podemos medir el punto preciso
        float oldError = (new Vector2(viewport.x, viewport.y) - Vector2.one * 0.5f).sqrMagnitude;
        float candidateError = (new Vector2(measured.x, measured.y) - Vector2.one * 0.5f).sqrMagnitude;
        if (adsSightReference != null && candidateError >= oldError)
        {
            Matrix4x4 alternate = original;
            alternate.m02 += 2f * correctionX * blend;
            alternate.m12 += 2f * correctionY * blend;
            weaponCamera.projectionMatrix = alternate;
            measured = weaponCamera.WorldToViewportPoint(adsSightReference.position);
            float alternateError = (new Vector2(measured.x, measured.y) - Vector2.one * 0.5f).sqrMagnitude;
            if (alternateError >= oldError) { weaponCamera.projectionMatrix = original; return; }
        }
        adsProjectionWasModified = true;
    }

    // CORRECCION VISUAL EXCLUSIVA DE VECTOR
    // El pivote de montaje (adsSightReference) no coincide con el centro
    // geometrico del accesorio. El prefab recibido tiene DQ_DefaultScope
    // como hijo directo de ese socket. Medimos la geometria renderizada
    // para colocar el centro del accesorio en el pixel central de WeaponCamera.
    // No tocamos la posicion de brazos, Camera transform ni GetShotAimRay.
    private Transform vectorDefaultScopeRoot;
    private Renderer[] vectorDefaultScopeRenderers;
    private bool vectorScopeWarningShown;

    private void UpdateVectorScopeProjection()
    {
        weaponCamera.ResetProjectionMatrix();
        adsProjectionWasModified = false;

        if (adsSightReference == null) return;

        // Buscar en la jerarquia real del prefab, no por nombre de FBX.
        if (vectorDefaultScopeRoot == null ||
            !vectorDefaultScopeRoot.IsChildOf(adsSightReference))
        {
            vectorDefaultScopeRoot = adsSightReference.Find("DQ_DefaultScope");
            vectorDefaultScopeRenderers = vectorDefaultScopeRoot != null
                ? vectorDefaultScopeRoot.GetComponentsInChildren<Renderer>(true)
                : null;
        }
        if (vectorDefaultScopeRoot == null ||
            !vectorDefaultScopeRoot.gameObject.activeInHierarchy ||
            vectorDefaultScopeRenderers == null)
        {
            if (!vectorScopeWarningShown)
            {
                Debug.LogWarning("[Vector ADS] No se encontro la mira DQ_DefaultScope debajo de adsSightReference. " +
                    "La proyeccion queda sin modificar. Verifica el Player.prefab.", this);
                vectorScopeWarningShown = true;
            }
            return;
        }

        bool found = false;
        Bounds scopeBounds = default(Bounds);
        foreach (Renderer renderer in vectorDefaultScopeRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;
            if (!found) { scopeBounds = renderer.bounds; found = true; }
            else scopeBounds.Encapsulate(renderer.bounds);
        }
        if (!found) return;

        Vector3 scopeCenter = scopeBounds.center;
        Vector3 viewport = weaponCamera.WorldToViewportPoint(scopeCenter);
        if (viewport.z <= 0.02f || float.IsNaN(viewport.x) || float.IsNaN(viewport.y) ||
            Mathf.Abs(viewport.x - 0.5f) > 0.8f || Mathf.Abs(viewport.y - 0.5f) > 0.8f)
            return;

        float maxCorrection = Mathf.Clamp(adsMaxViewportCorrection, 0.01f, 0.35f);
        float correctionX = Mathf.Clamp(0.5f - viewport.x, -maxCorrection, maxCorrection);
        float correctionY = Mathf.Clamp(0.5f - viewport.y, -maxCorrection, maxCorrection);
        float blend = Mathf.Clamp01(aimingBlend);
        if (blend <= 0.001f ||
            (Mathf.Abs(correctionX) < 0.0001f && Mathf.Abs(correctionY) < 0.0001f))
            return;

        // m02 y m12 desplazan la imagen de la camara overlay. Medimos ambas
        // direcciones y aceptamos solamente la que REDUCE el error real.
        Matrix4x4 original = weaponCamera.projectionMatrix;
        float oldError = (new Vector2(viewport.x - 0.5f, viewport.y - 0.5f)).sqrMagnitude;
        float bestError = oldError;
        Matrix4x4 bestProjection = original;
        for (int sign = -1; sign <= 1; sign += 2)
        {
            Matrix4x4 candidate = original;
            candidate.m02 += sign * 2f * correctionX * blend;
            candidate.m12 += sign * 2f * correctionY * blend;
            weaponCamera.projectionMatrix = candidate;
            Vector3 measured = weaponCamera.WorldToViewportPoint(scopeCenter);
            if (measured.z <= 0f || float.IsNaN(measured.x) || float.IsNaN(measured.y))
                continue;
            float error = (new Vector2(measured.x - 0.5f, measured.y - 0.5f)).sqrMagnitude;
            if (error < bestError)
            {
                bestError = error;
                bestProjection = candidate;
            }
        }
        // En la captura del juego el aro real de la mira de Vector queda
        // ~85 px POR ENCIMA del centro (pantalla de 734 px). El centro de
        // Bounds incluye la base/riel y NO es el centro del aro.
        // Esta compensacion baja SOLO la imagen dibujada por WeaponCamera;
        // la direccion fisica del disparo sigue siendo el centro de PlayerCamera.
        float bajarViewport = Mathf.Clamp(vectorAdsVerticalSightDown, 0f, 0.25f) * blend;
        if (bajarViewport > 0f)
        {
            // Calculamos el sentido de m12 con la camara real para evitar
            // invertir la correccion con distintas matrices de proyeccion.
            weaponCamera.projectionMatrix = bestProjection;
            float yOriginal = weaponCamera.WorldToViewportPoint(scopeCenter).y;
            Matrix4x4 probe = bestProjection;
            probe.m12 += 0.02f;
            weaponCamera.projectionMatrix = probe;
            float yProbe = weaponCamera.WorldToViewportPoint(scopeCenter).y;
            float dyDm12 = (yProbe - yOriginal) / 0.02f;
            float delta = (!float.IsNaN(dyDm12) && !float.IsInfinity(dyDm12) && Mathf.Abs(dyDm12) > 0.0001f)
                ? -bajarViewport / dyDm12
                : 2f * bajarViewport;
            bestProjection.m12 += Mathf.Clamp(delta, -0.5f, 0.5f);
        }
        weaponCamera.projectionMatrix = bestProjection;
        adsProjectionWasModified = bestError < oldError || bajarViewport > 0f;
    }

    private void RestoreAdsProjection()
    {
        if (!adsProjectionWasModified || weaponCamera == null) return;
        weaponCamera.ResetProjectionMatrix();
        adsProjectionWasModified = false;
    }

    private bool TryGetSightViewport(out Vector3 viewport)
    {
        viewport = new Vector3(0.5f, 0.5f, 1f);
        if (firePoint == null || weaponCamera == null || !weaponCamera.enabled) return false;

        // Primero la referencia visual de la mira; no el cañón, que puede
        // apuntar a un eje distinto al centro del hierro tras la animación.
        if (adsSightReference != null)
        {
            Vector3 rearViewport = weaponCamera.WorldToViewportPoint(adsSightReference.position);
            if (IsValidSightViewport(rearViewport))
            {
                viewport = rearViewport;
                return true;
            }
        }
        Vector3 barrel = firePoint.forward;
        if (Vector3.Dot(barrel, weaponCamera.transform.forward) >= 0.90f)
        {
            Vector3 farSight = firePoint.position + barrel * 25f;
            Vector3 boreViewport = weaponCamera.WorldToViewportPoint(farSight);
            if (IsValidSightViewport(boreViewport))
            {
                viewport = boreViewport;
                return true;
            }
        }
        return false;
    }

    private static bool IsValidSightViewport(Vector3 viewport)
    {
        return viewport.z > 0.01f && viewport.x > 0.15f && viewport.x < 0.85f &&
               viewport.y > 0.15f && viewport.y < 0.85f &&
               !float.IsNaN(viewport.x) && !float.IsNaN(viewport.y);

    }

    private Ray GetShotAimRay()
    {
        // ADS: la direccion fisica de la bala es el CENTRO de la camara
        // jugable. WeaponCamera solo dibuja el arma; sus SOCKET_Default
        // NO son la posicion real del alza/reticula y provocaban tiros bajos.
        //
        // Este metodo se invoca desde FlushPendingAdsShots() en LateUpdate,
        // DESPUES de CameraRecoil. Asi el raycast utiliza la misma orientacion
        // de PlayerCamera que se representa en pantalla durante el full auto.
        if (IsAiming)
            return playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Fuera de ADS se conserva la direccion y dispersion del hipfire.
        return new Ray(playerCamera.transform.position, playerCamera.transform.forward);
    }

    private void FlushPendingAdsShots()
    {
        int toFire = pendingAdsRounds;
        if (toFire <= 0) return;
        pendingAdsRounds = 0;
        for (int i = 0; i < toFire; i++)
        {
            // Primero sale la bala siguiendo la mira visible; DESPUES
            // comienza la patada de ESTE disparo, que se aplicara el frame
            // siguiente. Si se aplica antes, hasta la primera bala sube.
            ShootAndPublish();
            if (cameraRecoil != null)
                cameraRecoil.Fire(recoilPreset, true, recoilIntensityMultiplier * AttachmentRecoilMultiplier, UseAuthoredRecoil, AttachmentRecoilAxes);
            if (weaponRecoil != null)
                weaponRecoil.Fire(recoilPreset, true, recoilIntensityMultiplier * AttachmentRecoilMultiplier, UseAuthoredRecoil, AttachmentRecoilAxes);
        }
    }

    private void ShootAndPublish()
    {
        presentationTargets.Clear();
        Shoot();
        if (!presentation)
            presentation = GetComponentInParent<NetworkWeaponPresentation>();
        presentation?.PublishShot(this, presentationTargets.ToArray());
    }

    private void RenderIntegratedSight(ScriptableRenderContext context, Camera camera)
    {
        if (camera == weaponCamera) UpdateAdsVisualProjection();
    }

    private void OnEnable()
    {
        if (adsSightReference != null && adsSightReference.name == "DQ_OpticalSight")
            RenderPipelineManager.beginCameraRendering += RenderIntegratedSight;
        controls.Player.Enable();

        if (!isAutomatic)
            controls.Player.Fire.performed += OnFireSemiAuto;

        controls.Player.Reload.performed += OnReload;
        controls.Player.Aim.started += OnAimStarted;
        controls.Player.Aim.canceled += OnAimCanceled;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= RenderIntegratedSight;
        RestoreIntegratedAimPose();
        if (UsesIntegratedVisual) { integratedAimHeld = false; integratedAimFullOffset = Vector3.zero; integratedAimVelocity = 0; aimingBlend = 0; }
        // Si el jugador cambia de arma en el mismo frame que dispara,
        // no perder una bala cuyo cargador ya fue descontado.
        if (pendingAdsRounds > 0 && Application.isPlaying && Time.timeScale > 0f)
            FlushPendingAdsShots();
        else
            pendingAdsRounds = 0;

        if (!isAutomatic)
            controls.Player.Fire.performed -= OnFireSemiAuto;

        controls.Player.Reload.performed -= OnReload;
        controls.Player.Aim.started -= OnAimStarted;
        controls.Player.Aim.canceled -= OnAimCanceled;

        controls.Player.Disable();

        IsAiming = false;

        // Si el arma se apaga en medio de una recarga (por ejemplo al cambiar de arma), la recarga se cancela:
        // el evento que bajaba 'isReloading' ya no va a llegar y el arma quedaría bloqueada.
        isReloading = false;
        shellLoading = false;
        pendingReload = false;
        isChambering = false;
        boltActionActive = false;
        burstActive = false;
        if (float.IsPositiveInfinity(nextFireTime))
            nextFireTime = Time.time;
        if (burstRoutine != null)
        {
            StopCoroutine(burstRoutine);
            burstRoutine = null;
        }
        if (reloadFallbackRoutine != null)
        {
            StopCoroutine(reloadFallbackRoutine);
            reloadFallbackRoutine = null;
        }
        ResetReloadAnimationSpeed();
        RestoreAdsProjection();
    }

    // true mientras se guarda un arma y se saca la otra. Se consulta directo al WeaponSwitcher, así el
    // arma no responde aunque su componente quede encendido por algún motivo.
    private bool SwitchBlocked => weaponSwitcher != null && weaponSwitcher.IsSwitching;

    /// <summary>
    /// Cancela la recarga en curso: la munición que todavía no se cargó no se carga, se corta el sonido
    /// de recarga y el Animator vuelve a su estado de reposo. Lo usan el cambio de arma, el cuchillo y la granada.
    /// </summary>
    public void CancelReload()
    {
        pendingReload=false;
        bool estabaRecargando = isReloading || shellLoading;

        isReloading = false;
        shellLoading = false;

        // Aunque la bandera ya estuviera apagada, nunca dejamos el Animator con
        // una velocidad de clase aplicada al cambiar de arma o cancelar.
        ResetReloadAnimationSpeed();

        if (reloadFallbackRoutine != null)
        {
            StopCoroutine(reloadFallbackRoutine);
            reloadFallbackRoutine = null;
        }

        Animator cancelParts = Parts;
        if (cancelParts != null && cancelParts.HasState(0, Animator.StringToHash("Idle")))
            cancelParts.Play("Idle", 0, 0f);

        if (!estabaRecargando)
            return;

        // Corta el sonido de recarga que siga sonando.
        if (audioSource != null)
            audioSource.Stop();

        // Saca el Animator del clip de recarga. Sin esto, el Animator (que es el mismo para todas las armas)
        // seguía en el estado de recarga y el arma siguiente terminaba de reproducir esa animación.
        if (weaponSwitcher != null)
        {
            weaponSwitcher.ResetBaseLayerToIdle();
        }
        else if (weaponAnimator != null)
        {
            weaponAnimator.ResetTrigger("Reload");
            weaponAnimator.SetBool("ShellLoading", false);
        }
    }

    // Multiplicador de daño según la distancia: 1 hasta 'Damage Falloff Start', baja de forma lineal
    // y llega a 'Min Damage Multiplier' en 'Damage Falloff End' (o más lejos).
    private float DistanceFalloffMultiplier(float distance)
    {
        distance/=RuntimeAttachments?RuntimeAttachments.RangeMultiplier:1;
        if (distance <= damageFalloffStart)
            return 1f;

        if (distance >= damageFalloffEnd)
            return minDamageMultiplier;

        float t = Mathf.InverseLerp(damageFalloffStart, damageFalloffEnd, distance);
        return Mathf.Lerp(1f, minDamageMultiplier, t);
    }

    private int ApplyDistanceFalloff(int baseDamage, float distance)
    {
        return Mathf.RoundToInt(baseDamage * DistanceFalloffMultiplier(distance));
    }

    // Daño de UN impacto antes de la distancia. Escopeta: el daño total se reparte entre los perdigones
    // (200 de daño y 8 perdigones = 25 por perdigón). Arma normal: el daño completo.
    private float DanoBasePorImpacto
    {
        get
        {
            if (firesMultiplePellets && pelletsPerShot > 0)
                return damage * AttachmentDamageMultiplier / (float)pelletsPerShot;

            return damage * AttachmentDamageMultiplier;
        }
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
        forceReserveLimit || (ActiveDifficulty != null && ActiveDifficulty.limitedReserveAmmo);

    public int ReserveCapacity
    {
        get
        {
            if (forceReserveLimit)
                return Mathf.Max(0, reserveAmmoCapacity);

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

    /// <summary>
    /// C.A. 12: cuando una arma usa reserva limitada, la caja de munición deja de ser comprable
    /// apenas la RESERVA llega al máximo, aunque el cargador no esté lleno.
    /// Las armas antiguas sin reserva limitada conservan su comportamiento anterior.
    /// </summary>
    public bool PuedeComprarMunicion => UsesLimitedReserve
        ? ReserveAmmo < ReserveCapacity
        : currentAmmo < maxAmmo;

    public void RestaurarMunicion()
    {
        currentAmmo = maxAmmo;
        RefillReserveAmmo();
    }

    /// <summary>
    /// Restaura las cantidades exactas guardadas en el inventario.
    /// Se conserva la version sin parametros para reabastecer municion en tiendas.
    /// </summary>
    public void RestaurarMunicion(int municionCargador, int municionReserva)
    {
        currentAmmo = Mathf.Clamp(municionCargador, 0, maxAmmo);
        reserveAmmo = Mathf.Clamp(municionReserva, 0, ReserveCapacity);
        reserveInitialized = true;
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
        if (!isReloading || usesMultiPartReload)
            return;

        if (enforceReloadDuration && Time.time + 0.01f < reloadCanFillAt)
            return;

        FillMagazine();
    }

    public void AnimationInsertOneShell()
    {
        // Only an insert event may add a shotgun shell. Crossfades can emit duplicate events.
        if (!isReloading || !usesMultiPartReload || !shellLoading || Time.time-lastShellInsert < .12f)
            return;
        lastShellInsert=Time.time;
        PresentationEvent(NetworkWeaponPresentation.WeaponEvent.Insert);

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
        if (!isReloading)
            return;

        if (enforceReloadDuration && Time.time + 0.01f < reloadCanFinishAt)
            return;

        FinishReloadInternal();
    }

    private void FinishReloadInternal()
    {
        if (!isReloading)
            return;

        if (!usesMultiPartReload)
            FillMagazine();

        isReloading = false;
        shellLoading = false;

        if (weaponAnimator != null)
            weaponAnimator.SetBool("ShellLoading", false);

        if (reloadFallbackRoutine != null)
        {
            StopCoroutine(reloadFallbackRoutine);
            reloadFallbackRoutine = null;
        }

        ResetReloadAnimationSpeed();
    }

    public void AnimationReloadOpen()
    {
        PresentationEvent(NetworkWeaponPresentation.WeaponEvent.ReloadOpen);
        PlaySound(reloadOpenSound);
    }

    public void AnimationReloadClose()
    {
        PresentationEvent(NetworkWeaponPresentation.WeaponEvent.ReloadClose);
        PlaySound(reloadCloseSound);
    }

    public void AnimationBoltOpen()
    {
        PresentationEvent(NetworkWeaponPresentation.WeaponEvent.BoltOpen);
        PlaySound(reloadBoltOpenSound);
    }

    public void AnimationBoltClose()
    {
        PresentationEvent(NetworkWeaponPresentation.WeaponEvent.BoltClose);
        PlaySound(reloadBoltCloseSound);

        // Completion is emitted at the end of the bolt clip, after it returns to firing pose.
    }

    public void ForceStopAiming()
    {
        integratedAimHeld = false;
        IsAiming = false;
    }

    private void Update()
    {
        RestoreIntegratedAimPose();
        if (IsAiming && playerMovement != null && playerMovement.IsSprinting)
            IsAiming = false;

        if (isChambering && Time.time >= chamberReadyTime)
        {
            isChambering = false;
            boltActionActive = false;

            if (weaponAnimator != null)
                weaponAnimator.SetBool("BoltAction", false);
        }

        if(pendingReload && !isChambering && !isReloading && !InputLocked && !SwitchBlocked) {
            pendingReload=false;OnReload(default);
        }
        if (UsesIntegratedVisual)
            IsAiming = integratedAimHeld && !isReloading && !isChambering && !InputLocked && !SwitchBlocked && !PlayerInputGate.ActionsBlocked && (playerMovement == null || !playerMovement.IsSprinting);
        UpdateAimFOV();
        UpdateSpread();
        UpdateJumpSpread();
        UpdateAnimatorParams();

        if (isAutomatic && !usesBurstFire)
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

        if (PlayerInputGate.ActionsBlocked)
            return;

        if (UsesIntegratedVisual) integratedAimHeld = true;
        if (SwitchBlocked)
            return;

        if (InputLocked || isReloading)
            return;

        if (playerMovement != null && playerMovement.IsSprinting)
            return;

        IsAiming = true;
    }

    private void OnAimCanceled(InputAction.CallbackContext context)
    {
        integratedAimHeld = false;
        IsAiming = false;
    }

    private void UpdateAimFOV()
    {
        if (playerCamera == null)
            return;

        if (UsesIntegratedVisual||UsePackAim)
        {
            playerCamera.fieldOfView = Mathf.Lerp(defaultWorldFOV, AttachmentAimFov, aimingBlend);
            return;
        }
        float targetFOV = IsAiming ? AttachmentAimFov : defaultWorldFOV;

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            aimTransitionSpeed / AttachmentAdsDuration * Time.deltaTime
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

        if (debugSpread && Time.unscaledTime >= nextSpreadLog)
        {
            nextSpreadLog = Time.unscaledTime + 0.5f;

            Vector3 v = characterController.velocity;
           
        }

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

    [Tooltip("Escribe en la consola (cada 0.5 s) por qué la dispersión sube o no al caminar. Apagar al terminar.")]
    [SerializeField] private bool debugSpread = true;
    private float nextSpreadLog;

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

        if (UsesIntegratedVisual||UsePackAim)
        {
            aimingBlend = Mathf.SmoothDamp(aimingBlend, target, ref integratedAimVelocity, AttachmentAdsDuration / Mathf.Max(1f, aimingBlendSpeed), Mathf.Infinity, Time.deltaTime);
            if (Mathf.Abs(aimingBlend - target) < .0005f) { aimingBlend = target; integratedAimVelocity = 0; }
        }
        else
            aimingBlend = Mathf.MoveTowards(aimingBlend, target, aimingBlendSpeed / AttachmentAdsDuration * Time.deltaTime);

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
        if (!CanStartShot())
            return;

        if (usesBurstFire)
        {
            if (burstActive || Time.time < nextFireTime)
                return;

            // C.A. 7: cada pulsación inicia una ráfaga completa de 3 disparos y consume 3 balas.
            if (currentAmmo < burstCount)
            {
                PlayEmptyShotFeedback();
                nextFireTime = Time.time + burstCooldown;
                return;
            }

            burstRoutine = StartCoroutine(FireBurstRoutine());
            return;
        }

        if (Time.time < nextFireTime)
            return;

        if (currentAmmo <= 0)
        {
            PlayEmptyShotFeedback();
            nextFireTime = Time.time + fireRate;
            return;
        }

        nextFireTime = Time.time + fireRate;
        FireOneRound();
    }

    private bool CanStartShot()
    {
        if (PlayerInputGate.ActionsBlocked)
            return false;

        if (SwitchBlocked)
            return false;

        if (InputLocked || Time.timeScale == 0f)
            return false;

        if (EventSystem.current != null)
        {
            GameObject seleccionado = EventSystem.current.currentSelectedGameObject;
            if (seleccionado != null)
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                    return false;

                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        if (isReloading || isChambering)
            return false;

        if (playerMovement != null && playerMovement.IsSprinting)
            return false;

        return true;
    }

    private IEnumerator FireBurstRoutine()
    {
        burstActive = true;
        nextFireTime = float.PositiveInfinity;

        for (int i = 0; i < burstCount; i++)
        {
            if (!isActiveAndEnabled || currentAmmo <= 0 || SwitchBlocked || InputLocked)
                break;

            FireOneRound();

            if (i < burstCount - 1)
                yield return new WaitForSeconds(burstShotInterval);
        }

        burstActive = false;
        burstRoutine = null;
        nextFireTime = Time.time + burstCooldown;
    }

    private void PlayEmptyShotFeedback()
    {
        PlaySound(emptySound);
        if (weaponAnimator != null)
            weaponAnimator.SetTrigger("FireEmpty");
    }

    private void FireOneRound()
    {
        if (currentAmmo <= 0)
            return;

        PresentationShotSequence++;
        currentAmmo--;

        if (playerScore != null)
            playerScore.RegistrarDisparoServerRpc();

        // Apuntando NO introducimos dispersión invisible. El desvío del disparo
        // sigue la mirilla y el recoil; fuera de ADS conserva el spread original.
        if (!IsAiming)
        {
            currentSpread = Mathf.Clamp(
                currentSpread + spreadIncreasePerShot,
                0f,
                maxSpread
            );
        }

        // Solo para armas nuevas con ADS visual: recoil DESPUES del impacto.
        // Las armas originales y el hipfire mantienen su orden sin cambios.
        if (!IsAiming || !adsFireAlongVisualSight)
        {
            if (cameraRecoil != null)
                cameraRecoil.Fire(recoilPreset, false, recoilIntensityMultiplier * AttachmentRecoilMultiplier, UseAuthoredRecoil, AttachmentRecoilAxes);
            if (weaponRecoil != null)
                weaponRecoil.Fire(recoilPreset, false, recoilIntensityMultiplier * AttachmentRecoilMultiplier, UseAuthoredRecoil, AttachmentRecoilAxes);
        }

        PlaySound(PresentationShotSound);

        if (weaponAnimator != null)
        {
            weaponAnimator.SetTrigger("Fire");
            weaponAnimator.SetTrigger("WeaponPartsFire");
        }

        PlayPartsState("Fire");
        if (!ShotIsSuppressed) muzzle?.PlayEffect();

        // En ADS, el raycast se resuelve despues de CameraRecoil.LateUpdate
        // (con el recoil de las balas ANTERIORES), pero antes de iniciar la
        // patada de ESTA bala. Asi el disparo sigue la mira que se ve,
        // y el retroceso mueve la mira para la bala siguiente.
        if (IsAiming && adsFireAlongVisualSight)
            pendingAdsRounds++;
        else
            ShootAndPublish();

        if (requiresBoltActionAfterFire)
        {
            isChambering = true;
            chamberReadyTime = Time.time + boltActionDuration;
            boltActionActive = true;

            if (weaponAnimator != null)
                weaponAnimator.SetBool("BoltAction", true);
        }
    }

    private void PlayPartsState(string stateName)
    {
        Animator a = Parts;
        if (a == null)
            return;

        int hash = Animator.StringToHash(stateName);
        if (a.HasState(0, hash))
            a.Play(hash, 0, 0f);
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
        if (PlayerInputGate.ActionsBlocked)
            return;

        if (SwitchBlocked)
            return;

        if (InputLocked || isReloading)
            return;
        if(isChambering) {pendingReload=true;return;}

        if (currentAmmo == maxAmmo)
            return;

        if (UsesLimitedReserve && ReserveAmmo <= 0)
            return;

        if (playerMovement != null && playerMovement.IsSprinting)
            return;

        IsAiming = false;
        isReloading = true;
        pendingReload=false;
        lastShellInsert=float.NegativeInfinity;
        if(weaponAnimator) {weaponAnimator.ResetTrigger("Fire");weaponAnimator.SetBool("BoltAction",false);}

        bool wasEmpty = currentAmmo == 0;
        reloadStartedEmpty = wasEmpty;

        // Mantiene el modificador de clase, y para las armas nuevas además ajusta
        // el clip específico para que su duración base coincida con Reload Time.
        ApplyReloadAnimationSpeed(wasEmpty);

        if (enforceReloadDuration)
        {
            float classSpeed = Mathf.Max(0.01f, ClassReloadSpeedMultiplier);
            float duration = Mathf.Max(0.05f, reloadTime / classSpeed);
            reloadCanFillAt = Time.time + duration * 0.68f;
            reloadCanFinishAt = Time.time + duration;

            if (reloadFallbackRoutine != null)
                StopCoroutine(reloadFallbackRoutine);

            reloadFallbackRoutine = StartCoroutine(ReloadDurationFallback(duration));
        }

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
            weaponAnimator.SetTrigger("WeaponPartsReload");
        }

        PlayPartsState(wasEmpty ? "Reload Empty" : "Reload");
    }

    private IEnumerator ReloadDurationFallback(float duration)
    {
        float fillDelay = duration * 0.68f;
        yield return new WaitForSeconds(fillDelay);

        if (isReloading && !usesMultiPartReload)
            FillMagazine();

        yield return new WaitForSeconds(Mathf.Max(0f, duration - fillDelay));

        if (isReloading)
        {
            // La corrutina ya está terminando: se limpia primero para que FinishReloadInternal
            // no intente detener la misma corrutina desde adentro.
            reloadFallbackRoutine = null;
            FinishReloadInternal();
        }
        else
        {
            reloadFallbackRoutine = null;
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
    // Cada disparo comienza sin headshots registrados.
    headshotRegistradoEnDisparo = false;

    // En ADS, una bala individual siempre viaja por la dirección de la mirilla.
    // La escopeta conserva su distribución de perdigones alrededor de ese centro.
    float spreadForThisShot = IsAiming ? 0f : currentSpread * AttachmentSpreadMultiplier;
    if (firesMultiplePellets)
    {
        for (int i = 0; i < pelletsPerShot; i++)
            FirePellet(spreadForThisShot + pelletSpreadAngle, pelletCenterBias);
    }
    else
    {
        FirePellet(spreadForThisShot);
    }
}

    private void FirePellet(float spreadDegrees, float centerBias = 0f)
    {
        if (playerCamera == null)
            return;

        Ray aimRay = GetShotAimRay();
        Vector3 spreadDirection = ApplySpreadToDirection(
            aimRay.direction,
            spreadDegrees,
            centerBias
        );
        Ray ray = new Ray(aimRay.origin, spreadDirection);

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            EffectiveRange,
            ~0,
            QueryTriggerInteraction.Collide
        );

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        bool validHitFound = false;
        RaycastHit validHit = default;

        foreach (RaycastHit hit in hits)
        {
            // Al forzar QueryTriggerInteraction.Collide podemos detectar los hitboxes
            // auxiliares de cabeza/brazos. El resto de triggers del mapa se ignora.
            if (hit.collider.isTrigger)
            {
                bool esHitboxZombie =
                    hit.collider.GetComponent<ZombieHeadHitbox>() != null ||
                    hit.collider.GetComponent<ZombieLimbHitbox>() != null;

                if (!esHitboxZombie)
                    continue;
            }

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
//===================================== VIEJO ================================================================
 /*           // Daño a zombies.
            ZombieHealth zombieHealth =
                hit.collider.GetComponentInParent<ZombieHealth>();

            if (zombieHealth != null)
            {
                // El daño del perdigón (o de la bala) baja con la distancia. Siempre pega al menos 1.
                int finalDamage = Mathf.Max(
                    1,
                    Mathf.RoundToInt(DanoBasePorImpacto * DistanceFalloffMultiplier(hit.distance))
                );

                zombieHealth.TakeDamage(
                    finalDamage,
                    hit.point,
                    hit.normal
                );

                if (playerScore != null)
                    playerScore.RegistrarImpactoServerRpc();

                return;
            }*/
//==============================================================================================================

//======================================== NUEVO ===============================================================
            // Daño a zombies.
ZombieHealth zombieHealth =
    hit.collider.GetComponentInParent<ZombieHealth>();

if (zombieHealth != null)
{
    bool esHeadshot = false;

    foreach (RaycastHit h in hits)
    {
        ZombieHeadHitbox head =
            h.collider.GetComponent<ZombieHeadHitbox>();

        if (head != null && head.Owner == zombieHealth)
        {
            esHeadshot = true;
            break;
        }
    }

    // Busca hitboxes de brazo del mismo zombie a lo largo de este mismo rayo.
    // Esto permite distinguir brazos aunque el collider general del cuerpo también
    // haya sido alcanzado. La cabeza sigue teniendo prioridad.
    int hitVariantExplicito = -1;
    float closestLimbDistance = float.PositiveInfinity;

    foreach (RaycastHit h in hits)
    {
        ZombieLimbHitbox limb =
            h.collider.GetComponent<ZombieLimbHitbox>();

        if (limb == null || limb.Owner != zombieHealth)
            continue;

        // Evita clasificar como brazo un hitbox muy por detrás del impacto real.
        if (h.distance > hit.distance + 0.45f)
            continue;

        if (h.distance < closestLimbDistance)
        {
            closestLimbDistance = h.distance;
            hitVariantExplicito = limb.HitVariant;
        }
    }

    // Solo el primer headshot del disparo se registra para el logro.
    bool registrarHeadshot =
        esHeadshot && !headshotRegistradoEnDisparo;

    if (registrarHeadshot)
        headshotRegistradoEnDisparo = true;

    int finalDamage = Mathf.Max(
        1,
        Mathf.RoundToInt(
            DanoBasePorImpacto * DistanceFalloffMultiplier(hit.distance)
        )
    );

    zombieHealth.TakeDamage(
        finalDamage,
        hit.point,
        hit.normal,
        esHeadshot,
        registrarHeadshot,
        hitVariantExplicito
    );

    if (playerScore != null)
        playerScore.RegistrarImpactoServerRpc();

    return;
}
//==============================================================================================================

            // BARRIL EXPLOSIVO: un disparo lo hace explotar (el servidor decide y daña a los zombies alrededor).
            BarrilExplosivo barril =
                hit.collider.GetComponentInParent<BarrilExplosivo>();

            if (barril != null)
            {
                barril.RecibirDisparo(
                    Mathf.Max(1, Mathf.RoundToInt(DanoBasePorImpacto))
                );

                return;
            }

            // EASTER EGG: permite que los disparos dañen los objetos
            // sin otorgar puntos por impacto.
            EasterEgg easterEgg =
                hit.collider.GetComponentInParent<EasterEgg>();

            if (easterEgg != null)
            {
                easterEgg.TakeDamage(
                    Mathf.Max(1, Mathf.RoundToInt(DanoBasePorImpacto)),
                    hit.point,
                    hit.normal
                );

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
                ray.origin + spreadDirection * EffectiveRange;

            SpawnBulletTrail(missPoint);

            Debug.DrawRay(
                ray.origin,
                spreadDirection * EffectiveRange,
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
        presentationTargets.Add(targetPoint);
        if (bulletTrailPrefab == null || firePoint == null)
            return;
        PresentationPose(firePoint,out Vector3 worldPoint,out _);
        GameObject trailObject = Instantiate(
            bulletTrailPrefab,
            worldPoint,
            Quaternion.identity
        );

        BulletTrail trail = trailObject.GetComponent<BulletTrail>();

        if (trail != null)
            trail.Init(targetPoint);
    }

    // =========================================================
    // DIRECCIÓN CON DISPERSIÓN
    // =========================================================

    // La desviación se calcula en el espacio de la cámara (no en los ejes del mundo, que aplastaban el patrón
    // según hacia dónde se mirara) y dentro de un círculo cuyo radio es la dispersión en grados.
    // centerBias 0 = reparto parejo en el círculo; cuanto más alto, más perdigones cerca del centro.
    private Vector3 ApplySpreadToDirection(
        Vector3 baseDirection,
        float spreadDegrees,
        float centerBias = 0f
    )
    {
        if (spreadDegrees <= 0f)
            return baseDirection;

        float exponent = Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(centerBias));
        float radius = spreadDegrees * Mathf.Pow(Random.value, exponent);
        float angle = Random.value * Mathf.PI * 2f;

        float offsetX = Mathf.Cos(angle) * radius;
        float offsetY = Mathf.Sin(angle) * radius;

        Quaternion aim = Quaternion.LookRotation(baseDirection);

        return aim * Quaternion.Euler(offsetY, offsetX, 0f) * Vector3.forward;
    }
}
