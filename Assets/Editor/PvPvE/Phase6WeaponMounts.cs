#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static partial class CompleteWeaponsIntegrationBuilder
{
 public static void RefineMissingPhase6(){
  var root=PrefabUtility.LoadPrefabContents(WeaponAssetLocations.Resolve("Assets/Prefabs/Player.prefab"));var lines=new List<string>();
  try{
   var sw=root.GetComponentInChildren<WeaponSwitcher>(true);var so=new SerializedObject(sw);var rig=sw.GetComponent<Animator>();
   var hand=rig.GetComponentsInChildren<Transform>(true).First(t=>t.name.Equals("hand_r",StringComparison.OrdinalIgnoreCase));
   AnimationMode.StartAnimationMode();
   foreach(var s in Specs6()){
    var slot=ReadSlotTemplate(so,s.Id);var reference=ReadSlotTemplate(so,s.Family==Family.Handgun?"Handgun":"Rifle");
    var clips=new List<KeyValuePair<AnimationClip,AnimationClip>>();if(reference.FPOverride)reference.FPOverride.GetOverrides(clips);
    var entry=clips.FirstOrDefault(k=>k.Key.name.IndexOf("Idle_Pose",StringComparison.OrdinalIgnoreCase)>=0);
    var referencePose=entry.Value?entry.Value:entry.Key?entry.Key:RequireClip("A_FP_PCH_Handgun_Idle_Pose");
    Sample6(rig.gameObject,referencePose);
    Matrix4x4 handGrip=hand.worldToLocalMatrix*FindDeep(reference.Root.transform,"root").localToWorldMatrix;
    string code=s.Model.Substring(3), family=s.Family==Family.Handgun?"Handgun":code;
    var idle=RequireClip("A_FP_PCH_"+family+"_Idle_Pose");var aim=RequireClip("A_FP_PCH_"+family+"_Aim_Pose");
    Sample6(rig.gameObject,idle);
    var grip=FindDeep(slot.Root.transform,"root");
    Matrix4x4 relative=slot.Root.transform.worldToLocalMatrix*grip.localToWorldMatrix;
    Matrix4x4 desired=hand.localToWorldMatrix*handGrip;
    var world=desired*relative.inverse;
    // Capture the mounting pose as data; no runtime correction touches the shared rig.
    Vector3 position=slot.Root.transform.parent.InverseTransformPoint(world.GetColumn(3));
    Quaternion rotation=Quaternion.Inverse(slot.Root.transform.parent.rotation)*world.rotation;
    AnimationMode.StopAnimationMode();
    slot.Root.transform.localPosition=position;slot.Root.transform.localRotation=rotation;slot.Root.transform.localScale=Vector3.one;
    AnimationMode.StartAnimationMode();Sample6(rig.gameObject,idle);
    float idleError=Vector3.Distance(grip.position,(hand.localToWorldMatrix*handGrip).GetColumn(3));
    Sample6(rig.gameObject,aim);
    float aimError=Vector3.Distance(grip.position,(hand.localToWorldMatrix*handGrip).GetColumn(3));
    lines.Add(s.Id+" mount="+position+" rotation="+rotation.eulerAngles+" idleHandError="+idleError+" aimHandError="+aimError);
    AnimationMode.StopAnimationMode();
    var presentation=root.GetComponent<NetworkWeaponPresentation>();
    var definition=FindDefinition(presentation.Weapons,s.Id);var referenceDefinition=FindDefinition(presentation.Weapons,reference.Id);
    // SOCKET_Grip belongs to optional foregrips, not to the trigger hand.
    // The models use the same authored weapon origin and axes as the stable family template.
    RootGrip6(referenceDefinition,definition.Model,out definition.GripPosition,out definition.GripRotation);
    EditorUtility.SetDirty(presentation);
    var tpClips=TPClips6(s);var tpController=(AnimatorController)slot.TPOverride.runtimeAnimatorController;
    foreach(var layer in tpController.layers)if(layer.name!="WeaponSwitch")foreach(var state in States6(layer.stateMachine))ReplaceMotion6(state.motion,c=>Map6(c,tpClips,true),m=>state.motion=m);
    EditorUtility.SetDirty(tpController);
    var ctl=(AnimatorController)slot.FPOverride.runtimeAnimatorController;
    var layers=ctl.layers;var reload=layers.First(l=>l.name=="Reload");reload.blendingMode=AnimatorLayerBlendingMode.Override;ctl.layers=layers;EditorUtility.SetDirty(ctl);
    if(s.Id=="Sniper01"){
     var settings=new SerializedObject(slot.Weapon);SetFloat(settings,"boltActionDuration",2.05f);SetFloat(settings,"fireRate",2.05f);
     SetObject(settings,"reloadSound",null);SetObject(settings,"reloadEmptySound",null);
     SetObject(settings,"reloadBoltOpenSound",RequireAsset<AudioClip>("S_WEP_Sniper_01_Reload_Open"));SetObject(settings,"reloadBoltCloseSound",RequireAsset<AudioClip>("S_WEP_Sniper_01_Reload_Close"));settings.ApplyModifiedPropertiesWithoutUndo();
    }
    AnimationMode.StartAnimationMode();
   }
   AnimationMode.StopAnimationMode();PrefabUtility.SaveAsPrefabAsset(root,WeaponAssetLocations.Resolve("Assets/Prefabs/Player.prefab"));AssetDatabase.SaveAssets();
  }finally{if(AnimationMode.InAnimationMode())AnimationMode.StopAnimationMode();PrefabUtility.UnloadPrefabContents(root);}
  Directory.CreateDirectory(WeaponAssetLocations.Resolve(Phase6Output));File.WriteAllLines(WeaponAssetLocations.Resolve(Path.Combine(Phase6Output,"mounts.txt")),lines);
 }
 static void RootGrip6(NetworkWeaponPresentation.Definition reference,GameObject target,out Vector3 position,out Vector3 rotation){
  var staging=new GameObject("Phase6 origin fitting");staging.SetActive(false);
  try{
   var a=UnityEngine.Object.Instantiate(reference.Model,staging.transform);a.transform.localPosition=reference.GripPosition;a.transform.localRotation=Quaternion.Euler(reference.GripRotation);
   var b=UnityEngine.Object.Instantiate(target,staging.transform);b.transform.localPosition=Vector3.zero;b.transform.localRotation=Quaternion.identity;
   var desired=staging.transform.worldToLocalMatrix*FindDeep(a.transform,"root").localToWorldMatrix;
   var relative=b.transform.worldToLocalMatrix*FindDeep(b.transform,"root").localToWorldMatrix;
   var result=desired*relative.inverse;position=result.GetColumn(3);rotation=result.rotation.eulerAngles;
  }finally{UnityEngine.Object.DestroyImmediate(staging);}
 }
 static ClipSet TPClips6(Spec s){
  if(!s.Id.StartsWith("Sniper"))return ResolveTPClips(s);
  string prefix="A_TP_CH_"+s.Model.Substring(3)+"_";bool bolt=s.Id=="Sniper01";
  return new ClipSet{AimPose=RequireClip(prefix+"Aim_Pose"),Idle=RequireClip(prefix+"Idle_Pose"),Fire=OptionalClip(prefix+"Fire")??RequireClip("A_TP_CH_Sniper_03_Fire"),
   CrouchPose=RequireClip(prefix+"Crouch_Pose"),CrouchAimPose=RequireClip(prefix+"Crouch_Aim_Pose"),Jog=RequireClip(prefix+"Jog_F"),Sprint=RequireClip(prefix+"Sprint_F"),SprintImpulse=RequireClip(prefix+"Sprint_Impulse_F"),Inspect=RequireClip(prefix+"Inspect"),Reload=RequireClip(prefix+(bolt?"Reload_Open":"Reload")),ReloadEmpty=RequireClip(prefix+(bolt?"Reload_Open":"Reload_Empty"))};
 }
 static void Sample6(GameObject rig,AnimationClip clip){AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(rig,clip,0);AnimationMode.EndSampling();}
}
#endif
