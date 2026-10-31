using Unity.Netcode.Components;

// Player animation parameters originate in the existing owner-driven controller.
public sealed class OwnerNetworkAnimator : NetworkAnimator
{
    protected override bool OnIsServerAuthoritative() => false;
}
