#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.UI;
using DeadQuarantine.PvPvE.Networking;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace DeadQuarantine.Development
{
 public sealed class PvPvEPhase9SmokeDriver:MonoBehaviour {
  [Serializable] sealed class Result {public bool ok;public string role,error;public string[] checks;}
  string role,folder;bool finished;PvPvEMatchmaking service;PvPvEClassEditor editor;PvPvELobbyView lobby;readonly List<string> checks=new();
  static string Arg(string key){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null;}
  void Start(){role=Arg("-dq-role");if(role==null){enabled=false;return;}folder=Arg("-dq-output");Directory.CreateDirectory(folder);Application.runInBackground=true;StartCoroutine(Safe(Run()));}
  IEnumerator Safe(IEnumerator e){while(!finished){object value;try{if(!e.MoveNext())yield break;value=e.Current;}catch(Exception error){Complete(false,error.ToString());yield break;}if(value is IEnumerator nested)yield return Safe(nested);else yield return value;}}
  IEnumerator Until(Func<bool> condition,string message,float seconds=45){float end=Time.realtimeSinceStartup+seconds;while(!condition()){if(Time.realtimeSinceStartup>end)throw new TimeoutException(message+" "+service?.Error);yield return null;}}
  IEnumerator Wait(Task task){yield return Until(()=>task.IsCompleted,"Network task");if(task.IsFaulted)throw task.Exception;}
  void Write(string file,string value){File.WriteAllText(Path.Combine(folder,file),value);}
  bool Has(string file)=>File.Exists(Path.Combine(folder,file));
  void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);Write("progress-"+role+".json",JsonUtility.ToJson(new Result{role=role,checks=checks.ToArray()},true));}
  IEnumerator Barrier(string key){Write(role+"-"+key,"ok");yield return Until(()=>Has((role=="host"?"client1":"host")+"-"+key),key);}
  void Complete(bool ok,string error){finished=true;Write("result-"+role+".json",JsonUtility.ToJson(new Result{ok=ok,role=role,error=error,checks=checks.ToArray()},true));Application.Quit(ok?0:1);}
  void Click(string name){var button=editor.Panel.GetComponentsInChildren<Button>(true).Single(b=>b.name==name&&b.gameObject.activeInHierarchy);Check(button.interactable,name+" is interactable");button.onClick.Invoke();}
  IEnumerator Run(){
   service=FindFirstObjectByType<PvPvEMatchmaking>();lobby=service.GetComponent<PvPvELobbyView>();editor=service.GetComponent<PvPvEClassEditor>();
   var config=Instantiate(service.Settings);config.MapVoteSeconds=120;config.PostVoteSeconds=10;
   service.ConfigureForTest(config,new PvPvEPhase4SmokeDriver.LanDirectory(service,role=="host"));
   var menu=service.GetComponent<PvPvEModeMenu>();var initial=menu.GetComponentInParent<Canvas>().transform.Find("menuPrincipalPanel");if(initial)initial.gameObject.SetActive(false);
   menu.OpenModes();menu.OpenPvPvE();if(role!="host")yield return Until(()=>Has("host-ready"),"Host start");
   yield return Wait(service.SearchAsync());if(role=="host")Write("host-ready","ok");
   yield return Until(()=>lobby.IsShowing&&service.Roster&&service.Roster.Members.Count==2,"Real two-player lobby");
   Check(service.State==PvPvEConnectionState.Waiting&&service.Roster.CanStart,"Host + Client retain the existing teams and lobby");
   int character=CharacterSelection.SelectedIndex;editor.UseValidationProfile(role);editor.entryButton.onClick.Invoke();
   yield return Until(()=>editor.IsOpen&&editor.PreviewReady&&editor.CharacterPreviewReady,"Class editor opens through the lobby button");
   Check(editor.CharacterPreviewReady,"Class screen reuses the local lobby character without duplicating it");Check(editor.Store.Count==5,"Exactly five personal classes");
   if(Arg("-dq-pick10-ui")=="1"){yield return Pick10UI();}else if(Arg("-dq-restore")=="1"){
    for(int i=0;i<5;i++)Check(editor.Store.Get(i).name==role+" CLASE "+i,"Class "+i+" survives a full process restart");
    if(Arg("-dq-wildcards-ui")=="1")for(int i=0;i<5;i++){var c=editor.Store.Get(i);Check(c.wildcard1=="wildcard.overkill"&&c.wildcard2=="wildcard.greed"&&c.secondary=="BurstRifle"&&c.perk1=="perk.gloves"&&c.perk2=="perk.holster","Wildcards, second primary and same-category perks survive restart "+i);}
    if(Arg("-dq-perks-ui")=="1")for(int i=0;i<5;i++)Check(editor.Store.Get(i).perk1=="perk.gloves"&&editor.Store.Get(i).perk2=="perk.boots","Both perks survive a full process restart "+i);
   }else{
    if(Arg("-dq-wildcards-ui")=="1")for(int i=0;i<5;i++)editor.Store.Reset(i);
    for(int i=0;i<5;i++){
     Click("Class_"+i);Check(editor.Rename(role+" CLASE "+i),"Rename and persist class "+i);
     Click("Primary");var gun=editor.catalog.weapons.Where(g=>!g.secondary&&editor.catalog.attachments.Eligible(g.id)).ElementAt(i);
     Click("Choose_"+gun.id);yield return Until(()=>editor.PreviewReady,"Primary 3D preview ready");Check(editor.Current.primary==gun.id,"Primary chosen from visual cards "+i);
     Click("Secondary");Click("Choose_AutoPistol");yield return Until(()=>editor.PreviewReady,"Secondary preview ready");Check(editor.Current.secondary=="AutoPistol","Secondary independent from primary "+i);
     Click("Attachment_Scope");Click("Choose_Scope_02");yield return Until(()=>editor.PreviewReady,"Accessory preview ready");Check(editor.Current.secondaryAttachments.scope=="Scope_02","Compatible secondary optic saved "+i);
     var before=editor.Current.Copy();Check(!editor.ChooseAttachment(true,WeaponAttachmentKind.Support,"Bipod_01")&&editor.Current.secondaryAttachments.Equals(before.secondaryAttachments),"Pistol bipod is rejected without mutating class "+i);
     Click("Grenade");var grenade=editor.catalog.grenades[i%editor.catalog.grenades.Length];Click("Choose_"+grenade.id);Check(editor.Current.grenade==grenade.id,"Existing grenade selected "+i);
     Click("Healing");var healing=editor.catalog.healings[i%editor.catalog.healings.Length];Click("Choose_"+healing.id);Check(editor.Current.healing==healing.id,"Existing healing ID saved "+i);
     if(Arg("-dq-perks-ui")=="1"){
      Click("Perk_0");Click("Choose_perk.gloves");Click("Perk_1");
      Check(!editor.Panel.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Choose_perk.holster"&&b.gameObject.activeInHierarchy).interactable,"Same-category perk is visibly disabled "+i);
      if(i==2)Capture("perks-picker");Click("Choose_perk.boots");
      Check(!editor.ChoosePerk(1,"perk.holster")&&!editor.ChoosePerk(1,"perk.gloves")&&!editor.ChoosePerk(2,"perk.canteen"),"Invalid perk category, duplicate and third slot are rejected "+i);
      var saved=new PvPvEClassStore(editor.catalog,role).Get(i);Check(saved.perk1=="perk.gloves"&&saved.perk2=="perk.boots","Both perks persist in class "+i);
     }

     var roundTrip=new PvPvEClassStore(editor.catalog,role).Get(i);Check(roundTrip.primary==gun.id&&roundTrip.secondaryAttachments.scope=="Scope_02"&&roundTrip.name==role+" CLASE "+i,"Class and both accessory sets reload from persistent storage "+i);
    }
    editor.SelectClass(2);yield return Until(()=>editor.PreviewReady,"Final visual preview");yield return new WaitForSecondsRealtime(.3f);Capture("secondary");Click("WeaponFocus");yield return Until(()=>editor.PreviewReady,"Primary preview focus");yield return new WaitForSecondsRealtime(.2f);Capture("primary");Click("Primary");yield return new WaitForSecondsRealtime(.2f);Capture("picker");Click("ClosePicker");
    var backup=new PvPvEClassStore(editor.catalog,role+"-backup");var first=backup.Get(0);first.name="RECUPERABLE";Check(backup.Save(0,first,out _),"Backup fixture saved");var second=first.Copy();second.name="SEGUNDA";backup.Save(0,second,out _);
    PlayerPrefs.SetString("DQ.PvPvE.Classes.v1."+role+"-backup","{invalid");PlayerPrefs.Save();
    Check(new PvPvEClassStore(editor.catalog,role+"-backup").Get(0).name=="RECUPERABLE","Corrupt primary storage recovers the previous valid class backup");
    InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;var keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Escape));yield return new WaitForSecondsRealtime(.15f);
    Check(!editor.IsOpen&&lobby.IsShowing&&service.State==PvPvEConnectionState.Waiting,"Escape closes only the class editor without leaving the lobby");InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;editor.Open();yield return Until(()=>editor.PreviewReady,"Editor reopens after Escape");
   }
   if(Arg("-dq-wildcards-ui")=="1"&&Arg("-dq-restore")!="1")yield return WildcardsUI();
   Check(CharacterSelection.SelectedIndex==character,"Class equipment never changes character or skin selection");
   Check(service.Roster.Members.Count==2&&lobby.IsShowing,"Editing classes keeps networking and both 3D lobby actors alive");
   editor.Close();Check(!editor.IsOpen&&lobby.IsShowing,"Closing the editor returns to the same lobby");
   editor.Open();yield return Until(()=>editor.PreviewReady,"Reopen preview");Check(editor.Store.Get(2).name==role+" CLASE 2","Reopening preserves class edits");
   yield return Barrier("done");
   if(role=="host"){yield return Wait(service.CancelAndLeaveAsync());Write("closed","ok");}
   else yield return Until(()=>Has("closed")&&service.CanNavigateAway,"Host disconnect");
   yield return null;Check(!editor.IsOpen&&!lobby.IsShowing,"Disconnect closes class UI and destroys the preview");Complete(true,"");
  }
  void CheckPoints(int expected,string message){
   var text=editor.Panel.transform.Find("Pick10Counter").GetComponent<TMPro.TMP_Text>();
   Check(editor.PointsUsed==expected&&text.text==$"PICK 10   {expected}/10",message);
  }
  string Attachment(string gun,WeaponAttachmentKind kind)=>editor.catalog.attachments.entries.First(e=>editor.catalog.attachments.Allows(gun,kind,e.id)).id;
  IEnumerator Pick10UI(){
   int[] expected={4,6,8,9,10};
   if(Arg("-dq-restore")=="1"){
    for(int i=0;i<5;i++){editor.SelectClass(i);CheckPoints(expected[i],"Independent class points survive full process restart "+i);Check(editor.Current.name==role+" CLASE "+i,"Class name survives restart "+i);}
    yield break;
   }
   for(int i=0;i<5;i++){
    editor.SelectClass(i);Click("ResetClass");CheckPoints(4,"Reset restores four-point base "+i);
    Check(editor.Rename(role+" CLASE "+i)&&editor.ChooseWeapon(false,"SemiRifle")&&editor.ChooseWeapon(true,"AutoPistol"),"Existing class and weapon selectors reused "+i);Click("WeaponFocus");
    if(i>=1){Click("Perk_0");Click("Choose_perk.gloves");Click("Perk_1");Click("Choose_perk.boots");}
    if(i>=2){Click("Attachment_Scope");Click("Choose_"+Attachment(editor.Current.primary,WeaponAttachmentKind.Scope));Click("Attachment_Muzzle");Click("Choose_"+Attachment(editor.Current.primary,WeaponAttachmentKind.Muzzle));}
    if(i>=3){Click("Wildcard_0");Click("Choose_wildcard.healing");}
    if(i>=4){Click("Wildcard_1");Click("Choose_wildcard.grenades");}
    CheckPoints(expected[i],"Counter updates immediately for class "+i);
    Check(editor.catalog.Pick10Cost(new PvPvEClassStore(editor.catalog,role).Get(i))==expected[i],"Each class persists its own equipment-derived total "+i);
   }
   editor.SelectClass(4);yield return Until(()=>editor.PreviewReady,"Full ten-point preview");yield return new WaitForSecondsRealtime(.2f);Capture("pick10-full");
   var before=JsonUtility.ToJson(editor.Current);string support=Attachment(editor.Current.primary,WeaponAttachmentKind.Support);
   Click("Attachment_Support");var choice=editor.Panel.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Choose_"+support&&b.gameObject.activeInHierarchy);
   Check(!choice.interactable,"Eleventh-point accessory is visibly disabled");yield return new WaitForSecondsRealtime(.2f);Capture("pick10-blocked");
   Check(!editor.ChooseAttachment(false,WeaponAttachmentKind.Support,support)&&JsonUtility.ToJson(editor.Current)==before,"Direct selection also rejects the eleventh point without changing equipment");CheckPoints(10,"Rejected selection preserves counter");Click("ClosePicker");
   Check(!editor.ChoosePerk(1,"perk.holster")&&!editor.ChooseWildcard(1,"wildcard.healing"),"Same-cost incompatible perk and duplicate Wildcard stay invalid");
   Check(editor.ChooseAttachment(false,WeaponAttachmentKind.Muzzle,""),"Removing an accessory frees a point");CheckPoints(9,"Removal updates counter immediately");
   Check(editor.ChooseAttachment(false,WeaponAttachmentKind.Muzzle,Attachment(editor.Current.primary,WeaponAttachmentKind.Muzzle)),"Freed point can be spent again");CheckPoints(10,"Readdition restores ten points");
   Check(editor.ChooseWeapon(false,"BurstRifle"),"Replacing weapon clears its old accessories");CheckPoints(8,"Weapon replacement recalculates both cleared accessories");
   Check(editor.ChooseWeapon(false,"SemiRifle"),"Original selected primary can be restored");
   foreach(var kind in new[]{WeaponAttachmentKind.Scope,WeaponAttachmentKind.Muzzle})Check(editor.ChooseAttachment(false,kind,Attachment(editor.Current.primary,kind)),"Valid replacement accessory reapplied");CheckPoints(10,"Replacement class remains exactly at budget");
   // A phase-12 document may exceed ten: retain it, edit down in memory, save only when legal.
   var legacy=editor.catalog.Default(0);legacy.primary="SemiRifle";legacy.secondary="BurstRifle";legacy.wildcard1="wildcard.overkill";legacy.wildcard2="wildcard.greed";legacy.perk1="perk.gloves";legacy.perk2="perk.holster";
   foreach(WeaponAttachmentKind kind in Enum.GetValues(typeof(WeaponAttachmentKind))){legacy.primaryAttachments.Set(kind,Attachment(legacy.primary,kind));legacy.secondaryAttachments.Set(kind,Attachment(legacy.secondary,kind));}
   string profile=role+"-legacy-pick10",key="DQ.PvPvE.Classes.v1."+profile;
   PlayerPrefs.SetString(key,"{\"version\":1,\"classes\":["+JsonUtility.ToJson(legacy)+"]}");PlayerPrefs.Save();string original=PlayerPrefs.GetString(key);
   editor.UseValidationProfile(profile);editor.SelectClass(0);CheckPoints(16,"Legacy over-budget class remains visible with accurate warning");
   Check(editor.Current.primaryAttachments.Equals(legacy.primaryAttachments)&&editor.Current.secondaryAttachments.Equals(legacy.secondaryAttachments),"Migration never silently strips existing attachments");
   yield return new WaitForSecondsRealtime(.2f);Capture("pick10-legacy");
   foreach(WeaponAttachmentKind kind in Enum.GetValues(typeof(WeaponAttachmentKind)))Check(editor.ChooseAttachment(true,kind,""),"Legacy draft can reduce one point at a time");
   CheckPoints(12,"Intermediate legacy draft remains editable");Check(PlayerPrefs.GetString(key)==original,"Over-budget draft never overwrites the saved original");
   Check(editor.ChooseAttachment(false,WeaponAttachmentKind.Support,"")&&editor.ChooseAttachment(false,WeaponAttachmentKind.Muzzle,""),"Legacy draft reaches legal budget");CheckPoints(10,"Migrated class saves at ten");
   Check(editor.catalog.Validate(new PvPvEClassStore(editor.catalog,profile).Get(0),out _),"Adjusted legacy class reloads as fully valid");
   editor.UseValidationProfile(role);editor.SelectClass(2);CheckPoints(8,"Switching back restores the independent original profile");
  }
  IEnumerator WildcardsUI(){
   for(int i=0;i<5;i++){
    editor.SelectClass(i);Check(editor.ChooseWeapon(false,"SemiRifle"),"Fixed primary for Overkill test "+i);
    Click("Wildcard_0");if(i==0){yield return new WaitForSecondsRealtime(.25f);Capture("wildcards-picker");}Click("Choose_wildcard.overkill");
    Click("Wildcard_1");Check(!editor.Panel.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Choose_wildcard.overkill"&&b.gameObject.activeInHierarchy).interactable,"Duplicate Wildcard is visibly disabled "+i);Click("Choose_wildcard.greed");
    Click("Secondary");Click("Choose_BurstRifle");yield return Until(()=>editor.PreviewReady,"Second primary preview");
    Click("Perk_0");Click("Choose_perk.gloves");Click("Perk_1");Click("Choose_perk.holster");
    Check(!editor.ChooseWildcard(0,"")&&editor.Current.wildcard1=="wildcard.overkill","Removing required Overkill rejects without discarding selected weapon "+i);
    Check(!editor.ChooseWildcard(1,"")&&editor.Current.wildcard2=="wildcard.greed","Removing required Greed rejects without discarding perks "+i);
    Check(!editor.ChooseWildcard(2,"wildcard.healing")&&!editor.ChooseWildcard(1,"wildcard.overkill")&&!editor.ChooseWildcard(1,"unknown"),"Third slot, duplicate and unknown Wildcards rejected "+i);
    var saved=new PvPvEClassStore(editor.catalog,role).Get(i);Check(saved.secondary=="BurstRifle"&&saved.wildcard1=="wildcard.overkill"&&saved.wildcard2=="wildcard.greed"&&saved.perk2=="perk.holster","Both Wildcards and legal dependent equipment persist "+i);
   }
   editor.SelectClass(2);yield return Until(()=>editor.PreviewReady,"Complete Wildcard class preview");yield return new WaitForSecondsRealtime(.3f);Capture("wildcards-class");
   var temp=new PvPvEClassStore(editor.catalog,role+"-utilities");var c=temp.Get(0);c.wildcard1="wildcard.healing";c.wildcard2="wildcard.grenades";
   Check(temp.Save(0,c,out _)&&new PvPvEClassStore(editor.catalog,role+"-utilities").Get(0).wildcard2=="wildcard.grenades","Extra healing and grenade Wildcards persist too");
  }
  void Capture(string label){
   string captures=Path.Combine(folder,role,label);Directory.CreateDirectory(captures);
   var helper=gameObject.AddComponent<PvPvEPhase4SmokeDriver>();helper.enabled=false;
   var type=helper.GetType();foreach(var pair in new[]{("service",(object)service),("view",lobby),("folder",captures)})type.GetField(pair.Item1,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(helper,pair.Item2);
   type.GetMethod("CaptureLobby",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(helper,null);Destroy(helper);
  }
 }
}
#endif
