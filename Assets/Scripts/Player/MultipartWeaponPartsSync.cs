using UnityEngine;

/// <summary>Samples multipart weapon mechanics from the existing character animator.
/// Used only by weapons whose reload has separately authored open/insert/close clips.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class MultipartWeaponPartsSync : MonoBehaviour
{
 Animator parts, character;
 bool firstPerson;
 void OnEnable(){parts=GetComponent<Animator>();firstPerson=GetComponent<Weapon>()!=null;character=null;}
 void LateUpdate(){
  if(!parts)return;
  if(!character){
   if(firstPerson)character=GetComponentInParent<WeaponSwitcher>()?.GetComponent<Animator>();
   else character=GetComponentInParent<NetworkWeaponPresentation>()?.BodyAnimator;
  }
  if(!character||!character.isActiveAndEnabled)return;
  int reload=character.GetLayerIndex(firstPerson?"Reload":"Actions");
  string selected="Idle";float time=0;
  if(reload>=0)Select(character.GetCurrentAnimatorStateInfo(reload),ref selected,ref time);
  if(firstPerson && selected=="Idle"){
   int fire=character.GetLayerIndex("Fire");if(fire>=0)Select(character.GetCurrentAnimatorStateInfo(fire),ref selected,ref time);
  }
  parts.Play(selected,0,time);parts.Update(0);
 }
 static void Select(AnimatorStateInfo state,ref string selected,ref float time){
  foreach(string token in new[]{"Open","Insert","Close","Bolt"})
   if(state.IsName("Reload_"+token)||token=="Insert"&&state.IsName("Reload_Insert 2")||token=="Bolt"&&state.IsName("Bolt")){
    selected="Reload_"+token;time=Mathf.Clamp01(state.normalizedTime);return;
   }
 }
}
