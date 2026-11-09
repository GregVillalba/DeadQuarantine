using UnityEngine;

public partial class WeaponSwitcher
{
    PlayerHealth perkHealth;
    float switchBaseSpeed;
    bool perkSpeedApplied;
    float PerkSwitchMultiplier => (perkHealth ? perkHealth : perkHealth = GetComponentInParent<PlayerHealth>())?.PerkSwitchSpeed ?? 1;
    void BeginPerkSwitchSpeed()
    {
        if (!weaponAnimator || PerkSwitchMultiplier == 1) return;
        if (!perkSpeedApplied) { switchBaseSpeed = weaponAnimator.speed; perkSpeedApplied = true; }
        RefreshPerkSwitchSpeed();
    }
    void RefreshPerkSwitchSpeed() { if (perkSpeedApplied && weaponAnimator) weaponAnimator.speed = switchBaseSpeed * PerkSwitchMultiplier; }
    internal void EndPerkSwitchSpeed() { if (perkSpeedApplied && weaponAnimator) weaponAnimator.speed = switchBaseSpeed; perkSpeedApplied = false; }
}
