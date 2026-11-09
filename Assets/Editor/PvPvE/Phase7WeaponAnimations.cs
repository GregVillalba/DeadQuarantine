#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static partial class CompleteWeaponsIntegrationBuilder
{
 const string Phase7Dir="Assets/Generated/NewWeapons/Phase7";
 static string Output7=>Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE7_OUTPUT")??"Temp/Phase7";
 static Spec[] Specs7()=>CreateSpecs().Concat(Specs6()).ToArray();
 [MenuItem("Game/Dead Quarantine/PvPvE/Fase 7 - Corregir animaciones")]
 public static void RepairAnimationsPhase7(){
  Directory.CreateDirectory(Phase7Dir);Directory.CreateDirectory(Output7);AssetDatabase.Refresh();
  NormalizeRifleImports7();
  const string path="Assets/Prefabs/Player.prefab";string before=File.ReadAllText(path);
  var root=PrefabUtility.LoadPrefabContents(path);var text=new StringBuilder();
  try{
   var sw=root.GetComponentInChildren<WeaponSwitcher>(true);var so=new SerializedObject(sw);var rig=sw.GetComponent<Animator>();var presentation=root.GetComponent<NetworkWeaponPresentation>();
   var specs=Specs7();var modified=new HashSet<string>{"967506594462204975","7122332671699174642"};
   // Preserve every previous prefab record except these explicit non-protected weapon subtrees.
   var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
   var assetSw=new SerializedObject(asset.GetComponentInChildren<WeaponSwitcher>(true));
   foreach(var spec in specs){var original=ReadSlotTemplate(assetSw,spec.Id);foreach(var obj in original.Root.GetComponentsInChildren<Component>(true).Cast<UnityEngine.Object>().Concat(original.Root.GetComponentsInChildren<Transform>(true).Select(t=>(UnityEngine.Object)t.gameObject))){
    if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string _,out long id))modified.Add(id.ToString());
    var gameObject=obj is Component component?component.gameObject:obj as GameObject;
    var handle=gameObject?PrefabUtility.GetPrefabInstanceHandle(gameObject):null;
    if(handle&&AssetDatabase.TryGetGUIDAndLocalFileIdentifier(handle,out string _,out long instanceId))modified.Add(instanceId.ToString());
   }}
   foreach(var spec in specs){
    ResolveAnimation7(spec);var slot=ReadSlotTemplate(so,spec.Id);var settings=new SerializedObject(slot.Weapon);
    float reloadTime=settings.FindProperty("reloadTime").floatValue;
    var fp=Controller7(spec,(AnimatorController)(spec.Id=="Sniper01"?slot.FPOverride.runtimeAnimatorController:so.FindProperty("baseController").objectReferenceValue),false);
    var tp=Controller7(spec,(AnimatorController)(spec.Id=="Sniper01"?slot.TPOverride.runtimeAnimatorController:so.FindProperty("tpBaseController").objectReferenceValue),true);
    var slots=so.FindProperty("slots");for(int i=0;i<slots.arraySize;i++){var row=slots.GetArrayElementAtIndex(i);if(row.FindPropertyRelative("weaponId").stringValue!=spec.Id)continue;row.FindPropertyRelative("overrideController").objectReferenceValue=fp;row.FindPropertyRelative("tpOverrideController").objectReferenceValue=tp;}
    so.ApplyModifiedPropertiesWithoutUndo();
    var definition=FindDefinition(presentation.Weapons,spec.Id);definition.Controller=tp;
    var reference=ReadSlotTemplate(so,spec.Family==Family.Handgun?"Handgun":"Rifle");
    RestoreMount7(slot.Root,spec.ModelAsset);
    FitMount7(rig,slot,reference,spec.FP.Idle,spec.FP.AimPose,text);

    if(spec.Id!="Sniper01"){
     SetBool(settings,"enforceReloadDuration",false);
     SetFloat(settings,"reloadClipSpeedMultiplier",spec.FP.Reload.length/reloadTime);SetFloat(settings,"reloadEmptyClipSpeedMultiplier",spec.FP.ReloadEmpty.length/reloadTime);
     SetFloat(settings,"tpReloadClipSpeedMultiplier",spec.TP.Reload.length/reloadTime);SetFloat(settings,"tpReloadEmptyClipSpeedMultiplier",spec.TP.ReloadEmpty.length/reloadTime);
    }
    var marker=OpticalSight7(slot.Root);
    SetBool(settings,"adsCenterVisualSight",true);SetBool(settings,"adsFireAlongVisualSight",true);SetFloat(settings,"adsMaxViewportCorrection",.4f);SetObject(settings,"adsSightReference",marker);
    settings.ApplyModifiedPropertiesWithoutUndo();
    var parts=slot.Root.GetComponent<Animator>();
    parts.runtimeAnimatorController=Parts7(spec,parts.runtimeAnimatorController,false);
    if(spec.Family==Family.Handgun && !slot.Root.GetComponent<WeaponEmptySlideSync>())slot.Root.AddComponent<WeaponEmptySlideSync>();
    string modelPath=AssetDatabase.GetAssetPath(definition.Model);var model=PrefabUtility.LoadPrefabContents(modelPath);
    try{
     RestoreMount7(model,spec.ModelAsset);
     var animator=model.GetComponent<Animator>();animator.runtimeAnimatorController=Parts7(spec,animator.runtimeAnimatorController,true);
     if(spec.Family==Family.Handgun&&!model.GetComponent<WeaponEmptySlideSync>())model.AddComponent<WeaponEmptySlideSync>();
     PrefabUtility.SaveAsPrefabAsset(model,modelPath);
    }finally{PrefabUtility.UnloadPrefabContents(model);}
    RootGrip6(FindDefinition(presentation.Weapons,reference.Id),definition.Model,out definition.GripPosition,out definition.GripRotation);
    SwitchPoses7(root,spec,(AnimatorController)tp.runtimeAnimatorController);
    text.AppendLine(spec.Id+" FP="+fp.name+" TP="+tp.name+" reloadSeconds="+reloadTime);
   }
   File.WriteAllLines(Path.Combine(Output7,"AllowedPrefabRecords.txt"),modified);
   EditorUtility.SetDirty(presentation);PrefabUtility.SaveAsPrefabAsset(root,path);
   // Unity's serializer adds unrelated defaults and reformats old records. Keep them byte-for-byte.
   var old=Regex.Matches(before,@"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)",RegexOptions.Multiline|RegexOptions.Singleline).Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
   string saved=File.ReadAllText(path);File.WriteAllText(Path.Combine(Output7,"PlayerSerializedRaw.prefab"),saved);saved=Regex.Replace(saved,@"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)",m=>old.TryGetValue(m.Groups[1].Value,out string record)&&!modified.Contains(m.Groups[1].Value)?record:m.Value,RegexOptions.Multiline|RegexOptions.Singleline);
   File.WriteAllText(path,saved);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);AssetDatabase.SaveAssets();
  }finally{if(AnimationMode.InAnimationMode())AnimationMode.StopAnimationMode();PrefabUtility.UnloadPrefabContents(root);}
  var verify=PrefabUtility.LoadPrefabContents(path);
  try{
   var check=verify.GetComponentInChildren<WeaponSwitcher>(true);if(!check)throw new InvalidOperationException("Saved prefab lost its weapon rig");
   var serialized=new SerializedObject(check);
   foreach(var spec in Specs7()){var slot=ReadSlotTemplate(serialized,spec.Id);if(!slot.Weapon||!slot.FPOverride||!FindDeep(slot.Root.transform,"DQ_OpticalSight"))throw new InvalidOperationException(spec.Id+" lost a saved reference");}
  }finally{PrefabUtility.UnloadPrefabContents(verify);}
  File.WriteAllText(Path.Combine(Output7,"AnimationIntegration.txt"),text.ToString());
 }
 public static void InspectBindingsPhase7(){
  Directory.CreateDirectory(Output7);var text=new StringBuilder();
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");var so=new SerializedObject(prefab.GetComponentInChildren<WeaponSwitcher>(true));
  foreach(var spec in Specs7()){
   var slot=ReadSlotTemplate(so,spec.Id);var animator=slot.Root.GetComponent<Animator>();
   text.AppendLine(spec.Id+" mount="+slot.Root.transform.localPosition+" rotation="+slot.Root.transform.localEulerAngles);
   foreach(var clip in animator.runtimeAnimatorController.animationClips.Distinct()){
    text.AppendLine("PART "+clip.name);
    foreach(var binding in AnimationUtility.GetCurveBindings(clip).Where(b=>b.path==""||b.path.EndsWith("/root"))){var curve=AnimationUtility.GetEditorCurve(clip,binding);text.AppendLine("  "+binding.path+" "+binding.type.Name+" "+binding.propertyName+" "+curve.keys.First().value+".."+curve.keys.Last().value);}
   }
   text.AppendLine("FP poses "+string.Join(",",slot.FPOverride.animationClips.Where(c=>c.name.Contains("Pose")||c.name.Contains("Idle")).Select(c=>c.name)));
  }
  File.WriteAllText(Path.Combine(Output7,"Bindings.txt"),text.ToString());
 }
 static void NormalizeRifleImports7(){
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");var settings=new SerializedObject(prefab.GetComponentInChildren<WeaponSwitcher>(true));
  var protectedControllers=new List<string>{AssetDatabase.GetAssetPath(settings.FindProperty("baseController").objectReferenceValue),AssetDatabase.GetAssetPath(settings.FindProperty("tpBaseController").objectReferenceValue)};
  foreach(string id in new[]{"Handgun","Rifle","Escopeta"}){var slot=ReadSlotTemplate(settings,id);if(slot.FPOverride)protectedControllers.Add(AssetDatabase.GetAssetPath(slot.FPOverride));if(slot.TPOverride)protectedControllers.Add(AssetDatabase.GetAssetPath(slot.TPOverride));}
  var protectedDependencies=new HashSet<string>(AssetDatabase.GetDependencies(protectedControllers.ToArray(),true));
  foreach(string path in Directory.EnumerateFiles("Assets/Art/Animations/Character/ARs","A_FP_PCH_AR_0*.fbx")){
   string file=Path.GetFileName(path);if(!file.StartsWith("A_FP_PCH_AR_02_")&&!file.StartsWith("A_FP_PCH_AR_03_"))continue;
   if(protectedDependencies.Contains(path.Replace('\\','/')))throw new InvalidOperationException("Protected controller depends on "+path);
   var importer=(ModelImporter)AssetImporter.GetAtPath(path);if(Mathf.Abs(importer.globalScale-.01f)<.000001f)continue;
   importer.globalScale=.01f;importer.SaveAndReimport();
  }
 }
 static void ResolveAnimation7(Spec s){
  s.ModelAsset=RequireAsset<GameObject>(s.Model);
  if(!s.Id.StartsWith("Sniper")){s.FP=ResolveFPClips(s);s.TP=ResolveTPClips(s);}
  else{
   string prefix="A_FP_PCH_"+s.Model.Substring(3)+"_";bool bolt=s.Id=="Sniper01";
   s.FP=new ClipSet{Idle=RequireClip(prefix+"Idle_Pose"),AimPose=RequireClip(prefix+"Aim_Pose"),Fire=RequireClip(bolt?prefix+"Fire":"A_FP_PCH_Sniper_Fire"),AimFire=RequireClip(bolt?prefix+"Aim_Fire":"A_FP_PCH_Sniper_Aim_Fire"),Reload=RequireClip(prefix+(bolt?"Reload_Open":"Reload")),ReloadEmpty=RequireClip(prefix+(bolt?"Reload_Open":"Reload_Empty")),Holster=RequireClip(prefix+"Holster"),Unholster=RequireClip(prefix+"Unholster"),Run=RequireClip("A_FP_PCH_Run_02_Additive"),Inspect=RequireClip(prefix+"Inspect")};s.TP=TPClips6(s);
  }
  s.FP.Fire=EventClip7(s,s.FP.Fire,"Fire",s.Id=="Sniper01"?Array.Empty<(string,float)>():new[]{("OnEjectCasing",.18f)});
  s.FP.AimFire=EventClip7(s,s.FP.AimFire,"AimFire",s.Id=="Sniper01"?Array.Empty<(string,float)>():new[]{("OnEjectCasing",.18f)});
  if(s.Id!="Sniper01"){
   s.FP.Reload=ReloadClip7(s,s.FP.Reload,"Reload");s.FP.ReloadEmpty=ReloadClip7(s,s.FP.ReloadEmpty,"ReloadEmpty");
   s.FP.AimReload=s.FP.AimReload?ReloadClip7(s,s.FP.AimReload,"AimReload"):s.FP.Reload;
   s.FP.AimReloadEmpty=s.FP.AimReloadEmpty?ReloadClip7(s,s.FP.AimReloadEmpty,"AimReloadEmpty"):s.FP.ReloadEmpty;
  }
  s.FP.Holster=EventClip7(s,s.FP.Holster,"Holster",Array.Empty<(string,float)>());s.FP.Unholster=EventClip7(s,s.FP.Unholster,"Unholster",new[]{("OnAnimationEndedHolster",.98f)});
 }
 static AnimationClip ReloadClip7(Spec s,AnimationClip source,string role){
  var fill=AnimationUtility.GetAnimationEvents(source).FirstOrDefault(e=>e.functionName=="OnAmmunitionFill");
  return EventClip7(s,source,role,new[]{("OnAmmunitionFill",fill!=null?Mathf.Clamp(fill.time/source.length,.2f,.9f):.7f),("OnAnimationEndedReload",.99f)});
 }
 static AnimationClip EventClip7(Spec s,AnimationClip original,string role,(string,float)[] events){
  var clip=UnityEngine.Object.Instantiate(original);clip.name=s.Id+"_"+role;
  AnimationUtility.SetAnimationEvents(clip,events.Select(e=>new AnimationEvent{functionName=e.Item1=="OnEjectCasing"?"OnIntegratedEjectCasing":e.Item1,intParameter=Array.FindIndex(Specs7(),item=>item.Id==s.Id)+3,time=clip.length*e.Item2,messageOptions=SendMessageOptions.DontRequireReceiver}).ToArray());return Save7(clip,Phase7Dir+"/"+clip.name+".anim");
 }
 static T Save7<T>(T value,string path) where T:UnityEngine.Object{var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(value,path);return value;}
 static AnimatorOverrideController Controller7(Spec s,AnimatorController source,bool tp){
  string path=Phase7Dir+"/"+s.Id+(tp?"_TP":"_FP")+".controller";
  if(!AssetDatabase.LoadAssetAtPath<AnimatorController>(path)&&!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source),path))throw new InvalidOperationException(path);
  var ctl=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
  if(!tp)for(int i=ctl.layers.Length-1;i>=0;i--)if(new[]{"HandGunADD","NoAmmoADD","WigglesRunning","WigglesCrouching","Transition"}.Contains(ctl.layers[i].name))ctl.RemoveLayer(i);
  float interval=s.Burst?s.BurstShotInterval:s.FireInterval;var c=tp?s.TP:s.FP;
  foreach(var layer in ctl.layers){
   foreach(var state in States6(layer.stateMachine)){
    if(tp&&layer.name=="WeaponSwitch")continue;
    // Base controllers can contain mesh-embedded clips named Scene, which have no character-pose role in their filename.
    if(!tp&&layer.name=="Poses"&&state.motion is BlendTree poses){
     var children=poses.children;
     for(int i=0;i<children.Length;i++)children[i].motion=children[i].threshold<.5f?c.Idle:c.AimPose;
     poses.children=children;EditorUtility.SetDirty(poses);
    }
    ReplaceMotion6(state.motion,clip=>{
     string file=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(clip));
     if(!tp&&file==s.Id+"_AimFire")return c.AimFire;
     if(!tp&&file==s.Id+"_Fire")return c.Fire;
     return Map6(clip,c,tp);
    },m=>state.motion=m);
    if(!tp&&s.Id=="Sniper01"&&layer.name=="Fire"&&state.name=="Bolt")
     state.motion=EventClip7(s,RequireClip("A_FP_PCH_Sniper_01_Reload_Bolt"),"BoltCycle",new[]{("AnimationBoltOpen",.1f),("OnEjectCasing",.3f),("AnimationBoltClose",.85f)});
    if(!tp && s.Id=="Sniper01")ReplaceMotion6(state.motion,clip=>{
     var events=AnimationUtility.GetAnimationEvents(clip);if(!events.Any(e=>e.functionName=="OnEjectCasing"))return null;
     var copy=UnityEngine.Object.Instantiate(clip);copy.name=s.Id+"_Chamber_"+clip.name;
     foreach(var e in events)if(e.functionName=="OnEjectCasing"){e.functionName="OnIntegratedEjectCasing";e.intParameter=Array.FindIndex(Specs7(),item=>item.Id==s.Id)+3;}
     AnimationUtility.SetAnimationEvents(copy,events);return Save7(copy,Phase7Dir+"/"+copy.name+".anim");
    },motion=>state.motion=motion);
    if(state.name=="Fire" && layer.name==(tp?"Actions":"Fire") && s.Id!="Sniper01"){
     state.speed=Mathf.Max(1,c.Fire.length/interval);state.speedParameterActive=false;
     foreach(var tr in state.transitions)if(tr.destinationState&&tr.destinationState.name=="Default"){tr.exitTime=.90f;tr.hasFixedDuration=true;tr.duration=Mathf.Min(.005f,interval*.05f);}
    }
    if(!tp&&layer.name=="Reload"&&state.name is "Reload" or "Reload Empty")foreach(var tr in state.transitions){tr.exitTime=1;tr.duration=.02f;}
   }
   foreach(var tr in layer.stateMachine.anyStateTransitions)if(tr.destinationState&&tr.destinationState.name=="Fire"){tr.duration=0;tr.canTransitionToSelf=true;}
  }
  if(!tp){
   var layers=ctl.layers;var reload=layers.First(l=>l.name=="Reload");reload.blendingMode=AnimatorLayerBlendingMode.Override;
   foreach(var state in States6(reload.stateMachine).Where(st=>!st.motion))state.writeDefaultValues=false;
   var holster=layers.First(l=>l.name=="Holster");holster.blendingMode=AnimatorLayerBlendingMode.Override;
   var sm=holster.stateMachine;var draw=States6(sm).First(st=>st.name=="Unholster");var hide=States6(sm).First(st=>st.name=="Holster");
   var ready=States6(sm).FirstOrDefault(st=>st.name=="Ready");
   if(!ready){ready=sm.AddState("Ready");ready.writeDefaultValues=false;var done=draw.AddTransition(ready);done.hasExitTime=true;done.exitTime=1;done.duration=.06f;done.hasFixedDuration=true;var next=ready.AddTransition(hide);next.duration=.04f;next.hasExitTime=false;next.AddCondition(AnimatorConditionMode.If,0,"Holstered");}
   ctl.layers=layers;
  }
  EditorUtility.SetDirty(ctl);
  var aoc=Save7(new AnimatorOverrideController(ctl){name="Phase7_"+s.Id+(tp?"_TP":"_FP")},Phase7Dir+"/"+s.Id+(tp?"_TP":"_FP")+".overrideController");return aoc;
 }
 static void FitMount7(Animator rig,SlotTemplate slot,SlotTemplate reference,AnimationClip idle,AnimationClip aim,StringBuilder text){
  // All pack models share the authored ik_hand_gun origin. Do not inherit manual offsets from protected meshes.
  slot.Root.transform.localPosition=Vector3.zero;slot.Root.transform.localRotation=Quaternion.Euler(90,0,0);slot.Root.transform.localScale=Vector3.one;
  AnimationMode.StartAnimationMode();Sample6(rig.gameObject,aim);
  var physical=FindDeep(slot.Root.transform,"root");var camera=(Camera)new SerializedObject(slot.Weapon).FindProperty("weaponCamera").objectReferenceValue;
  var pivot=physical.position;var delta=Quaternion.LookRotation(camera.transform.up,-camera.transform.forward)*Quaternion.Inverse(physical.rotation);
  var desiredRotation=delta*slot.Root.transform.rotation;var desiredPosition=pivot+delta*(slot.Root.transform.position-pivot);
  var localPosition=slot.Root.transform.parent.InverseTransformPoint(desiredPosition);var localRotation=Quaternion.Inverse(slot.Root.transform.parent.rotation)*desiredRotation;
  AnimationMode.StopAnimationMode();slot.Root.transform.localPosition=localPosition;slot.Root.transform.localRotation=localRotation;
  text.AppendLine(slot.Id+" authoredOrigin="+localPosition+" alignedRotation="+localRotation.eulerAngles);
 }
 static void RestoreMount7(GameObject target,GameObject source){
  for(var bone=FindDeep(source.transform,"root");bone&&bone!=source.transform;bone=bone.parent){
   var destination=target.transform.Find(AnimationUtility.CalculateTransformPath(bone,source.transform));
   destination.localPosition=bone.localPosition;destination.localRotation=bone.localRotation;destination.localScale=bone.localScale;
  }
 }
 static Transform OpticalSight7(GameObject weapon){
  var physical=FindDeep(weapon.transform,"root");var scope=FindDeep(weapon.transform,"DQ_DefaultScope");if(!scope)throw new InvalidOperationException(weapon.name+" missing default sights");
  Bounds bounds=default;bool first=true;
  foreach(var renderer in scope.GetComponentsInChildren<Renderer>(true)){
   var mf=renderer.GetComponent<MeshFilter>();var sk=renderer as SkinnedMeshRenderer;var mesh=mf?mf.sharedMesh:sk?sk.sharedMesh:null;if(!mesh)continue;
   var b=mesh.bounds;for(int i=0;i<8;i++){var local=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var point=physical.InverseTransformPoint(renderer.transform.TransformPoint(local));if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}
  }
  if(first)throw new InvalidOperationException(weapon.name+" missing sight geometry");
  var marker=physical.Find("DQ_OpticalSight");if(!marker){marker=new GameObject("DQ_OpticalSight").transform;marker.SetParent(physical,false);}
  marker.localPosition=new Vector3(bounds.center.x,bounds.max.y,Mathf.Lerp(bounds.min.z,bounds.max.z,.80f));return marker;
 }
 static AnimatorController Parts7(Spec s,RuntimeAnimatorController original,bool tp){
  string path=Phase7Dir+"/"+s.Id+(tp?"_TPParts":"_FPParts")+".controller";
  if(!AssetDatabase.LoadAssetAtPath<AnimatorController>(path)&&!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original),path))throw new InvalidOperationException(path);
  var ctl=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);var sm=ctl.layers[0].stateMachine;
  var idle=sm.states.First(st=>st.state.name=="Idle").state;idle.motion=FindEmbeddedClip(AssetDatabase.GetAssetPath(s.ModelAsset),"_Ref_Pose");
  if(s.Id=="Sniper01"){StripMountCurves7(s,ctl,tp);EditorUtility.SetDirty(ctl);return ctl;}
  var slide=FindEmbeddedClip(AssetDatabase.GetAssetPath(s.ModelAsset),"A_WEP_"+s.Model.Substring(3)+"_Slide_Back_Pose");
  if(s.Family==Family.Handgun){
   if(!slide)throw new InvalidOperationException(s.Id+" missing slide-back clip");
   if(!ctl.parameters.Any(p=>p.name=="Empty"))ctl.AddParameter("Empty",AnimatorControllerParameterType.Bool);
   var empty=sm.states.FirstOrDefault(st=>st.state.name=="Empty").state;if(!empty){empty=sm.AddState("Empty");empty.motion=slide;var enter=idle.AddTransition(empty);enter.duration=0;enter.AddCondition(AnimatorConditionMode.If,0,"Empty");var exit=empty.AddTransition(idle);exit.duration=0;exit.AddCondition(AnimatorConditionMode.IfNot,0,"Empty");}
  }
  var fire=sm.states.First(st=>st.state.name=="Fire").state;
  if(s.Id=="BurstRifle"&&(!fire.motion||AnimationUtility.GetCurveBindings((AnimationClip)fire.motion).Length==0)){
   // AR_02 has no authored Fire take. Animate its actual charging bolt, as intended by the old integrator.
   var bolt=s.ModelAsset.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name.IndexOf("bolt",StringComparison.OrdinalIgnoreCase)>=0);
   if(!bolt)throw new InvalidOperationException("AR_02 bolt not found: "+string.Join(",",s.ModelAsset.GetComponentsInChildren<Transform>(true).Select(t=>t.name)));
   var clip=new AnimationClip{name=s.Id+(tp?"_TP":"_FP")+"_MechanicalFire",frameRate=60};
   var root=FindDeep(s.ModelAsset.transform,"root");var offset=bolt.parent.InverseTransformDirection(root.TransformDirection(Vector3.up)).normalized*.025f;var point=bolt.localPosition;
   string bonePath=AnimationUtility.CalculateTransformPath(bolt,s.ModelAsset.transform);
   foreach(var axis in new[]{("x",point.x,offset.x),("y",point.y,offset.y),("z",point.z,offset.z)})AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(bonePath,typeof(Transform),"m_LocalPosition."+axis.Item1),new AnimationCurve(new Keyframe(0,axis.Item2),new Keyframe(.025f,axis.Item2+axis.Item3),new Keyframe(.065f,axis.Item2)));
   fire.motion=Save7(clip,Phase7Dir+"/"+clip.name+".anim");fire.speed=1;
  }
  if(!fire.motion || AnimationUtility.GetCurveBindings((AnimationClip)fire.motion).Length==0)throw new InvalidOperationException(s.Id+" missing mechanical fire motion");
  StripMountCurves7(s,ctl,tp);EditorUtility.SetDirty(ctl);return ctl;
 }
 static void StripMountCurves7(Spec s,AnimatorController controller,bool tp){
  var physical=FindDeep(s.ModelAsset.transform,"root");var anchors=new HashSet<string>{""};
  for(var t=physical;t&&t!=s.ModelAsset.transform;t=t.parent)anchors.Add(AnimationUtility.CalculateTransformPath(t,s.ModelAsset.transform));
  foreach(var state in States6(controller.layers[0].stateMachine)){
   if(!(state.motion is AnimationClip source))continue;var clip=UnityEngine.Object.Instantiate(source);clip.name=s.Id+(tp?"_TP":"_FP")+"_Parts_"+state.name.Replace(" ","_");
   // The rig owns the mounting pose. Mechanical clips may only move descendants of that mount.
   foreach(var binding in AnimationUtility.GetCurveBindings(clip).Where(b=>b.type==typeof(Transform)&&anchors.Contains(b.path)))AnimationUtility.SetEditorCurve(clip,binding,null);
   AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());state.motion=Save7(clip,Phase7Dir+"/"+clip.name+".anim");
  }
 }
 static void SwitchPoses7(GameObject root,Spec s,AnimatorController controller){
  var body=root.GetComponent<NetworkWeaponPresentation>().BodyAnimator;var saved=body.runtimeAnimatorController;
  var sampler=new AnimatorController();sampler.AddLayer("Sample");var sm=sampler.layers[0].stateMachine;var state=sm.AddState("Pose");sm.defaultState=state;
  try{
   body.runtimeAnimatorController=sampler;using var handler=new HumanPoseHandler(body.avatar,body.transform);var relaxed=new HumanPose();var held=new HumanPose();
   state.motion=RequireClip("A_TP_CH_Idle_Relaxed");body.Rebind();body.Update(.01f);handler.GetHumanPose(ref relaxed);
   state.motion=s.TP.Idle;body.Rebind();body.Update(.01f);handler.GetHumanPose(ref held);
   if(held.muscles.Zip(relaxed.muscles,(a,b)=>Mathf.Abs(a-b)).Sum()<.25f)throw new InvalidOperationException(s.Id+" TP switch pose sampling failed");
   foreach(bool draw in new[]{false,true}){
    var clip=new AnimationClip{name=s.Id+(draw?"_TP_Unholster":"_TP_Holster")};float duration=draw?s.FP.Unholster.length:s.FP.Holster.length;
    for(int m=0;m<HumanTrait.MuscleCount;m++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),HumanTrait.MuscleName[m]),AnimationCurve.EaseInOut(0,draw?relaxed.muscles[m]:held.muscles[m],duration,draw?held.muscles[m]:relaxed.muscles[m]));
    var savedClip=Save7(clip,Phase7Dir+"/"+clip.name+".anim");var layer=controller.layers.First(l=>l.name=="WeaponSwitch");States6(layer.stateMachine).First(st=>st.name==(draw?"Unholster":"Holster")).motion=savedClip;
   }EditorUtility.SetDirty(controller);
  }finally{body.runtimeAnimatorController=saved;body.Rebind();UnityEngine.Object.DestroyImmediate(sampler);}
 }
}
#endif
