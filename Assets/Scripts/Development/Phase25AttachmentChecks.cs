#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DeadQuarantine.Development { public sealed partial class Phase6WeaponsSmokeDriver {
  IEnumerator Attachments25(){
   Local.ShowLocalBodyForTesting=false;
   var network=Local.GetComponent<NetworkWeaponAttachments>();var other=Remote.GetComponent<NetworkWeaponAttachments>();
   var catalog=network.catalog;
   var restartChoice=new WeaponAttachmentChoice{scope="Scope_02",muzzle="Muzzle_Silencer_01"};
   int restartIndex=Array.FindIndex(Local.Weapons,d=>d.Id=="AutoPistol");
   if(Arg("-dq-attachments-restore")=="1"){
    Check(NetworkWeaponAttachments.TryLoad("AutoPistol",out var saved)&&saved.Equals(restartChoice),"Fresh process reads non-default saved accessory IDs");
    yield return Until(()=>network.GetChoice(restartIndex).Equals(restartChoice),"Saved selection approved again after a process restart");
    Local.Switcher.UnlockWeapon("AutoPistol");yield return Until(()=>Local.Switcher.CurrentWeaponIndex==restartIndex&&!Local.Switcher.IsSwitching,"Restored weapon equip");
    yield return Barrier("25a-restore-equipped");
    yield return Until(()=>Remote.VisibleWeapon&&Remote.VisibleWeapon.GetComponent<WeaponAttachments>()&&Remote.VisibleWeapon.GetComponent<WeaponAttachments>().ApprovedChoice.Equals(restartChoice),"Restored selection visible to the other process");
    Check(Local.Switcher.CurrentWeapon.ShotIsSuppressed&&Mathf.Abs(Local.Switcher.CurrentWeapon.AttachmentDamageMultiplier-.9f)<.001f,"Restored selection applies real effects");
    CaptureRemote("restored-attachments");yield return Barrier("25a-restore-complete");Complete(true,"");yield break;
   }
   Check(catalog.entries.Length==17&&catalog.mountingHardware.Length==3,"Original attachments, independent flashlight and three mounting rails inventoried");
   foreach(var id in new[]{"Handgun","Rifle","Escopeta"})Check(catalog.Eligible(id)&&catalog.entries.Any(e=>e.weapons.Contains(id)),id+" original weapon now has compatible authored accessories");
   var bad=new WeaponAttachmentChoice{support="Bipod_01"};
   Check(!catalog.Validate("AutoPistol",bad)&&!catalog.Validate("SemiRifle",new WeaponAttachmentChoice{muzzle="missing"}),"Compatibility rejects pistol bipods and unknown IDs");
   // Exercise the actual server RPC wrapper, bypassing the public local validation deliberately.
   network.SendMessage("RequestServerRpc",new NetworkWeaponAttachments.Row(0,bad));
   network.SendMessage("RequestServerRpc",new NetworkWeaponAttachments.Row(4,new WeaponAttachmentChoice{muzzle="missing"}));
   yield return Barrier("25a-invalid-sent");yield return new WaitForSeconds(.3f);
   if(Network.IsServer)Check(network.RejectedRequests>=2&&other.RejectedRequests>=2,"Authority rejects forged requests from Host and Client");
   foreach(string id in Local.Weapons.Select(d=>d.Id)){
    int index=Array.FindIndex(Local.Weapons,d=>d.Id==id);Local.Switcher.UnlockWeapon(id);
    yield return Until(()=>Local.Switcher.CurrentWeaponIndex==index&&!Local.Switcher.IsSwitching,id+" equip");
    var weapon=Local.Switcher.CurrentWeapon;weapon.InputLocked=false;yield return new WaitForSeconds(.35f);
    yield return Barrier("25a-"+id+"-equipped");yield return Until(()=>Remote.EquippedIndex==index&&Remote.VisibleWeapon,id+" remote equip");
    var attachments=weapon.GetComponent<WeaponAttachments>();
    foreach(var entry in catalog.entries.Where(e=>Array.IndexOf(e.weapons,id)>=0)){
     var selection=default(WeaponAttachmentChoice);selection.Set(entry.kind,entry.id);
     yield return Select8(network,index,id,selection);
     var model=WeaponAttachments.Find(weapon.transform,"DQ_ATT_"+entry.id);Check(model,id+" "+entry.id+" real FP model mounted");
     var remoteAttachments=Remote.VisibleWeapon.GetComponent<WeaponAttachments>();
     Check(remoteAttachments&&WeaponAttachments.Find(remoteAttachments.transform,"DQ_ATT_"+entry.id),id+" "+entry.id+" real TP model synchronized");
     var ownThirdPerson=WeaponAttachments.Find(Local.VisibleWeapon.transform,"DQ_ATT_"+entry.id);
     Check(ownThirdPerson&&ownThirdPerson.GetComponentsInChildren<Renderer>(true).All(r=>r.forceRenderingOff),id+" "+entry.id+" own TP accessory stays hidden with the local body");
     if(id=="AutoPistol"&&entry.id=="Scope_01"){
      Local.ShowLocalBodyForTesting=true;yield return new WaitForSeconds(.1f);
      Check(ownThirdPerson.GetComponentsInChildren<Renderer>(true).All(r=>!r.forceRenderingOff),"Inspector body testing option reveals own TP accessories");
      Local.ShowLocalBodyForTesting=false;yield return new WaitForSeconds(.1f);
     }
     var socket=WeaponAttachments.Find(weapon.transform,entry.socket);var bounds=WeaponAttachments.BoundsIn(model.gameObject,socket);
     Check(bounds.size.magnitude<3&&Mathf.Sqrt(bounds.SqrDistance(Vector3.zero))<.4f,id+" "+entry.id+" attachment scale and mount bounds valid");
     if(entry.kind==WeaponAttachmentKind.Scope){
      weapon.SendMessage("OnAimStarted",default(InputAction.CallbackContext));yield return new WaitForSeconds(.7f);yield return new WaitForEndOfFrame();
      var optic=(Transform)Field7(weapon,"adsSightReference");var wc=(Camera)Field7(weapon,"weaponCamera");var vp=wc.WorldToViewportPoint(optic.position);Note7(vp.z>0&&Vector2.Distance(new Vector2(vp.x,vp.y),Vector2.one*.5f)<.03f,id+" "+entry.id+" physical optic calibrated without changing animation ("+vp+")");
      if(new[]{"Handgun","Rifle","Escopeta"}.Contains(id)&&entry.id=="Scope_01")Capture(role+"-"+id+"-scope-aim.png");var camera=(Camera)Field7(weapon,"playerCamera");Check(Mathf.Abs(camera.fieldOfView-entry.aimFov)<.3f,id+" "+entry.id+" changes actual aim FOV");
      weapon.SendMessage("OnAimCanceled",default(InputAction.CallbackContext));yield return new WaitForSeconds(.25f);
     }
     if(entry.suppressed)Check(Mathf.Abs(weapon.AttachmentDamageMultiplier-.9f)<.001f&&weapon.PresentationShotSound==entry.shotSound,id+" "+entry.id+" damage and authored suppressed audio applied");
     if(entry.laser){
      Check(model.GetComponent<LineRenderer>()&&weapon.AttachmentSpreadMultiplier<1,id+" "+entry.id+" real laser and hip precision active");
      yield return new WaitForSeconds(.1f);
      var emitter=WeaponAttachments.Find(model,"DQ_LaserEmitter");var beam=model.GetComponent<LineRenderer>();
      Check(emitter&&Vector3.Distance(beam.GetPosition(0),emitter.position)<.025f,id+" laser beam originates at its mounted device after the weapon pose updates");
     }
     if(entry.flashlight)Check(model.GetComponentInChildren<Light>(),id+" utility flashlight emits real light");
     if(entry.id=="Grip_01")Check(Mathf.Abs(weapon.AttachmentRecoilMultiplier-.8f)<.001f,id+" grip modifies actual recoil input");
     if(entry.requiresBracing){
      Check(Mathf.Abs(weapon.AttachmentRecoilMultiplier-1)<.001f,id+" bipod inactive while standing");
      var movement=Local.GetComponent<PlayerMovement>();movement.MovementLocked=false;movement.enabled=true;
      ((PlayerControls)Field7(movement,"controls")).asset.RemoveAllBindingOverrides();
      InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
      Keys7(Key.LeftCtrl);yield return new WaitForSeconds(.6f);
      Check(attachments.IsBraced&&Mathf.Abs(weapon.AttachmentRecoilMultiplier-.65f)<.001f,id+" bipod stabilizes while grounded crouched and stationary (crouch="+movement.IsCrouching+", grounded="+Local.GetComponent<CharacterController>().isGrounded+")");
      Keys7();yield return new WaitForSeconds(.3f);
      if(movement.IsCrouching){Keys7(Key.LeftCtrl);yield return new WaitForSeconds(.15f);Keys7();yield return new WaitForSeconds(.3f);}movement.enabled=false;
     }
     yield return Barrier("25a-"+id+"-"+entry.id+"-observed");
    }
    var combined=default(WeaponAttachmentChoice);
    foreach(WeaponAttachmentKind kind in Enum.GetValues(typeof(WeaponAttachmentKind))){var e=catalog.entries.FirstOrDefault(e=>e.kind==kind&&Array.IndexOf(e.weapons,id)>=0);if(e!=null)combined.Set(kind,e.id);}
    yield return Select8(network,index,id,combined);Capture(role+"-"+id+"-attachments.png");CaptureRemote(id+"-attachments");
    string json=NetworkWeaponAttachments.Serialize(id,combined);
    Check(NetworkWeaponAttachments.Deserialize(json,id,out var restored)&&restored.Equals(combined)&&NetworkWeaponAttachments.TryLoad(id,out var saved)&&saved.Equals(combined),id+" stable IDs persist and round-trip for future class storage");
    Check(!NetworkWeaponAttachments.Deserialize(json,"other",out _),id+" persistence rejects mismatched weapon");
    int shots=Remote.RemoteShotCount,flashes=Remote.RemoteFlashEvents,sounds=Remote.RemoteSoundEvents;int ammo=weapon.CurrentAmmo;
    yield return Barrier("25a-"+id+"-shot");weapon.SendMessage("TryFire");
    yield return Until(()=>weapon.CurrentAmmo<ammo&&Remote.RemoteShotCount>shots,id+" suppressed remote shot");
    Check(Remote.RemoteFlashEvents==flashes&&Remote.RemoteSoundEvents>sounds&&weapon.ShotIsSuppressed,id+" suppressed shots synchronize sound and suppress muzzle flash");
    yield return Until(()=>!weapon.IsChambering,id+" bolt completes",5);
    yield return new WaitForSeconds(.5f);
    weapon.SendMessage("OnReload",default(InputAction.CallbackContext));Check(weapon.IsReloading,id+" attachment loadout preserves reload");
    int reserve=weapon.ReserveAmmo,spent=weapon.UsesLimitedReserve?weapon.CapacidadCargador-weapon.CurrentAmmo:0;
    yield return Until(()=>!weapon.IsReloading,id+" attached reload finishes",15);
    Check(weapon.CurrentAmmo==weapon.CapacidadCargador&&weapon.ReserveAmmo==reserve-spent,id+" attachments preserve capacity and ammunition conservation (mag="+weapon.CurrentAmmo+", capacity="+weapon.CapacidadCargador+", reserve="+weapon.ReserveAmmo+", expected="+(reserve-spent)+")");
    yield return Select8(network,index,id,default);
    Check(!weapon.ShotIsSuppressed&&Mathf.Abs(weapon.AttachmentDamageMultiplier-1)<.001f&&Mathf.Abs(weapon.AttachmentRecoilMultiplier-1)<.001f,id+" removing attachments restores baseline without drift");
    yield return Barrier("25a-"+id+"-done");
   }
   Check(network.Request("AutoPistol",restartChoice),"Save a non-default choice for the separate restart test");
   yield return Until(()=>network.GetChoice(restartIndex).Equals(restartChoice),"Restart fixture choice approved");
   Check(NetworkWeaponAttachments.TryLoad("AutoPistol",out var finalSaved)&&finalSaved.Equals(restartChoice),"Non-default restart fixture flushed to storage");
   yield return Barrier("25a-complete");Complete(phase7Issues.Count==0,string.Join("\n",phase7Issues));
  }

}}
#endif
