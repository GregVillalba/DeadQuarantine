using UnityEngine;

public class ThirdPersonSync : MonoBehaviour
{
    [SerializeField] private Animator thirdPersonAnimator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Weapon weapon;
    [SerializeField] private PlayerMovement playerMovement;

    private int currentAmmoLastFrame;
    private bool wasReloading;

    private void Start()
    {
        if (thirdPersonAnimator != null)
        {
            thirdPersonAnimator.SetFloat("old_pistol", 0f);
        }

        if (weapon != null)
        {
            currentAmmoLastFrame = weapon.CurrentAmmo;
        }
    }

    private void Update()
    {
        if (thirdPersonAnimator == null || weapon == null || characterController == null) return;

        float speed = characterController.velocity.magnitude;

        thirdPersonAnimator.SetFloat("TP_Speed", speed);
        thirdPersonAnimator.SetBool("TP_IsAiming", weapon.IsAiming);
        thirdPersonAnimator.SetBool("TP_IsEmpty", weapon.CurrentAmmo == 0);

        if (playerMovement != null)
        {
            thirdPersonAnimator.SetBool("TP_IsCrouching", playerMovement.IsCrouching);
        }

        DetectFire();
        DetectReload();
    }

    private void DetectFire()
    {
        if (weapon.CurrentAmmo < currentAmmoLastFrame)
        {
            thirdPersonAnimator.SetTrigger("TP_Fire");
        }

        currentAmmoLastFrame = weapon.CurrentAmmo;
    }

    private void DetectReload()
    {
        bool isReloadingNow = weapon.IsReloading;

        if (isReloadingNow && !wasReloading)
        {
            thirdPersonAnimator.SetTrigger("TP_Reload");
        }

        wasReloading = isReloadingNow;
    }
}