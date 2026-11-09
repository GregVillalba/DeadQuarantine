#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.Collections;using System.Linq;using UnityEngine;using UnityEngine.InputSystem;using DeadQuarantine.PvPvE;using DeadQuarantine.PvPvE.Networking;
namespace DeadQuarantine.Development {public sealed partial class PvPvEWeaponPoseModeDriver {
 IEnumerator ReviewCaptureCorrections(){
  var light=Local.GetComponent<Flashlight>();Check(light,"Existing flashlight component reused");
  Check(!light.FlashlightAvailable&&!light.LaserAvailable,"No utility without class accessory");
  Keys(Key.F);yield return new WaitForSecondsRealtime(.15f);Keys();Check(!light.UtilityEnabled&&!light.BeamVisible,"F cannot enable an unequipped utility");
  yield return Sync("utility-none");
  var baseline=new System.Collections.Generic.Dictionary<ulong,WeaponAttachmentChoice>();
  if(who==0)foreach(var p in Players)baseline[p.OwnerClientId]=p.GetComponent<NetworkWeaponAttachments>().GetChoice(p.GetComponent<NetworkWeaponPresentation>().EquippedIndex);
  foreach(string utility in new[]{"Laser_Sight_01","Flashlight_01"}){
   if(who==0)foreach(var p in Players){var c=service.Settings.Loadouts.Default(0);var np=p.GetComponent<NetworkWeaponPresentation>();c.primary=np.Weapons[np.EquippedIndex].Id;c.secondary="Handgun";c.primaryAttachments=new WeaponAttachmentChoice{utility=utility};c.secondaryAttachments=default;c.perk1=c.perk2=c.wildcard1=c.wildcard2="";Check(p.GetComponent<NetworkWeaponAttachments>().ApplyClassServer(c),"Authority equips "+utility);}
   var a=Switcher.CurrentWeapon.GetComponent<WeaponAttachments>();yield return Until(()=>a.ApprovedChoice.utility==utility,"Utility selection synchronized");
   bool flashlightOnly=utility=="Flashlight_01";Check(light.FlashlightAvailable==flashlightOnly&&light.LaserAvailable!=flashlightOnly,"Flashlight and laser availability match actual accessory");
   Keys(Key.F);yield return new WaitForSecondsRealtime(.2f);Keys();yield return Until(()=>light.UtilityEnabled,"F enables equipped utility");
   Check(light.BeamVisible==flashlightOnly,"Only flashlight accessories emit the original beam");
   Check(a.GetComponentsInChildren<LineRenderer>().Any(l=>l.enabled&&l.gameObject.activeInHierarchy)!=flashlightOnly,"Only laser accessories emit a laser");
   Keys(Key.F);yield return new WaitForSecondsRealtime(.15f);Keys();yield return Until(()=>!light.UtilityEnabled,"F disables utility");yield return Sync("reviewed-"+utility);
  }
  var restore=new System.Collections.Generic.Dictionary<ulong,PvPvEClassDefinition>();
  if(who==0)foreach(var player in Players){var c=service.Settings.Loadouts.Default(0);var np=player.GetComponent<NetworkWeaponPresentation>();
   c.primary=np.Weapons[np.EquippedIndex].Id;c.secondary="Handgun";c.perk1=c.perk2=c.wildcard1=c.wildcard2="";c.primaryAttachments=new WeaponAttachmentChoice{scope="Scope_05",utility="Laser_Flashlight_01"};c.secondaryAttachments=default;
   var old=c.Copy();old.primaryAttachments=baseline[player.OwnerClientId];if(PvPvELoadoutCatalog.AttachmentCount(old.primaryAttachments)>2)old.wildcard1="wildcard.primary_gunfighter";restore[player.OwnerClientId]=old;
   Check(player.GetComponent<NetworkWeaponAttachments>().ApplyClassServer(c),"Authority accepts scope/utility fixture");
  }
  var attachment=Switcher.CurrentWeapon.GetComponent<WeaponAttachments>();
  yield return Until(()=>attachment.ApprovedChoice.scope=="Scope_05"&&light.FlashlightAvailable&&light.LaserAvailable,"Approved utility reaches each player");
  Keys(Key.F);yield return new WaitForSecondsRealtime(.2f);Keys();yield return Until(()=>light.UtilityEnabled&&light.BeamVisible,"F enables original beam with equipped accessory");
  Check(!Local.GetComponentsInChildren<Light>(true).Any(l=>l.name=="Attachment beam"),"No duplicate attachment lights");
  yield return Sync("utility-on");yield return new WaitForSecondsRealtime(.3f);
  foreach(var player in Players){Check(player.GetComponent<Flashlight>().UtilityEnabled,"Utility toggle synchronized for "+player.OwnerClientId);Check(player.GetComponent<Flashlight>().BeamVisible,"Original remote/local beam visible for "+player.OwnerClientId);}
  var savedPosition=Local.transform.position;var savedRotation=Local.transform.rotation;
  var controller=Local.GetComponent<CharacterController>();bool controllerOn=controller.enabled;var movement=Local.GetComponent<PlayerMovement>();bool movementOn=movement.enabled;Position();
  var gun=Switcher.CurrentWeapon;gun.SendMessage("OnAimStarted",default(UnityEngine.InputSystem.InputAction.CallbackContext));
  yield return Until(()=>attachment.ScopeRendering,"Actual scope camera renders only for local ADS");
  var target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.name="Scope validation target";target.transform.position=gun.AimCamera.transform.position+gun.AimCamera.transform.forward*8;target.transform.localScale=Vector3.one*.4f;
  var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",Color.red);target.GetComponent<Renderer>().material=material;
  yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();
  var rt=attachment.ScopeTexture;Check(rt&&rt.IsCreated(),"Scope owns a live render texture");var prior=RenderTexture.active;var sample=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
  try{RenderTexture.active=rt;sample.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);sample.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,role+"-scope-lens.png"),sample.EncodeToPNG());
   Debug.Log("SCOPE camera mask="+gun.AimCamera.cullingMask+" origin="+gun.AimCamera.transform.position+" texture="+sample.GetPixel(256,256));Check(sample.GetPixels32().Count(p=>p.r>100&&p.r>p.g*2)>200,"Scope lens contains the world target, not only a FOV change");}finally{RenderTexture.active=prior;Destroy(sample);}
  yield return CaptureInterface("14-real-scope");Destroy(target);Destroy(material);gun.SendMessage("OnAimCanceled",default(UnityEngine.InputSystem.InputAction.CallbackContext));yield return Until(()=>!attachment.ScopeRendering,"Scope camera stops outside ADS");
  controller.enabled=false;Local.transform.SetPositionAndRotation(savedPosition,savedRotation);Local.GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(savedPosition,savedRotation,Local.transform.localScale);controller.enabled=controllerOn;movement.enabled=movementOn;
  Keys(Key.F);yield return new WaitForSecondsRealtime(.15f);Keys();yield return Sync("scope-complete");
  if(who==0)foreach(var player in Players)player.GetComponent<NetworkWeaponAttachments>().ApplyClassServer(restore[player.OwnerClientId]);
  yield return Until(()=>!light.FlashlightAvailable,"Removing accessory removes original beam");
 }
 IEnumerator VerifyExistingCharacterPreview(GameObject popup){
  var screen=popup.GetComponentInChildren<CharacterSelectScreen>(true);var stage=(CharacterPreviewStage)Field(screen,"stage");
  yield return new WaitForEndOfFrame();var rt=(RenderTexture)Field(stage,"texture");var prior=RenderTexture.active;var sample=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
  try{RenderTexture.active=rt;sample.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);sample.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,role+"-character-preview.png"),sample.EncodeToPNG());var pixels=sample.GetPixels32();Check(pixels.Count(c=>c.a>25&&(c.r>25||c.g>25||c.b>25))>500,"Reused character preview renders the actual character");Check(sample.GetPixel(0,0).a<.05f,"Character preview background preserves transparency");}finally{RenderTexture.active=prior;Destroy(sample);}
 }

 IEnumerator ReviewVictoryShot(){
  double beforePoints=Stats(0).Points;
  Local.GetComponent<PlayerMovement>().enabled=false;Layout(0);yield return Sync("actual-pvp-shot-position");yield return new WaitForSecondsRealtime(.4f);
  if(who==0){var target=Actor(1);FireAt(new Vector3(target.transform.position.x,target.GetComponent<PvPvEHurtbox>().HeadThreshold+.1f,target.transform.position.z));}
  yield return Until(()=>Combat.ScoreLocked.Value&&Hud.ScoreboardVisible,"Real network weapon shot resolves victory and results");
  yield return Until(()=>Combat.KillFeed.Count>0,"Authoritative shot produces a killfeed event");
  var entry=Combat.KillFeed[Combat.KillFeed.Count-1];Check(entry.Kind==PvPvEDamageKind.Bullet&&entry.Headshot,"Server raycast confirms the headshot");
  Check(Stats(0).Points==beforePoints+Combat.Settings.EliminationAward(false,true,false).Total,"PvP awards configured individual points after actual zombie kills");
  var score=Local.GetComponentInChildren<ScoreHUD>(true);yield return Until(()=>score.DisplayedScore==Stats(who).Points.ToString(),"PvP score reaches original HUD on every peer");
  yield return Until(()=>Hud.LocalSummary.Contains("PUNTOS "+Stats(who).Points),"TAB/results reflects the same authoritative score");
  Check(Enumerable.Range(0,Combat.KillFeed.Count).Select(i=>Combat.KillFeed[i].Sequence).Distinct().Count()==Combat.KillFeed.Count,"Killfeed does not duplicate death sequences");
  var weapon=Hud.CanvasRoot.transform.Find("KillFeed/Death 0/WeaponIcon");var head=Hud.CanvasRoot.transform.Find("KillFeed/Death 0/Headshot").GetComponent<UnityEngine.UI.Image>();
  yield return Until(()=>weapon.gameObject.activeInHierarchy&&head.gameObject.activeInHierarchy,"Weapon icon and confirmed-headshot icon displayed");
  Check(head.sprite==Resources.Load<Sprite>("PvPvE/Headshot"),"Killfeed uses supplied headshot artwork");
  var board=Hud.CanvasRoot.transform.Find("Scoreboard").gameObject;var alpha=board.GetComponent<CanvasGroup>()??board.AddComponent<CanvasGroup>();alpha.alpha=0;
  yield return CaptureInterface("16-confirmed-headshot-feed");alpha.alpha=1;
 }

 IEnumerator ReviewExistingMelee(){
  var savedPosition=Local.transform.position;var savedRotation=Local.transform.rotation;
  var controller=Local.GetComponent<CharacterController>();bool controllerOn=controller.enabled;var movement=Local.GetComponent<PlayerMovement>();bool movementOn=movement.enabled;
  movement.enabled=false;Layout(1);yield return Sync("knife-friendly-position");yield return new WaitForSecondsRealtime(.3f);
  int friendlyBefore=Actor(2).CurrentHealth.Value;
  if(who==0){var gun=Switcher.CurrentWeapon;gun.AimCamera.transform.rotation=Quaternion.LookRotation(Actor(2).transform.position+Vector3.up-gun.AimCamera.transform.position);Keys(Key.V);}
  yield return new WaitForSecondsRealtime(.3f);if(who==0){var melee=Local.GetComponentInChildren<MeleeAttack>(true);Check((bool)Field(melee,"isAttacking"),"Existing V input starts the knife animation");yield return Until(()=>(bool)Field(melee,"hitDelivered"),"Existing animation event delivers melee hit",5);yield return CaptureInterface("14-knife");Keys();}
  yield return new WaitForSecondsRealtime(.6f);Check(Actor(2).CurrentHealth.Value==friendlyBefore,"Existing knife does not damage an ally");yield return Sync("knife-friendly-done");
  Layout(0);yield return Sync("knife-enemy-position");yield return new WaitForSecondsRealtime(.3f);int enemyBefore=Actor(1).CurrentHealth.Value;
  if(who==0){var gun=Switcher.CurrentWeapon;gun.AimCamera.transform.rotation=Quaternion.LookRotation(Actor(1).transform.position+Vector3.up-gun.AimCamera.transform.position);Keys(Key.V);}
  yield return new WaitForSecondsRealtime(.3f);if(who==0){Keys();Keys(Key.V);}yield return new WaitForSecondsRealtime(.12f);if(who==0)Keys();
  yield return Until(()=>Actor(1).CurrentHealth.Value<enemyBefore,"Knife damage reaches all peers");
  Check(Actor(1).CurrentHealth.Value==enemyBefore-Actor(0).GetComponentInChildren<MeleeAttack>(true).PvPvEDamage,"Knife hits once despite two arm events and repeated input");
  yield return new WaitForSecondsRealtime(.5f);yield return Sync("knife-enemy-done");
  controller.enabled=false;Local.transform.SetPositionAndRotation(savedPosition,savedRotation);Local.GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(savedPosition,savedRotation,Local.transform.localScale);controller.enabled=controllerOn;movement.enabled=movementOn;
 }

 void KillRealZombieForScore(ulong owner){
  var director=PvPvEZombieDirector.Active;var prefab=Combat.Settings.WalkerPrefab;
  var gate=new GameObject("Zombie validation gate");gate.SetActive(false);var go=Instantiate(prefab,new Vector3(50020,0,0),Quaternion.identity,gate.transform);var agent=go.GetComponent<UnityEngine.AI.NavMeshAgent>();if(agent)agent.enabled=false;go.transform.SetParent(null);Destroy(gate);
  var zombie=go.GetComponent<ZombieHealth>();zombie.NetworkObject.Spawn(true);
  ((System.Collections.Generic.List<ZombieHealth>)Field(director,"spawned")).Add(zombie);
  zombie.RecibirDisparoPvPvEServidor(999999,go.transform.position,Vector3.up,owner,false,-1);
  Check(zombie.IsDead,"Existing zombie damage/death resolver executed");
 }
}}
#endif
