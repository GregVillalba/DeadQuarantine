using UnityEngine;

// Attached only to the Ready state of the additional weapon controllers.
// An empty override state otherwise retains the last unholster pose over reload and ADS.
public sealed class WeaponHolsterLayerWeight : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => Apply(animator, layerIndex);

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => Apply(animator, layerIndex);

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => animator.SetLayerWeight(layerIndex, 1f);

    static void Apply(Animator animator, int layerIndex)
        => animator.SetLayerWeight(layerIndex, animator.IsInTransition(layerIndex) || animator.GetBool("Holstered") ? 1f : 0f);
}
