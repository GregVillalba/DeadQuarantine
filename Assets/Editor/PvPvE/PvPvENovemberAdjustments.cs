#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Text;using UnityEditor;using UnityEngine;using DeadQuarantine.PvPvE;using DeadQuarantine.PvPvE.Networking;
public static class PvPvENovemberAdjustments {
 static string Output=>Environment.GetEnvironmentVariable("DQ_NOVEMBER_OUTPUT");
 public static void PrepareAndBuild(){Prepare();WeaponPoseModeBuild.Build();}
 public static void Prepare(){
 var settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");settings.ProtocolVersion=6026;settings.MaxZombiesAlive=10;settings.HeadshotScoreBonus=.5f;settings.PlayerLongshotScoreBonus=.25f;settings.ZombieLongshotScoreBonus=.2f;
 var wc=settings.Loadouts.wildcards;var greed=wc.Find("wildcard.greed");greed.name="Perk 1 Greed";greed.description="Un segundo perk de Movilidad. Cada perk consume un punto Pick 10.";greed.kind=PvPvEWildcard.Perk1Greed;
 void Add(string id,string name,PvPvEWildcard kind,string description,Sprite icon){if(wc.Find(id)!=null)return;wc.entries=wc.entries.Append(new PvPvEWildcardCatalog.Entry{id=id,name=name,kind=kind,description=description,icon=icon}).ToArray();}
 Add("wildcard.handling_greed","Perk 2 Greed",PvPvEWildcard.Perk2Greed,"Un segundo perk de Manejo. Cada perk consume un punto Pick 10.",greed.icon);
 Add("wildcard.primary_gunfighter","Primary Gunfighter",PvPvEWildcard.PrimaryGunfighter,"Habilita un tercer accesorio en la primaria.",wc.Find("wildcard.overkill").icon);
 Add("wildcard.secondary_gunfighter","Secondary Gunfighter",PvPvEWildcard.SecondaryGunfighter,"Habilita un tercer accesorio en la secundaria.",wc.Find("wildcard.overkill").icon);
 var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");var presentation=player.GetComponent<NetworkWeaponPresentation>();var slots=new SerializedObject(player.GetComponentInChildren<WeaponSwitcher>(true)).FindProperty("slots");var log=new StringBuilder();
 foreach(var d in presentation.Weapons){var spec=settings.Loadouts.Find(d.Id);RuntimeAnimatorController runtime=null;
 for(int i=0;i<slots.arraySize;i++){var slot=slots.GetArrayElementAtIndex(i);if(slot.FindPropertyRelative("weaponId").stringValue==d.Id)runtime=slot.FindPropertyRelative("overrideController").objectReferenceValue as RuntimeAnimatorController;}
 if(runtime==null)runtime=player.GetComponentInChildren<WeaponSwitcher>(true).GetComponent<Animator>().runtimeAnimatorController;
 var w=new SerializedObject(d.Source);float Speed(bool empty)=>Mathf.Max(.01f,w.FindProperty(empty?"reloadEmptyClipSpeedMultiplier":"reloadClipSpeedMultiplier").floatValue);
 float Timing(bool empty){if(w.FindProperty("enforceReloadDuration").boolValue)return w.FindProperty("reloadTime").floatValue*.68f;var clip=StateClip(runtime,empty?"reloadempty":"reload");if(!clip)throw new Exception("Missing exact reload state: "+d.Id+" "+empty+" controller="+(runtime?runtime.name:"NULL"));var events=AnimationUtility.GetAnimationEvents(clip).Where(e=>e.functionName.Contains("AmmunitionFill")).ToArray();float fill=events.Length>0?events.Min(e=>e.time):clip.length;log.AppendLine(d.Id+" "+clip.name+" fill="+fill+" length="+clip.length+" speed="+Speed(empty));return fill/Speed(empty)/StateSpeed(runtime,empty?"reloadempty":"reload");}
 if(!spec.shellReload){float old=spec.reloadFillSeconds>0?spec.reloadFillSeconds:spec.reloadSeconds,oldEmpty=spec.emptyReloadFillSeconds>0?spec.emptyReloadFillSeconds:spec.emptyReloadSeconds;spec.reloadFillSeconds=Timing(false);spec.emptyReloadFillSeconds=Timing(true);spec.reloadSeconds=w.FindProperty("enforceReloadDuration").boolValue?w.FindProperty("reloadTime").floatValue:StateClip(runtime,"reload").length/Speed(false)/StateSpeed(runtime,"reload");spec.emptyReloadSeconds=w.FindProperty("enforceReloadDuration").boolValue?w.FindProperty("reloadTime").floatValue:StateClip(runtime,"reloadempty").length/Speed(true)/StateSpeed(runtime,"reloadempty");log.AppendLine("AUTHORITY WINDOW "+d.Id+" before="+old+"/"+oldEmpty+" now="+spec.reloadFillSeconds+"/"+spec.emptyReloadFillSeconds);if(Mathf.Abs(old-spec.reloadFillSeconds)>.0001f||Mathf.Abs(oldEmpty-spec.emptyReloadFillSeconds)>.0001f)throw new Exception("Unexpected authority timing change: "+d.Id);}
 }
 foreach(var image in player.GetComponentsInChildren<UnityEngine.UI.Image>(true)){if(FullPath(image.transform).Contains("info_general"))log.AppendLine("HUD "+FullPath(image.transform)+" sprite="+(image.sprite?image.sprite.name:"NULL")+" color="+image.color+" material="+(image.material?image.material.name:"NULL"));}
 CalibrateUtilityIcons(settings.Loadouts,log);
 File.WriteAllText(Path.Combine(Output,"Prepared.txt"),log.ToString());EditorUtility.SetDirty(settings);EditorUtility.SetDirty(wc);EditorUtility.SetDirty(settings.Loadouts);AssetDatabase.SaveAssets();
 }
 static void CalibrateUtilityIcons(PvPvELoadoutCatalog catalog,StringBuilder log){
 foreach(var gun in catalog.weapons){if(!gun.model||!gun.whiteBody)continue;var clone=UnityEngine.Object.Instantiate(gun.model);clone.SetActive(false);
 try{var physical=WeaponAttachments.Find(clone.transform,"root");if(!physical)continue;var baseScope=WeaponAttachments.Find(clone.transform,"DQ_DefaultScope");if(baseScope)UnityEngine.Object.DestroyImmediate(baseScope.gameObject);
 var bounds=WeaponAttachments.BoundsIn(clone,physical);var bodyPixels=WeaponIconBounds.Get(gun.whiteBody);float scale=bodyPixels.width/Mathf.Max(.001f,bounds.size.y);
 foreach(var entry in catalog.attachments.entries.Where(e=>e.kind==WeaponAttachmentKind.Utility)){
 var icon=Array.Find(entry.weaponIcons,i=>i.weapon==gun.id);if(icon==null||!icon.icon)continue;var socket=WeaponAttachments.Find(clone.transform,entry.socket);if(!socket)continue;
 var accessory=UnityEngine.Object.Instantiate(entry.model,socket,false);accessory.transform.localPosition=entry.MountPosition(gun.id);accessory.transform.localRotation=Quaternion.identity;accessory.transform.localScale=Vector3.one;
 var local=WeaponAttachments.BoundsIn(accessory,physical);var source=WeaponIconBounds.Get(icon.icon);
 var desired=new Vector2(bodyPixels.xMin+(bounds.max.y-local.center.y)*scale,bodyPixels.yMax-(bounds.max.z-local.center.z)*scale);
 icon.offset=desired-source.center;log.AppendLine("UTILITY "+gun.id+"/"+entry.id+" offset="+icon.offset+" body="+bounds+" point="+local.center);UnityEngine.Object.DestroyImmediate(accessory);
 }
 }finally{UnityEngine.Object.DestroyImmediate(clone);}}
 EditorUtility.SetDirty(catalog.attachments);
 }
 static string FullPath(Transform t)=>t.parent?FullPath(t.parent)+"/"+t.name:t.name;
 public static AnimationClip StateClip(RuntimeAnimatorController runtime,string wanted){
 var overrides=runtime as AnimatorOverrideController;var core=overrides?overrides.runtimeAnimatorController:runtime;while(core is AnimatorOverrideController nested)core=nested.runtimeAnimatorController;
 var controller=core as UnityEditor.Animations.AnimatorController;if(!controller)return null;
 foreach(var layer in controller.layers){var clip=FindState(layer.stateMachine,wanted);if(clip){if(overrides){var replaced=overrides[clip];if(replaced)clip=replaced;}return clip;}}
 return null;
 }
 public static float StateSpeed(RuntimeAnimatorController runtime,string wanted){var core=runtime;while(core is AnimatorOverrideController wrapper)core=wrapper.runtimeAnimatorController;var controller=core as UnityEditor.Animations.AnimatorController;if(!controller)return 1;foreach(var layer in controller.layers){float speed=FindSpeed(layer.stateMachine,wanted);if(speed>0)return speed;}return 1;}
 static float FindSpeed(UnityEditor.Animations.AnimatorStateMachine machine,string wanted){foreach(var state in machine.states)if(state.state.name.Replace("_","").Replace(" ","").ToLowerInvariant()==wanted)return Mathf.Max(.01f,state.state.speed);foreach(var child in machine.stateMachines){float value=FindSpeed(child.stateMachine,wanted);if(value>0)return value;}return 0;}
 static AnimationClip FindState(UnityEditor.Animations.AnimatorStateMachine machine,string wanted){foreach(var state in machine.states)if(state.state.name.Replace("_","").Replace(" ","").ToLowerInvariant()==wanted&&state.state.motion is AnimationClip clip)return clip;foreach(var child in machine.stateMachines){var clip=FindState(child.stateMachine,wanted);if(clip)return clip;}return null;}
 public static void Diagnose(){
 var log=new StringBuilder();var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");var network=player.GetComponent<NetworkWeaponPresentation>();var slots=new SerializedObject(player.GetComponentInChildren<WeaponSwitcher>(true)).FindProperty("slots");
 var settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");
 foreach(var d in network.Weapons){var spec=settings.Loadouts.Find(d.Id);log.AppendLine(d.Id+" serverReload="+spec.reloadSeconds+"/"+spec.emptyReloadSeconds);var w=new SerializedObject(d.Source);foreach(var key in new[]{"reloadTime","enforceReloadDuration","reloadClipSpeedMultiplier","reloadEmptyClipSpeedMultiplier"})log.AppendLine(key+"="+w.FindProperty(key));
 for(int i=0;i<slots.arraySize;i++){var slot=slots.GetArrayElementAtIndex(i);if(slot.FindPropertyRelative("weaponId").stringValue!=d.Id)continue;var c=slot.FindPropertyRelative("overrideController").objectReferenceValue as RuntimeAnimatorController;if(!c)continue;foreach(var clip in c.animationClips.Distinct().Where(c=>c.name.ToLower().Contains("reload"))){log.AppendLine(clip.name+" length="+clip.length);foreach(var e in AnimationUtility.GetAnimationEvents(clip))log.AppendLine(" event "+e.time+" "+e.functionName);}}
 }
 File.WriteAllText(Path.Combine(Output,"ReloadDiagnostic.txt"),log.ToString());
 }
}
#endif
