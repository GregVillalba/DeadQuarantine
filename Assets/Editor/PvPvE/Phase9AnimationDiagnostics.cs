#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
public static partial class CompleteWeaponsIntegrationBuilder
{
 public static void AnalyzeAnimationsPhase9(){
  var root=AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetLocations.Resolve("Assets/Prefabs/Player.prefab"));var rig=root.GetComponentInChildren<WeaponSwitcher>(true);var so=new SerializedObject(rig);var log=new StringBuilder();
  foreach(var s in Specs7()){
   var slot=ReadSlotTemplate(so,s.Id);var controller=(AnimatorController)slot.FPOverride.runtimeAnimatorController;
   log.AppendLine("WEAPON "+s.Id);
   var settings=new SerializedObject(slot.Weapon);
   var parts=slot.Root.GetComponent<Animator>();
   foreach(var clip in parts.runtimeAnimatorController.animationClips.Where(c=>c.name.Contains("Reload")).Distinct())log.AppendLine("PARTS "+clip.name+" length="+clip.length);
   foreach(string field in new[]{"reloadClipSpeedMultiplier","reloadEmptyClipSpeedMultiplier","partsReloadClipSpeedMultiplier","partsReloadEmptyClipSpeedMultiplier"})log.AppendLine(field+"="+settings.FindProperty(field).floatValue);
   foreach(var l in controller.layers.Where(l=>l.name=="Poses"||l.name=="Reload"||l.name=="MovementActions")){
    log.AppendLine("LAYER "+l.name+" "+l.blendingMode+" mask="+(l.avatarMask?l.avatarMask.name:"NONE"));
    foreach(var st in States6(l.stateMachine)){
     log.AppendLine("STATE "+st.name+" speed="+st.speed+" param="+st.speedParameter+" write="+st.writeDefaultValues);
     if(st.motion)Dump9(st.motion,rig.transform,log);
    }
   }
  }
  string output=Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE9_OUTPUT");Directory.CreateDirectory(WeaponAssetLocations.Resolve(output));File.WriteAllText(WeaponAssetLocations.Resolve(Path.Combine(output,"AnimationDiagnostics.txt")),log.ToString());
 }
 static void Dump9(Motion m,Transform rig,StringBuilder log){
  if(m is BlendTree tree){foreach(var child in tree.children){log.AppendLine("BLEND "+tree.blendParameter+" "+child.threshold);Dump9(child.motion,rig,log);}return;}
  if(m is not AnimationClip c)return;
  var bindings=AnimationUtility.GetCurveBindings(c);var transforms=bindings.Where(b=>b.type==typeof(Transform)).ToArray();
  log.AppendLine("CLIP "+AssetDatabase.GetAssetPath(c)+" length="+c.length+" bound="+transforms.Count(b=>b.path==""||rig.Find(b.path))+"/"+transforms.Length+" events="+string.Join(",",AnimationUtility.GetAnimationEvents(c).Select(e=>e.functionName)));
  foreach(var b in transforms.Where(b=>b.path!=""&&!rig.Find(b.path)).Take(3))log.AppendLine("UNBOUND "+b.path);
 }
}
#endif
