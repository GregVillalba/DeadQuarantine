using UnityEngine;
using Unity.Netcode;

public class ThirdPersonAnimatorSync : MonoBehaviour
{
    [SerializeField] private Animator thirdPersonAnimator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private PlayerMovement playerMovement;

    [SerializeField] private float maxSpeedForNormalization = 8f; // = sprintSpeed de PlayerMovement

    [Header("Look (tercera persona)")]
    [SerializeField] private float lookSensitivityScale = 1f / 80f; // 1 dividido el pitch máximo de PlayerLook
    [SerializeField] private bool invertLook = true;

    [Header("Velocidades (igualar a PlayerMovement)")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float moveDamp = 0.1f;

    private float groundedTimer, aimW, crouchW;
    private bool aimTarget;

    private int currentAmmoLastFrame;
    private bool hasAmmoBaseline;
    private bool wasGrounded = true;

    private void Start()
    {
        Debug.Log(
            "[ThirdPersonAnimatorSync] Animator=" + (thirdPersonAnimator != null) +
            " | CharController=" + (characterController != null) +
            " | WeaponSwitcher=" + (weaponSwitcher != null) +
            " | PlayerHealth=" + (playerHealth != null) +
            " | PlayerLook=" + (playerLook != null) +
            " | PlayerMovement=" + (playerMovement != null)
        );
    }

    private void Update()
    {
        if (thirdPersonAnimator == null || characterController == null)
            return;

        NetworkObject networkObject = GetComponentInParent<NetworkObject>();

        if (networkObject != null && !networkObject.IsOwner)
        {
            Debug.Log("[ThirdPersonAnimatorSync] Cortado por IsOwner = false");
            return;
        }

        UpdateMovementParams();
        UpdateStateParams();
        UpdateWeaponParams();
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
        a.SetBool("TP_IsGrounded", groundedTimer < 0.12f);

        if (wasGrounded && !rawGrounded && v.y > 0.5f)
            a.SetTrigger("TP_Jump");
        wasGrounded = rawGrounded;

        aimW = Mathf.MoveTowards(aimW, aimTarget ? 1f : 0f, Time.deltaTime * 8f);
        crouchW = Mathf.MoveTowards(crouchW, crouching ? 1f : 0f, Time.deltaTime * 8f);
        a.SetFloat("TP_AimWeight", aimW);
        a.SetFloat("TP_CrouchWeight", crouchW);

        if (playerLook != null)
        {
            float normalizedLook = Mathf.Clamp(playerLook.Pitch * lookSensitivityScale, -1f, 1f);

            if (invertLook)
                normalizedLook = -normalizedLook;

            thirdPersonAnimator.SetFloat("TP_Look", normalizedLook);
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

    private void UpdateWeaponParams()
    {
        if (weaponSwitcher == null)
            return;

        Weapon weapon = weaponSwitcher.CurrentWeapon;

        thirdPersonAnimator.SetFloat(   // <- antes era SetInteger
            "TP_WeaponID",
            weapon != null ? weaponSwitcher.CurrentWeaponIndex + 1 : 0
        );

        if (weapon == null)
            return;

        thirdPersonAnimator.SetBool("TP_IsReloading", weapon.IsReloading);
        thirdPersonAnimator.SetBool("TP_IsAiming", weapon.IsAiming);

        DetectFire(weapon);
    }

    private void DetectFire(Weapon weapon)
    {
        if (!hasAmmoBaseline)
        {
            currentAmmoLastFrame = weapon.CurrentAmmo;
            hasAmmoBaseline = true;
            return;
        }

        if (weapon.CurrentAmmo < currentAmmoLastFrame)
            thirdPersonAnimator.SetTrigger("TP_Fire");

        currentAmmoLastFrame = weapon.CurrentAmmo;
    }
}