#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
public static partial class CompleteWeaponsIntegrationBuilder {
 const string Folder25="weapon-assets:/NewWeapons/Phase25";
 const string Source25="C:/Users/andre/Documents/GitHub/Low Poly Shooter Pack v4.3.1 (1)/Assets/Infima Games/Low Poly Shooter Pack";
 public static void InstallPhase25(){
  Directory.CreateDirectory(WeaponAssetLocations.Resolve(Folder25));AssetDatabase.Refresh();var output=Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE25_OUTPUT");Directory.CreateDirectory(WeaponAssetLocations.Resolve(output));
  const string path="Assets/Prefabs/Player.prefab";string before=File.ReadAllText(WeaponAssetLocations.Resolve(path));var asset=AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetLocations.Resolve(path));var allowed=new HashSet<string>();
  void Allow(UnityEngine.Object obj){if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string _,out long id))allowed.Add(id.ToString());}
  var assetSw=new SerializedObject(asset.GetComponentInChildren<WeaponSwitcher>(true));Allow(asset.GetComponentInChildren<WeaponSwitcher>(true));Allow(asset.GetComponent<NetworkWeaponPresentation>());Allow(asset.GetComponent<NetworkWeaponPresentation>().BodyAnimator);
  foreach(var def in asset.GetComponent<NetworkWeaponPresentation>().Weapons){var slot=ReadSlotTemplate(assetSw,def.Id);Allow(slot.Root);Allow(slot.Root.GetComponent<PackWeaponPresentation>());Allow(slot.Root.GetComponent<WeaponAttachments>());if(slot.Root.name.StartsWith("DQ_Weapon_",StringComparison.Ordinal)){Allow(slot.Weapon);if(def.Id is "AutoPistol" or "BurstRifle" or "SMG05" or "Sniper02")Allow(WeaponAttachments.Find(slot.Root.transform,"DQ_OpticalSight"));}}
  var root=PrefabUtility.LoadPrefabContents(WeaponAssetLocations.Resolve(path));var log=new StringBuilder();
  try{
   var sw=root.GetComponentInChildren<WeaponSwitcher>(true);var so=new SerializedObject(sw);var rig=sw.GetComponent<Animator>();var presentation=root.GetComponent<NetworkWeaponPresentation>();
   var catalog=root.GetComponent<NetworkWeaponAttachments>().catalog;
   foreach(var definition in presentation.Weapons){
    bool legacy=!definition.Source.name.StartsWith("DQ_Weapon_",StringComparison.Ordinal);var slot=ReadSlotTemplate(so,definition.Id);
    var profile=slot.Root.GetComponent<PackWeaponPresentation>();if(!profile)profile=slot.Root.AddComponent<PackWeaponPresentation>();profile.preserveLegacyAnimation=legacy;
    if(legacy){
     var source=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(WeaponAssetLocations.Resolve(definition.Id=="Handgun"?"Assets/Animations/ThirdPersonAnimator.controller":definition.Id=="Rifle"?"Assets/Animations/OverrideThirdPerson/OC_TP_Rifle.overrideController":"Assets/Animations/OverrideThirdPerson/OC_TP_Shotgun.overrideController"));
     var tp=CopyController25(source,Folder25+"/"+definition.Id+"_TP_Legacy.controller");
     if(!tp.layers.Any(l=>l.name=="PackFire")){var sm=new AnimatorStateMachine{name="PackFire"};AssetDatabase.AddObjectToAsset(sm,tp);var rest=sm.AddState("Rest");rest.writeDefaultValues=false;sm.defaultState=rest;tp.AddLayer(new AnimatorControllerLayer{name="PackFire",stateMachine=sm,defaultWeight=0,blendingMode=AnimatorLayerBlendingMode.Additive});EditorUtility.SetDirty(tp);}
     var wrapper=new AnimatorOverrideController(tp){name="Phase25_"+definition.Id+"_TP_Legacy"};if(source is AnimatorOverrideController original){var overrides=new List<KeyValuePair<AnimationClip,AnimationClip>>();original.GetOverrides(overrides);wrapper.ApplyOverrides(overrides);}
     definition.Controller=Save25(wrapper,Folder25+"/"+definition.Id+"_TP_Legacy.overrideController");
     var legacySlots=so.FindProperty("slots");for(int i=0;i<legacySlots.arraySize;i++){var row=legacySlots.GetArrayElementAtIndex(i);if(row.FindPropertyRelative("weaponId").stringValue==definition.Id)row.FindPropertyRelative("tpOverrideController").objectReferenceValue=definition.Controller;}
    }
    if(!legacy){
     string fpPath=Folder25+"/"+definition.Id+"_FP.controller";var fp=CopyController25(slot.FPOverride,fpPath);
     var reloadLayer=fp.layers.First(l=>l.name=="Reload").stateMachine;
     foreach(var transition in reloadLayer.anyStateTransitions.Concat(States6(reloadLayer).SelectMany(state=>state.transitions)).Where(t=>t.destinationState&&(t.destinationState.name=="Reload"||t.destinationState.name=="Reload Empty"))){transition.duration=0;transition.hasFixedDuration=true;EditorUtility.SetDirty(transition);}
     foreach(var empty in States6(reloadLayer).Where(s=>!s.motion))if(!empty.behaviours.OfType<WeaponActionLayerWeight>().Any())empty.AddStateMachineBehaviour<WeaponActionLayerWeight>();
     var holster=fp.layers.First(l=>l.name=="Holster").stateMachine;
     foreach(var state in States6(holster)){
      if(state.name=="Holster"&&state.motion is AnimationClip hide)state.speed=hide.length/.35f;
      if(state.name=="Unholster"&&state.motion is AnimationClip draw){var copy=UnityEngine.Object.Instantiate(draw);copy.name=definition.Id+"_Unholster";AnimationUtility.SetAnimationEvents(copy,AnimationUtility.GetAnimationEvents(draw).Where(e=>e.functionName!="OnAnimationEndedHolster").ToArray());state.motion=Save25(copy,Folder25+"/"+copy.name+".anim");state.speed=draw.length/.45f;foreach(var t in state.transitions)if(t.destinationState&&t.destinationState.name=="Ready"){t.exitTime=1f;t.duration=.015f;t.hasFixedDuration=true;}}
      EditorUtility.SetDirty(state);
     }
     var fpWrapper=Save25(new AnimatorOverrideController(fp){name="Phase25_"+definition.Id+"_FP"},Folder25+"/"+definition.Id+"_FP.overrideController");
     string tpPath=Folder25+"/"+definition.Id+"_TP.controller";var tp=CopyController25(definition.Controller,tpPath);var action=tp.layers.First(l=>l.name=="Actions");var fire=States6(action.stateMachine).FirstOrDefault(s=>s.name=="Fire");var existing=tp.layers.FirstOrDefault(l=>l.name=="PackFire");var fireClip=fire?.motion as AnimationClip??(existing!=null?States6(existing.stateMachine).FirstOrDefault(s=>s.name=="Fire")?.motion as AnimationClip:null);
     if(fireClip is AnimationClip original){
      var clip=ImportAdditive25(original,log);foreach(var transition in action.stateMachine.anyStateTransitions.Where(t=>t.destinationState==fire).ToArray())action.stateMachine.RemoveAnyStateTransition(transition);
      foreach(var state in States6(action.stateMachine))foreach(var transition in state.transitions.Where(t=>t.destinationState==fire).ToArray())state.RemoveTransition(transition);
      if(fire)action.stateMachine.RemoveState(fire);foreach(var state in States6(action.stateMachine).Where(s=>!s.motion))state.writeDefaultValues=false;
      var old=tp.layers.FirstOrDefault(l=>l.name=="PackFire");if(old!=null)tp.RemoveLayer(Array.FindIndex(tp.layers,l=>l.name=="PackFire"));
      var machine=new AnimatorStateMachine{name="PackFire"};AssetDatabase.AddObjectToAsset(machine,tp);var idle=machine.AddState("Rest");idle.writeDefaultValues=false;machine.defaultState=idle;
      var shot=machine.AddState("Fire");shot.motion=clip;shot.speed=1;shot.writeDefaultValues=false;
      var enter=machine.AddAnyStateTransition(shot);enter.hasExitTime=false;enter.duration=0;enter.canTransitionToSelf=true;enter.AddCondition(AnimatorConditionMode.If,0,"TP_Fire");enter.AddCondition(AnimatorConditionMode.IfNot,0,"TP_IsReloading");
      var exit=shot.AddTransition(idle);exit.hasExitTime=true;exit.exitTime=.98f;exit.duration=.04f;exit.hasFixedDuration=true;
      tp.AddLayer(new AnimatorControllerLayer{name="PackFire",stateMachine=machine,defaultWeight=1,avatarMask=action.avatarMask,blendingMode=AnimatorLayerBlendingMode.Additive});
     }
     EditorUtility.SetDirty(tp);EditorUtility.SetDirty(fp);
     var tpWrapper=Save25(new AnimatorOverrideController(tp){name="Phase25_"+definition.Id+"_TP"},Folder25+"/"+definition.Id+"_TP.overrideController");definition.Controller=tpWrapper;
     var slots=so.FindProperty("slots");for(int i=0;i<slots.arraySize;i++){var row=slots.GetArrayElementAtIndex(i);if(row.FindPropertyRelative("weaponId").stringValue==definition.Id){row.FindPropertyRelative("overrideController").objectReferenceValue=fpWrapper;row.FindPropertyRelative("tpOverrideController").objectReferenceValue=tpWrapper;}}
     // Reference weapon uses the pack's AR/SMG recoil data, not the attenuated legacy Handgun preset.
     string model=definition.Model.name;var spec=Specs7().Single(s=>s.Id==definition.Id);string sourcePrefab=Path.Combine(Source25,"Prefabs/Weapons/P_LPSP_WEP_"+spec.Model.Substring(3)+".prefab");
     string recoil=File.ReadAllText(WeaponAssetLocations.Resolve(sourcePrefab));var data=Regex.Match(recoil,@"weaponRecoilData: .*guid: (\w+)").Groups[1].Value;
     string arGuid=Regex.Match(File.ReadAllText(WeaponAssetLocations.Resolve(Source25+"/Data/Recoil/SO_WEP_RD_AR.asset.meta")),@"guid: (\w+)").Groups[1].Value;
     var ws=new SerializedObject(slot.Weapon);ws.FindProperty("recoilPreset").enumValueIndex=data==arGuid?(int)RecoilPreset.AR:(int)RecoilPreset.SMG;ws.ApplyModifiedPropertiesWithoutUndo();
     if(definition.Id is "AutoPistol" or "BurstRifle" or "SMG05" or "Sniper02"){
      var scope=WeaponAttachments.Find(slot.Root.transform,"DQ_DefaultScope");var physical=WeaponAttachments.Find(slot.Root.transform,"root");var bounds=WeaponAttachments.BoundsIn(scope.gameObject,physical);var marker=WeaponAttachments.Find(slot.Root.transform,"DQ_OpticalSight");Vector3 sight=new(bounds.center.x,bounds.min.y,bounds.max.z);if(definition.Id is "SMG05" or "Sniper02")sight=FrontIronSight25(scope.gameObject,physical,bounds);marker.localPosition=sight;
     }
     log.AppendLine(definition.Id+": original recoil GUID="+data+"; full authored holster=.35s / unholster=.45s; additive TP fire");
    }
    so.ApplyModifiedPropertiesWithoutUndo();
    var attachments=new SerializedObject(slot.Root.GetComponent<WeaponAttachments>());attachments.FindProperty("catalog").objectReferenceValue=catalog;attachments.FindProperty("stableWeaponId").stringValue=definition.Id;attachments.ApplyModifiedPropertiesWithoutUndo();
    Calibrate25(root,slot,rig,profile,log);
   }
   presentation.BodyAnimator.runtimeAnimatorController=presentation.Weapons.First(d=>d.Id=="Handgun").Controller;so.FindProperty("tpBaseController").objectReferenceValue=presentation.BodyAnimator.runtimeAnimatorController;
   var ids=presentation.Weapons.Select(d=>d.Id).ToArray();catalog.eligibleWeapons=ids;
   foreach(var entry in catalog.entries){var permitted=new List<string>(entry.weapons);foreach(string id in new[]{"Handgun","Rifle","Escopeta"}){
     var slot=ReadSlotTemplate(so,id);bool compatible=WeaponAttachments.Find(slot.Root.transform,entry.socket);
     if(id=="Handgun")compatible&=entry.kind!=WeaponAttachmentKind.Support&&(entry.kind!=WeaponAttachmentKind.Scope||entry.id is "Scope_01" or "Scope_02")&&!entry.flashlight;
     if(id=="Escopeta")compatible&=!entry.requiresBracing&&entry.id!="Muzzle_Silencer_03";
     if(compatible&&!permitted.Contains(id))permitted.Add(id);
    }entry.weapons=permitted.ToArray();}
   EditorUtility.SetDirty(catalog);EditorUtility.SetDirty(presentation);so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,WeaponAssetLocations.Resolve(path));
   var previous=Regex.Matches(before,@"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)",RegexOptions.Multiline|RegexOptions.Singleline).Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
   // Nested prefab additions need the component references written by Unity. Restoring
   // complete GameObject/PrefabInstance records would leave new components orphaned.
   // Preserve only existing transforms (apart from optical markers) and the three
   // protected Weapon records; their animation fields and statistics remain byte-identical.
   var markers=new HashSet<string>(previous.Values.Where(r=>r.Contains("m_EditorClassIdentifier: Scripts::Weapon")).Select(r=>Regex.Match(r,@"adsSightReference: \{fileID: (\d+)\}").Groups[1].Value));
   var preserve=new HashSet<string>{"3180206767226728580","2410896950522157583","6744171994494050035"};
   foreach(var record in previous)if(record.Value.StartsWith("--- !u!4 ",StringComparison.Ordinal)&&!markers.Contains(record.Key))preserve.Add(record.Key);
   foreach(var record in previous)if(record.Value.StartsWith("--- !u!114 ",StringComparison.Ordinal)&&!allowed.Contains(record.Key))preserve.Add(record.Key);
   string saved=Regex.Replace(File.ReadAllText(WeaponAssetLocations.Resolve(path)),@"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)",m=>{
    if(!previous.TryGetValue(m.Groups[1].Value,out var record))return m.Value;
    if(preserve.Contains(m.Groups[1].Value))return record;
    if(record.StartsWith("--- !u!1001 ",StringComparison.Ordinal)){var originalMods=Regex.Match(record,@"^    m_Modifications:.*?(?=^    m_RemovedComponents:)",RegexOptions.Multiline|RegexOptions.Singleline);if(originalMods.Success)return Regex.Replace(m.Value,@"^    m_Modifications:.*?(?=^    m_RemovedComponents:)",_=>originalMods.Value,RegexOptions.Multiline|RegexOptions.Singleline);}
    return m.Value;
   },RegexOptions.Multiline|RegexOptions.Singleline);File.WriteAllText(WeaponAssetLocations.Resolve(path),saved);AssetDatabase.ImportAsset(WeaponAssetLocations.Resolve(path),ImportAssetOptions.ForceUpdate);AssetDatabase.SaveAssets();
   File.WriteAllText(WeaponAssetLocations.Resolve(Path.Combine(output,"Install.txt")),log.ToString());File.WriteAllLines(WeaponAssetLocations.Resolve(Path.Combine(output,"AllowedRecords.txt")),allowed);
   var settings=AssetDatabase.LoadAssetAtPath<DeadQuarantine.PvPvE.Networking.PvPvENetworkSettings>(WeaponAssetLocations.Resolve("Assets/PvPvE/Settings/PvPvENetworkSettings.asset"));settings.ProtocolVersion=5025;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
  }finally{PrefabUtility.UnloadPrefabContents(root);}
 }
 static AnimatorController CopyController25(RuntimeAnimatorController source,string path){var original=source is AnimatorOverrideController o?o.runtimeAnimatorController:source;if(!AssetDatabase.LoadAssetAtPath<AnimatorController>(WeaponAssetLocations.Resolve(path))&&!AssetDatabase.CopyAsset(WeaponAssetLocations.Resolve(AssetDatabase.GetAssetPath(original)),WeaponAssetLocations.Resolve(path)))throw new InvalidOperationException(path);return AssetDatabase.LoadAssetAtPath<AnimatorController>(WeaponAssetLocations.Resolve(path));}
 static T Save25<T>(T value,string path)where T:UnityEngine.Object{var old=AssetDatabase.LoadAssetAtPath<T>(WeaponAssetLocations.Resolve(path));if(old){EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(value,WeaponAssetLocations.Resolve(path));return value;}
 static AnimationClip ImportAdditive25(AnimationClip original,StringBuilder log){
  string name=Path.GetFileName(AssetDatabase.GetAssetPath(original));string source=Directory.EnumerateFiles(WeaponAssetLocations.Resolve(Source25+"/Art/Animations/Character"),name,SearchOption.AllDirectories).Single();string folder=Folder25+"/Source431";Directory.CreateDirectory(WeaponAssetLocations.Resolve(folder));string dest=folder+"/"+name;if(!File.Exists(WeaponAssetLocations.Resolve(dest))){File.Copy(WeaponAssetLocations.Resolve(source),WeaponAssetLocations.Resolve(dest));AssetDatabase.ImportAsset(WeaponAssetLocations.Resolve(dest),ImportAssetOptions.ForceUpdate);}
  var importer=(ModelImporter)AssetImporter.GetAtPath(WeaponAssetLocations.Resolve(dest));bool needsImport=importer.animationType!=ModelImporterAnimationType.Human||importer.avatarSetup!=ModelImporterAvatarSetup.CreateFromThisModel||importer.clipAnimations.Length==0||importer.clipAnimations.Any(c=>!c.hasAdditiveReferencePose||c.events.Length>0);if(needsImport){importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;var clips=importer.defaultClipAnimations;foreach(var clip in clips){clip.name=Path.GetFileNameWithoutExtension(source);clip.hasAdditiveReferencePose=true;clip.additiveReferencePoseFrame=clip.firstFrame;clip.events=Array.Empty<AnimationEvent>();}importer.clipAnimations=clips;importer.SaveAndReimport();}
  using var sha=SHA256.Create();if(!sha.ComputeHash(File.ReadAllBytes(WeaponAssetLocations.Resolve(source))).SequenceEqual(sha.ComputeHash(File.ReadAllBytes(WeaponAssetLocations.Resolve(dest)))))throw new InvalidOperationException("Reference bytes changed: "+name);log.AppendLine("4.3.1 binary verified; Humanoid/CreateFromThisModel/additive frame0: "+dest);return AssetDatabase.LoadAllAssetsAtPath(WeaponAssetLocations.Resolve(dest)).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
 }
 static Vector3 FrontIronSight25(GameObject scope,Transform physical,Bounds bounds){
  var vertices=new List<Vector3>();foreach(var r in scope.GetComponentsInChildren<Renderer>(true)){var mf=r.GetComponent<MeshFilter>();var sk=r as SkinnedMeshRenderer;var mesh=mf?mf.sharedMesh:sk?sk.sharedMesh:null;if(mesh)vertices.AddRange(mesh.vertices.Select(v=>physical.InverseTransformPoint(r.transform.TransformPoint(v))));}
  var front=vertices.Where(v=>v.y<=bounds.min.y+Mathf.Max(.015f,bounds.size.y*.2f)&&Mathf.Abs(v.x-bounds.center.x)<=Mathf.Max(.001f,bounds.size.x*.15f)).ToArray();
  return new Vector3(bounds.center.x,bounds.min.y,front.Length>0?front.Max(v=>v.z):bounds.max.z);
 }
 static void Calibrate25(GameObject player,SlotTemplate slot,Animator rig,PackWeaponPresentation profile,StringBuilder log){
  var value=slot.FPOverride?slot.FPOverride:(RuntimeAnimatorController)new SerializedObject(player.GetComponentInChildren<WeaponSwitcher>(true)).FindProperty("baseController").objectReferenceValue;
  var clips=value.animationClips;var aim=clips.FirstOrDefault(c=>AssetDatabase.GetAssetPath(c).IndexOf("Aim_Pose",StringComparison.OrdinalIgnoreCase)>=0);if(!aim)throw new InvalidOperationException("No authored aim pose: "+slot.Id);
  var settings=new SerializedObject(slot.Weapon);var camera=(Camera)settings.FindProperty("weaponCamera").objectReferenceValue;var physical=WeaponAttachments.Find(slot.Root.transform,"root");
  AnimationMode.StartAnimationMode();try{Sample6(rig.gameObject,aim);profile.aimPhysicalToCamera=camera.transform.worldToLocalMatrix*physical.localToWorldMatrix;profile.cameraToRigParent=rig.transform.parent.worldToLocalMatrix*camera.transform.localToWorldMatrix;}finally{AnimationMode.StopAnimationMode();}
  log.AppendLine("STATIC ADS calibration "+slot.Id+" from "+AssetDatabase.GetAssetPath(aim)+"; legacy="+profile.preserveLegacyAnimation);EditorUtility.SetDirty(profile);
 }
}
#endif
