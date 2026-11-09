#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
public static partial class CompleteWeaponsIntegrationBuilder
{
 static string Phase6Output=>Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE6_OUTPUT")??"Documentation/PvPvE/Verificacion/Fase6";
 static readonly string[] MissingModels6={"SK_Handgun_01","SK_Handgun_02","SK_SMG_02","SK_SMG_05","SK_Sniper_01","SK_Sniper_02","SK_Sniper_03"};
 public static void InspectMissingPhase6()
 {
  Directory.CreateDirectory(Phase6Output);var b=new StringBuilder();
  var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
  try{
   var sw=root.GetComponentInChildren<WeaponSwitcher>(true);var so=new SerializedObject(sw);
   foreach(string key in new[]{"baseController","tpBaseController"}){
    var ctl=so.FindProperty(key).objectReferenceValue as AnimatorController;b.AppendLine("CONTROLLER "+key+" "+AssetDatabase.GetAssetPath(ctl));
    b.AppendLine("PARAMETERS "+string.Join(",",ctl.parameters.Select(p=>p.name+":"+p.type)));
    foreach(var layer in ctl.layers)foreach(var st in States6(layer.stateMachine)){
     b.AppendLine(layer.name+"/"+st.name+" motion="+(st.motion?st.motion.name:"null")+" speed="+st.speed+" parameter="+st.speedParameter+" mask="+(layer.avatarMask?AssetDatabase.GetAssetPath(layer.avatarMask):"none"));
     foreach(var t in st.transitions)b.AppendLine("  ->"+(t.destinationState?t.destinationState.name:"exit")+" exit="+t.exitTime+" "+string.Join(",",t.conditions.Select(c=>c.parameter+" "+c.mode+" "+c.threshold)));
    }
   }
   var slots=so.FindProperty("slots");foreach(string id in new[]{"Handgun","Rifle","Escopeta"}){
    var template=ReadSlotTemplate(so,id);var weapon=new SerializedObject(template.Weapon);b.AppendLine("TEMPLATE "+id+" root="+AnimationUtility.CalculateTransformPath(template.Root.transform,root.transform));
    foreach(string key in new[]{"audioSource","weaponRecoil","cameraRecoil","weaponAnimator","firePoint","playerCamera","weaponCamera"}){var obj=weapon.FindProperty(key).objectReferenceValue;var c=obj as Component;b.AppendLine(key+"="+(c?AnimationUtility.CalculateTransformPath(c.transform,root.transform):AssetDatabase.GetAssetPath(obj)));}
   }
   var models=MissingModels6.Select(RequireAsset<GameObject>).ToArray();
   foreach(var model in models){string path=AssetDatabase.GetAssetPath(model);b.AppendLine("MODEL "+path);
    b.AppendLine("SOCKETS "+string.Join(",",model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("SOCKET")).Select(t=>t.name+"="+t.localPosition+" rot="+t.localEulerAngles)));
    foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")))b.AppendLine(" PART "+clip.name+" len="+clip.length+" paths="+string.Join(",",AnimationUtility.GetCurveBindings(clip).Select(c=>c.path).Distinct().Take(5)));
   }
   foreach(string path in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/Art/") && (p.Contains("SM_Sniper_")||p.Contains("SM_Handgun_01")||p.Contains("SM_Handgun_02")||p.Contains("T_HUD_Sniper_")||p.Contains("T_HUD_Handgun_01")||p.Contains("T_HUD_Handgun_02"))))b.AppendLine("ASSET "+path);
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  File.WriteAllText(Path.Combine(Phase6Output,"IntegrationInputs.txt"),b.ToString());
 }
 static IEnumerable<AnimatorState> States6(AnimatorStateMachine sm)=>sm.states.Select(s=>s.state).Concat(sm.stateMachines.SelectMany(s=>States6(s.stateMachine)));
}
#endif
