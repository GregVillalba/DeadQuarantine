#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Unity.Netcode.Components;
public static partial class OriginalCityWeaponsTools
{
 const string Art="Assets/Animations/CoopPresentation";
 const string Models="Assets/Prefabs/Weapons/ThirdPerson";
 static AnimationClip Clip(string file) {
  string path=AssetDatabase.GetAllAssetPaths().FirstOrDefault(p=>Path.GetFileName(p).Equals(file,StringComparison.OrdinalIgnoreCase));
  if(path==null)throw new InvalidOperationException("Missing clip "+file);
  return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
 }
 static AnimationClip SaveClip(AnimationClip clip,string path) {var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(old){EditorUtility.CopySerialized(clip,old);UnityEngine.Object.DestroyImmediate(clip);return old;}AssetDatabase.CreateAsset(clip,path);return clip;}
 static IEnumerable<AnimatorState> AllStates(AnimatorStateMachine sm) => sm.states.Select(s=>s.state).Concat(sm.stateMachines.SelectMany(s=>AllStates(s.stateMachine)));
 static void Exit(AnimatorStateTransition transition,float time=1) {transition.conditions=Array.Empty<AnimatorCondition>();transition.hasExitTime=true;transition.exitTime=time;transition.duration=.03f;}
 static void Param(AnimatorController ctrl,string name,AnimatorControllerParameterType type) {if(!ctrl.parameters.Any(p=>p.name==name))ctrl.AddParameter(name,type);}
 public static void ConfigureWeapons() {
  Directory.CreateDirectory(Output);Directory.CreateDirectory(Art);Directory.CreateDirectory(Models);AssetDatabase.Refresh();
  RepairAnimators();
  RepairWeaponRevision();
  var player=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
  try {
   var sw=player.GetComponentInChildren<WeaponSwitcher>(true);var so=new SerializedObject(sw);var slots=so.FindProperty("slots");
   var fp=(Animator)so.FindProperty("weaponAnimator").objectReferenceValue;var body=(Animator)so.FindProperty("thirdPersonAnimator").objectReferenceValue;
   foreach(var old in body.GetComponents<NetworkAnimator>().Where(c=>c.GetType()==typeof(NetworkAnimator)).ToArray())UnityEngine.Object.DestroyImmediate(old);
   var sync=body.GetComponent<OwnerNetworkAnimator>();if(!sync)sync=body.gameObject.AddComponent<OwnerNetworkAnimator>();sync.Animator=body;
   foreach(var old in player.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c && c.GetType().Name=="ThirdPersonSync").ToArray())UnityEngine.Object.DestroyImmediate(old);
   var nameplate=player.GetComponents<MonoBehaviour>().First(c=>c && c.GetType().Name=="PlayerNameplate");var nameSettings=new SerializedObject(nameplate);
   var label=(TMPro.TMP_Text)nameSettings.FindProperty("txtPlayerName").objectReferenceValue;
   var overlayShader=Shader.Find("TextMeshPro/Distance Field Overlay")??Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
   if(!overlayShader)throw new InvalidOperationException("Missing existing TMP overlay shader");
   string nameMaterialPath=Art+"/PlayerNameOverlay.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(nameMaterialPath);
   if(!material){material=new Material(label.fontSharedMaterial);AssetDatabase.CreateAsset(material,nameMaterialPath);}material.shader=overlayShader;material.renderQueue=4000;EditorUtility.SetDirty(material);nameSettings.FindProperty("overlayTemplate").objectReferenceValue=material;nameSettings.ApplyModifiedPropertiesWithoutUndo();
   var presentation=player.GetComponent<NetworkWeaponPresentation>();if(!presentation)presentation=player.AddComponent<NetworkWeaponPresentation>();
   if(!body.GetComponent<ThirdPersonWeaponGrip>())body.gameObject.AddComponent<ThirdPersonWeaponGrip>();
   presentation.Body=body.transform;presentation.BodyAnimator=body;presentation.Switcher=sw;presentation.Weapons=new NetworkWeaponPresentation.Definition[slots.arraySize];
   for(int i=0;i<slots.arraySize;i++) {
    var slot=slots.GetArrayElementAtIndex(i);string id=slot.FindPropertyRelative("weaponId").stringValue;
    var source=(Weapon)slot.FindPropertyRelative("weaponComponent").objectReferenceValue;
    foreach(var muzzle in source.GetComponentsInChildren<Muzzle>(true)) {
     var config=new SerializedObject(muzzle);config.FindProperty("flashParticlesPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/P_LPSP_WEP_Flash.prefab");config.FindProperty("flashLightPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/P_LPSP_WEP_Flash_Light.prefab");config.FindProperty("flashLightOffset").vector3Value=Vector3.zero;config.ApplyModifiedPropertiesWithoutUndo();
     foreach(Transform child in muzzle.transform.Cast<Transform>().ToArray())if(child.GetComponent<ParticleSystem>()||child.GetComponent<Light>()||child.name.StartsWith("P_LPSP_WEP_Flash"))UnityEngine.Object.DestroyImmediate(child.gameObject);
    }
    var model=MakeModel(player,fp,body,slot,i,out Vector3 grip,out Vector3 rotation);
    presentation.Weapons[i]=new NetworkWeaponPresentation.Definition{Id=id,Source=source,Model=model,Controller=(RuntimeAnimatorController)(slot.FindPropertyRelative("tpOverrideController").objectReferenceValue??so.FindProperty("tpBaseController").objectReferenceValue),Casing=(GameObject)slot.FindPropertyRelative("casingPrefab").objectReferenceValue,GripPosition=grip,GripRotation=rotation};
    if(source.UsesMultiPartReload) {var ws=new SerializedObject(source);ws.FindProperty("boltActionDuration").floatValue=.12f+Clip("A_FP_PCH_Shotgun_01_Reload_Bolt.fbx").length;ws.ApplyModifiedPropertiesWithoutUndo();}
   }
   foreach(var old in body.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("SM_Wep_")).ToArray())old.gameObject.SetActive(false);
   // Sampling for grip calibration happens in separate temporary clones. No authored poses are saved here.
   PrefabUtility.SaveAsPrefabAsset(player,"Assets/Prefabs/Player.prefab");
  }finally {PrefabUtility.UnloadPrefabContents(player);}
  AssetDatabase.SaveAssets();
 }
 static void RepairAnimators() {
  var fp=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/FirstPersonAnimatorHandgun.controller");
  var movement=fp.layers.First(l=>l.name=="MovementActions").stateMachine;
  foreach(var state in AllStates(movement))foreach(var transition in state.transitions) {
   if(state.name=="Running" && transition.destinationState && transition.destinationState.name=="BTree Locomotion") {transition.conditions=Array.Empty<AnimatorCondition>();transition.AddCondition(AnimatorConditionMode.IfNot,0,"Running");}
   if(state.name=="BTree Locomotion" && transition.destinationState && transition.destinationState.name=="Running") {transition.conditions=Array.Empty<AnimatorCondition>();transition.AddCondition(AnimatorConditionMode.If,0,"Running");}
  }
  var fire=fp.layers.First(l=>l.name=="Fire").stateMachine;
  var shot=fire.states.First(s=>s.state.name=="Fire").state;var bolt=fire.states.First(s=>s.state.name=="Bolt").state;
  foreach(var tr in shot.transitions) {if(tr.destinationState==bolt){Exit(tr,.98f);tr.AddCondition(AnimatorConditionMode.If,0,"BoltAction");}else {Exit(tr,.98f);tr.AddCondition(AnimatorConditionMode.IfNot,0,"BoltAction");}}
  foreach(var tr in bolt.transitions)Exit(tr,.98f);
  foreach(var tr in fire.states.First(s=>s.state.name=="Fire Empty").state.transitions)Exit(tr,.95f);
  var reload=fp.layers.First(l=>l.name=="Reload").stateMachine;
  foreach(var state in AllStates(reload).Where(s=>s.name=="Reload"||s.name=="Reload Empty"))foreach(var tr in state.transitions)Exit(tr,.99f);
  foreach(var tr in AllStates(reload).First(s=>s.name=="Reload_Close").transitions)Exit(tr,1);
  Param(fp,"WeaponPartsFire",AnimatorControllerParameterType.Trigger);Param(fp,"WeaponPartsReload",AnimatorControllerParameterType.Trigger);
  var parts=fp.layers.First(l=>l.name=="HandGunADD").stateMachine;
  foreach(var transition in parts.anyStateTransitions) {
   var conditions=transition.conditions;
   for(int i=0;i<conditions.Length;i++) {var c=conditions[i];if(c.parameter=="Fire")c.parameter="WeaponPartsFire";if(c.parameter=="Reload")c.parameter="WeaponPartsReload";conditions[i]=c;}
   transition.conditions=conditions;
  }
  foreach(var tr in parts.anyStateTransitions.Where(t=>t.conditions.Any(c=>c.parameter=="BoltAction")).ToArray())parts.RemoveAnyStateTransition(tr);
  foreach(var tr in parts.anyStateTransitions.Where(t=>t.destinationState && t.destinationState.name=="Fire"))tr.conditions=tr.conditions.Where(c=>c.parameter!="MultiPartReload").ToArray();
  foreach(var state in AllStates(parts)) {
   if(state.name=="Reload Open")foreach(var tr in state.transitions)Exit(tr,1);
   if(state.name=="Reload Empty")foreach(var tr in state.transitions)Exit(tr,.99f);
   if(state.name=="Bolt Action")foreach(var tr in state.transitions)Exit(tr,.98f);
   if(state.name=="Reload Close")foreach(var tr in state.transitions){tr.destinationState=AllStates(parts).First(s=>s.name=="Bolt Action");Exit(tr,1);}
   if(state.name=="Fire") {
    state.speed=1;
    foreach(var tr in state.transitions){Exit(tr,.98f);tr.AddCondition(AnimatorConditionMode.IfNot,0,"BoltAction");}
    var target=AllStates(parts).First(s=>s.name=="Bolt Action");
    if(!state.transitions.Any(t=>t.destinationState==target)){var tr=state.AddTransition(target);Exit(tr,.98f);tr.AddCondition(AnimatorConditionMode.If,0,"BoltAction");}
   }
  }
  var shotgun=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/Animations/OverrideFirstPerson/OC_Shotgun.overrideController");
  var run=UnityEngine.Object.Instantiate(Clip("A_FP_PCH_Shotgun_01_Run.fbx"));run.name="Shotgun_Run_Additive";
  var settings=AnimationUtility.GetAnimationClipSettings(run);settings.loopTime=true;settings.loopBlend=true;AnimationUtility.SetAnimationClipSettings(run,settings);
  AnimationUtility.SetAdditiveReferencePose(run,Clip("A_FP_PCH_Shotgun_01_Idle_Pose.fbx"),0);
  var runAsset=SaveClip(run,Art+"/Shotgun_Run_Additive.anim");
  var close=UnityEngine.Object.Instantiate(Clip("A_FP_PCH_Shotgun_01_Reload_Close.fbx"));close.name="Shotgun_Reload_Close";AnimationUtility.SetAnimationEvents(close,AnimationUtility.GetAnimationEvents(close).Where(e=>e.functionName!="AnimationReloadFinished"&&e.functionName!="OnAnimationEndedReload").ToArray());
  var closeAsset=SaveClip(close,Art+"/Shotgun_Reload_Close.anim");
  var boltAssets=new Dictionary<string,AnimationClip>();
  foreach(string name in new[]{"A_FP_PCH_Shotgun_01_Reload_Bolt","A_FP_PCH_Shotgun_01_Reload_Bolt_Aiming"}) {
   var clip=UnityEngine.Object.Instantiate(Clip(name+".fbx"));clip.name=name+"_Complete";
   var events=AnimationUtility.GetAnimationEvents(clip).Where(e=>e.functionName!="OnAnimationEndedReload"&&e.functionName!="AnimationReloadFinished").ToList();events.Add(new AnimationEvent{time=clip.length*.98f,functionName="OnAnimationEndedReload"});AnimationUtility.SetAnimationEvents(clip,events.ToArray());boltAssets[name]=SaveClip(clip,Art+"/"+clip.name+".anim");
  }
  var partFire=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Rebound/Shotgun_Fire_WEP.anim"));partFire.name="Shotgun_Fire_WEP_Synchronized";
  float scale=Clip("A_FP_PCH_Shotgun_01_Fire.fbx").length/partFire.length;
  foreach(var binding in AnimationUtility.GetCurveBindings(partFire)) {var curve=AnimationUtility.GetEditorCurve(partFire,binding);var keys=curve.keys;for(int i=0;i<keys.Length;i++){keys[i].time*=scale;keys[i].inTangent/=scale;keys[i].outTangent/=scale;}curve.keys=keys;AnimationUtility.SetEditorCurve(partFire,binding,curve);}
  var fireSettings=AnimationUtility.GetAnimationClipSettings(partFire);fireSettings.stopTime=Clip("A_FP_PCH_Shotgun_01_Fire.fbx").length;AnimationUtility.SetAnimationClipSettings(partFire,fireSettings);
  var partFireAsset=SaveClip(partFire,Art+"/Shotgun_Fire_WEP_Synchronized.anim");
  var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();shotgun.GetOverrides(pairs);
  for(int i=0;i<pairs.Count;i++) {var key=pairs[i].Key;AnimationClip value=pairs[i].Value;string name=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(key));
   if(key.name.Contains("Handgun_Run"))value=runAsset;
   if(key.name=="Fire_Handgun_03")value=partFireAsset;
   if(name=="A_FP_PCH_Shotgun_01_Reload_Close")value=closeAsset;
   if(boltAssets.TryGetValue(name,out var replacement))value=replacement;
   pairs[i]=new(key,value);
  }shotgun.ApplyOverrides(pairs);EditorUtility.SetDirty(shotgun);EditorUtility.SetDirty(fp);
  var tp=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/ThirdPersonAnimator.controller");
  Param(tp,"TP_ShellLoading",AnimatorControllerParameterType.Bool);Param(tp,"TP_IsChambering",AnimatorControllerParameterType.Bool);Param(tp,"TP_ReloadSpeed",AnimatorControllerParameterType.Float);
  var parameters=tp.parameters;foreach(var p in parameters)if(p.name=="TP_ReloadSpeed")p.defaultFloat=1;tp.parameters=parameters;
  foreach(var layer in tp.layers)foreach(var state in AllStates(layer.stateMachine)) {
   if(state.timeParameter=="LocomotionTime"){state.timeParameter="TP_LocomotionTime";state.timeParameterActive=false;}
   if(state.name.StartsWith("Reload")){state.speedParameter="TP_ReloadSpeed";state.speedParameterActive=true;}
   if(state.motion is BlendTree tree)RepairBlend(tree);
  }
  var actions=tp.layers.First(l=>l.name=="Actions").stateMachine;var states=AllStates(actions).ToArray();
  var tpFire=states.First(s=>s.name=="Fire");tpFire.speed=1;
  if(!actions.anyStateTransitions.Any(t=>t.destinationState==tpFire)){var tr=actions.AddAnyStateTransition(tpFire);tr.AddCondition(AnimatorConditionMode.If,0,"TP_Fire");tr.AddCondition(AnimatorConditionMode.IfNot,0,"TP_IsReloading");tr.canTransitionToSelf=true;tr.duration=.025f;}
  foreach(var tr in states.First(s=>s.name=="Default").transitions.Where(t=>t.destinationState==tpFire).ToArray())states.First(s=>s.name=="Default").RemoveTransition(tr);
  var insert=states.First(s=>s.name=="Reload_Insert");foreach(var tr in insert.transitions){Exit(tr,1);tr.AddCondition(AnimatorConditionMode.IfNot,0,"TP_ShellLoading");}
  if(!insert.transitions.Any(t=>t.destinationState==insert)){var tr=insert.AddTransition(insert);Exit(tr,1);tr.AddCondition(AnimatorConditionMode.If,0,"TP_ShellLoading");tr.canTransitionToSelf=true;}
  var tpBolt=states.First(s=>s.name=="Reload_Bolt");foreach(var tr in tpBolt.transitions){Exit(tr,.98f);tr.AddCondition(AnimatorConditionMode.IfNot,0,"TP_IsReloading");tr.AddCondition(AnimatorConditionMode.IfNot,0,"TP_IsChambering");}
  if(!tpFire.transitions.Any(t=>t.destinationState==tpBolt)){var tr=tpFire.AddTransition(tpBolt);Exit(tr,.98f);tr.AddCondition(AnimatorConditionMode.If,0,"TP_IsChambering");}
  foreach(var tr in tpFire.transitions.Where(t=>t.destinationState!=tpBolt)){if(!tr.conditions.Any(c=>c.parameter=="TP_IsChambering"))tr.AddCondition(AnimatorConditionMode.IfNot,0,"TP_IsChambering");}
  foreach(var tr in states.First(s=>s.name=="Reload_Close").transitions)Exit(tr,1);
  FixTPOverride("Assets/Animations/OverrideThirdPerson/OC_TP_Rifle.overrideController","AR_01");
  FixTPOverride("Assets/Animations/OverrideThirdPerson/OC_TP_Shotgun.overrideController","Shotgun_01");EditorUtility.SetDirty(tp);
 }
 static void RepairBlend(BlendTree tree) {
  string Fix(string n)=>n=="Look"?"TP_Look":n=="WeaponID"?"TP_WeaponID":n=="MoveX"?"TP_MoveX":n=="MoveZ"?"TP_MoveZ":n;
  tree.blendParameter=Fix(tree.blendParameter);tree.blendParameterY=Fix(tree.blendParameterY);foreach(var child in tree.children)if(child.motion is BlendTree nested)RepairBlend(nested);EditorUtility.SetDirty(tree);
 }
 static void FixTPOverride(string path,string weapon) {
  var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();controller.GetOverrides(pairs);
  for(int i=0;i<pairs.Count;i++){var key=pairs[i].Key;string name=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(key));var value=pairs[i].Value;
   if(name=="A_TP_CH_Idle_Relaxed")value=null;
   if(name.StartsWith("A_TP_CH_Handgun_")) {
    string suffix=name.Substring("A_TP_CH_Handgun_".Length);
    string target="A_TP_CH_"+weapon+"_"+suffix+".fbx";
    if(suffix=="Fire" && weapon=="AR_01")target="A_TP_CH_AR_Fire.fbx";
    if(!(weapon=="Shotgun_01" && (suffix=="Reload"||suffix=="Reload_Empty")))value=Clip(target);
   }pairs[i]=new(key,value);
  }controller.ApplyOverrides(pairs);EditorUtility.SetDirty(controller);
 }
 static GameObject MakeModel(GameObject player,Animator fp,Animator body,SerializedProperty slot,int index,out Vector3 position,out Vector3 rotation) {
  string id=slot.FindPropertyRelative("weaponId").stringValue;var source=(GameObject)slot.FindPropertyRelative("weaponRoot").objectReferenceValue;
  var sample=UnityEngine.Object.Instantiate(player);sample.SetActive(true);
  GameObject model;
  try {
   var transforms=sample.GetComponentsInChildren<Transform>(true);
   var sampleFP=transforms.First(t=>t.name==fp.name).GetComponent<Animator>();
   var sampleBody=transforms.First(t=>t.name==body.name).GetComponent<Animator>();
   var sampleSource=transforms.First(t=>t.name==source.name);
   var fpController=slot.FindPropertyRelative("overrideController").objectReferenceValue as RuntimeAnimatorController;
   if(!fpController)fpController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/FirstPersonAnimatorHandgun.controller");
   foreach(var t in transforms.Where(t=>t.name.StartsWith("SK_Handgun_")||t.name=="SK_AR_01"||t.name=="SK_Shotgun_01"))t.gameObject.SetActive(t==sampleSource);
   sampleFP.gameObject.SetActive(true);sampleFP.enabled=true;sampleFP.runtimeAnimatorController=fpController;sampleFP.Rebind();
   sampleFP.SetBool("Holstered",false);sampleFP.Update(0);
   // Sample the authored neutral grip on the active generic FP rig.
   string token=index==0?"Handgun":index==1?"AR_01":"Shotgun_01";
   Clip("A_FP_PCH_"+token+"_Idle_Pose.fbx").SampleAnimation(sampleFP.gameObject,0);
   var tpController=slot.FindPropertyRelative("tpOverrideController").objectReferenceValue as RuntimeAnimatorController;
   if(!tpController)tpController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/ThirdPersonAnimator.controller");
   SampleAim(sampleBody,tpController,index);
   var right=sampleBody.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Hand_R");
   var left=sampleBody.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Hand_L");
   var fpRight=sampleFP.GetComponentsInChildren<Transform>(true).First(t=>t.name=="hand_r");
   var fpLeft=sampleFP.GetComponentsInChildren<Transform>(true).First(t=>t.name=="hand_l");
   var barrel=sampleSource.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SOCKET_Muzzle").parent;
   var sourceForward=sampleSource.InverseTransformDirection(-barrel.up);
   var sourceUp=sampleSource.InverseTransformDirection(barrel.forward);
   var rotationWorld=Quaternion.LookRotation(sampleBody.transform.forward,sampleBody.transform.up)*Quaternion.Inverse(Quaternion.LookRotation(sourceForward,sourceUp));
   var gripLocal=sampleSource.InverseTransformPoint(Palm(fpRight).GetColumn(3));
   var positionWorld=(Vector3)Palm(right).GetColumn(3)-rotationWorld*gripLocal;
   var world=Matrix4x4.TRS(positionWorld,rotationWorld,Vector3.one);
   var mapping=world*sampleSource.worldToLocalMatrix;
   var attachment=right.worldToLocalMatrix*world;
   position=attachment.GetColumn(3);rotation=attachment.rotation.eulerAngles;
   model=UnityEngine.Object.Instantiate(sampleSource.gameObject);
   model.name="SM_Wep_FP_"+id;model.SetActive(false);model.transform.SetParent(null);model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
   // Left-hand IK uses the FP foregrip and the TP hand's anatomical frame.
   var leftPalm=Palm(left);var mappedPalm=mapping*Palm(fpLeft);
   var leftRotation=mappedPalm.rotation*Quaternion.Inverse(Quaternion.Inverse(left.rotation)*leftPalm.rotation);
   var leftPosition=(Vector3)mappedPalm.GetColumn(3)-leftRotation*left.InverseTransformPoint(leftPalm.GetColumn(3));
   var targetMatrix=world.inverse*Matrix4x4.TRS(leftPosition,leftRotation,Vector3.one);
   var gripParent=model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="SOCKET_Grip")??model.transform;
   var grip=new GameObject("TP_LeftHandGrip").transform;grip.SetParent(model.transform,false);
   grip.localPosition=targetMatrix.GetColumn(3);grip.localRotation=targetMatrix.rotation;grip.SetParent(gripParent,true);
  }finally {UnityEngine.Object.DestroyImmediate(sample);}
  try {
   foreach(var component in model.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(component);
   foreach(var particles in model.GetComponentsInChildren<ParticleSystem>(true).ToArray())if(particles)UnityEngine.Object.DestroyImmediate(particles.gameObject);
   foreach(var light in model.GetComponentsInChildren<Light>(true).ToArray())if(light)UnityEngine.Object.DestroyImmediate(light.gameObject);
   foreach(var c in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
   foreach(var c in model.GetComponentsInChildren<AudioSource>(true))UnityEngine.Object.DestroyImmediate(c);
   foreach(var a in model.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(a);
   var muzzle=model.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SOCKET_Muzzle");var muzzleSource=source.GetComponentsInChildren<Muzzle>(true).First();muzzle.gameObject.AddComponent<Muzzle>().CopySettingsFrom(muzzleSource);
   var controller=MakePartsController(source,model,id,slot);
   var animator=model.AddComponent<Animator>();animator.runtimeAnimatorController=controller;
   model.SetActive(true);return PrefabUtility.SaveAsPrefabAsset(model,Models+"/"+id+".prefab");
  }finally {UnityEngine.Object.DestroyImmediate(model);}
 }
 static void SampleAim(Animator animator,RuntimeAnimatorController controller,int index) {
  animator.gameObject.SetActive(true);animator.enabled=true;animator.runtimeAnimatorController=controller;animator.Rebind();
  int switchLayer=animator.GetLayerIndex("WeaponSwitch");if(switchLayer>=0)animator.SetLayerWeight(switchLayer,0);
  animator.SetFloat("TP_AimWeight",1);animator.SetBool("TP_IsAiming",true);animator.SetFloat("TP_CrouchWeight",0);animator.SetFloat("TP_WeaponID",index+1);
  animator.SetBool("TP_IsGrounded",true);animator.SetBool("TP_IsAlive",true);animator.SetFloat("TP_Look",0);animator.Update(.1f);animator.enabled=false;
 }
 static Vector3 PalmPosition(Transform hand) => Palm(hand).GetColumn(3);
 static Matrix4x4 Palm(Transform hand) {
  var bones=hand.GetComponentsInChildren<Transform>(true);
  Transform Finger(string token)=>bones.FirstOrDefault(t=>t.name.IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0 && (t.name.Contains("01")||t.name.Contains("1")));
  var middle=Finger("middle");var index=Finger("index");var pinky=Finger("pinky")??Finger("little")??bones.FirstOrDefault(t=>t.name.StartsWith("Finger_01"));
  if(!middle)middle=pinky;
  if(!middle || !index || !pinky)return Matrix4x4.TRS(hand.position,hand.rotation,Vector3.one);
  Vector3 forward=(middle.position-hand.position).normalized,right=(index.position-pinky.position).normalized;
  return Matrix4x4.TRS(hand.position+forward*Vector3.Distance(hand.position,middle.position)*.25f,Quaternion.LookRotation(forward,Vector3.Cross(forward,right)),Vector3.one);
 }
 static RuntimeAnimatorController MakePartsController(GameObject source,GameObject model,string id,SerializedProperty slot) {
  string path=Art+"/"+id+"_Parts.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
  if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
  var sm=controller.layers[0].stateMachine;foreach(var state in sm.states)sm.RemoveState(state.state);sm.AddState("Idle");
  var fp=slot.FindPropertyRelative("overrideController").objectReferenceValue as RuntimeAnimatorController;if(!fp)fp=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/FirstPersonAnimatorHandgun.controller");
  foreach(var clip in fp.animationClips.Distinct()) {
   string state=clip.name.Contains("Reload_Open_WEP")?"Reload Open":clip.name.Contains("Reload_Insert_WEP")?"Reload Insert":clip.name.Contains("Reload_Close_WEP")?"Reload Close":clip.name.Contains("Reload_Bolt_WEP")?"Bolt Action":clip.name.StartsWith("Fire_")||clip.name.Contains("_Fire_WEP")?"Fire":clip.name.StartsWith("Reload_Empty_")?"Reload Empty":clip.name.StartsWith("Reload_")?"Reload":null;
   if(state==null || sm.states.Any(s=>s.state.name==state))continue;
   var copy=new AnimationClip{name=id+"_"+state.Replace(" ","_")};
   foreach(var binding in AnimationUtility.GetCurveBindings(clip)) {
    string prefix=source.name+"/";int start=binding.path.IndexOf(prefix,StringComparison.Ordinal);string relative=start>=0?binding.path.Substring(start+prefix.Length):binding.path;
    if(relative!="" && !model.transform.Find(relative))continue;
    // FP placement curves must never animate the world-space gun attachment.
    if(relative.Split('/').Length < 3)continue;
    var changed=binding;changed.path=relative;AnimationUtility.SetEditorCurve(copy,changed,AnimationUtility.GetEditorCurve(clip,binding));
   }
   if(AnimationUtility.GetCurveBindings(copy).Length==0){UnityEngine.Object.DestroyImmediate(copy);continue;}
   var saved=SaveClip(copy,Art+"/"+copy.name+".anim");sm.AddState(state).motion=saved;
  }
  var idle=new AnimationClip{name=id+"_Parts_Idle"};
  foreach(var bone in model.GetComponentsInChildren<Transform>(true)) {
   string relative=AnimationUtility.CalculateTransformPath(bone,model.transform);if(relative.Split('/').Length<3)continue;
   for(int axis=0;axis<3;axis++)AnimationUtility.SetEditorCurve(idle,EditorCurveBinding.FloatCurve(relative,typeof(Transform),"m_LocalPosition."+"xyz"[axis]),AnimationCurve.Constant(0,1,bone.localPosition[axis]));
   for(int axis=0;axis<4;axis++)AnimationUtility.SetEditorCurve(idle,EditorCurveBinding.FloatCurve(relative,typeof(Transform),"m_LocalRotation."+"xyzw"[axis]),AnimationCurve.Constant(0,1,bone.localRotation[axis]));
  }
  var idleState=sm.states.First(s=>s.state.name=="Idle").state;idleState.motion=SaveClip(idle,Art+"/"+id+"_Parts_Idle.anim");sm.defaultState=idleState;
  foreach(var entry in sm.states.Where(e=>e.state!=idleState)) {
   var state=entry.state;AnimatorState next=idleState;float end=.98f;
   if(id=="Escopeta" && state.name=="Fire"){next=sm.states.First(e=>e.state.name=="Bolt Action").state;end=.12f/((AnimationClip)state.motion).length;}
   if(state.name=="Reload Open"){next=sm.states.First(e=>e.state.name=="Reload Insert").state;end=1;}
   if(state.name=="Reload Insert") {
    var clip=(AnimationClip)state.motion;var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);continue;
   }
   if(state.name=="Reload Close")end=1;
   var tr=state.AddTransition(next);Exit(tr,end);
  }
  EditorUtility.SetDirty(controller);return controller;
 }
}
#endif
