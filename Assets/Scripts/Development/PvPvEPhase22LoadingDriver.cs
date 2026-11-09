#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.Networking;
using DeadQuarantine.PvPvE.UI;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

namespace DeadQuarantine.Development
{
    public sealed class PvPvEPhase22LoadingDriver : MonoBehaviour
    {
        public PvPvENetworkSettings Settings; public GameObject PlayerPrefab; public GameObject[] GrenadePrefabs;
        string role, mode, folder; bool finished; Keyboard keyboard; PvPvEMatchmaking service;
        readonly List<string> checks=new(); readonly List<Metric> metrics=new();
        [Serializable] struct Metric { public string name; public float value; }
        [Serializable] sealed class Result {public bool ok;public string role,mode,error;public string[] checks;public Metric[] metrics;}
        NetworkManager Network=>NetworkManager.Singleton;
        PlayerHealth Local=>Network&&Network.LocalClient?.PlayerObject?Network.LocalClient.PlayerObject.GetComponent<PlayerHealth>():null;
        static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static string Arg(string key){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null;}
        void Awake(){DontDestroyOnLoad(gameObject);}
        void Start(){role=Arg("-dq-role");if(role==null){enabled=false;return;}mode=Arg("-dq-case")??"slow";folder=Arg("-dq-output");Directory.CreateDirectory(folder);Application.runInBackground=true;PlayerPrefs.DeleteAll();StartCoroutine(Safe(Run()));}
        IEnumerator Safe(IEnumerator r){while(!finished){object current;try{if(!r.MoveNext())yield break;current=r.Current;}catch(Exception e){Complete(false,e.ToString());yield break;}if(current is IEnumerator child)yield return Safe(child);else yield return current;}}
        IEnumerator Until(Func<bool> test,string label,float seconds=35){float end=Time.realtimeSinceStartup+seconds;while(!test()){if(Time.realtimeSinceStartup>end)throw new TimeoutException(label);yield return null;}}
        IEnumerator Wait(Task task){yield return Until(()=>task.IsCompleted,"Network operation");if(task.IsFaulted)throw task.Exception;}
        void Write(string name,string value="ok")=>File.WriteAllText(Path.Combine(folder,name),value);
        bool Has(string name)=>File.Exists(Path.Combine(folder,name));
        IEnumerator Barrier(string key){Write(role+"-"+key);yield return Until(()=>Has((role=="host"?"client1":"host")+"-"+key),key);}
        void Check(bool condition,string label){if(!condition)throw new Exception(label);checks.Add(label);Save("progress-");}
        void Save(string prefix,bool ok=false,string error=""){Write(prefix+role+".json",JsonUtility.ToJson(new Result{ok=ok,role=role,mode=mode,error=error,checks=checks.ToArray(),metrics=metrics.ToArray()},true));}
        void Complete(bool ok,string error){if(finished)return;finished=true;Save("result-",ok,error);StartCoroutine(Quit(ok));}
        IEnumerator Quit(bool ok){if(service&&!service.CanNavigateAway)yield return Wait(service.CancelAndLeaveAsync());else if(Network&&Network.IsListening)Network.Shutdown();yield return new WaitForSecondsRealtime(.3f);Application.Quit(ok?0:1);}
        void MetricValue(string name,float value){metrics.Add(new Metric{name=name,value=value});}
        static object Field(object obj,string name)=>obj.GetType().GetField(name,Fields).GetValue(obj);
        PvPvEPreparationFlow Flow=>service&&service.Roster?service.Roster.GetComponent<PvPvEPreparationFlow>():null;
        PvPvETeamDeployment Deployment=>service&&service.Roster?service.Roster.GetComponent<PvPvETeamDeployment>():null;
        PvPvECombat Combat=>service&&service.Roster?service.Roster.GetComponent<PvPvECombat>():null;
        PvPvEInitialClassView View=>PvPvEInitialClassView.Active;

