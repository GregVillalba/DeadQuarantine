using UnityEngine;

// Keeps the support hand on the same foregrip used by the first-person model.
// The authored reload and switch animations retain control during those actions.
[RequireComponent(typeof(Animator))]
public sealed class ThirdPersonWeaponGrip : MonoBehaviour
{
    Animator animator;
    NetworkWeaponPresentation presentation;
    Transform weapon, target;
    void Awake() { animator=GetComponent<Animator>();presentation=GetComponentInParent<NetworkWeaponPresentation>(); }
    void OnAnimatorIK(int layerIndex)
    {
        if (!animator) animator=GetComponent<Animator>();
        if (!presentation) presentation=GetComponentInParent<NetworkWeaponPresentation>();
        if (!animator || !animator.isHuman || !presentation || layerIndex!=animator.layerCount-1) return;
        var current=presentation.VisibleWeapon;
        if (!current) return;
        if (weapon!=current.transform) {
            weapon=current.transform;target=null;
            foreach(var child in weapon.GetComponentsInChildren<Transform>(true))if(child.name=="TP_LeftHandGrip"){target=child;break;}
        }
        bool animated=animator.GetBool("TP_IsReloading") || animator.GetBool("TP_Holstering") || animator.GetBool("TP_Unholstering");
        float weight=target && !animated ? 1:0;
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,weight);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,weight);
        if(weight>0){animator.SetIKPosition(AvatarIKGoal.LeftHand,target.position);animator.SetIKRotation(AvatarIKGoal.LeftHand,target.rotation);}
    }
}
