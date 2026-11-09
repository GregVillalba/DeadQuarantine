#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DeadQuarantine.PvPvE.Networking;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace DeadQuarantine.Development
{
 public sealed class PvPvEPhase3SmokeDriver : MonoBehaviour
 {
  [Serializable] sealed class Result {public bool ok;public string role,mode,error;public string[] checks;}
  public PvPvENetworkSettings Settings;
  PvPvEMatchmaking service;PvPvENetworkSettings config;
  string role,mode,folder,run;int total;
  readonly List<string> checks=new();bool finished;
  static string Arg(string key){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null;}
  void Start(){role=Arg("-dq-role");if(string.IsNullOrEmpty(role)){enabled=false;return;}mode=Arg("-dq-mode")??"cloud";folder=Arg("-dq-output");run=Arg("-dq-run")??"local";total=int.Parse(Arg("-dq-count")??"3");Directory.CreateDirectory(folder);StartCoroutine(Safe(Run()));}
  IEnumerator Safe(IEnumerator routine){while(!finished){object value;try{if(!routine.MoveNext())yield break;value=routine.Current;}catch(Exception e){Complete(false,e.ToString());yield break;}if(value is IEnumerator inner)yield return Safe(inner);else yield return value;}}
  IEnumerator Until(Func<bool> condition,string message,float timeout=100){float end=Time.realtimeSinceStartup+timeout;while(!condition()){if(Time.realtimeSinceStartup>end)throw new TimeoutException(message+" State="+service?.State+" Error="+service?.Error);yield return null;}}
  IEnumerator Wait(Task task,string message){yield return Until(()=>task.IsCompleted,message);if(task.IsFaulted)throw task.Exception;}
  void Write(string file,string value){string path=Path.Combine(folder,file);File.WriteAllText(path+".tmp",value);for(int i=0;;i++){try{if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);return;}catch(IOException)when(i<30){System.Threading.Thread.Sleep(10);}}}
  string Read(string file){using var stream=File.Open(Path.Combine(folder,file),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);using var r=new StreamReader(stream);return r.ReadToEnd();}
  bool Has(string file)=>File.Exists(Path.Combine(folder,file));
  void Check(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);Write("progress-"+role+".json",JsonUtility.ToJson(new Result{role=role,mode=mode,checks=checks.ToArray()},true));}
  void Complete(bool ok,string error){if(finished)return;finished=true;Write("result-"+role+".json",JsonUtility.ToJson(new Result{ok=ok,role=role,mode=mode,error=error,checks=checks.ToArray()},true));if(service)_=service.CancelAndLeaveAsync();Application.Quit(ok?0:1);}
  string Teams(){var members=new List<PvPvEMember>();foreach(var m in service.Roster.Members)members.Add(m);return string.Join(",",members.OrderBy(m=>m.ClientId).Select(m=>m.ClientId+":"+(byte)m.Team));}
  bool Balanced()=>service.Roster && Math.Abs(service.Roster.Count(PvPvETeam.Alpha)-service.Roster.Count(PvPvETeam.Bravo))<=1 && service.Roster.Count(PvPvETeam.Alpha)<=5 && service.Roster.Count(PvPvETeam.Bravo)<=5;
  IEnumerator Search(){yield return Wait(service.SearchAsync(),"Matchmaking did not complete");Check(service.State==PvPvEConnectionState.Waiting,"Real network connection and authoritative roster initialized ("+service.State+"; "+service.Error+")");}
  IEnumerator Run()
  {
   config=Instantiate(Settings);config.SessionPool="dq.p3."+run;
   if(mode=="cloud"){var options=new InitializationOptions().SetProfile("p3-"+run.Substring(0,Math.Min(8,run.Length))+"-"+role);yield return Wait(UnityServices.InitializeAsync(options),"Services initialization failed");}
   service=GetComponent<PvPvEMatchmaking>();
   IPvPvESessions directory=mode=="cloud"?new PvPvEUnitySessions():new LanDirectory(service,role=="host",run,()=>Has("host-ready2")?Read("host-ready2"):Read("host-ready"));
   service.ConfigureForTest(config,directory);
   if(role=="host")yield return Host();else if(role=="overflow")yield return Overflow();else yield return Client();
  }
  IEnumerator Host()
  {
   yield return Search();string first=service.SessionId;
   Check(service.IsHost&&service.Roster.Members.Count==1&&!service.Roster.CanStart,"Host alone does not satisfy the two-team minimum");Write("host-ready",first);
   yield return Until(()=>service.Roster.Members.Count==total,"Participants never reached the room");Check(Balanced()&&service.Roster.CanStart,"Server assigns balanced teams up to the configured capacity");
   string teams=Teams();Write("room-full",teams);
   yield return Until(()=>Enumerable.Range(1,total-1).All(i=>Has("snapshot-client"+i)),"Clients did not receive roster snapshot");
   for(int i=1;i<total;i++)Check(Read("snapshot-client"+i)==teams,"Client "+i+" receives identical authoritative team membership");
   if(total==10){yield return Until(()=>Has("result-overflow.json"),"Overflow connection did not finish");Check(Read("result-overflow.json").Contains("\"ok\": true")&&service.Roster.Members.Count==10,"Eleventh player is rejected without changing the full roster");}
   Write("leave-client1","yes");yield return Until(()=>Has("left-client1")&&service.Roster.Members.Count==total-1,"Disconnect did not remove player");Check(Balanced(),"Departure removes membership and rebalances waiting teams");
   Write("rejoin-client1","yes");yield return Until(()=>Has("rejoined-client1")&&service.Roster.Members.Count==total,"Reconnection did not complete");Check(Balanced(),"Reconnection reuses a free team slot without duplicate membership");
   Write("closing","yes");yield return Wait(service.CancelAndLeaveAsync(),"Host room did not close");Check(service.CanNavigateAway,"Host cleanup releases session and NGO configuration");
   yield return Until(()=>Enumerable.Range(1,total-1).All(i=>Has("lost-client"+i)),"Clients did not clear disconnected room");
   yield return Search();Check(service.IsHost&&service.SessionId!=first,"A new room can be created after clean shutdown");Write("host-ready2",service.SessionId);
   yield return Until(()=>service.Roster.Members.Count==total,"Second cycle did not reconnect clients");yield return Until(()=>Enumerable.Range(1,total-1).All(i=>Has("joined2-client"+i)),"Second cycle clients did not finish initialization");Check(Balanced(),"Second cycle starts with a clean balanced roster");
   Write("closing2","yes");yield return Wait(service.CancelAndLeaveAsync(),"Second host cleanup failed");
   yield return Until(()=>Enumerable.Range(1,total-1).All(i=>Has("result-client"+i+".json")),"Clients did not finish");
   if(mode=="cloud") {
    config.SessionPool+=".cancel";var observed=new ObservedDirectory(new PvPvEUnitySessions());service.ConfigureForTest(config,observed);
    var searching=service.SearchAsync();yield return Until(()=>observed.Started,"SDK request did not start");var cancelling=service.CancelAndLeaveAsync();service.SearchAsync();
    yield return Wait(cancelling,"Pending SDK cancellation did not clean up");Check(observed.Calls==1&&service.CanNavigateAway,"Cancelling an in-flight Services request closes its late room and blocks overlapping searches");
    var remaining=awaitableQuery(config);yield return Wait(remaining,"Cancelled room query failed");Check(remaining.Result==0,"Cancelled search leaves no discoverable room in its isolated test pool");
   }
   Complete(true,"");
  }
  static async Task<int> awaitableQuery(PvPvENetworkSettings config){for(int i=0;i<10;i++){var q=new QuerySessionsOptions{FilterOptions=PvPvEUnitySessions.Query(config).Filters};var result=await MultiplayerService.Instance.QuerySessionsAsync(q);if(result.Sessions.Count==0)return 0;await Task.Delay(1000);}return 1;}
  IEnumerator Client()
  {
   yield return Until(()=>Has("host-ready"),"Host was not ready");yield return Search();Check(service.SessionId==Read("host-ready"),"Automatic discovery joins the existing room without a code");
   yield return Until(()=>service.Roster.Members.Count==total&&Has("room-full"),"Roster never synchronized");Write("snapshot-"+role,Teams());
   if(role=="client1"){
    yield return Until(()=>Has("leave-client1"),"Leave command missing");yield return Wait(service.CancelAndLeaveAsync(),"Client did not leave");Check(service.CanNavigateAway,"Client leave releases its network scope");Write("left-client1","yes");
    yield return Until(()=>Has("rejoin-client1"),"Rejoin command missing");yield return Search();Check(service.SessionId==Read("host-ready"),"Reconnect discovers the same available room");Write("rejoined-client1","yes");
   }
   yield return Until(()=>Has("closing")&&service.CanNavigateAway,"Host departure did not release client");Check(service.State==PvPvEConnectionState.Failed,"Host departure is surfaced instead of leaving a stale ready state");Write("lost-"+role,"yes");
   yield return Until(()=>Has("host-ready2"),"Second host cycle missing");yield return Search();Check(service.SessionId==Read("host-ready2"),"Second search joins the fresh room");Write("joined2-"+role,"yes");
   yield return Until(()=>Has("closing2")&&service.CanNavigateAway,"Second client cleanup failed");Complete(true,"");
  }
  IEnumerator Overflow(){yield return Until(()=>Has("room-full"),"Room never became full");yield return Wait(service.SearchAsync(),"Overflow admission did not resolve");Check(service.State==PvPvEConnectionState.Failed&&service.CanNavigateAway,"Full-room admission rejects and cleans up the eleventh connection");Complete(true,"");}
  sealed class ObservedDirectory : IPvPvESessions {
   readonly IPvPvESessions inner;public bool Started;public int Calls;
   public ObservedDirectory(IPvPvESessions inner)=>this.inner=inner;
   public Task InitializeAsync()=>inner.InitializeAsync();
   public Task<IPvPvESession> FindOrCreateAsync(PvPvENetworkSettings settings){Started=true;Calls++;return inner.FindOrCreateAsync(settings);}
  }
  sealed class LanDirectory : IPvPvESessions {
   readonly PvPvEMatchmaking owner;readonly bool host;readonly string run;readonly Func<string> roomId;
   public LanDirectory(PvPvEMatchmaking owner,bool host,string run,Func<string> roomId){this.owner=owner;this.host=host;this.run=run;this.roomId=roomId;}
   public Task InitializeAsync()=>Task.CompletedTask;
   public Task<IPvPvESession> FindOrCreateAsync(PvPvENetworkSettings settings){var network=owner.Network;network.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>().SetConnectionData("127.0.0.1",7793,"0.0.0.0");if(!(host?network.StartHost():network.StartClient()))throw new Exception("LAN transport failed to start");return Task.FromResult<IPvPvESession>(new LocalLease(network,host,host?"lan-"+run+"-"+generation++:roomId()));}
   static int generation;
  }
  sealed class LocalLease : IPvPvESession {
   readonly Unity.Netcode.NetworkManager network;public string Id{get;}public bool IsHost{get;}public int ParticipantCount=>network && network.IsServer?network.ConnectedClients.Count:1;public Task CloseAdmissionAsync()=>Task.CompletedTask;public event Action Closed;
   public LocalLease(Unity.Netcode.NetworkManager n,bool h,string id){network=n;IsHost=h;Id=id;}
   public Task LeaveAsync(){if(network&&network.IsListening)network.Shutdown();return Task.CompletedTask;}
  }
 }
}
#endif