        PvPvELoadingScreen Loading=>Flow?Flow.GetComponent<PvPvELoadingScreen>():null;
        int mapIndex;bool loadingObserved;
        void LateUpdate(){if(finished||loadingObserved||!Loading||!Loading.IsShowing||Flow.Stage.Value!=PvPvEPrepareStage.LoadingMap)return;try{loadingObserved=true;CaptureLoading("loading-map.png");}catch(Exception e){Complete(false,e.ToString());}}
        IEnumerator Run(){
            service=FindFirstObjectByType<PvPvEMatchmaking>();CharacterSelection.SelectedIndex=role=="host"?1:2;mapIndex=int.Parse(Arg("-dq-map")??"1");
            var config=Instantiate(service.Settings);config.MaxZombiesAlive=0;config.MapVoteSeconds=15;config.PostVoteSeconds=12;config.PreMatchSeconds=3;config.PlayerPrepareSeconds=90;
            service.ConfigureForTest(config,new DirectoryAdapter(service,config.PlayerPrefab,role=="host"));var store=new PvPvEClassStore(config.Loadouts);
            for(int i=0;i<5;i++){var c=config.Loadouts.Default(i);c.primary="SemiRifle";c.secondary="AutoPistol";Check(store.Save(i,c,out _),"Existing saved class "+i);}
            var menu=service.GetComponent<PvPvEModeMenu>();var initial=menu.GetComponentInParent<Canvas>().transform.Find("menuPrincipalPanel");if(initial)initial.gameObject.SetActive(false);menu.OpenModes();menu.OpenPvPvE();
            if(role!="host")yield return Until(()=>Has("host-ready"),"Host ready",150);yield return Wait(service.SearchAsync());Write(role+"-connected");if(role=="host")Write("host-ready");
            yield return Until(()=>Flow&&service.Roster.Members.Count==2,"Two actual peers",150);
            var vote=service.Roster.GetComponent<PvPvEMapVoting>();yield return Until(()=>vote.Phase.Value==PvPvEVotePhase.Voting,"Voting");Check(Loading&&!Loading.IsShowing,"Loading art hidden while voting");vote.CastVoteServerRpc(mapIndex);
            yield return Until(()=>Loading.IsShowing&&vote.Phase.Value==PvPvEVotePhase.VoteResult,"Winner preview",60);yield return new WaitForSecondsRealtime(.2f);
            Check(Loading.Preview==config.Maps[mapIndex].Preview&&Loading.MapText==config.Maps[mapIndex].Name,"Winner uses current configured map name and preview");Check(Loading.StatusText.StartsWith("LA PARTIDA COMIENZA EN"),"Distinct post-vote server countdown displayed");
            Check(UnityEngine.EventSystems.EventSystem.current&&Flow.GetComponent<PvPvEPreparationView>().Panel.activeInHierarchy,"Existing leave action and event system remain available");
            CaptureLoading("vote-result.png");Screen.SetResolution(1280,800,false);yield return new WaitForSecondsRealtime(.3f);CheckBounds();CaptureLoading("vote-result-16x10.png");yield return Barrier("preview");
            if(mode=="leave"){
                if(role=="client1"){Flow.GetComponent<PvPvEPreparationView>().Panel.GetComponentInChildren<Button>().onClick.Invoke();yield return Until(()=>service.CanNavigateAway&&!Flow,"Leave action closes session");yield return null;Check(!FindFirstObjectByType<PvPvELoadingScreen>(),"Loading canvas removed when leaving winner preview");Write("client-left");Complete(true,"");yield break;}
                yield return Until(()=>Has("client-left")&&vote.Phase.Value==PvPvEVotePhase.WaitingForTeams,"Empty lobby resets vote");yield return Until(()=>!Loading.IsShowing,"Preview hidden after winner is invalidated");Check(vote.Winner.Value==-1,"Cancelled result has no stale map preview");yield return ExitAndVerify();Complete(true,"");yield break;
            }
            yield return Until(()=>Local&&View&&View.IsShowing,"NGO map and class selection",180);yield return new WaitForSecondsRealtime(.2f);
            Check(loadingObserved,"Recovered screen presented during actual NGO map loading");Check(!Loading.IsShowing&&PvPvEPreparationFlow.LocalControlsLocked,"Class selection is uncovered and gameplay stays locked");
            Check(UnityEngine.EventSystems.EventSystem.current&&UnityEngine.EventSystems.EventSystem.current.isActiveAndEnabled,"Input system survives original menu deactivation");
            Check(PvPvEMapContext.Active.Scenario.TipoEscenario==config.Maps[mapIndex].Id,"Actual loaded scenario matches loading preview");yield return Until(()=>View.PreviewsReady,"Existing class models",120);yield return Barrier("class");
            if(role=="host"){View.Choose(0);View.Confirm();yield return Until(()=>Flow.TryGet(Network.LocalClientId,out var entry)&&entry.Has(PvPvEReadyFlags.Ready),"Host ready");Write("host-selected");yield return new WaitForSecondsRealtime(1);Check(!Flow.AllReady&&Flow.Stage.Value!=PvPvEPrepareStage.Playing,"Loading art does not bypass slower class participant");Write("host-waited");}
            else {yield return Until(()=>Has("host-waited"),"Host waits for client");View.Choose(1);View.Confirm();}
            yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Countdown,"Original synchronized countdown",150);yield return new WaitForSecondsRealtime(.1f);Check(!Loading.IsShowing&&PvPvEPreparationFlow.LocalControlsLocked,"Loading image yields to pre-match countdown");
            yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Playing&&!PvPvEPreparationFlow.LocalControlsLocked,"Actual start");yield return new WaitForSecondsRealtime(.2f);
            Check(!Loading.IsShowing&&!Flow.GetComponent<PvPvEPreparationView>().Panel.activeSelf&&PvPvEView.Active,"Loading and preparation overlays hidden in match; current HUD retained");
            yield return Barrier("playing");yield return ExitAndVerify();Check(!FindFirstObjectByType<PvPvELoadingScreen>(),"Loading component and canvas cleaned on exit");Complete(true,"");
        }
        void CheckBounds(){Canvas.ForceUpdateCanvases();var canvas=Loading.Panel.GetComponent<Canvas>();foreach(string name in new[]{"MapInformation/Mode","MapInformation/MapName","MapInformation/State"}){var r=(RectTransform)canvas.transform.Find(name);var corners=new Vector3[4];r.GetWorldCorners(corners);Check(corners.All(c=>{var pos=RectTransformUtility.WorldToScreenPoint(null,c);return pos.x>=-1&&pos.y>=-1&&pos.x<=Screen.width+1&&pos.y<=Screen.height+1;}),"Loading text fits 16:10: "+name);}}
        void CaptureLoading(string name){
            var prep=Flow.GetComponent<PvPvEPreparationView>().Panel;var list=new[]{Loading.Panel.GetComponent<Canvas>(),prep?prep.GetComponent<Canvas>():null}.Where(c=>c).ToArray();var modes=list.Select(c=>c.renderMode).ToArray();var cameras=list.Select(c=>c.worldCamera).ToArray();var planes=list.Select(c=>c.planeDistance).ToArray();var camera=new GameObject("Loading validation camera").AddComponent<Camera>();camera.transform.position=new Vector3(90000,0,-10);camera.orthographic=true;camera.nearClipPlane=.1f;camera.farClipPlane=5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;var target=new RenderTexture(1600,900,24);var active=RenderTexture.active;
            try{foreach(var c in list){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}Canvas.ForceUpdateCanvases();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,role+"-"+name),image.EncodeToPNG());Destroy(image);}
            finally{for(int i=0;i<list.Length;i++){list[i].renderMode=modes[i];list[i].worldCamera=cameras[i];list[i].planeDistance=planes[i];}RenderTexture.active=active;target.Release();Destroy(target);Destroy(camera.gameObject);}
        }
        IEnumerator ExitAndVerify(){yield return Wait(service.CancelAndLeaveAsync());yield return Until(()=>!SceneManager.GetSceneByName(service.Settings.MatchScene).isLoaded,"Map unloaded",120);Check(!PvPvECombat.Active&&!PvPvEPreparationFlow.LocalControlsLocked,"Combat authority and input state cleaned on exit");}
        sealed class DirectoryAdapter:IPvPvESessions
        {
            readonly PvPvEMatchmaking service;readonly GameObject prefab;readonly bool host;
            public DirectoryAdapter(PvPvEMatchmaking service,GameObject prefab,bool host){this.service=service;this.prefab=prefab;this.host=host;}
            public Task InitializeAsync()=>Task.CompletedTask;
            public Task<IPvPvESession> FindOrCreateAsync(PvPvENetworkSettings settings){var n=service.Network;n.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7799,"0.0.0.0");if(!(host?n.StartHost():n.StartClient()))throw new Exception("NGO start failed");return Task.FromResult<IPvPvESession>(new Lease(n,host));}
        }
        sealed class Lease:IPvPvESession
        {
            readonly NetworkManager n;public string Id=>"phase22-native";public bool IsHost{get;}public int ParticipantCount=>n&&n.IsServer?n.ConnectedClients.Count:1;public event Action Closed{add{}remove{}}
            public Lease(NetworkManager n,bool host){this.n=n;IsHost=host;}public Task CloseAdmissionAsync()=>Task.CompletedTask;public Task LeaveAsync(){if(n&&n.IsListening)n.Shutdown();return Task.CompletedTask;}
        }
    }
}
#endif
