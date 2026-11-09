#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DeadQuarantine.Development {
 public sealed partial class Phase6WeaponsSmokeDriver {
  [Serializable] class AnimationMetric25 {public string id;public float equipSeconds,adsError,cameraKick,reticleKick,tpPoseJump,firstReloadError,secondReloadError,firstReloadSeconds,secondReloadSeconds;public int firstSamples,secondSamples;}
  int traceFrames25;
  bool TraceSwitch25(int index,string id){var a=Local.Switcher.GetComponent<Animator>();int l=a.GetLayerIndex("Holster");var state=a.GetCurrentAnimatorStateInfo(l);if((traceFrames25++%20)==0)System.IO.File.AppendAllText(System.IO.Path.Combine(folder,role+"-switch-"+id+".txt"),"index="+Local.Switcher.CurrentWeaponIndex+" switching="+Local.Switcher.IsSwitching+" phase="+Local.Switcher.IsHolstering+" hash="+state.shortNameHash+" norm="+state.normalizedTime+" transition="+a.IsInTransition(l)+" weight="+a.GetLayerWeight(l)+" bool="+a.GetBool("Holstered")+" speed="+a.speed+"\n");return Local.Switcher.CurrentWeaponIndex==index&&!Local.Switcher.IsSwitching;}
  IEnumerator Animations25(){
   Local.ShowLocalBodyForTesting=false;var motion=Local.Switcher.GetComponent<WeaponMotionApplier>();var cameraMotion=Local.GetComponentInChildren<CameraRecoil>(true);cameraMotion.enabled=true;
   foreach(string id in Phase7Ids.Where(id=>string.IsNullOrEmpty(Arg("-dq-phase25-ids"))||Arg("-dq-phase25-ids").Split(',').Contains(id))){
    var m=new AnimationMetric25{id=id};int index=Array.FindIndex(Local.Weapons,d=>d.Id==id);float began=Time.realtimeSinceStartup;Local.Switcher.UnlockWeapon(id);yield return Until(()=>TraceSwitch25(index,id),id+" first equip",10);m.equipSeconds=Time.realtimeSinceStartup-began;
    Note7(m.equipSeconds<1.4f,id+" complete switch uses authored motions without 2.5s timeout ("+m.equipSeconds+")");var weapon=Local.Switcher.CurrentWeapon;weapon.InputLocked=false;var rig=Local.Switcher.GetComponent<Animator>();
    Note7(rig.GetLayerWeight(rig.GetLayerIndex("Holster"))<.01f,id+" first usable frame releases holster layer");
    // Deliberately no settling delay or ADS before the first-ever reload.
    for(int pass=0;pass<2;pass++){
     weapon.RestaurarMunicion(weapon.CapacidadCargador-1,100);int reserve=weapon.ReserveAmmo;float start=Time.realtimeSinceStartup;weapon.SendMessage("OnReload",default(InputAction.CallbackContext));Check(weapon.IsReloading,id+" immediate reload "+pass);
     var bones=rig.GetComponentsInChildren<Transform>(true).Where(b=>new[]{"hand_l","hand_r","lowerarm_l","lowerarm_r","upperarm_l","upperarm_r"}.Contains(b.name.ToLowerInvariant())).ToArray();
     var reference=new GameObject("Phase25 source pose sampler");CopyTransforms9(rig.transform,reference.transform);var sampler=reference.AddComponent<Animator>();sampler.avatar=rig.avatar;sampler.runtimeAnimatorController=rig.runtimeAnimatorController;sampler.cullingMode=AnimatorCullingMode.AlwaysAnimate;sampler.Rebind();sampler.Update(0);int layer=rig.GetLayerIndex("Reload");for(int l=1;l<sampler.layerCount;l++)sampler.SetLayerWeight(l,l==layer?1:0);sampler.enabled=false;
     int samples=0;float error=0;bool captured=false;float deadline=Time.realtimeSinceStartup+20;
     while(weapon.IsReloading){
      if(Time.realtimeSinceStartup>deadline)throw new TimeoutException(id+" immediate reload ends");var state=rig.GetCurrentAnimatorStateInfo(layer);
      if(!weapon.UsesMultiPartReload&&state.IsName("Reload")&&!rig.IsInTransition(layer)&&state.normalizedTime>.01f&&state.normalizedTime<.85f){sampler.Play(state.fullPathHash,layer,state.normalizedTime);sampler.Update(0);foreach(var bone in bones){var expected=reference.transform.Find(Path9(bone,rig.transform));error=Mathf.Max(error,Quaternion.Angle(bone.localRotation,expected.localRotation));}samples++;}
      if(!captured&&state.normalizedTime>.25f&&state.normalizedTime<.65f){Capture(role+"-"+id+"-reload-first-"+pass+".png");captured=true;}
      yield return null;
     }
     Destroy(reference);Check(weapon.CurrentAmmo==weapon.CapacidadCargador&&weapon.ReserveAmmo==reserve-1,id+" reload conserves magazine/reserve "+pass);
     if(pass==0){m.firstReloadError=error;m.firstSamples=samples;m.firstReloadSeconds=Time.realtimeSinceStartup-start;}else{m.secondReloadError=error;m.secondSamples=samples;m.secondReloadSeconds=Time.realtimeSinceStartup-start;}
     if(!weapon.UsesMultiPartReload)Note7(samples>=2&&error<15,id+" reload from first frames matches authored source "+pass+" (samples="+samples+", error="+error+")");
     yield return Barrier("25-"+id+"-reload-"+pass);
    }
    Note7(Mathf.Abs(m.firstReloadSeconds-m.secondReloadSeconds)<1f,id+" first and subsequent reload durations agree");
    yield return Until(()=>Remote.EquippedIndex==index&&Remote.VisibleWeapon,id+" remote model selected");yield return new WaitForSeconds(.35f);
    weapon.SendMessage("OnAimStarted",default(InputAction.CallbackContext));yield return new WaitForSeconds(.7f);
    var profile=weapon.GetComponent<PackWeaponPresentation>();var phys=WeaponAttachments.Find(weapon.transform,"root");Write(role+"-aim-debug-"+id+".txt","blend="+Field7(weapon,"aimingBlend")+" offset="+Field7(weapon,"packAimOffset")+" applied="+Field7(weapon,"packAppliedOffset")+" rig="+rig.transform.localPosition+"\nphys="+phys.localToWorldMatrix+"\ncanonical="+profile.aimPhysicalToCamera+"\nparent="+profile.cameraToRigParent+"\nUsePack="+profile.AuthoredRecoil);var sight=(Transform)Field7(weapon,"adsSightReference");var camera=(Camera)Field7(weapon,"weaponCamera");var point=camera.WorldToViewportPoint(sight.position);Write(role+"-aim-point-"+id+".txt","\nview="+point+" sightPhysical="+phys.InverseTransformPoint(sight.position)+" canonicalSight="+profile.aimPhysicalToCamera.MultiplyPoint3x4(phys.InverseTransformPoint(sight.position))+" camera="+camera.transform.worldToLocalMatrix);m.adsError=Vector2.Distance(new Vector2(point.x,point.y),Vector2.one*.5f);Note7(m.adsError<.025f,id+" default iron/optic centres from authored pose ("+m.adsError+")");Capture(role+"-"+id+"-aim.png");
    var body=Remote.BodyAnimator;var joints=new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm}.Select(body.GetBoneTransform).Where(b=>b).ToArray();var initial=joints.Select(b=>b.localRotation).ToArray();Quaternion beforeCamera=Camera.main.transform.rotation;Vector3 beforeSight=point;int shots=Remote.RemoteShotCount;
    yield return Barrier("25-"+id+"-fire");weapon.SendMessage("TryFire");float stop=Time.realtimeSinceStartup+.45f;
    while(Time.realtimeSinceStartup<stop){m.cameraKick=Mathf.Max(m.cameraKick,Quaternion.Angle(beforeCamera,Camera.main.transform.rotation));var current=camera.WorldToViewportPoint(sight.position);m.reticleKick=Mathf.Max(m.reticleKick,Vector2.Distance(new Vector2(current.x,current.y),new Vector2(beforeSight.x,beforeSight.y)));for(int b=0;b<joints.Length;b++)m.tpPoseJump=Mathf.Max(m.tpPoseJump,Quaternion.Angle(initial[b],joints[b].localRotation));yield return null;}
    yield return Until(()=>Remote.RemoteShotCount>shots,id+" remote real firing");Note7(m.cameraKick>.05f&&m.cameraKick<25,id+" reference camera recoil is visible and bounded ("+m.cameraKick+")");Note7(m.reticleKick>.0001f,id+" sight is not pinned during weapon recoil ("+m.reticleKick+")");Note7(m.tpPoseJump<65,id+" TP fire retains weapon-family base pose ("+m.tpPoseJump+")");CaptureRemote("25-"+id+"-fire");
    weapon.SendMessage("OnAimCanceled",default(InputAction.CallbackContext));yield return Until(()=>!weapon.IsChambering,id+" bolt finishes",6);yield return new WaitForSeconds(.7f);Note7((float)Field7(weapon,"aimingBlend")<.001f,id+" aim out restores idle without accumulated correction");
    weapon.RestaurarMunicion(0,100);int reserveEmpty=weapon.ReserveAmmo;weapon.SendMessage("OnReload",default(InputAction.CallbackContext));yield return Until(()=>!weapon.IsReloading,id+" empty reload ends",25);Check(weapon.CurrentAmmo==weapon.CapacidadCargador&&weapon.ReserveAmmo==reserveEmpty-weapon.CapacidadCargador,id+" empty reload exact ammunition");
    Write(role+"-metric-"+id+".json",JsonUtility.ToJson(m,true));yield return Barrier("25-"+id+"-done");
   }
   yield return Barrier("25-finished");Complete(phase7Issues.Count==0,string.Join("\n",phase7Issues));
  }
 }
}
#endif
