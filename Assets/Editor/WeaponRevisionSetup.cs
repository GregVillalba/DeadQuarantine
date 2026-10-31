#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
public static partial class OriginalCityWeaponsTools
{
 static void RepairWeaponRevision() {
  var fp=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/FirstPersonAnimatorHandgun.controller");
  var layers=fp.layers;
  var partsIndex=Array.FindIndex(layers,l=>l.name=="HandGunADD");
  var mask=new AvatarMask();for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
  var player=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
  try {
   var sw=new SerializedObject(player.GetComponentInChildren<WeaponSwitcher>(true));var animator=(Animator)sw.FindProperty("weaponAnimator").objectReferenceValue;
   var transforms=animator.GetComponentsInChildren<Transform>(true);mask.transformCount=transforms.Length;
   for(int i=0;i<transforms.Length;i++) {
    string path=AnimationUtility.CalculateTransformPath(transforms[i],animator.transform);mask.SetTransformPath(i,path);
    int start=path.IndexOf("/SKEL_",StringComparison.Ordinal);
    mask.SetTransformActive(i,start>=0 && path.Substring(start+1).Split('/').Length>=3);
   }
  }finally {PrefabUtility.UnloadPrefabContents(player);}
  string maskPath=Art+"/WeaponMechanical.mask";var oldMask=AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
  if(oldMask){EditorUtility.CopySerialized(mask,oldMask);UnityEngine.Object.DestroyImmediate(mask);mask=oldMask;}else AssetDatabase.CreateAsset(mask,maskPath);
  layers[partsIndex].blendingMode=AnimatorLayerBlendingMode.Override;layers[partsIndex].avatarMask=mask;fp.layers=layers;
  // Pump starts directly after recoil, with matching arms and weapon-piece timing.
  foreach(var layerName in new[]{"Fire","HandGunADD"}) {
   var sm=fp.layers.First(l=>l.name==layerName).stateMachine;
   var fire=AllStates(sm).First(s=>s.name=="Fire");var bolt=AllStates(sm).First(s=>s.name==(layerName=="Fire"?"Bolt":"Bolt Action"));
   foreach(var tr in fire.transitions) {
    Exit(tr,tr.destinationState==bolt ? .12f/Clip("A_FP_PCH_Shotgun_01_Fire.fbx").length:.98f);
    tr.AddCondition(tr.destinationState==bolt?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"BoltAction");
   }
  }
  foreach(var layerName in new[]{"Reload","HandGunADD"}) {
   var sm=fp.layers.First(l=>l.name==layerName).stateMachine;
   var close=AllStates(sm).First(s=>s.name==(layerName=="Reload"?"Reload_Close":"Reload Close"));
   foreach(var tr in close.transitions){tr.destinationState=sm.defaultState;Exit(tr,1);}
  }
  var closeClip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Art+"/Shotgun_Reload_Close.anim");
  var events=AnimationUtility.GetAnimationEvents(closeClip).Where(e=>e.functionName!="OnAnimationEndedReload"&&e.functionName!="AnimationReloadFinished").ToList();
  events.Add(new AnimationEvent{time=closeClip.length*.98f,functionName="OnAnimationEndedReload"});AnimationUtility.SetAnimationEvents(closeClip,events.ToArray());
  var tp=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/ThirdPersonAnimator.controller");
  var actions=tp.layers.First(l=>l.name=="Actions").stateMachine;var states=AllStates(actions).ToArray();
  var insert=states.First(s=>s.name=="Reload_Insert");foreach(var tr in insert.transitions){Exit(tr,1);tr.AddCondition(tr.destinationState==insert?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"TP_ShellLoading");}
  var tpFire=states.First(s=>s.name=="Fire");foreach(var tr in tpFire.transitions.Where(t=>t.destinationState && t.destinationState.name=="Reload_Bolt"))ExitAndCondition(tr,.12f/Clip("A_TP_CH_Shotgun_01_Fire.fbx").length,"TP_IsChambering");
  foreach(var tr in states.First(s=>s.name=="Reload_Close").transitions){tr.destinationState=states.First(s=>s.name=="Default");Exit(tr,1);}
  // Run IK after all animated upper-body layers, including the action layer.
  var tpLayers=tp.layers;tpLayers[tpLayers.Length-1].iKPass=true;tp.layers=tpLayers;
  Param(tp,"TP_Holstering",AnimatorControllerParameterType.Bool);Param(tp,"TP_Unholstering",AnimatorControllerParameterType.Bool);
  ConfigureHolster(tp);
  EditorUtility.SetDirty(fp);EditorUtility.SetDirty(tp);
 }
 static void ExitAndCondition(AnimatorStateTransition tr,float time,string parameter){Exit(tr,time);tr.AddCondition(AnimatorConditionMode.If,0,parameter);}
 static void ConfigureHolster(AnimatorController controller) {
  // There are no supplied TP holster clips. Blend the existing authored arm poses.
  // Humanoid muscle curves keep this compatible with all three character skins.
  var player=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));player.SetActive(true);
  var body=player.GetComponent<NetworkWeaponPresentation>().Body.GetComponent<Animator>();
  if(!body.isHuman)throw new InvalidOperationException("TP holster generation requires the existing humanoid avatar");
  try {
   using var handler=new HumanPoseHandler(body.avatar,body.transform);
   var pose=new HumanPose();var relaxed=new HumanPose();
   var neutralController=new AnimatorOverrideController(controller);
   // Sample imported clips directly through a temporary single-state humanoid controller.
   string path=Art+"/PoseSampling.controller";var sample=AssetDatabase.LoadAssetAtPath<AnimatorController>(path)??AnimatorController.CreateAnimatorControllerAtPath(path);
   var sm=sample.layers[0].stateMachine;foreach(var s in sm.states)sm.RemoveState(s.state);var state=sm.AddState("Pose");sm.defaultState=state;
   var holsterTree=new BlendTree{name="Holster by weapon",blendType=BlendTreeType.Simple1D,blendParameter="TP_WeaponID",useAutomaticThresholds=false};
   var drawTree=new BlendTree{name="Draw by weapon",blendType=BlendTreeType.Simple1D,blendParameter="TP_WeaponID",useAutomaticThresholds=false};
   AssetDatabase.AddObjectToAsset(holsterTree,controller);AssetDatabase.AddObjectToAsset(drawTree,controller);
   state.motion=Clip("A_TP_CH_Idle_Relaxed.fbx");body.runtimeAnimatorController=sample;body.Rebind();body.Update(.01f);handler.GetHumanPose(ref relaxed);
   for(int i=0;i<3;i++) {
    string token=i==0?"Handgun":i==1?"AR_01":"Shotgun_01";
    state.motion=Clip("A_TP_CH_"+token+"_Idle_Pose.fbx");body.Rebind();body.Update(.01f);handler.GetHumanPose(ref pose);
    foreach(bool draw in new[]{false,true}) {
     var clip=new AnimationClip{name=token+(draw?"_TP_Unholster":"_TP_Holster")};
     for(int m=0;m<HumanTrait.MuscleCount;m++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),HumanTrait.MuscleName[m]),AnimationCurve.EaseInOut(0,draw?relaxed.muscles[m]:pose.muscles[m],.4f,draw?pose.muscles[m]:relaxed.muscles[m]));
     var saved=SaveClip(clip,Art+"/"+clip.name+".anim");(draw?drawTree:holsterTree).AddChild(saved,i+1);
    }
   }
   var old=controller.layers.FirstOrDefault(l=>l.name=="WeaponSwitch");
   if(old!=null)controller.RemoveLayer(Array.FindIndex(controller.layers,l=>l.name=="WeaponSwitch"));
   controller.AddLayer("WeaponSwitch");var layers=controller.layers;var layer=layers[layers.Length-1];layer.defaultWeight=0;layer.iKPass=true;
   var mask=new AvatarMask();for(int b=0;b<(int)AvatarMaskBodyPart.LastBodyPart;b++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)b,b==(int)AvatarMaskBodyPart.LeftArm||b==(int)AvatarMaskBodyPart.RightArm||b==(int)AvatarMaskBodyPart.LeftFingers||b==(int)AvatarMaskBodyPart.RightFingers);
   string maskPath=Art+"/SwitchArms.mask";var existing=AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);if(existing){EditorUtility.CopySerialized(mask,existing);UnityEngine.Object.DestroyImmediate(mask);mask=existing;}else AssetDatabase.CreateAsset(mask,maskPath);layer.avatarMask=mask;
   var machine=layer.stateMachine;var empty=machine.AddState("Default");empty.writeDefaultValues=false;machine.defaultState=empty;
   foreach(var name in new[]{"Holster","Unholster"}) {
    var action=machine.AddState(name);action.motion=name=="Holster"?holsterTree:drawTree;action.writeDefaultValues=false;
    var tr=machine.AddAnyStateTransition(action);tr.duration=.04f;tr.canTransitionToSelf=false;tr.AddCondition(AnimatorConditionMode.If,0,name=="Holster"?"TP_Holstering":"TP_Unholstering");
    var exit=action.AddTransition(empty);exit.duration=.12f;exit.AddCondition(AnimatorConditionMode.IfNot,0,name=="Holster"?"TP_Holstering":"TP_Unholstering");
   }
   layers[layers.Length-1]=layer;controller.layers=layers;
   UnityEngine.Object.DestroyImmediate(neutralController);
  }finally {UnityEngine.Object.DestroyImmediate(player);}
 }
 public static void ReviseAndPreview(){ConfigureWeapons();PreviewWeapons();}
}
#endif
