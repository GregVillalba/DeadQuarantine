#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Text;using UnityEditor;using UnityEditor.Animations;using UnityEngine;
public static partial class CompleteWeaponsIntegrationBuilder {
 public static void InstallPistolPresentation(){
 const string path="Assets/Prefabs/Player.prefab";string original=File.ReadAllText(WeaponAssetLocations.Resolve(path));var root=PrefabUtility.LoadPrefabContents(WeaponAssetLocations.Resolve(path));var allowed=new System.Collections.Generic.HashSet<string>();var log=new StringBuilder();
 try{var so=new SerializedObject(root.GetComponentInChildren<WeaponSwitcher>(true));
 foreach(var id in new[]{"AutoPistol","Handgun01","Handgun02"}){
 var slot=ReadSlotTemplate(so,id);var persistent=ReadSlotTemplate(new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetLocations.Resolve(path)).GetComponentInChildren<WeaponSwitcher>(true)),id).Weapon;AssetDatabase.TryGetGUIDAndLocalFileIdentifier(persistent,out string _,out long file);allowed.Add(file.ToString());
 var ctl=PoseController(slot.FPOverride);var fire=ctl.layers.First(l=>l.name=="Fire");foreach(var clip in ctl.animationClips.Where(c=>AssetDatabase.GetAssetPath(c).EndsWith("/"+id+"_Fire.anim")||AssetDatabase.GetAssetPath(c).EndsWith("/"+id+"_AimFire.anim"))){var events=AnimationUtility.GetAnimationEvents(clip);foreach(var ev in events)if(ev.functionName=="OnIntegratedEjectCasing")ev.time=.035f;AnimationUtility.SetAnimationEvents(clip,events);EditorUtility.SetDirty(clip);}foreach(var state in States6(fire.stateMachine))if(state.name=="Fire"){state.speed=1;EditorUtility.SetDirty(state);}
 var ws=new SerializedObject(slot.Weapon);
 foreach(var key in new[]{"reloadClipSpeedMultiplier","reloadEmptyClipSpeedMultiplier","partsReloadClipSpeedMultiplier","partsReloadEmptyClipSpeedMultiplier","tpPartsReloadClipSpeedMultiplier","tpPartsReloadEmptyClipSpeedMultiplier"})ws.FindProperty(key).floatValue=1;
 var tp=PoseController(root.GetComponent<NetworkWeaponPresentation>().Weapons.Single(d=>d.Id==id).Controller);
 foreach(var state in States6(tp.layers.First(l=>l.name=="Actions").stateMachine))if(state.name is "Reload" or "Reload Empty"){
  state.speed=1;EditorUtility.SetDirty(state);ws.FindProperty(state.name=="Reload"?"tpReloadClipSpeedMultiplier":"tpReloadEmptyClipSpeedMultiplier").floatValue=((AnimationClip)state.motion).length/(state.name=="Reload"?2.2333333f:2.7333333f);
 }
 ws.FindProperty("reloadTime").floatValue=2.2333333f;ws.ApplyModifiedPropertiesWithoutUndo();
 var parts=slot.Root.GetComponent<Animator>();foreach(var c in new[]{PoseController(parts.runtimeAnimatorController),PoseController(root.GetComponent<NetworkWeaponPresentation>().Weapons.Single(d=>d.Id==id).Model.GetComponent<Animator>().runtimeAnimatorController)}){
 var machine=c.layers[0].stateMachine;var shot=States6(machine).Single(st=>st.name=="Fire");var empty=States6(machine).Single(st=>st.name=="Empty");shot.speed=1;
 foreach(var transition in shot.transitions.Where(t=>t.destinationState&&t.destinationState.name=="Idle")){if(!transition.conditions.Any(x=>x.parameter=="Empty"))transition.AddCondition(AnimatorConditionMode.IfNot,0,"Empty");EditorUtility.SetDirty(transition);}
 var stop=shot.transitions.FirstOrDefault(t=>t.destinationState==empty)??shot.AddTransition(empty);stop.hasExitTime=true;stop.exitTime=Mathf.Min(1,.10f/((AnimationClip)shot.motion).length);stop.duration=.01f;stop.hasFixedDuration=true;if(!stop.conditions.Any(x=>x.parameter=="Empty"))stop.AddCondition(AnimatorConditionMode.If,0,"Empty");EditorUtility.SetDirty(stop);EditorUtility.SetDirty(shot);EditorUtility.SetDirty(c);
 }
 EditorUtility.SetDirty(ctl);EditorUtility.SetDirty(tp);log.AppendLine(id+": authored fire speed 1; reload 2.233s / empty 2.733s; last shot completes its mechanical cycle into Empty.");
 }
 PrefabUtility.SaveAsPrefabAsset(root,WeaponAssetLocations.Resolve(path));
 var old=System.Text.RegularExpressions.Regex.Matches(original,@"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)",System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.Singleline).Cast<System.Text.RegularExpressions.Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
 File.WriteAllText(WeaponAssetLocations.Resolve(path),System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(WeaponAssetLocations.Resolve(path)),@"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)",m=>!allowed.Contains(m.Groups[1].Value)&&old.TryGetValue(m.Groups[1].Value,out var before)?before:m.Value,System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.Singleline));AssetDatabase.ImportAsset(WeaponAssetLocations.Resolve(path));AssetDatabase.SaveAssets();
 }finally{PrefabUtility.UnloadPrefabContents(root);}File.WriteAllText(WeaponAssetLocations.Resolve(Path.Combine(Environment.GetEnvironmentVariable("DQ_UI_OUTPUT"),"PistolsInstalled.txt")),log.ToString());
 }
 public static void InstallAndBuildPistols(){InstallPistolPresentation();Phase25WeaponsValidation.Build();}
 public static void InspectPistols(){
 var log=new StringBuilder();var root=PrefabUtility.LoadPrefabContents(WeaponAssetLocations.Resolve("Assets/Prefabs/Player.prefab"));
 try{var so=new SerializedObject(root.GetComponentInChildren<WeaponSwitcher>(true));
 foreach(var id in new[]{"Handgun","AutoPistol","Handgun01","Handgun02"}){
 var slot=ReadSlotTemplate(so,id);var w=new SerializedObject(slot.Weapon);log.AppendLine("\n"+id+" root="+slot.Root.name);
 foreach(var key in new[]{"reloadTime","reloadClipSpeedMultiplier","reloadEmptyClipSpeedMultiplier","partsReloadClipSpeedMultiplier","partsReloadEmptyClipSpeedMultiplier","recoilPreset","fireRate","recoilIntensityMultiplier","enforceReloadDuration"}){var prop=w.FindProperty(key);if(prop!=null)log.AppendLine(key+"="+(prop.propertyType==SerializedPropertyType.Float?prop.floatValue.ToString():prop.propertyType==SerializedPropertyType.Boolean?prop.boolValue.ToString():prop.intValue.ToString()));}
 var controller=slot.FPOverride?slot.FPOverride:(RuntimeAnimatorController)so.FindProperty("baseController").objectReferenceValue;
 foreach(var layer in PoseController(controller).layers){foreach(var state in States6(layer.stateMachine))if(layer.name is "Fire" or "Reload"||state.name.Contains("Fire")){log.AppendLine("FP "+layer.name+"/"+state.name+" speed="+state.speed+" weight="+layer.defaultWeight+" motion="+(state.motion?state.motion.name:"EMPTY"));if(state.motion is AnimationClip clip)log.AppendLine("  "+AssetDatabase.GetAssetPath(clip)+" length="+clip.length+" bindings="+AnimationUtility.GetCurveBindings(clip).Length);}}
 var parts=(Animator)w.FindProperty("partsAnimator").objectReferenceValue;
 if(!parts)parts=slot.Root.GetComponent<Animator>();log.AppendLine("Parts="+(parts?parts.name:"NONE")+" emptySync="+slot.Root.GetComponentsInChildren<WeaponEmptySlideSync>(true).Length);
 if(parts&&parts.runtimeAnimatorController)foreach(var st in States6(PoseController(parts.runtimeAnimatorController).layers[0].stateMachine)){log.AppendLine("PART "+st.name+" speed="+st.speed+" motion="+(st.motion?st.motion.name:"EMPTY"));foreach(var t in st.transitions)log.AppendLine(" -> "+t.destinationState?.name+" exit="+t.hasExitTime+":"+t.exitTime+" conditions="+string.Join(",",t.conditions.Select(c=>c.parameter+"="+c.mode)));}
 }
 }finally{PrefabUtility.UnloadPrefabContents(root);}File.WriteAllText(WeaponAssetLocations.Resolve(Path.Combine(Environment.GetEnvironmentVariable("DQ_UI_OUTPUT"),"PistolDiagnostics.txt")),log.ToString());
 }
}
#endif
