#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.Collections;using System.Linq;using UnityEngine;using UnityEngine.InputSystem;using Unity.Netcode.Components;using DeadQuarantine.PvPvE;
namespace DeadQuarantine.Development {public sealed partial class PvPvEWeaponPoseModeDriver {
 IEnumerator ReviewNovemberRuntime(){
  if(Arg("-dq-followup-exit")!=null||Arg("-dq-skip-reloads")=="1")yield break;
  var catalog=service.Settings.Loadouts;var presentation=Local.GetComponent<NetworkWeaponPresentation>();
  foreach(var id in new[]{new PvPvEClassStore(catalog,PoseProfile).Get(0).primary,new PvPvEClassStore(catalog,PoseProfile).Get(0).secondary}){
   var gun=catalog.Find(id);var source=presentation.Weapons.Single(d=>d.Id==id).Source;var attachments=source.GetComponent<WeaponAttachments>();
   if(gun.whiteMagazine){Check(attachments.MagazineIndex>=0&&attachments.MagazineSprite,"PvPvE retains default physical magazine for "+id);var options=(WeaponAttachmentOption[])Field(attachments,"magazines");var model=options[attachments.MagazineIndex].model;if(model)Check(model.activeSelf,"Optional magazine model enabled after class restoration: "+id);}
  }
  yield return new WaitForSecondsRealtime(.5f);var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Reload validation floor";ground.transform.position=new(50000,-.5f,0);ground.transform.localScale=new(200,1,200);var savedPosition=Local.transform.position;var savedRotation=Local.transform.rotation;Keys();Movement.ResetMovementState();Movement.enabled=false;Layout(0);yield return Sync("november-reload-layout");yield return new WaitForSecondsRealtime(.4f);
  var weapon=Switcher.CurrentWeapon;var look=Local.GetComponentInChildren<PlayerLook>(true);bool lookOn=look&&look.enabled;if(look)look.enabled=false;int savedLimit=Combat.Settings.KillLimit;Combat.Settings.KillLimit=50;
  for(int cycle=0;cycle<2;cycle++){
   // Normal reload then empty reload, using only actual Weapon fire/reload methods and clip events.
   foreach(bool empty in new[]{false,true}){
    var camera=weapon.AimCamera;camera.transform.rotation=Quaternion.LookRotation(Vector3.up);int previous=weapon.CurrentAmmo;
    if(empty){float end=Time.realtimeSinceStartup+45;while(weapon.CurrentAmmo>0&&Time.realtimeSinceStartup<end){weapon.SendMessage("TryFire");yield return new WaitForSecondsRealtime(.18f);}Check(weapon.CurrentAmmo==0,"Actual shots empty the magazine before reload");}
    else{weapon.SendMessage("TryFire");yield return Until(()=>weapon.CurrentAmmo<previous,"Actual shot before normal reload");}
    yield return new WaitForSecondsRealtime(.5f);Debug.Log("BEFORE RELOAD actor="+who+" ammo="+weapon.CurrentAmmo+" reserve="+weapon.ReserveAmmo+" gate="+PlayerInputGate.ActionsBlocked+" input="+weapon.InputLocked+" switching="+Switcher.IsSwitching+" sprint="+Movement.IsSprinting+" chamber="+weapon.IsChambering+" reloading="+weapon.IsReloading+" pause="+PauseController.LocalPlayerPaused+" shop="+NPCShopUI.LocalShopOpen+" class="+DeadQuarantine.PvPvE.UI.PvPvEInitialClassView.BlocksLocalGameplay+" place="+ColocadorObjetos.ColocandoLocal+" scoreLocked="+Combat.ScoreLocked.Value+" stage="+Flow.Stage.Value+" movement="+Movement.enabled+" root="+Local.transform.position+" eye="+weapon.AimOrigin);weapon.SendMessage("OnReload",default(InputAction.CallbackContext));Debug.Log("AFTER RELOAD actor="+who+" reloading="+weapon.IsReloading);yield return Until(()=>weapon.IsReloading,"Reload started");yield return Until(()=>!weapon.IsReloading,"Reload animation finished",30);Check(weapon.CurrentAmmo==weapon.CapacidadCargador,"Local magazine refilled after reload: ammo="+weapon.CurrentAmmo+" reserve="+weapon.ReserveAmmo);
    yield return Until(()=>Combat.TryAmmo((ulong)who,Switcher.CurrentWeaponIndex,out var ammo)&&!ammo.Reloading&&ammo.Magazine==weapon.CapacidadCargador,"Server and local magazine agree after "+(empty?"empty":"normal")+" reload, cycle "+cycle,15);
    yield return Sync("november-refill-"+cycle+"-"+empty);
    for(int shooter=0;shooter<3;shooter++){
     int target=shooter==1?0:1;string key="november-damage-"+cycle+"-"+empty+"-"+shooter;
     if(who==0){Actor(target).CurrentHealth.Value=Actor(target).MaxHealth;Actor(target).Armor.Value=Actor(target).MaxArmor;}
     yield return Sync(key+"-health");yield return Until(()=>Actor(target).Armor.Value==Actor(target).MaxArmor,"Fresh armor for nonlethal shot check");yield return new WaitForSecondsRealtime(.25f);long beforeVitals=(long)Actor(target).CurrentHealth.Value+Actor(target).Armor.Value;yield return Sync(key+"-baseline");
     if(who==shooter){int beforeAmmo=weapon.CurrentAmmo;Debug.Log("RELOAD CHECK before fire: ammo="+beforeAmmo+" reserve="+weapon.ReserveAmmo+" next="+Field(weapon,"nextFireTime")+" burst="+Field(weapon,"burstActive")+" input="+weapon.InputLocked+" reload="+weapon.IsReloading+" chamber="+weapon.IsChambering+" gate="+PlayerInputGate.ActionsBlocked+" selected="+UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject+" cursor="+Cursor.lockState+" time="+Time.time);FireAt(Actor(target).transform.position+Vector3.up*.9f);yield return new WaitForSecondsRealtime(.4f);Check(weapon.CurrentAmmo<beforeAmmo,"Local gun emits rounds after reload; before="+beforeAmmo+" after="+weapon.CurrentAmmo+" next="+Field(weapon,"nextFireTime")+" burst="+Field(weapon,"burstActive"));}
     yield return new WaitForSecondsRealtime(.6f);if(who==0){Combat.TryAmmo((ulong)shooter,Actor(shooter).GetComponent<NetworkWeaponPresentation>().EquippedIndex,out var snapshot);Write(key+"-diagnostic",Combat.LastShotDiagnostic+"\naccepted="+Combat.AcceptedShots+" rejected="+Combat.RejectedShots+" ammo="+snapshot.Magazine+" reloading="+snapshot.Reloading+" targetHealth="+Actor(target).CurrentHealth.Value+" targetPosition="+Actor(target).transform.position);}
     yield return Until(()=>((long)Actor(target).CurrentHealth.Value+Actor(target).Armor.Value)<beforeVitals,"Real player damage after "+(empty?"empty":"normal")+" reload by peer "+shooter,15);
     Check(Actor(target).CurrentHealth.Value>0,"Reload damage check does not manufacture a kill");yield return Sync(key);
    }
   }
  }
  Check(Combat.AlphaKills.Value==0&&Combat.BravoKills.Value==0,"Reload fixture grants no unintended kills");Combat.Settings.KillLimit=savedLimit;
  if(who==0)foreach(var player in Players){player.CurrentHealth.Value=player.MaxHealth;player.Armor.Value=0;}
  var controller=Local.GetComponent<CharacterController>();controller.enabled=false;Local.transform.SetPositionAndRotation(savedPosition,savedRotation);Local.GetComponent<NetworkTransform>().Teleport(savedPosition,savedRotation,Local.transform.localScale);controller.enabled=true;Movement.enabled=true;if(look)look.enabled=lookOn;Destroy(ground);yield return Sync("november-reloads-complete");
 }
 IEnumerator ReviewNovemberZombieAwards(){
  var savedPosition=Local.transform.position;var savedRotation=Local.transform.rotation;bool movementOn=Movement.enabled;Movement.enabled=false;Place(who*20,who==0?0:-20);yield return Sync("november-zombie-position");yield return new WaitForSecondsRealtime(.3f);
  double initial=Stats(0).Points,awarded=0;int teamKills=Combat.AlphaKills.Value;
  for(int test=0;test<4;test++){
   bool head=(test&1)!=0,far=(test&2)!=0;string key="november-zombie-"+test;var spec=service.Settings.Loadouts.Find(Actor(0).GetComponent<NetworkWeaponPresentation>().Weapons[Actor(0).GetComponent<NetworkWeaponPresentation>().EquippedIndex].Id);
   if(who==0){var gate=new GameObject("Zombie bonus fixture");gate.SetActive(false);var point=new Vector3(50010+test*4,.08f,far?Combat.Settings.LongshotThreshold(spec)+15:8);var go=Instantiate(Combat.Settings.WalkerPrefab,point,Quaternion.identity,gate.transform);var agent=go.GetComponent<UnityEngine.AI.NavMeshAgent>();if(agent)agent.enabled=false;go.transform.SetParent(null);Destroy(gate);var zombie=go.GetComponent<ZombieHealth>();zombie.NetworkObject.Spawn(true);go.GetComponent<ZombieAI>().enabled=false;foreach(var animator in go.GetComponentsInChildren<Animator>())animator.enabled=false;((System.Collections.Generic.List<ZombieHealth>)Field(PvPvEZombieDirector.Active,"spawned")).Add(zombie);zombie.RecibirDanoServidor(zombie.MaximumHealth-1,point,Vector3.up,0);Write(key,zombie.NetworkObjectId.ToString());Check(Stats(0).Points==initial+awarded,"Nonlethal zombie damage grants no elimination bonus");}
   yield return Until(()=>Has(key),"Zombie bonus fixture spawned");ulong objectId=ulong.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(folder,key)));yield return Until(()=>Network.SpawnManager.SpawnedObjects.ContainsKey(objectId),"Zombie replicated to every client");var target=Network.SpawnManager.SpawnedObjects[objectId].GetComponent<ZombieHealth>();yield return Sync(key+"-ready");
   if(who==0){var headbox=target.GetComponentInChildren<ZombieHeadHitbox>(true);Vector3 point=head&&headbox?headbox.GetComponent<Collider>().bounds.center:target.transform.position+Vector3.up*.8f;FireAt(point);}
   yield return Until(()=>target&&target.IsDead,"Real weapon ray kills zombie bonus case "+test,15);var award=Combat.Settings.EliminationAward(true,head,far);awarded+=award.Total;
   yield return Until(()=>Stats(0).Points==initial+awarded,"Correct synchronized zombie bonus: "+award.Total,10);Check(Combat.AlphaKills.Value==teamKills&&Combat.BravoKills.Value==0,"Zombie bonuses never advance team kills");
   if(who==0){PvPvEZombieDirector.Active.ZombieDied(target,0);Check(Stats(0).Points==initial+awarded,"Repeated zombie death does not duplicate score");var score=Local.GetComponentInChildren<ScoreHUD>(true);yield return Until(()=>score.DisplayedScore==Stats(0).Points.ToString("0.##"),"Zombie bonus reaches original score HUD");yield return new WaitForSecondsRealtime(.3f);var hud=Local.GetComponentInChildren<HUDController>(true);Check(!((GameObject)Field(hud,"headshotSkull")).activeSelf,"Old red skull remains hidden in PvPvE");Check(!((GameObject)Field(hud,"hitMarker")).activeSelf,"Head and body hitmarkers both finish after the normal duration");}
   yield return CaptureInterface("14-zombie-bonus-"+test);yield return Sync(key+"-checked");
  }
  var controller=Local.GetComponent<CharacterController>();bool enabledBefore=controller.enabled;controller.enabled=false;Local.transform.SetPositionAndRotation(savedPosition,savedRotation);Local.GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(savedPosition,savedRotation,Local.transform.localScale);controller.enabled=enabledBefore;Movement.enabled=movementOn;Local.GetComponentInChildren<PlayerLook>(true).enabled=true;yield return Sync("november-zombie-bonuses-done");
 }

}}
#endif
