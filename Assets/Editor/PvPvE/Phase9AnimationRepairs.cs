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
public static partial class CompleteWeaponsIntegrationBuilder
{
 const string Anim9="weapon-assets:/NewWeapons/Phase9";
 static readonly Dictionary<string,AnimationClip> referenceClips9=new();
 public static void RepairAnimationsPhase9(){
  referenceClips9.Clear();Directory.CreateDirectory(WeaponAssetLocations.Resolve(Anim9));AssetDatabase.Refresh();
  const string path="Assets/Prefabs/Player.prefab";string before=File.ReadAllText(WeaponAssetLocations.Resolve(path));var root=PrefabUtility.LoadPrefabContents(WeaponAssetLocations.Resolve(path));var log=new StringBuilder();
  try{
   var switcher=root.GetComponentInChildren<WeaponSwitcher>(true);var settings=new SerializedObject(switcher);
   foreach(var spec in Specs7()){
    var slot=ReadSlotTemplate(settings,spec.Id);
    string file=Anim9+"/"+spec.Id+"_FP.controller";
    if(!AssetDatabase.LoadAssetAtPath<AnimatorController>(WeaponAssetLocations.Resolve(file)))AssetDatabase.CopyAsset(WeaponAssetLocations.Resolve(AssetDatabase.GetAssetPath(slot.FPOverride.runtimeAnimatorController)),WeaponAssetLocations.Resolve(file));
    var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(WeaponAssetLocations.Resolve(file));
    // Empty masked action states must contribute no pose; writing defaults here pins the arms over reload/ADS.
    foreach(var layer in controller.layers)foreach(var state in States6(layer.stateMachine).Where(s=>!s.motion)){state.writeDefaultValues=false;EditorUtility.SetDirty(state);}
    var ready=States6(controller.layers.First(l=>l.name=="Holster").stateMachine).First(s=>s.name=="Ready");
    if(!ready.behaviours.OfType<WeaponHolsterLayerWeight>().Any())ready.AddStateMachineBehaviour<WeaponHolsterLayerWeight>();
    foreach(var state in States6(controller.layers.First(l=>l.name=="Poses").stateMachine))
     ReplaceMotion6(state.motion,clip=>ReferenceClip9(clip,log),motion=>state.motion=motion);
    if(spec.Id!="Sniper01"){
     ClipSet clips;
     if(spec.Id.StartsWith("Sniper")){string prefix="A_FP_PCH_"+spec.Model.Substring(3)+"_";clips=new ClipSet{Reload=RequireClip(prefix+"Reload"),ReloadEmpty=RequireClip(prefix+"Reload_Empty")};}
     else clips=ResolveFPClips(spec);
     foreach(bool empty in new[]{false,true}){
      var source=ReferenceClip9(empty?clips.ReloadEmpty:clips.Reload,log);
      var copy=UnityEngine.Object.Instantiate(source);copy.name=spec.Id+(empty?"_ReloadEmpty":"_Reload");
      var fill=AnimationUtility.GetAnimationEvents(source).FirstOrDefault(e=>e.functionName=="OnAmmunitionFill");
      AnimationUtility.SetAnimationEvents(copy,new[]{new AnimationEvent{functionName="OnAmmunitionFill",time=fill!=null?fill.time:copy.length*.7f},new AnimationEvent{functionName="OnAnimationEndedReload",time=copy.length*.99f}});
      var saved=Save9(copy,Anim9+"/"+copy.name+".anim");
      var state=States6(controller.layers.First(l=>l.name=="Reload").stateMachine).First(s=>s.name==(empty?"Reload Empty":"Reload"));
      state.motion=saved;EditorUtility.SetDirty(state);
      log.AppendLine(spec.Id+" "+state.name+" => "+AssetDatabase.GetAssetPath(source));
     }
    }
    EditorUtility.SetDirty(controller);var wrapper=Save9(new AnimatorOverrideController(controller){name="Phase9_"+spec.Id+"_FP"},Anim9+"/"+spec.Id+"_FP.overrideController");
    string tpFile=Anim9+"/"+spec.Id+"_TP.controller";
    if(!AssetDatabase.LoadAssetAtPath<AnimatorController>(WeaponAssetLocations.Resolve(tpFile)))AssetDatabase.CopyAsset(WeaponAssetLocations.Resolve(AssetDatabase.GetAssetPath(slot.TPOverride.runtimeAnimatorController)),WeaponAssetLocations.Resolve(tpFile));
    var tp=AssetDatabase.LoadAssetAtPath<AnimatorController>(WeaponAssetLocations.Resolve(tpFile));
    AnimationClip Human(AnimationClip original){
     string asset=AssetDatabase.GetAssetPath(original);
     if(!Path.GetFileName(asset).StartsWith("A_TP_CH_",StringComparison.Ordinal)||AssetImporter.GetAtPath(WeaponAssetLocations.Resolve(asset)) is not ModelImporter importer)return null;
     return importer.animationType==ModelImporterAnimationType.Human&&importer.avatarSetup==ModelImporterAvatarSetup.CreateFromThisModel&&original.humanMotion?null:ReferenceClip9(original,log,true,true);
    }
    foreach(var layer in tp.layers)foreach(var state in States6(layer.stateMachine))ReplaceMotion6(state.motion,Human,m=>state.motion=m);
    if(spec.Id!="Sniper01"){
     var clips=TPClips6(spec);var actions=tp.layers.First(l=>l.name=="Actions").stateMachine;
     foreach(bool empty in new[]{false,true}){var original=empty?clips.ReloadEmpty:clips.Reload;var state=States6(actions).First(s=>empty?(s.name=="Reload Empty"||s.name=="Reload_Empty"):s.name=="Reload");state.motion=Human(original)??original;EditorUtility.SetDirty(state);}
    }
    EditorUtility.SetDirty(tp);var tpWrapper=Save9(new AnimatorOverrideController(tp){name="Phase9_"+spec.Id+"_TP"},Anim9+"/"+spec.Id+"_TP.overrideController");
    var presentation=root.GetComponent<NetworkWeaponPresentation>();FindDefinition(presentation.Weapons,spec.Id).Controller=tpWrapper;EditorUtility.SetDirty(presentation);
    var slots=settings.FindProperty("slots");for(int i=0;i<slots.arraySize;i++){var s=slots.GetArrayElementAtIndex(i);if(s.FindPropertyRelative("weaponId").stringValue==spec.Id){s.FindPropertyRelative("overrideController").objectReferenceValue=wrapper;s.FindPropertyRelative("tpOverrideController").objectReferenceValue=tpWrapper;}}
    foreach(var clip in tpWrapper.animationClips.Distinct()){
     string imported=AssetDatabase.GetAssetPath(clip);if(AssetImporter.GetAtPath(WeaponAssetLocations.Resolve(imported)) is not ModelImporter importer)continue;
     if(!Path.GetFileName(imported).StartsWith("A_TP_CH_",StringComparison.Ordinal)){
      log.AppendLine("Existing non-weapon base locomotion/look layer preserved: "+imported);continue;
     }
     if(importer.animationType!=ModelImporterAnimationType.Human||importer.avatarSetup!=ModelImporterAvatarSetup.CreateFromThisModel||!clip.humanMotion)
      throw new InvalidOperationException("TP must be Humanoid / Create From This Model: "+imported);
     log.AppendLine("TP VERIFIED Humanoid / Create From This Model: "+imported);
    }
   }
   settings.ApplyModifiedPropertiesWithoutUndo();
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetLocations.Resolve(path));AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab.GetComponentInChildren<WeaponSwitcher>(true),out string _,out long allowed);
   AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab.GetComponent<NetworkWeaponPresentation>(),out string _,out long presentationId);
   PrefabUtility.SaveAsPrefabAsset(root,WeaponAssetLocations.Resolve(path));
   var old=Regex.Matches(before,@"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)",RegexOptions.Multiline|RegexOptions.Singleline).Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
   var savedText=Regex.Replace(File.ReadAllText(WeaponAssetLocations.Resolve(path)),@"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)",m=>old.TryGetValue(m.Groups[1].Value,out var value)&&m.Groups[1].Value!=allowed.ToString()&&m.Groups[1].Value!=presentationId.ToString()?value:m.Value,RegexOptions.Multiline|RegexOptions.Singleline);
   File.WriteAllText(WeaponAssetLocations.Resolve(path),savedText);AssetDatabase.ImportAsset(WeaponAssetLocations.Resolve(path),ImportAssetOptions.ForceUpdate);AssetDatabase.SaveAssets();
   string output=Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE9_OUTPUT");File.WriteAllText(WeaponAssetLocations.Resolve(Path.Combine(output,"AnimationRepair.txt")),log.ToString());File.WriteAllLines(WeaponAssetLocations.Resolve(Path.Combine(output,"AllowedPlayerRecords.txt")),new[]{allowed.ToString(),presentationId.ToString()});
  }finally{PrefabUtility.UnloadPrefabContents(root);}
 }
 static T Save9<T>(T value,string path) where T:UnityEngine.Object {var previous=AssetDatabase.LoadAssetAtPath<T>(WeaponAssetLocations.Resolve(path));if(previous){EditorUtility.CopySerialized(value,previous);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(previous);return previous;}AssetDatabase.CreateAsset(value,WeaponAssetLocations.Resolve(path));return value;}
 static AnimationClip ReferenceClip9(AnimationClip clip,StringBuilder log,bool human=false,bool force=false){
  const string reference="C:/Users/andre/Documents/GitHub/Low Poly Shooter Pack v4.3.1 (1)/Assets/Infima Games/Low Poly Shooter Pack/Art/Animations/Character";
  string local=AssetDatabase.GetAssetPath(clip);string source=Directory.EnumerateFiles(WeaponAssetLocations.Resolve(reference),Path.GetFileName(local),SearchOption.AllDirectories).Single();
  if(referenceClips9.TryGetValue(source,out var cached))return cached;
  using var sha=SHA256.Create();
  if(!force&&sha.ComputeHash(File.ReadAllBytes(WeaponAssetLocations.Resolve(local))).SequenceEqual(sha.ComputeHash(File.ReadAllBytes(WeaponAssetLocations.Resolve(source))))){log.AppendLine("SOURCE 4.3.1 binary verified: "+local);referenceClips9[source]=clip;return clip;}
  string directory=Anim9+"/Source431";if(!AssetDatabase.IsValidFolder(WeaponAssetLocations.Resolve(directory)))AssetDatabase.CreateFolder(WeaponAssetLocations.Resolve(Anim9),"Source431");
  string destination=directory+"/"+Path.GetFileName(source);
  if(!File.Exists(WeaponAssetLocations.Resolve(destination))){File.Copy(WeaponAssetLocations.Resolve(source),WeaponAssetLocations.Resolve(destination));AssetDatabase.ImportAsset(WeaponAssetLocations.Resolve(destination),ImportAssetOptions.ForceUpdate);}
  var importer=(ModelImporter)AssetImporter.GetAtPath(WeaponAssetLocations.Resolve(destination));
  importer.animationType=human?ModelImporterAnimationType.Human:ModelImporterAnimationType.Generic;importer.avatarSetup=human?ModelImporterAvatarSetup.CreateFromThisModel:ModelImporterAvatarSetup.NoAvatar;importer.importAnimation=true;importer.globalScale=.01f;
  var animations=importer.defaultClipAnimations;
  string metadata=File.ReadAllText(WeaponAssetLocations.Resolve(source+".meta"));
  float Number(string key,float fallback){var match=Regex.Match(metadata,@"(?m)^\s+"+key+@": ([-\d.]+)\s*$");return match.Success?float.Parse(match.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture):fallback;}
  importer.globalScale=Number("globalScale",human?1:.01f);
  var events=Regex.Matches(metadata,@"(?m)^\s+- time: ([\d.]+)\r?\n\s+functionName: ([^\r\n]+)").Cast<Match>().Select(m=>new AnimationEvent{time=float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture),functionName=m.Groups[2].Value.Trim(),messageOptions=SendMessageOptions.DontRequireReceiver}).ToArray();
  foreach(var animation in animations){animation.name=Path.GetFileNameWithoutExtension(source);animation.firstFrame=Number("firstFrame",animation.firstFrame);animation.lastFrame=Number("lastFrame",animation.lastFrame);animation.loopTime=Number("loopTime",0)>0;animation.hasAdditiveReferencePose=Number("hasAdditiveReferencePose",1)>0;animation.additiveReferencePoseFrame=Number("additiveReferencePoseFrame",0);animation.events=events;}
  importer.clipAnimations=animations;importer.SaveAndReimport();
  var imported=AssetDatabase.LoadAllAssetsAtPath(WeaponAssetLocations.Resolve(destination)).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
  log.AppendLine("SOURCE 4.3.1 isolated import, original shared clip preserved: "+destination);
  referenceClips9[source]=imported;
  return imported;
 }
}
#endif
