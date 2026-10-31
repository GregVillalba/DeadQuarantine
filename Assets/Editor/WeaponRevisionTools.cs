#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
public static class WeaponRevisionTools
{
 public static string Output=>Environment.GetEnvironmentVariable("DQ_REVIEW_OUTPUT")??Path.GetFullPath("Temp/WeaponRevision");
 public static void Inspect() {
  Directory.CreateDirectory(Output);var b=new StringBuilder();
  foreach(var path in AssetDatabase.GetAllAssetPaths().Where(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".anim"))) {
   foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview") && (c.name.IndexOf("holster",StringComparison.OrdinalIgnoreCase)>=0 || c.name.IndexOf("knife",StringComparison.OrdinalIgnoreCase)>=0 || c.name.IndexOf("melee",StringComparison.OrdinalIgnoreCase)>=0)))b.AppendLine("ACTION "+path+" | "+clip.name+" human="+clip.isHumanMotion);
  }
  var player=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
  try {
   var presentation=player.GetComponent<NetworkWeaponPresentation>();var rig=presentation.Switcher.GetComponent<Animator>();
   foreach(var t in presentation.Body.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Hand")||t.name.Contains("Finger")))b.AppendLine("BONE "+t.name+" p="+t.localPosition+" r="+t.localEulerAngles);
   foreach(var clipPath in new[]{"Assets/Animations/Rebound/Shotgun_Fire_WEP.anim","Assets/Animations/Rebound/Shotgun_Reload_Open_WEP.anim","Assets/Animations/Rebound/Shotgun_Reload_Insert_WEP.anim","Assets/Animations/Rebound/Shotgun_Reload_Close_WEP.anim"}) {
    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);b.AppendLine("CLIP "+clipPath+" length="+clip.length);
    foreach(var binding in AnimationUtility.GetCurveBindings(clip).Where(c=>c.path.Contains("forestock")||c.path.Contains("bolt"))) {
     var curve=AnimationUtility.GetEditorCurve(clip,binding);b.AppendLine(binding.path+" "+binding.propertyName+" => "+string.Join(",",new[]{0f,.2f,.4f,.6f,.8f,1f}.Select(t=>(t*clip.length).ToString("F3")+":"+curve.Evaluate(t*clip.length).ToString("F4"))));
    }
   }
  }finally {PrefabUtility.UnloadPrefabContents(player);}
  File.WriteAllText(Path.Combine(Output,"inspection.txt"),b.ToString());
 }
}
#endif
