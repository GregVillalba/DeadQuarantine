#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.Collections;using System.Linq;using System.IO;using UnityEngine;using TMPro;using DeadQuarantine.PvPvE;using DeadQuarantine.PvPvE.Networking;using DeadQuarantine.PvPvE.UI;
namespace DeadQuarantine.Development {public sealed partial class PvPvEWeaponPoseModeDriver {
 IEnumerator VerifyPreviewPixels(PvPvEWeaponPreview preview,string label){
  yield return new WaitForEndOfFrame();var target=(RenderTexture)preview.GetComponent<UnityEngine.UI.RawImage>().texture;var previous=RenderTexture.active;var sample=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
  try{RenderTexture.active=target;sample.ReadPixels(new Rect(0,0,target.width,target.height),0,0);sample.Apply();int visible=sample.GetPixels32().Count(c=>c.a>25&&(c.r>25||c.g>25||c.b>25));Check(visible>500,label+" renders actual visible model pixels at low quality: "+visible);}finally{RenderTexture.active=previous;Destroy(sample);}
 }
 IEnumerator VerifyCancelSearch(PvPvEModeMenu menu){
  var search=service.SearchAsync();yield return null;var lobby=service.GetComponent<PvPvELobbyView>();Check(lobby.IsShowing,"Search opens lobby before connection");lobby.Leave();yield return Until(()=>service.CanNavigateAway&&menu.ModeSelectionPanel.activeInHierarchy,"Cancel search returns to modes without stale room",30);Check(!service.Network,"Canceled search releases its network scope");menu.OpenPvPvE();
 }
 IEnumerator ReviewEveryWeaponPreview(PvPvEClassEditor editor,bool secondary){
  foreach(var gun in editor.catalog.weapons.Where(g=>secondary?g.secondary:!g.secondary)){
   var button=UiButton(editor.Panel.transform,"Choose_"+gun.id);button.onClick.Invoke();var preview=editor.Panel.transform.Find("ItemPicker/PickerModelFrame/PickerModel").GetComponent<PvPvEWeaponPreview>();yield return Until(()=>preview.Ready,gun.id+" clicked preview ready");yield return VerifyPreviewPixels(preview,gun.id+" click preview");if(gun.id=="SMG02")yield return CaptureInterface("05-SMG02-preview");
  }
 }
 IEnumerator ReviewFollowupFeedback(){
  yield return ReviewCaptureCorrections();yield return ReviewExistingMelee();
  yield return Sync("feedback-start");int hits=Combat.LocalConfirmedHits;
  if(who==0){Combat.ApplyDamageServer(0,Actor(1),1,PvPvEDamageKind.Bullet,false,1,"BurstRifle");Combat.ApplyDamageServer(1,Actor(0),1,PvPvEDamageKind.Bullet,false,1,"Handgun01");Combat.ApplyDamageServer(2,Actor(1),1,PvPvEDamageKind.Bullet,false,1,"Rifle");}
  yield return Until(()=>Combat.LocalConfirmedHits>hits,"Server-confirmed enemy hit reaches the shooter");var marker=(GameObject)Field(Local.GetComponentInChildren<HUDController>(true),"hitMarker");Check(marker.activeInHierarchy,"PvP displays the existing hitmarker");yield return CaptureInterface("14-pvp-hitmarker");yield return Sync("hits-received");
  hits=Combat.LocalConfirmedHits;if(who==0)Check(!Combat.ApplyDamageServer(0,Actor(2),1,PvPvEDamageKind.Bullet,false,1,"BurstRifle"),"Friendly damage rejected");yield return new WaitForSecondsRealtime(.25f);Check(Combat.LocalConfirmedHits==hits,"No friendly hitmarker");
  if(who==0)foreach(var player in Players){KillRealZombieForScore(player.OwnerClientId);KillRealZombieForScore(player.OwnerClientId);}
  var feedback=Combat.GetComponent<PvPvEKillFeedback>();yield return Until(()=>feedback.AccumulatedPoints==2*Combat.Settings.ZombieKillScore,"Confirmed zombie awards accumulate in the same feedback burst");yield return new WaitForSecondsRealtime(.35f);Check(feedback.Message.Contains("+"+(2*Combat.Settings.ZombieKillScore)),"Animated points reach the accumulated total");yield return CaptureInterface("14-accumulated-points");var score=Local.GetComponentInChildren<ScoreHUD>(true);yield return Until(()=>score&&score.DisplayedScore==Stats(who).Points.ToString(),"HUD displays actual authoritative individual points");Check(Combat.AlphaKills.Value==0&&Combat.BravoKills.Value==0,"Actual zombie deaths do not award team kills");yield return Sync("feedback-done");
 }
 IEnumerator PauseLeave(){var pause=Local.GetComponentInChildren<PauseController>(true);pause.PausarJuego();yield return Until(()=>pause.IsPauseHomeVisible,"Pause visible before leaving");var button=pause.PauseHomePanel.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.gameObject.activeInHierarchy&&b.GetComponentInChildren<TMP_Text>(true).text.Trim().StartsWith("Salir"));button.onClick.Invoke();yield return Until(()=>service.CanNavigateAway,"Actual pause exit cleans up session",40);Check(service.State==PvPvEConnectionState.Idle,"Pause exit returns to menu without quitting application");}
 IEnumerator ReviewFollowupExit(){
  string mode=Arg("-dq-followup-exit");yield return Sync("exit-start");
  if(mode=="client"){
   if(who==1){yield return PauseLeave();Write("bravo-left");Complete(true,"");yield break;}
   yield return Until(()=>Combat.ScoreLocked.Value&&Combat.Winner.Value==PvPvETeam.Alpha,"Empty opposing team gives surviving team victory",40);yield return CaptureInterface("18-empty-team-victory");yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Lobby,"Survivors return to lobby after rival team leaves",100);Check(service.State==PvPvEConnectionState.Waiting,"Survivors keep a valid session");Check(Cursor.visible&&Cursor.lockState==CursorLockMode.None,"Survivor lobby unlocks visible cursor");yield return CaptureInterface("19-survivor-lobby");Write(role+"-exit-verified");yield return Until(()=>Has("host-exit-verified")&&Has("client2-exit-verified"),"Both survivors remain in lobby");Complete(true,"");yield break;
  }
  if(who==2){yield return PauseLeave();Write("alpha-support-left");Complete(true,"");yield break;}
  yield return Until(()=>Has("alpha-support-left"),"Additional Alpha member leaves");
  if(who==0){Check(Combat.IsPlaying,"One member leaving does not end a valid 1v1");yield return PauseLeave();Complete(true,"");yield break;}
  yield return Until(()=>Combat.ScoreLocked.Value&&Combat.Winner.Value==PvPvETeam.Bravo,"Graceful host departure awards remaining team victory",40);yield return CaptureInterface("18-host-left-victory");yield return Until(()=>service.CanNavigateAway&&service.GetComponent<PvPvELobbyView>().IsShowing,"Survivor returns to a usable local 3D lobby after host closes",60);Check(service.GetComponent<PvPvELobbyView>().SearchLobbyRequested,"Survivor can search again and edit classes");Check(Cursor.visible&&Cursor.lockState==CursorLockMode.None,"Host departure leaves visible unlocked cursor");var editor=service.GetComponent<PvPvEClassEditor>();editor.Open();yield return Until(()=>editor.IsOpen&&editor.PreviewReady&&!editor.CharacterPreviewReady,"Class editor works after host departure");yield return CaptureInterface("20-class-editor-after-host");editor.Close();yield return CaptureInterface("19-host-left-lobby");Complete(true,"");
 }
}}
#endif
