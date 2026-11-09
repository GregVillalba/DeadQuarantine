using Unity.Netcode.Components;

// Player animation parameters originate in the existing owner-driven controller.
public sealed class OwnerNetworkAnimator : NetworkAnimator
{
    protected override void Awake()
    {
        // NGO allocates its layer caches in Awake. Start with the same topology
        // as every weapon variant before the owner/remote presentation changes it.
        var presentation = GetComponentInParent<NetworkWeaponPresentation>();
        if (presentation && Animator && presentation.BodyAnimator == Animator && presentation.Weapons.Length > 0)
        {
            var first = presentation.Weapons[0];
            if (first.Source && first.Source.GetComponent<PackWeaponPresentation>() && first.Controller)
                Animator.runtimeAnimatorController = first.Controller;
        }
        base.Awake();
    }
    protected override bool OnIsServerAuthoritative() => false;
}
