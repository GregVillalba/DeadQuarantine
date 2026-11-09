using UnityEngine;
// Empty override states must release their last pose before the next action or ADS.
// Installed only on the additional weapon controllers.
public sealed class WeaponActionLayerWeight : StateMachineBehaviour {
 public override void OnStateEnter(Animator a,AnimatorStateInfo s,int layer)=>Apply(a,s,layer);
 public override void OnStateUpdate(Animator a,AnimatorStateInfo s,int layer)=>Apply(a,s,layer);
 public override void OnStateExit(Animator a,AnimatorStateInfo s,int layer)=>a.SetLayerWeight(layer,1);
 static void Apply(Animator a,AnimatorStateInfo state,int layer)=>a.SetLayerWeight(layer,a.IsInTransition(layer)&&a.GetNextAnimatorStateInfo(layer).shortNameHash!=state.shortNameHash?1:0);
}
