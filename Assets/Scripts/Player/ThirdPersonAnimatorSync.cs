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
    private float crouchW;
    private bool wasGrounded = true;

    private Weapon cachedWeapon;
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
            a.SetTrigger("TP_Jump");

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

    // Busca el arma que realmente está activa. Si la referencia del WeaponSwitcher
    // es nula o apunta a un arma desactivada, usa la primera Weapon activa del Player.
    private Weapon ResolveWeapon()
    {
        Weapon w = weaponSwitcher != null ? weaponSwitcher.CurrentWeapon : null;

        if (w != null && w.isActiveAndEnabled)
            return w;

        if (cachedWeapon == null || !cachedWeapon.isActiveAndEnabled)
            cachedWeapon = transform.root.GetComponentInChildren<Weapon>(false);

        return cachedWeapon;
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
                weapon != null ? weaponSwitcher.CurrentWeaponIndex + 1 : 0
            );
        }

        bool aiming = weapon != null && weapon.IsAiming;
        bool reloading = weapon != null && weapon.IsReloading;

        // El peso de apuntado se actualiza SIEMPRE, haya arma o no.
        aimW = Mathf.MoveTowards(aimW, aiming ? 1f : 0f, Time.deltaTime * aimBlendSpeed);

        a.SetFloat("TP_AimWeight", aimW);
        a.SetBool("TP_IsAiming", aiming);
        a.SetBool("TP_IsReloading", reloading);

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

        // TP_IsEmpty se "congela" al empezar la recarga: durante la animación la
        // munición se rellena y sin esto el estado cambiaría de Reload_Empty a Reload.
        if (reloading && !wasReloading)
            reloadStartedEmpty = weapon.CurrentAmmo == 0;

        wasReloading = reloading;

        a.SetBool("TP_IsEmpty", reloading ? reloadStartedEmpty : weapon.CurrentAmmo == 0);

        DetectFire(weapon);
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
            float actionWeight = upperBodyWeight * Mathf.Lerp(1f, actionWeightWhileAiming, aimW);
            thirdPersonAnimator.SetLayerWeight(actionsLayer, actionWeight);
        }
    }
}