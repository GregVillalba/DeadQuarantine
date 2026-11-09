#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
public static partial class CompleteWeaponsIntegrationBuilder {
 public static void DiagnosePhase25(){
  var output=Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE25_OUTPUT");Directory.CreateDirectory(output);var log=new StringBuilder();var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");var sw=player.GetComponentInChildren<WeaponSwitcher>(true);var so=new SerializedObject(sw);var presentation=player.GetComponent<NetworkWeaponPresentation>();
  foreach(var d in presentation.Weapons){var slot=ReadSlotTemplate(so,d.Id);var settings=new SerializedObject(d.Source);log.AppendLine("WEAPON "+d.Id+" GO="+d.Source.name+" root="+slot.Root.name+" model="+AssetDatabase.GetAssetPath(d.Model)+" grip="+d.GripPosition+"/"+d.GripRotation);foreach(string field in new[]{"reloadTime","recoilIntensityMultiplier","recoilPreset","reloadClipSpeedMultiplier","reloadEmptyClipSpeedMultiplier","enforceReloadDuration","adsCenterVisualSight","adsSightReference","weaponCamera","weaponRecoil","cameraRecoil"}){var prop=settings.FindProperty(field);log.AppendLine(field+"="+(prop.propertyType==SerializedPropertyType.Float?prop.floatValue.ToString():prop.propertyType==SerializedPropertyType.Boolean?prop.boolValue.ToString():prop.propertyType==SerializedPropertyType.Enum?prop.enumValueIndex.ToString():prop.objectReferenceValue?prop.objectReferenceValue.name:"NULL"));}
   log.AppendLine("SOCKETS "+string.Join(" | ",slot.Root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("SOCKET")||t.name=="root"||t.name.Contains("Optical")).Select(t=>t.name+":"+t.localPosition+":"+t.localEulerAngles)));
   DumpController25(slot.FPOverride,"FP",log);DumpController25(d.Controller,"TP",log);
  }
  File.WriteAllText(Path.Combine(output,"Diagnostics.txt"),log.ToString());
 }
 static void DumpController25(RuntimeAnimatorController value,string role,StringBuilder log){var controller=value is AnimatorOverrideController o?o.runtimeAnimatorController:value;log.AppendLine(role+" CONTROLLER "+AssetDatabase.GetAssetPath(value));if(controller is not AnimatorController c)return;foreach(var l in c.layers.Where(l=>new[]{"Holster","Reload","Actions","Locomotion","Poses","WeaponSwitch"}.Contains(l.name))){log.AppendLine("LAYER "+l.name+" weight="+l.defaultWeight+" blend="+l.blendingMode+" mask="+(l.avatarMask?l.avatarMask.name:"none"));foreach(var state in States6(l.stateMachine)){log.AppendLine("STATE "+state.name+" speed="+state.speed+" wd="+state.writeDefaultValues+" motion="+(state.motion?state.motion.name:"NULL"));if(state.motion is AnimationClip clip){log.AppendLine("CLIP "+AssetDatabase.GetAssetPath(clip)+" length="+clip.length+" events="+string.Join(",",AnimationUtility.GetAnimationEvents(clip).Select(e=>e.time+":"+e.functionName)));}if(state.motion is BlendTree tree)foreach(var child in tree.children)log.AppendLine("CHILD "+child.threshold+" "+(child.motion?child.motion.name:"NULL"));foreach(var t in state.transitions)log.AppendLine("TO "+(t.destinationState?t.destinationState.name:"EXIT")+" exit="+t.hasExitTime+"/"+t.exitTime+" duration="+t.duration);}}}
}
#endif
