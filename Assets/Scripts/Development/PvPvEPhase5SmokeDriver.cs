#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DeadQuarantine.PvPvE.Networking;
using DeadQuarantine.PvPvE.UI;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UI;
namespace DeadQuarantine.Development
{
 public sealed class PvPvEPhase5SmokeDriver:MonoBehaviour
 {
  [Serializable] sealed class Result{public bool ok;public string role,mode,error;public string[] checks;}
  string role,mode,run,folder;bool done;readonly List<string> checks=new();
  PvPvEMatchmaking service;PvPvELobbyView view;PvPvEModeMenu menu;PvPvENetworkSettings config;
  PvPvEMapVoting Vote=>service.Roster?service.Roster.GetComponent<PvPvEMapVoting>():null;
  static string Arg(string key){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null;}
  void Start(){role=Arg("-dq-role");if(role==null){enabled=false;return;}Application.targetFrameRate=30;mode=Arg("-dq-mode")??"cloud";run=Arg("-dq-run");folder=Arg("-dq-output");Directory.CreateDirectory(folder);StartCoroutine(Safe(Run()));}
  IEnumerator Safe(IEnumerator e){while(!done){object value;try{if(!e.MoveNext())yield break;value=e.Current;}catch(Exception ex){Finish(false,ex.ToString());yield break;}if(value is IEnumerator child)yield return Safe(child);else yield return value;}}
  IEnumerator Until(Func<bool> condition,string message,float seconds=100){double end=Time.realtimeSinceStartupAsDouble+seconds;while(!condition()){if(Time.realtimeSinceStartupAsDouble>end)throw new Exception(message+"; "+service?.State+"; "+service?.Error);yield return null;}}
  IEnumerator Wait(Task t){yield return Until(()=>t.IsCompleted,"Task timeout");if(t.IsFaulted)throw t.Exception;}
  bool Has(string name)=>File.Exists(Path.Combine(folder,name));
  string Read(string name)=>File.ReadAllText(Path.Combine(folder,name));
  void Write(string name,string value="yes"){string path=Path.Combine(folder,name);File.WriteAllText(path+".tmp",value);if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);}
  void Check(bool pass,string message){if(!pass)throw new Exception(message);checks.Add(message);Write("progress-"+role+".json",JsonUtility.ToJson(new Result{role=role,mode=mode,checks=checks.ToArray()},true));}
  void Finish(bool ok,string error){if(done)return;done=true;Write("result-"+role+".json",JsonUtility.ToJson(new Result{ok=ok,role=role,mode=mode,error=error,checks=checks.ToArray()},true));Application.Quit(ok?0:1);}
  string Counts()=>string.Join(",",Enumerable.Range(0,3).Select(i=>Vote.Count(i)));
  void Click(int map){var ui=view.LobbyCanvas.GetComponentInChildren<PvPvEMapVotingView>();ui.transform.Find("Map"+map).GetComponent<Button>().onClick.Invoke();}
  IEnumerator Search(){yield return Wait(service.SearchAsync());Check(service.State==PvPvEConnectionState.Waiting,"Real NGO connection and lobby initialized");yield return Until(()=>Vote&&Vote.IsSpawned,"Voting behaviour missing");}
  IEnumerator Run()
  {
   service=FindFirstObjectByType<PvPvEMatchmaking>();view=service.GetComponent<PvPvELobbyView>();menu=service.GetComponent<PvPvEModeMenu>();config=Instantiate(service.Settings);config.SessionPool="dq.p5."+run;
   // Shorter configurable post-vote wait only in this development fixture; production stays 15 + 10.
   config.MapVoteSeconds=15;config.PostVoteSeconds=3;
   if(mode=="cloud")yield return Wait(UnityServices.InitializeAsync(new InitializationOptions().SetProfile("p5-"+run+"-"+role)));
   service.ConfigureForTest(config,mode=="cloud"?new PvPvEUnitySessions():new PvPvEPhase4SmokeDriver.LanDirectory(service,role=="host"));
   var initial=menu.GetComponentInParent<Canvas>().transform.Find("menuPrincipalPanel");if(initial)initial.gameObject.SetActive(false);menu.OpenModes();menu.OpenPvPvE();
   if(role=="client1")yield return Until(()=>Has("host-ready"),"Host missing");
   if(role=="client2")yield return Until(()=>Has("allow-client2"),"Initial ballots missing");
   yield return Search();
   if(role=="host"){
    Check(Vote.Phase.Value==PvPvEVotePhase.WaitingForTeams&&Vote.Deadline.Value==0,"Host alone has no vote countdown");Write("host-ready");
    yield return Until(()=>Vote.Phase.Value==PvPvEVotePhase.Voting,"Two-team voting did not start");Click(0);Click(0);Write("voting-ready");
    yield return Until(()=>Has("first-voted"),"Client vote missing");Check(Counts()=="1,0,1"&&Vote.Ballots.Count==2,"Changed/repeated votes remain one ballot per player; invalid map IDs ignored");
    double deadline=Vote.Deadline.Value;Write("allow-client2");
    yield return Until(()=>Has("client-left")&&service.Roster.Members.Count==2&&Vote.Ballots.Count==2,"Departed vote was not removed");
    Check(Vote.Count(0)==1&&Vote.Count(2)==1&&Math.Abs(Vote.Deadline.Value-deadline)<.01,"Departure removes only its vote; rebalance preserves valid votes and original deadline");Write("rejoin");
   }else if(role=="client1"){
    yield return Until(()=>Has("voting-ready")&&Vote.Phase.Value==PvPvEVotePhase.Voting,"Voting start missing");Click(1);Click(2);Click(2);Vote.CastVoteServerRpc(-1);Vote.CastVoteServerRpc(99);
    yield return Until(()=>Counts()=="1,0,1"&&Vote.Ballots.Count==2,"Vote change not accepted");Write("first-voted");
    yield return Until(()=>Has("client2-voted"),"Late join vote missing");yield return Wait(service.CancelAndLeaveAsync());Write("client-left");
    yield return Until(()=>Has("rejoin"),"Rejoin signal missing");yield return Search();Check(Vote.VoteOf(service.Network.LocalClientId)==-1,"Reconnection does not retain a departed ballot");Click(2);
   }else{
    yield return Until(()=>Vote.Ballots.Count==2,"Late join snapshot missing");Check(Counts()=="1,0,1","Late join receives current votes and countdown");Click(2);yield return Until(()=>Vote.Count(2)==2,"Late vote rejected");Write("client2-voted");
   }
   yield return Until(()=>service.Roster.Members.Count==3&&Vote.Count(2)==2&&Vote.Count(0)==1,"Final majority not synchronized");
   Write("snapshot-"+role,Counts());
   if(role=="host"){
    yield return Until(()=>Has("snapshot-client1")&&Has("snapshot-client2"),"Snapshots missing");Check(Read("snapshot-client1")==Counts()&&Read("snapshot-client2")==Counts(),"Host and clients have identical counts");
    if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)CaptureUI("Voting.png");
   }
   for(int cycle=0;cycle<3;cycle++){
    if(cycle>0){
     if(role=="host"){yield return Until(()=>Has("selected-"+(cycle-1)+"-client1")&&Has("selected-"+(cycle-1)+"-client2"),"Previous result not acknowledged");Vote.BeginNextLobbyCycle();Write("cycle-"+cycle);}
     yield return Until(()=>Has("cycle-"+cycle)&&Vote.Phase.Value==PvPvEVotePhase.Voting,"New cycle missing");
     if(cycle==2){if(role=="host")Click(0);if(role=="client1")Click(1);}
    }
    yield return Until(()=>Vote.Phase.Value==PvPvEVotePhase.VoteResult,"Voting deadline missing");int winner=Vote.Winner.Value;
    Check(cycle==0?winner==2:cycle==1?winner>=0&&winner<3:winner==0||winner==1,"Server resolves "+(cycle==0?"majority":cycle==1?"abstention tie":"highest-vote tie")+" correctly");
    string frozen=Counts();Vote.CastVoteServerRpc((winner+1)%3);yield return new WaitForSecondsRealtime(.2f);Check(Counts()==frozen&&Vote.Winner.Value==winner,"Closed voting rejects late changes");
    Check(Vote.Remaining>0&&Vote.Remaining<=config.PostVoteSeconds+1,"Result uses a separate synchronized post-vote countdown");
    yield return Until(()=>Vote.Phase.Value==PvPvEVotePhase.MapSelected,"Post-vote deadline missing");Check(Vote.Winner.Value==winner&&Vote.SelectedMap!=null&&Vote.Deadline.Value==0,"Confirmed map remains available without prematurely loading a scene");
    Write("selected-"+cycle+"-"+role,winner.ToString());
    if(role=="host"){
     yield return Until(()=>Has("selected-"+cycle+"-client1")&&Has("selected-"+cycle+"-client2"),"Result snapshots missing");Check(Read("selected-"+cycle+"-client1")==winner.ToString()&&Read("selected-"+cycle+"-client2")==winner.ToString(),"All peers receive identical winning map for cycle "+cycle);
     if(cycle==0&&SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)CaptureUI("Result.png");
    }
   }
   if(role=="host"){
    Write("leave-all");yield return Until(()=>Has("left-client1")&&Has("left-client2")&&service.Roster.Members.Count==1&&Vote.Phase.Value==PvPvEVotePhase.WaitingForTeams,"Empty-team reset missing");Check(Vote.Ballots.Count==0&&Vote.Winner.Value==-1&&Vote.Deadline.Value==0,"Losing a team clears result and waits without softlock");
    yield return Wait(service.CancelAndLeaveAsync());Check(!view.IsShowing&&service.CanNavigateAway,"Host cleanup restores menu");
   }else{yield return Until(()=>Has("leave-all"),"Final leave signal missing");yield return Wait(service.CancelAndLeaveAsync());Check(!view.IsShowing&&service.CanNavigateAway,"Client cleanup restores menu");Write("left-"+role);}
   Finish(true,"");
  }
  void CaptureUI(string file)
  {
   var canvas=view.LobbyCanvas;var old=canvas.renderMode;var oldCamera=canvas.worldCamera;float plane=canvas.planeDistance;
   var camera=new GameObject("Vote validation camera").AddComponent<Camera>();camera.transform.position=new Vector3(50000,0,-10);camera.orthographic=true;camera.orthographicSize=5;camera.nearClipPlane=.1f;camera.farClipPlane=5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.03f,.03f);camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
   var rt=new RenderTexture(1600,900,24);var active=RenderTexture.active;
   try{canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;var t=new Texture2D(1600,900,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1600,900),0,0);t.Apply();File.WriteAllBytes(Path.Combine(folder,file),t.EncodeToPNG());Destroy(t);}
   finally{canvas.renderMode=old;canvas.worldCamera=oldCamera;canvas.planeDistance=plane;RenderTexture.active=active;rt.Release();Destroy(rt);Destroy(camera.gameObject);}
  }
 }
}
#endif
