using UnityEngine;
using Unity.Netcode;

public class ThirdPersonAnimatorSync : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Animator thirdPersonAnimator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Movimiento (igualar a PlayerMovement)")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float moveDamp = 0.1f;
    [SerializeField] private float groundedGrace = 0.12f;

    [Header("Look (tercera persona)")]
    [SerializeField] private float lookSensitivityScale = 1f / 80f; // 1 dividido el pitch máximo de PlayerLook
    [SerializeField] private bool invertLook = true;

    [Header("Apuntado, agachado y acciones")]
    [SerializeField] private float aimBlendSpeed = 8f;
    [SerializeField] private float crouchBlendSpeed = 8f;
    [SerializeField, Range(0f, 1f)] private float actionWeightWhileAiming = 0.35f;
    [SerializeField] private float layerFadeSpeed = 6f;

    [Header("Debug")]
    [SerializeField] private bool debugAim;

    private NetworkObject networkObject;

    private int upperBodyLayer = -1;
    private int actionsLayer = -1;
    private float upperBodyWeight = 1f;

    private float groundedTimer;
    private float aimW;
    private float actualAimW; // Peso del input real: no cambia al adoptar pose Aim en Idle.
    private float crouchW;
    private bool wasGrounded = true;

    private Weapon lastWeapon;
    private int lastAmmo;
    private bool hasAmmoBaseline;
    private bool wasReloading;
    private bool reloadStartedEmpty;
    private bool lastAimLogged;

    private void Awake()
    {
        networkObject = GetComponentInParent<NetworkObject>();
    }

    private void Start()
    {
        if (thirdPersonAnimator != null)
        {
            upperBodyLayer = thirdPersonAnimator.GetLayerIndex("UpperBody");
            actionsLayer = thirdPersonAnimator.GetLayerIndex("Actions");
        }

        Debug.Log(
            "[ThirdPersonAnimatorSync] Animator=" + (thirdPersonAnimator != null) +
            " | CharController=" + (characterController != null) +
            " | WeaponSwitcher=" + (weaponSwitcher != null) +
            " | PlayerHealth=" + (playerHealth != null) +
            " | PlayerLook=" + (playerLook != null) +
            " | PlayerMovement=" + (playerMovement != null) +
            " | Layer UpperBody=" + upperBodyLayer +
            " | Layer Actions=" + actionsLayer
        );
    }

    private void Update()
    {
        if (thirdPersonAnimator == null || characterController == null)
            return;

        if (networkObject != null && !networkObject.IsOwner)
            return;

        thirdPersonAnimator.SetBool("TP_Holstering", weaponSwitcher && weaponSwitcher.IsHolstering);
        thirdPersonAnimator.SetBool("TP_Unholstering", weaponSwitcher && weaponSwitcher.IsUnholstering);
        UpdateMovementParams();
        UpdateStateParams();
        UpdateWeaponParams();   // calcula aimW, así que va antes de los pesos de layer
        UpdateLayerWeights();
    }

    // =========================================================
    // MOVIMIENTO
    // =========================================================

    private void UpdateMovementParams()
    {
        Animator a = thirdPersonAnimator;

        Vector3 v = characterController.velocity;
        Vector3 local = characterController.transform.InverseTransformDirection(v);
        float horizontal = new Vector2(v.x, v.z).magnitude;

        bool crouching = playerMovement != null && playerMovement.IsCrouching;
        bool sprinting = playerMovement != null && playerMovement.IsSprinting && !crouching;
        float refSpeed = crouching ? crouchSpeed : walkSpeed;

        Vector2 dir = Vector2.ClampMagnitude(new Vector2(local.x, local.z) / refSpeed, 1f);

        a.SetFloat("TP_MoveX", dir.x, moveDamp, Time.deltaTime);
        a.SetFloat("TP_MoveZ", dir.y, moveDamp, Time.deltaTime);
        a.SetFloat("TP_MoveAmount", dir.magnitude, moveDamp, Time.deltaTime);
        a.SetFloat("TP_Speed", horizontal);
        a.SetFloat("TP_VerticalSpeed", v.y);
        a.SetBool("TP_IsSprinting", sprinting);

        bool rawGrounded = characterController.isGrounded;
        groundedTimer = rawGrounded ? 0f : groundedTimer + Time.deltaTime;
        a.SetBool("TP_IsGrounded", groundedTimer < groundedGrace);

        if (wasGrounded && !rawGrounded && v.y > 0.5f)
            TriggerAction("TP_Jump");

        wasGrounded = rawGrounded;

        crouchW = Mathf.MoveTowards(crouchW, crouching ? 1f : 0f, Time.deltaTime * crouchBlendSpeed);
        a.SetFloat("TP_CrouchWeight", crouchW);

        if (playerLook != null)
        {
            float normalizedLook = Mathf.Clamp(playerLook.Pitch * lookSensitivityScale, -1f, 1f);

            if (invertLook)
                normalizedLook = -normalizedLook;

            a.SetFloat("TP_Look", normalizedLook);
        }
    }

    // =========================================================
    // ESTADO DEL PERSONAJE
    // =========================================================

    private void UpdateStateParams()
    {
        if (playerHealth != null)
            thirdPersonAnimator.SetBool("TP_IsAlive", playerHealth.IsAlive);

        if (playerMovement != null)
            thirdPersonAnimator.SetBool("TP_IsCrouching", playerMovement.IsCrouching);
    }

    // =========================================================
    // ARMA
    // =========================================================

    // Devuelve el arma equipada actualmente, directo desde el WeaponSwitcher.
    private Weapon ResolveWeapon()
    {
        return weaponSwitcher != null ? weaponSwitcher.CurrentWeapon : null;
    }

    /// <summary>
    /// Usa la pose corporal de apuntado solo cuando el personaje está quieto
    /// con un rifle o la MAC-10. No toca las poses de P90, Vector, pistola ni escopeta.
    /// Los nombres corresponden a los WeaponRoots del Player y a WeaponName,
    /// por lo que funciona con el Rifle original y los dos rifles nuevos.
    /// </summary>
    private bool ShouldUseAimPoseAsIdle(Weapon weapon, bool reloading)
    {
        if (weapon == null || reloading || characterController == null)
            return false;

        if (weaponSwitcher != null && weaponSwitcher.IsSwitching)
            return false;

        if (playerMovement != null &&
            (playerMovement.IsSprinting || playerMovement.IsCrouching))
            return false;

        Vector3 v = characterController.velocity;
        if (new Vector2(v.x, v.z).sqrMagnitude > 0.25f * 0.25f)
            return false;

        string rootName = weapon.gameObject.name;
        string displayName = weapon.WeaponName ?? string.Empty;

        bool rifle = rootName == "SK_AR_01" ||
                     rootName == "DQ_Weapon_SemiRifle" ||
                     rootName == "DQ_Weapon_BurstRifle" ||
                     displayName.IndexOf("rifle", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool mac10 = rootName == "DQ_Weapon_MAC10" ||
                     displayName.IndexOf("MAC-10", System.StringComparison.OrdinalIgnoreCase) >= 0;
        return rifle || mac10;
    }

    private void UpdateWeaponParams()
    {
        Animator a = thirdPersonAnimator;
        Weapon weapon = ResolveWeapon();

        if (weaponSwitcher != null)
        {
            // Se mantiene por los estados viejos que todavía usan TP_WeaponID.
            a.SetFloat(
                "TP_WeaponID",
                weapon != null ? weaponSwitcher.CurrentThirdPersonWeaponId : 0
            );
        }

        bool aiming = weapon != null && weapon.IsAiming;
        bool reloading = weapon != null && weapon.IsReloading;

        // Congela si la recarga comenzó con cargador vacío ANTES de calcular la velocidad TP.
        if (weapon != null && reloading && !wasReloading)
            reloadStartedEmpty = weapon.CurrentAmmo == 0;

        // Postura TP de reposo: el Animator base usa TP_AimWeight en un Blend Tree,
        // no un estado Idle independiente. En estas armas queremos la misma postura
        // corporal que al apuntar mientras están quietas, SIN simular que apuntan
        // (TP_IsAiming sigue reflejando el input real).
        bool aimPoseAsIdle = ShouldUseAimPoseAsIdle(weapon, reloading);
        actualAimW = Mathf.MoveTowards(actualAimW, aiming ? 1f : 0f,
            Time.deltaTime * aimBlendSpeed);
        aimW = Mathf.MoveTowards(aimW, (aiming || aimPoseAsIdle) ? 1f : 0f,
            Time.deltaTime * aimBlendSpeed);

        a.SetFloat("TP_AimWeight", aimW);
        a.SetBool("TP_IsAiming", aiming);
        a.SetBool("TP_IsReloading", reloading);
        a.SetFloat("TP_ReloadSpeed", reloading && weapon != null ? weapon.PresentationThirdPersonReloadSpeed(reloadStartedEmpty) : 1f);
        // NUEVO: gatilla la rama de recarga por partes (Shotgun_01, Sniper_01).
        a.SetBool("TP_MultiPartReload", weapon != null && weapon.UsesMultiPartReload);

        if (debugAim && aiming != lastAimLogged)
        {
            Debug.Log(
                "[ThirdPersonAnimatorSync] arma=" + (weapon != null ? weapon.WeaponName : "NULL") +
                " | IsAiming=" + aiming
            );
            lastAimLogged = aiming;
        }

        if (weapon == null)
        {
            hasAmmoBaseline = false;
            lastWeapon = null;
            wasReloading = false;
            return;
        }

        // TP_IsEmpty se mantiene con el valor congelado durante toda la recarga.
        wasReloading = reloading;

        a.SetBool("TP_IsEmpty", reloading ? reloadStartedEmpty : weapon.CurrentAmmo == 0);
        // NUEVO: espejo del loop bala por bala y del ciclo de cerrojo.
        a.SetBool("TP_ShellLoading", weapon.ShellLoading);
        a.SetBool("TP_IsChambering", weapon.IsChambering);

        // Fire is driven by the actual accepted local shot, not an ammo delta.
        lastWeapon=weapon;lastAmmo=weapon.CurrentAmmo;hasAmmoBaseline=true;
    }

    public void NotifyFire(Weapon weapon)
    {
        if(weapon==null || !thirdPersonAnimator)return;
        TriggerAction("TP_Fire");
        lastWeapon=weapon;lastAmmo=weapon.CurrentAmmo;hasAmmoBaseline=true;
    }
    private void TriggerAction(string name)
    {
        var sync=thirdPersonAnimator.GetComponent<OwnerNetworkAnimator>();
        if(sync && sync.IsSpawned)sync.SetTrigger(name);else thirdPersonAnimator.SetTrigger(name);
    }
    private void DetectFire(Weapon weapon)
    {
        // Al cambiar de arma se reinicia la referencia, para que la munición
        // distinta de la otra arma no cuente como un disparo.
        if (!hasAmmoBaseline || weapon != lastWeapon)
        {
            lastWeapon = weapon;
            lastAmmo = weapon.CurrentAmmo;
            hasAmmoBaseline = true;
            return;
        }

        if (weapon.CurrentAmmo < lastAmmo)
            thirdPersonAnimator.SetTrigger("TP_Fire");

        lastAmmo = weapon.CurrentAmmo;
    }

    // =========================================================
    // PESOS DE LAYERS
    // =========================================================

    private float playerCharacterSpeed() {var character=GetComponentInParent<PlayerCharacter>();return character?character.ReloadSpeedMultiplier:1f;}

    private void UpdateLayerWeights()
    {
        bool bodyFree = playerHealth == null || (playerHealth.IsAlive && !playerHealth.IsDowned);

        upperBodyWeight = Mathf.MoveTowards(
            upperBodyWeight,
            bodyFree ? 1f : 0f,
            Time.deltaTime * layerFadeSpeed
        );

        if (upperBodyLayer >= 0)
            thirdPersonAnimator.SetLayerWeight(upperBodyLayer, upperBodyWeight);

        // Al apuntar, el clip de disparo (que es de cadera) se aplica con menos fuerza
        // para que los brazos no "salten" a la pose de cadera en cada tiro.
        if (actionsLayer >= 0)
        {
            float actionWeight = upperBodyWeight * Mathf.Lerp(1f, actionWeightWhileAiming, actualAimW);
            thirdPersonAnimator.SetLayerWeight(actionsLayer, actionWeight);
        }
    }
}