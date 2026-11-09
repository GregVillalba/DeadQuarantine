#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DeadQuarantine.Development
{
 public sealed partial class Phase6WeaponsSmokeDriver {
  IEnumerator Animations9(){
   Local.ShowLocalBodyForTesting=false;
   foreach(string id in Phase7Ids){
    int index=Array.FindIndex(Local.Weapons,d=>d.Id==id);Local.Switcher.UnlockWeapon(id);
    yield return Until(()=>Local.Switcher.CurrentWeaponIndex==index&&!Local.Switcher.IsSwitching,id+" equipped");
    var weapon=Local.Switcher.CurrentWeapon;weapon.InputLocked=false;yield return new WaitForSeconds(.3f);yield return Barrier("9-"+id+"-equip");
    var rig=Local.Switcher.GetComponent<Animator>();var bones=rig.GetComponentsInChildren<Transform>(true).Where(t=>new[]{"hand_l","hand_r","lowerarm_l","lowerarm_r","upperarm_l","upperarm_r"}.Contains(t.name.ToLowerInvariant())).ToArray();
    Check(bones.Length>=4,id+" authored FP arm bones found");
    weapon.SendMessage("OnAimStarted",default(InputAction.CallbackContext));
    yield return new WaitForSeconds(.7f);yield return CheckSight7(id,weapon);Capture(role+"-"+id+"-aim-full.png");
    float before=(float)Field7(weapon,"aimingBlend");weapon.SendMessage("OnAimCanceled",default(InputAction.CallbackContext));
    yield return new WaitForSeconds(.035f);float leaving=(float)Field7(weapon,"aimingBlend");
    Check(before>.99f&&leaving>.05f&&leaving<before,id+" aim out fades instead of snapping off");Capture(role+"-"+id+"-aim-out.png");
    yield return new WaitForSeconds(.8f);Check((float)Field7(weapon,"aimingBlend")<.001f,id+" aim out restores neutral pose completely");
    for(int pass=0;pass<2;pass++){
     bool empty=pass==1;weapon.RestaurarMunicion(empty?0:weapon.CapacidadCargador-1,100);int reserveBefore=weapon.ReserveAmmo;
     weapon.SendMessage("OnAimStarted",default(InputAction.CallbackContext));yield return new WaitForSeconds(.6f);
     var initial=bones.Select(b=>b.localRotation).ToArray();
     weapon.SendMessage("OnReload",default(InputAction.CallbackContext));Check(weapon.IsReloading,id+" "+pass+" reload starts through existing controls");
     int layer=rig.GetLayerIndex("Reload");var parts=weapon.GetComponent<Animator>();float maxMotion=0,maxError=0,maxPartsDrift=0;int samples=0;bool captured=false;float deadline=Time.realtimeSinceStartup+20;
     var reference=new GameObject("Reference FP skeleton");CopyTransforms9(rig.transform,reference.transform);var sampler=reference.AddComponent<Animator>();sampler.avatar=rig.avatar;sampler.runtimeAnimatorController=rig.runtimeAnimatorController;sampler.cullingMode=AnimatorCullingMode.AlwaysAnimate;sampler.Rebind();sampler.Update(0);for(int l=1;l<sampler.layerCount;l++)sampler.SetLayerWeight(l,l==layer?1:0);sampler.enabled=false;reference.SetActive(true);
     while(weapon.IsReloading){
      if(Time.realtimeSinceStartup>deadline)throw new TimeoutException(id+" reload completion");
      var state=rig.GetCurrentAnimatorStateInfo(layer);
      if(!weapon.UsesMultiPartReload&&(state.IsName(empty?"Reload Empty":"Reload"))&&!rig.IsInTransition(layer)&&state.normalizedTime>.15f&&state.normalizedTime<.9f){
       sampler.Play(state.fullPathHash,layer,Mathf.Clamp01(state.normalizedTime));sampler.Update(0);
       for(int b=0;b<bones.Length;b++){
        var path=Path9(bones[b],rig.transform);var expected=reference.transform.Find(path);
        maxMotion=Mathf.Max(maxMotion,Quaternion.Angle(initial[b],bones[b].localRotation));
        maxError=Mathf.Max(maxError,Quaternion.Angle(expected.localRotation,bones[b].localRotation));
       }
       maxPartsDrift=Mathf.Max(maxPartsDrift,Mathf.Abs(parts.GetCurrentAnimatorStateInfo(0).normalizedTime-state.normalizedTime));samples++;
       if(!captured&&state.normalizedTime>.4f){Capture(role+"-"+id+(empty?"-empty":"-partial")+"-reload.png");CaptureRemote(id+(empty?"-empty":"-partial")+"-reload");captured=true;}
      }
      if(weapon.UsesMultiPartReload)CaptureReload(id);
      yield return null;
     }
     Destroy(reference);
     if(!weapon.UsesMultiPartReload){
      Check(samples>2&&maxMotion>5,id+" "+pass+" reload moves authored arms through distinct poses (samples="+samples+", motion="+maxMotion+", error="+maxError+")");
      Note7(maxError<15,id+" "+pass+" arms match the source reload clip (max angular error="+maxError+")");
      Note7(maxPartsDrift<.1f,id+" "+pass+" magazine and hands stay synchronized (drift="+maxPartsDrift+")");
     }
     Check(weapon.CurrentAmmo==weapon.CapacidadCargador&&weapon.ReserveAmmo==reserveBefore-(empty?weapon.CapacidadCargador:1),id+" "+pass+" reload preserves exact ammunition");
     yield return Until(()=>weapon.IsAiming,id+" returns to held aim after reload",3);
     weapon.SendMessage("OnAimCanceled",default(InputAction.CallbackContext));yield return new WaitForSeconds(.7f);
     yield return Barrier("9-"+id+"-reload-"+pass);
    }
   }
   yield return Barrier("9-complete");Complete(phase7Issues.Count==0,string.Join("\n",phase7Issues));
  }
  static string Path9(Transform bone,Transform root){var names=new List<string>();for(var t=bone;t&&t!=root;t=t.parent)names.Add(t.name);names.Reverse();return string.Join("/",names);}
  static void CopyTransforms9(Transform source,Transform target){foreach(Transform child in source){var copy=new GameObject(child.name).transform;copy.SetParent(target,false);copy.localPosition=child.localPosition;copy.localRotation=child.localRotation;copy.localScale=child.localScale;CopyTransforms9(child,copy);}}
 }
}
#endif
