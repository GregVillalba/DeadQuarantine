#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DeadQuarantine.PvPvE.Networking;
using DeadQuarantine.PvPvE.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
namespace DeadQuarantine.Development
{
 public sealed class PvPvEPhase4SmokeDriver:MonoBehaviour
 {
  [Serializable] sealed class Result{public bool ok;public string role,mode,error;public string[] checks;}
  PvPvEMatchmaking service;PvPvELobbyView view;PvPvEModeMenu menu;PvPvENetworkSettings config;
  string role,mode,folder,run;int total;readonly List<string> checks=new();bool finished;
  static string Arg(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:null;}
  void Start(){role=Arg("-dq-role");if(role==null){enabled=false;return;}mode=Arg("-dq-mode")??"cloud";folder=Arg("-dq-output");run=Arg("-dq-run");total=int.Parse(Arg("-dq-count")??"3");Directory.CreateDirectory(folder);StartCoroutine(Safe(Run()));}
  IEnumerator Safe(IEnumerator e){while(!finished){object current;try{if(!e.MoveNext())yield break;current=e.Current;}catch(Exception error){Finish(false,error.ToString());yield break;}if(current is IEnumerator inner)yield return Safe(inner);else yield return current;}}
  IEnumerator Until(Func<bool> condition,string message,float timeout=100){float end=Time.realtimeSinceStartup+timeout;while(!condition()){if(Time.realtimeSinceStartup>end)throw new Exception(message+"; "+service?.State+"; "+service?.Error);yield return null;}}
  IEnumerator Wait(Task t,string message){yield return Until(()=>t.IsCompleted,message);if(t.IsFaulted)throw t.Exception;}
  bool Has(string f)=>File.Exists(Path.Combine(folder,f));
  string Read(string f)=>File.ReadAllText(Path.Combine(folder,f));
  void Write(string f,string value="yes"){string path=Path.Combine(folder,f);File.WriteAllText(path+".tmp",value);if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);}
  void Check(bool value,string text){if(!value)throw new Exception(text);checks.Add(text);Write("progress-"+role+".json",JsonUtility.ToJson(new Result{role=role,mode=mode,checks=checks.ToArray()},true));}
  void Finish(bool ok,string error){if(finished)return;finished=true;Write("result-"+role+".json",JsonUtility.ToJson(new Result{ok=ok,role=role,mode=mode,error=error,checks=checks.ToArray()},true));Application.Quit(ok?0:1);}
  List<PvPvEMember> Members(){var result=new List<PvPvEMember>();if(service.Roster)foreach(var m in service.Roster.Members)result.Add(m);return result.OrderBy(m=>m.Team).ThenBy(m=>m.ClientId).ToList();}
  bool ModelsReady(){if(!view.IsShowing || !service.Roster)return false;var actors=view.Stage.GetComponents<LobbyCinematicPlayerDisplay>();var members=Members();return actors.Count(a=>a.IsVisible)==members.Count && members.Select((m,i)=>actors[i].BuiltCharacterIndex==m.CharacterIndex && actors[i].TryGetHeadWorldPosition(out _)).All(v=>v);}
  bool Character(ulong id,int index)=>service.Roster && service.Roster.TryGetMember(id,out var m)&&m.CharacterIndex==index;
  IEnumerator Run()
  {
   service=FindFirstObjectByType<PvPvEMatchmaking>();view=service.GetComponent<PvPvELobbyView>();menu=service.GetComponent<PvPvEModeMenu>();config=Instantiate(service.Settings);config.SessionPool="dq.p4."+run;
   int number=role=="host"?0:int.Parse(role.Substring(6));CharacterSelection.SelectedIndex=number%5;
   if(mode=="cloud")yield return Wait(UnityServices.InitializeAsync(new InitializationOptions().SetProfile("p4-"+run+"-"+role)),"Services init");
   service.ConfigureForTest(config,mode=="cloud"?new PvPvEUnitySessions():new LanDirectory(service,role=="host"));
   var canvas=menu.GetComponentInParent<Canvas>();var initial=canvas.transform.Find("menuPrincipalPanel");if(initial)initial.gameObject.SetActive(false);menu.OpenModes();menu.OpenPvPvE();
   if(role!="host")yield return Until(()=>Has("host-ready"),"Host ready");
   yield return Wait(service.SearchAsync(),"Connection");Check(service.State==PvPvEConnectionState.Waiting,"Real NGO session connected");
   if(role=="host")Write("host-ready");
   yield return Until(()=>service.Roster.Members.Count==total && ModelsReady(),"Initial models");
   Check(service.Network.SpawnManager.SpawnedObjects.Count==1,"No legacy menu gameplay objects are spawned into the PvPvE session");
   Check(view.IsShowing && !menu.PvPvEPanel.activeSelf,"Search opens the 3D lobby and hides the entry page");
   Check(ModelsReady(),"Exactly one initialized original character model per connected member");
   Check(service.Roster.TryGetMember(service.Network.LocalClientId,out var me)&&me.CharacterIndex==number%5,"Approved character is retained from the existing saved selection");
   Write("id-"+role,service.Network.LocalClientId.ToString());Write("ready-"+role);
   yield return Until(()=>Enumerable.Range(1,total-1).All(i=>Has("ready-client"+i)),"Initial clients");
   ulong target=ulong.Parse(Read("id-client1"));
   for(int index=0;index<5;index++){
    if(role=="host")Write("select-"+index);
    yield return Until(()=>Has("select-"+index),"Selection signal");
    if(role=="client1"){
     view.OpenCharacters();var selector=view.CharacterPopup.GetComponentInChildren<CharacterSelectMenu>(true);var variant=config.Characters.GetVariant(index);selector.SelectClass(variant.type);selector.SelectGender(variant.gender);view.CharacterPopup.GetComponentInChildren<CharacterSelectScreen>(true).Confirm();
     Check(!view.CharacterPopup.activeSelf && CharacterSelection.SelectedIndex==index,"Existing selector confirms variant "+index+" without scene change");
    }
    yield return Until(()=>Character(target,index)&&ModelsReady(),"Character replication "+index);
    Check(Character(target,index)&&ModelsReady(),"Authoritative variant "+index+" reaches every 3D presentation");Write("variant-"+index+"-"+role);
    if(role=="host")yield return Until(()=>Enumerable.Range(1,total-1).All(i=>Has("variant-"+index+"-client"+i)),"Remote variant ack");
   }
   // Client input cannot select nonexistent models; this RPC only addresses the authenticated sender.
   if(role=="client1"){service.Roster.SetCharacterServerRpc(-1);service.Roster.SetCharacterServerRpc(999);yield return new WaitForSecondsRealtime(.5f);Check(Character(target,4),"Server rejects invalid character requests");Write("invalid-tested");}
   yield return Until(()=>Has("invalid-tested"),"Invalid selection test");
   if(role=="host"){CharacterSelection.SelectedIndex=2;yield return Until(()=>Character(service.Network.LocalClientId,2)&&ModelsReady(),"Host character update");Write("host-changed");}
   yield return Until(()=>Has("host-changed")&&Character(0,2)&&ModelsReady(),"Host choice replication");Check(Character(0,2),"Host selection is replicated to all clients");
   if(role=="host"){
    yield return Until(()=>Has("client-left")&&service.Roster.Members.Count==total-1&&ModelsReady(),"Departure visual cleanup");Check(ModelsReady(),"Departure removes its actor and rebalances teams without ghosts");Write("rejoin");
   }else if(role=="client1"){
    yield return Wait(service.CancelAndLeaveAsync(),"Client leave");yield return null;Check(!view.IsShowing&&!view.CharacterPopup.activeSelf,"Leaving closes the stage and popup");Write("client-left");yield return Until(()=>Has("rejoin"),"Rejoin signal");yield return Wait(service.SearchAsync(),"Rejoin connection");
   }
   yield return Until(()=>Has("rejoin")&&service.Roster && service.Roster.Members.Count==total&&ModelsReady(),"Late join appearance");
   Check(Character(0,2)&&ModelsReady(),"Late join receives current authoritative characters and original models");
   if(role=="client1")Check(Character(service.Network.LocalClientId,4),"Rejoining retains the saved selection");
   Write("late-ready-"+role);
   if(role=="host"){
    yield return Until(()=>Enumerable.Range(1,total-1).All(i=>Has("late-ready-client"+i)),"Late join acknowledgements");
    if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null){yield return new WaitForSecondsRealtime(1);CaptureLobby();yield return new WaitForSecondsRealtime(1);}
    Write("close");view.Leave();yield return Until(()=>service.CanNavigateAway,"Host leave");yield return null;Check(!view.IsShowing&&menu.ModeSelectionPanel.activeSelf,"Host exit restores the mode menu and destroys its stage");
   }else{
    yield return Until(()=>Has("close")&&service.CanNavigateAway,"Host disconnect");yield return null;Check(!view.IsShowing&&!view.CharacterPopup.activeSelf,"Host disconnect removes the stage and selector on clients");
   }
   Finish(true,"");
  }
  void CaptureLobby()
  {
   var camera=view.Stage.GetComponentsInChildren<Camera>(true).Single(c=>c.name=="LobbyCinematicCamera");
   var canvas=view.LobbyCanvas;var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float plane=canvas.planeDistance;
   var target=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1600,900,24);
   var ui=new GameObject("Validation UI camera").AddComponent<Camera>();ui.transform.position=new Vector3(50000,0,-10);ui.orthographic=true;ui.orthographicSize=5;ui.nearClipPlane=.1f;ui.farClipPlane=5;
   var overlay=ui.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
   var stack=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().cameraStack;
   void Save(string name){RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,name),image.EncodeToPNG());Destroy(image);}
   try{
    canvas.enabled=false;camera.targetTexture=rt;camera.Render();Save("World.png");canvas.enabled=true;
    canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=ui;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();
    overlay.renderType=UnityEngine.Rendering.Universal.CameraRenderType.Overlay;stack.Add(ui);
    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=rt});Save("Lobby.png");
    stack.Remove(ui);overlay.renderType=UnityEngine.Rendering.Universal.CameraRenderType.Base;ui.clearFlags=CameraClearFlags.SolidColor;ui.backgroundColor=new Color(.02f,.03f,.03f);ui.targetTexture=rt;ui.Render();Save("Interface.png");
    Write("visual-state.txt","Camera "+camera.transform.position+" "+camera.transform.eulerAngles+" enabled="+camera.enabled+" canvas="+canvas.enabled+" active="+canvas.gameObject.activeInHierarchy+" panel="+view.IsShowing+" actors="+Members().Count);
   }finally{stack.Remove(ui);canvas.enabled=true;canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=plane;camera.targetTexture=target;RenderTexture.active=active;rt.Release();Destroy(rt);Destroy(ui.gameObject);}
  }
  public sealed class LanDirectory:IPvPvESessions
  {
   readonly PvPvEMatchmaking owner;readonly bool host;
   public LanDirectory(PvPvEMatchmaking owner,bool host){this.owner=owner;this.host=host;}
   public Task InitializeAsync()=>Task.CompletedTask;
   public Task<IPvPvESession> FindOrCreateAsync(PvPvENetworkSettings settings){var n=owner.Network;n.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7794,"0.0.0.0");if(!(host?n.StartHost():n.StartClient()))throw new Exception("LAN start");return Task.FromResult<IPvPvESession>(new Lease(n,host));}
  }
  sealed class Lease:IPvPvESession
  {
   readonly NetworkManager network;public string Id=>"phase4-fixture";public bool IsHost{get;}public int ParticipantCount=>network&&network.IsServer?network.ConnectedClients.Count:1;public event Action Closed;public Task CloseAdmissionAsync()=>Task.CompletedTask;
   public Lease(NetworkManager n,bool h){network=n;IsHost=h;}public Task LeaveAsync(){if(network&&network.IsListening)network.Shutdown();return Task.CompletedTask;}
  }
 }
}
#endif
