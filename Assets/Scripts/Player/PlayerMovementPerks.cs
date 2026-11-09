using UnityEngine;

public partial class PlayerMovement
{
    double chargedSlide;
    public bool IsSliding => playerHealth && playerHealth.Deslizando && characterController.isGrounded && !MovementLocked && !isParkouring;
    void UpdatePerkSlide()
    {
        if (!playerHealth || !playerHealth.PerksCatalog || !playerHealth.TienePerk(DeadQuarantine.PvPvE.PassivePerk.Kneepads)) return;
        if (playerHealth.Deslizando && playerHealth.SlideUntil.Value > chargedSlide)
        {
            chargedSlide = playerHealth.SlideUntil.Value;
            CurrentStamina = Mathf.Max(0, CurrentStamina - playerHealth.PerksCatalog.slideCost);
            if (CurrentStamina == 0) isExhausted = true;
        }
        if (controls.Player.Crouch.WasPressedThisFrame() && IsSprinting && characterController.isGrounded && !MovementLocked &&
            !PlayerInputGate.ActionsBlocked && playerHealth.IsAlive && CurrentStamina >= playerHealth.PerksCatalog.slideCost)
        {
            Vector3 direction = transform.right * moveInput.x + transform.forward * moveInput.y;
            if (direction.sqrMagnitude > .1f) playerHealth.SolicitarDeslizamientoServerRpc(direction.normalized);
        }
    }
}
