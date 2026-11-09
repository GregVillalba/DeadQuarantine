#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.Collections;using System.Linq;using UnityEngine;using UnityEngine.InputSystem;
namespace DeadQuarantine.Development {public sealed partial class Phase6WeaponsSmokeDriver {
 IEnumerator VerifyOpticPresentation(){
 var nwa=Local.GetComponent<NetworkWeaponAttachments>();
 foreach(var id in new[]{"Sniper01","Sniper02","Sniper03"}){
  int index=Array.FindIndex(Local.Weapons,d=>d.Id==id);Local.Switcher.UnlockWeapon(id);yield return Until(()=>Local.Switcher.CurrentWeaponIndex==index&&!Local.Switcher.IsSwitching,id+" equipped");var gun=Local.Switcher.CurrentWeapon;gun.InputLocked=false;var world=(Camera)Field7(gun,"playerCamera");float bare=0;
  foreach(var optic in new[]{"","Scope_01","Scope_05","Scope_06","Scope_07"}){
   yield return Select8(nwa,index,id,new WeaponAttachmentChoice{scope=optic});gun.SendMessage("OnAimStarted",default(UnityEngine.InputSystem.InputAction.CallbackContext));yield return new WaitForSeconds(1);yield return new WaitForEndOfFrame();var camera=(Camera)Field7(gun,"weaponCamera");var marker=(Transform)Field7(gun,"adsSightReference");var point=camera.WorldToViewportPoint(marker.position);Check(point.z>0&&Vector2.Distance(new Vector2(point.x,point.y),Vector2.one*.5f)<.025f,id+"/"+optic+" optic is centred: "+point);Check(Mathf.Abs(world.fieldOfView-gun.AttachmentAimFov)<1,id+"/"+optic+" real zoom follows selected optic: "+world.fieldOfView);if(optic=="")bare=world.fieldOfView;else if(optic=="Scope_07")Check(world.fieldOfView<bare,id+" long optic magnifies more than irons");Capture(role+"-"+id+"-"+(optic==""?"iron":optic)+"-ADS.png");Check(Remote.VisibleWeapon.GetComponent<WeaponAttachments>().ApprovedChoice.scope==optic,id+" remote optic matches authority");gun.SendMessage("OnAimCanceled",default(UnityEngine.InputSystem.InputAction.CallbackContext));yield return new WaitForSeconds(.5f);
  }yield return Barrier(id+"-optics-done");
 }Complete(true,"");
 }
 IEnumerator VerifyPistolPresentation(){
 foreach(var id in new[]{"Handgun","AutoPistol","Handgun01","Handgun02"}){
  int index=Array.FindIndex(Local.Weapons,d=>d.Id==id);Local.Switcher.UnlockWeapon(id);yield return Until(()=>Local.Switcher.CurrentWeaponIndex==index&&!Local.Switcher.IsSwitching,id+" equipped");var gun=Local.Switcher.CurrentWeapon;gun.InputLocked=false;yield return Barrier(id+"-ready");yield return Until(()=>Remote.EquippedIndex==index&&Remote.VisibleWeapon,id+" replicated");
  gun.RestaurarMunicion(1,100);yield return new WaitForSeconds(.25f);var slide=WeaponAttachments.Find(gun.transform,"slide");var closed=slide.localPosition;var wrist=BonePose(Local.Switcher.GetComponent<Animator>(),"hand_r",HumanBodyBones.RightHand);var start=wrist.rotation;float kick=0;gun.SendMessage("TryFire");yield return Until(()=>gun.CurrentAmmo==0,id+" last shot consumes one round");
  for(int i=0;i<12;i++){yield return new WaitForSeconds(.025f);kick=Mathf.Max(kick,Quaternion.Angle(start,wrist.rotation));if(i==2||i==6)Capture(role+"-"+id+"-fire-"+i+".png");}
  Check(kick>1,id+" visible authored kick: "+kick);yield return new WaitForSeconds(.8f);yield return new WaitForEndOfFrame();Capture(role+"-"+id+"-empty.png");
  if(id!="Handgun"){var parts=gun.GetComponent<Animator>();Check(parts.GetCurrentAnimatorStateInfo(0).IsName("Empty"),id+" empty slide state held");Check(Vector3.Distance(closed,slide.localPosition)>.003f,id+" slide actually moves back");var remoteSlide=WeaponAttachments.Find(Remote.VisibleWeapon.transform,"slide");Check(Remote.VisibleWeapon.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Empty"),id+" remote holds empty slide");CaptureRemote(id+"-empty");}
  yield return Barrier(id+"-empty");float began=Time.time;gun.SendMessage("OnReload",default(InputAction.CallbackContext));yield return Until(()=>gun.IsReloading,id+" reload starts");yield return Until(()=>!gun.IsReloading,id+" reload ends",8);float elapsed=Time.time-began;Check(gun.CurrentAmmo==gun.CapacidadCargador,id+" reload restores ammo");if(id!="Handgun")Check(elapsed*gun.PresentationCharacterReloadSpeed>2.4f&&elapsed*gun.PresentationCharacterReloadSpeed<3.4f,id+" full authored empty reload duration: "+elapsed);yield return new WaitForSeconds(.2f);CheckColdPose(Local.Switcher.GetComponent<Animator>(),id,"after empty reload");
  if(id!="Handgun")Check(Vector3.Distance(closed,slide.localPosition)<.003f,id+" reload releases slide");Capture(role+"-"+id+"-reload-complete.png");if(id!="Handgun"){gun.RestaurarMunicion(3,100);int ejects=Remote.EjectCount;yield return Barrier(id+"-burst-start");for(int shot=0;shot<3;shot++){gun.SendMessage("TryFire");yield return new WaitForSeconds(gun.PresentationFireInterval+.02f);}yield return Until(()=>Remote.EjectCount>=ejects+3,id+" consecutive shots eject every casing",5);Check(gun.CurrentAmmo==0,id+" full cadence consumes three rounds");}yield return Barrier(id+"-done");
 }
 var pause=Local.GetComponentInChildren<PauseController>(true);pause.PausarJuego();yield return null;
 Check(pause.PauseHomePanel.activeInHierarchy,"Cooperative pause opens with the same home panel");Check(pause.PauseHomePanel.transform.Find("DQ_PauseTitle"),"Cooperative pause uses shared presentation");Check(!pause.PauseHomePanel.transform.Find("PvPvE_ChangeClass"),"Cooperative pause has no PvPvE-only class action");pause.ReanudarJuego();yield return null;Check(!PauseController.LocalPlayerPaused,"Cooperative pause resumes controls");
 Complete(true,"");
 }
}}
#endif
