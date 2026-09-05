using UnityEngine;

public class WeaponAnimationEvents : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Weapon weapon;

    [Header("Vaina")]
    [SerializeField] private GameObject casingPrefab;
    [SerializeField] private Transform casingEjectPoint;
    [SerializeField] private float casingForce = 2f;

    [Header("Weapon Switcher")]
    [SerializeField] private WeaponSwitcher weaponSwitcher;

    public void OnEjectCasing()
    {
        if (casingPrefab == null || casingEjectPoint == null)
            return;

        Instantiate(
            casingPrefab,
            casingEjectPoint.position,
            casingEjectPoint.rotation
        );
    }
    public void OnSlideBack()
    {
        // El retroceso de la corredera ya está contenido
        // en la propia animación A_FP_PCH_Handgun_Reload_Empty.
        // Este evento existe para ser compatible con el clip.
    }

    public void OnAmmunitionFill()
    {
        if (weapon != null)
            weapon.AnimationAmmunitionFill();
    }

    public void OnAnimationEndedReload()
    {
        if (weapon != null)
            weapon.AnimationReloadFinished();
    }

    public void SetWeapon(Weapon newWeapon)
    {
        weapon = newWeapon;
    }

    public void OnAnimationEndedHolster()
    {
        // Se dispara al terminar A_FP_PCH_Handgun_Unholster:
        // el arma ya terminó de salir y está lista para disparar.
        if (weaponSwitcher != null)
            weaponSwitcher.NotifyUnholsterFinished();
    }

}