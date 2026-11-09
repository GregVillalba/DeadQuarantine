#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DeadQuarantine.PvPvE.Networking;
using UnityEditor;
using UnityEngine;
public static partial class Phase17CombatBuild {
 static string Output=>Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE17_OUTPUT");
 public static void Install(){
  var config=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");config.ProtocolVersion=5017;
  var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");var presentation=player.GetComponent<NetworkWeaponPresentation>();
  var switcher=new SerializedObject(player.GetComponentInChildren<WeaponSwitcher>(true));var slots=switcher.FindProperty("slots");var report=new List<string>();
  foreach(var spec in config.Loadouts.weapons){
   var definition=presentation.Weapons.Single(d=>d.Id==spec.id);var serialized=new SerializedObject(definition.Source);
   float F(string key,float fallback=0)=>serialized.FindProperty(key)?.floatValue??fallback;
   int I(string key,int fallback=0)=>serialized.FindProperty(key)?.intValue??fallback;
   bool B(string key)=>serialized.FindProperty(key)?.boolValue??false;
   spec.damage=I("damage");spec.magazine=I("maxAmmo");spec.range=F("range");spec.pellets=B("firesMultiplePellets")?I("pelletsPerShot",1):1;
   spec.fireInterval=B("usesBurstFire")?F("burstShotInterval",.1f):F("fireRate",.1f);if(B("requiresBoltActionAfterFire"))spec.fireInterval=Mathf.Max(spec.fireInterval,F("boltActionDuration"));
   spec.burstCount=B("usesBurstFire")?I("burstCount",3):1;spec.burstCooldown=F("burstCooldown");spec.falloffStart=F("damageFalloffStart");spec.falloffEnd=F("damageFalloffEnd");spec.minDamageMultiplier=F("minDamageMultiplier",1);spec.maxSpread=F("maxSpread",35)+F("pelletSpreadAngle");spec.shellReload=B("usesMultiPartReload");
   AnimatorOverrideController controller=null;
   for(int i=0;i<slots.arraySize;i++){var slot=slots.GetArrayElementAtIndex(i);if(slot.FindPropertyRelative("weaponId").stringValue==spec.id){controller=slot.FindPropertyRelative("overrideController").objectReferenceValue as AnimatorOverrideController;break;}}
   var clips=controller?controller.animationClips:Array.Empty<AnimationClip>();
   AnimationClip Clip(string part)=>clips.FirstOrDefault(c=>c.name.ToLowerInvariant().Contains("reload")&&c.name.ToLowerInvariant().Contains(part));
   RuntimeAnimatorController runtime=controller?controller:player.GetComponentInChildren<WeaponSwitcher>(true).GetComponent<Animator>().runtimeAnimatorController;
   var reload=PvPvENovemberAdjustments.StateClip(runtime,"reload");var emptyReload=PvPvENovemberAdjustments.StateClip(runtime,"reloadempty");
   float Fill(AnimationClip clip,float speed){if(!clip)return F("reloadTime",1.5f);var events=AnimationUtility.GetAnimationEvents(clip).Where(e=>e.functionName.Contains("AmmunitionFill")).ToArray();return(events.Length>0?events.Min(e=>e.time):clip.length)/Mathf.Max(.01f,speed);}
   spec.reloadFillSeconds=B("enforceReloadDuration")?F("reloadTime",1.5f)*.68f:Fill(reload,F("reloadClipSpeedMultiplier",1)*PvPvENovemberAdjustments.StateSpeed(runtime,"reload"));
   spec.emptyReloadFillSeconds=B("enforceReloadDuration")?F("reloadTime",1.5f)*.68f:Fill(emptyReload,F("reloadEmptyClipSpeedMultiplier",1)*PvPvENovemberAdjustments.StateSpeed(runtime,"reloadempty"));
   spec.reloadSeconds=B("enforceReloadDuration")?F("reloadTime",1.5f):reload?reload.length/Mathf.Max(.01f,F("reloadClipSpeedMultiplier",1)*PvPvENovemberAdjustments.StateSpeed(runtime,"reload")):F("reloadTime",1.5f);
   spec.emptyReloadSeconds=B("enforceReloadDuration")?F("reloadTime",1.5f):emptyReload?emptyReload.length/Mathf.Max(.01f,F("reloadEmptyClipSpeedMultiplier",1)*PvPvENovemberAdjustments.StateSpeed(runtime,"reloadempty")):spec.reloadSeconds;
   spec.shellSeconds=Clip("insert")?Clip("insert").length:.5f;spec.shellOpenSeconds=(Clip("open")?Clip("open").length:.5f)+spec.shellSeconds;
   if(spec.damage<=0||spec.magazine<=0||spec.range<=0||spec.pellets<1||spec.pellets>16||spec.reloadSeconds<=0)throw new Exception("Invalid existing weapon metadata: "+spec.id);
   report.Add(spec.id+" damage="+spec.damage+" pellets="+spec.pellets+" magazine="+spec.magazine+" interval="+spec.fireInterval+" reload="+spec.reloadSeconds+" empty="+spec.emptyReloadSeconds);
  }
  var melee=player.GetComponentInChildren<MeleeAttack>(true);if(melee)config.KillSound=new SerializedObject(melee).FindProperty("impactSound").objectReferenceValue as AudioClip;
  EditorUtility.SetDirty(config.Loadouts);EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();File.WriteAllLines(Path.Combine(Output,"WeaponMetadata.txt"),report);
  const string path="Assets/PvPvE/Prefabs/PvPvETeamRoster.prefab";string text=File.ReadAllText(path);string guid=AssetDatabase.AssetPathToGUID("Assets/Scripts/PvPvE/Networking/PvPvECombat.cs");
  if(!text.Contains("guid: "+guid)){const string id="5170000000000000017";text=text.Replace("  - component: {fileID: 5160000000000000016}","  - component: {fileID: 5160000000000000016}\n  - component: {fileID: "+id+"}");text+="\n--- !u!114 &"+id+"\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 4958710834160195841}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: "+guid+", type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n";File.WriteAllText(path,text);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);}
 }
}
#endif
