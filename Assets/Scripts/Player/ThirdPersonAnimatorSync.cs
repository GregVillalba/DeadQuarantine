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
        Vector3 localVelocity =
            characterController.transform.InverseTransformDirection(characterController.velocity);

        float speed = characterController.velocity.magnitude;
        float normalizedSpeed = Mathf.Clamp01(speed / maxSpeedForNormalization);

        thirdPersonAnimator.SetFloat("TP_Speed", speed);
        thirdPersonAnimator.SetFloat("TP_MoveX", localVelocity.x);
        thirdPersonAnimator.SetFloat("TP_MoveZ", localVelocity.z);
        thirdPersonAnimator.SetFloat("TP_LocomotionTime", normalizedSpeed);

        bool grounded = characterController.isGrounded;
        thirdPersonAnimator.SetBool("TP_IsGrounded", grounded);

        if (!grounded && wasGrounded)
            thirdPersonAnimator.SetTrigger("TP_Jump");

        wasGrounded = grounded;

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

        thirdPersonAnimator.SetInteger(
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