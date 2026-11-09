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
    public sealed class PvPvEPhase23MatchDriver : MonoBehaviour
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


        PlayerHealth[] Players=>FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None).Where(p=>p.IsSpawned).OrderBy(p=>p.OwnerClientId).ToArray();
        PlayerHealth Actor(int id)=>Players.FirstOrDefault(p=>p.OwnerClientId==(ulong)id);
        PvPvEView Hud=>PvPvEView.Active;PvPvENetworkSettings config;
        string Title=>Hud.CanvasRoot.transform.Find("Scoreboard/Title").GetComponent<TMPro.TMP_Text>().text;
        double previousStart;int rounds;
        void FireAt(Vector3 point){var look=Local.GetComponentInChildren<PlayerLook>(true);look.enabled=false;var camera=Local.GetComponentsInChildren<Camera>(true).First(c=>c.CompareTag("MainCamera"));camera.transform.rotation=Quaternion.LookRotation(point-camera.transform.position);var gun=Local.GetComponentInChildren<WeaponSwitcher>(true).CurrentWeapon;gun.GetType().GetField("currentSpread",Fields).SetValue(gun,-4f);gun.SendMessage("OnFireSemiAuto",default(UnityEngine.InputSystem.InputAction.CallbackContext));}
        void Position(){Local.GetComponent<PlayerMovement>().enabled=false;Local.GetComponent<CharacterController>().enabled=false;Local.transform.SetPositionAndRotation(new Vector3(50000+(role=="host"?-4:0),.08f,role=="host"?0:2),Quaternion.identity);Local.GetComponent<NetworkTransform>().Teleport(Local.transform.position,Quaternion.identity,Local.transform.localScale);}
        IEnumerator Run(){
            service=FindFirstObjectByType<PvPvEMatchmaking>();CharacterSelection.SelectedIndex=role=="host"?1:2;config=Instantiate(service.Settings);config.MaxZombiesAlive=0;config.MapVoteSeconds=12;config.PostVoteSeconds=1;config.PreMatchSeconds=1;config.ResultsSeconds=8;config.SpawnProtectionSeconds=0;config.RespawnSeconds=1.5f;config.CorpseSeconds=.4f;config.MatchDurationSeconds=mode=="time"?12:mode=="overtime"?4:90;config.KillLimit=mode=="limit"?1:50;
            service.ConfigureForTest(config,new DirectoryAdapter(service,config.PlayerPrefab,role=="host"));var store=new PvPvEClassStore(config.Loadouts);
            for(int i=0;i<5;i++){var c=config.Loadouts.Default(i);c.primary="SemiRifle";c.secondary="AutoPistol";Check(store.Save(i,c,out _),"Saved class "+i);}
            var menu=service.GetComponent<PvPvEModeMenu>();var initial=menu.GetComponentInParent<Canvas>().transform.Find("menuPrincipalPanel");if(initial)initial.gameObject.SetActive(false);menu.OpenModes();menu.OpenPvPvE();if(role!="host")yield return Until(()=>Has("host-ready"),"Host ready",150);yield return Wait(service.SearchAsync());Write(role+"-connected");if(role=="host")Write("host-ready");yield return Until(()=>Flow&&service.Roster.Members.Count==2,"Two actual peers",150);
            int games=mode=="limit"?2:1;
            for(rounds=0;rounds<games;rounds++){
                yield return StartRound();
                if(mode=="empty"){
                    yield return Barrier("depart");if(role=="client1"){yield return ExitAndVerify();Write("client-left");Complete(true,"");yield break;}
                    yield return Until(()=>Has("client-left")&&Combat.ScoreLocked.Value,"Empty rival team ends match",90);Check(Flow.Stage.Value==PvPvEPrepareStage.Playing&&Combat.Winner.Value==PvPvETeam.Alpha,"Empty team resolves victory instead of preparation failure");
                }else{
                    if(mode=="overtime"){
                        yield return Until(()=>Combat.Resolution.Value==PvPvEMatchResolution.Overtime,"Tie enters authoritative sudden death",20);Check(!Combat.ScoreLocked.Value&&Combat.RemainingSeconds==0,"Overtime remains playable at zero time");yield return Until(()=>Hud.MatchText.Contains("OVERTIME"),"Overtime HUD");Capture("overtime.png",Hud.CanvasRoot.GetComponent<Canvas>());yield return Barrier("overtime-shown");
                        if(role=="host"){Combat.RecordZombieKillServer(0);Actor(1).TakeDamage(99999,true);}yield return Until(()=>Combat.TryStats(1,out var row)&&row.Deaths==1,"Environment death during overtime");Check(Combat.AlphaKills.Value==0&&Combat.BravoKills.Value==0&&!Combat.ScoreLocked.Value,"Zombie points and environmental death cannot win overtime");yield return Barrier("non-pvp-observed");
                        yield return Until(()=>Actor(1)&&Combat.Alive(Actor(1)),"Overtime respawn",40);Position();yield return Barrier("overtime-back");yield return new WaitForSecondsRealtime(.4f);
                    }
                    if(role=="host"){Actor(1).SetMaxHealth(50,true);FireAt(Actor(1).transform.position+Vector3.up);}
                    yield return Until(()=>Combat.AlphaKills.Value==1,"Original gun produces valid PvP kill",30);
                    if(mode=="time"){Check(!Combat.ScoreLocked.Value,"One kill below target does not end regulation");yield return Until(()=>Combat.ScoreLocked.Value,"Time expiry awards leader",25);}
                    else yield return Until(()=>Combat.ScoreLocked.Value,"Valid kill resolves match");
                    Check(Combat.Winner.Value==PvPvETeam.Alpha&&Combat.BravoKills.Value==0,"All peers agree winner and team kill score");Check(Combat.ResultReason.Value.ToString()==(mode=="time"?"Tiempo agotado.":mode=="overtime"?"Muerte súbita.":"Objetivo alcanzado."),"Correct end condition synchronized");
                    if(role=="host"){double count=Enumerable.Range(0,Combat.Stats.Count).Sum(i=>Combat.Stats[i].Points);Combat.ApplyDamageServer(1,Actor(0),99999,PvPvEDamageKind.Bullet,false,0,"SemiRifle");Combat.RecordZombieKillServer(1);if(Actor(1))Combat.RecordDeathServer(Actor(1));Check(Enumerable.Range(0,Combat.Stats.Count).Sum(i=>Combat.Stats[i].Points)==count&&Combat.AlphaKills.Value==1&&Combat.BravoKills.Value==0,"Opposite lethal hit duplicate death and zombie score cannot alter final result");}
                }
                yield return Until(()=>Hud.ScoreboardVisible&&Title.Contains(role=="host"?"VICTORIA":"DERROTA"),"Forced result table without TAB");Check(PvPvEPreparationFlow.LocalControlsLocked&&Combat.MatchStoppedAt.Value>=Combat.MatchStartedAt.Value,"Controls blocked and final clock frozen");Capture("results-"+rounds+".png",Hud.CanvasRoot.GetComponent<Canvas>());
                if(mode!="empty")yield return Barrier("result-"+rounds);
                yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Lobby&&!SceneManager.GetSceneByName(config.MatchScene).isLoaded,"Automatic synchronized map unload and same-room return",180);yield return new WaitForSecondsRealtime(.3f);
                Check(Network.IsConnectedClient&&!service.CanNavigateAway&&!Network.LocalClient.PlayerObject,"Room connection persists without stale player");Check(!Combat.ScoreLocked.Value&&Combat.Winner.Value==PvPvETeam.None&&Combat.MatchStartedAt.Value<0&&Combat.MatchDeadline.Value<0,"Result and shared clock reset for next match");Check(Combat.AlphaKills.Value==0&&Combat.BravoKills.Value==0&&Enumerable.Range(0,Combat.Stats.Count).All(i=>Combat.Stats[i].Kills==0&&Combat.Stats[i].Deaths==0&&Combat.Stats[i].Points==0)&&Combat.KillFeed.Count==0,"Score stats and feed reset exactly once");
                Check(Deployment.Players.Count==0&&Flow.Participants.Count==0&&Flow.GetComponent<PvPvEInitialClassSelection>().Confirmed.Count==0,"Placement readiness and confirmed classes cleared");Check(!Hud.CanvasRoot.activeSelf&&!PvPvEPreparationFlow.LocalControlsLocked,"Match overlays and input gates released in lobby");
                Check(FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Count(e=>e.isActiveAndEnabled)==1,"One active UI event system after restoring menu");
                yield return Until(()=>service.GetComponent<PvPvELobbyView>().IsShowing,"Original 3D lobby restored",40);Capture("lobby-"+rounds+".png",service.GetComponent<PvPvELobbyView>().LobbyCanvas);
                if(role=="host")Check(Has("admission-open"),"Existing room admission reopened through session adapter");
                if(mode=="empty")Check(service.Roster.GetComponent<PvPvEMapVoting>().Phase.Value==PvPvEVotePhase.WaitingForTeams,"Empty lobby waits instead of starting impossible vote");
                else {Check(service.Roster.Members.Count==2&&service.Roster.TryGetMember(Network.LocalClientId,out var member)&&member.CharacterIndex==(role=="host"?1:2),"Participants and selected appearance preserved");Check(service.Roster.GetComponent<PvPvEMapVoting>().Phase.Value==PvPvEVotePhase.Voting,"New map vote begins in same room");yield return Barrier("lobby-"+rounds);}
            }
            if(mode!="empty")yield return Barrier("exit");yield return ExitAndVerify();Complete(true,"");
        }
        IEnumerator StartRound(){
            var voting=service.Roster.GetComponent<PvPvEMapVoting>();yield return Until(()=>voting.Phase.Value==PvPvEVotePhase.Voting,"Vote",60);voting.CastVoteServerRpc(1);yield return Until(()=>Local&&View&&View.IsShowing,"Class for new map cycle",180);yield return Until(()=>View.PreviewsReady,"Class previews",120);View.Choose(rounds%5);View.Confirm();yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Playing&&!PvPvEPreparationFlow.LocalControlsLocked,"Actual synchronized match start",150);
            yield return Until(()=>Combat.MatchStartedAt.Value>previousStart,"Fresh authoritative match clock");previousStart=Combat.MatchStartedAt.Value;Check(Combat.AlphaKills.Value==0&&Combat.BravoKills.Value==0,"Fresh game starts without prior score");
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(50000,-.5f,20);floor.transform.localScale=new Vector3(30,1,100);SceneManager.MoveGameObjectToScene(floor,SceneManager.GetSceneByName(config.MatchScene));Position();yield return Barrier("placed-"+rounds);yield return new WaitForSecondsRealtime(.4f);
        }
        IEnumerator ExitAndVerify(){yield return Wait(service.CancelAndLeaveAsync());yield return Until(()=>!SceneManager.GetSceneByName(service.Settings.MatchScene).isLoaded,"Map unloaded",120);Check(!PvPvECombat.Active&&!PvPvEPreparationFlow.LocalControlsLocked,"Combat authority and input state cleaned on exit");}
        void Capture(string name,Canvas canvas)
        {
            canvas=canvas.rootCanvas;
            var mode=canvas.renderMode;var previous=canvas.worldCamera;float plane=canvas.planeDistance;var camera=new GameObject("Perks validation UI camera").AddComponent<Camera>();camera.transform.position=new Vector3(50000,0,-10);camera.orthographic=true;camera.nearClipPlane=.1f;camera.farClipPlane=5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.04f,.045f);
            var target=new RenderTexture(1600,900,24);var active=RenderTexture.active;
            try{canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,role+"-"+name),image.EncodeToPNG());Destroy(image);}
            finally{canvas.renderMode=mode;canvas.worldCamera=previous;canvas.planeDistance=plane;RenderTexture.active=active;target.Release();Destroy(target);Destroy(camera.gameObject);}
        }
        sealed class DirectoryAdapter:IPvPvESessions
        {
            readonly PvPvEMatchmaking service;readonly GameObject prefab;readonly bool host;
            public DirectoryAdapter(PvPvEMatchmaking service,GameObject prefab,bool host){this.service=service;this.prefab=prefab;this.host=host;}
            public Task InitializeAsync()=>Task.CompletedTask;
            public Task<IPvPvESession> FindOrCreateAsync(PvPvENetworkSettings settings){var n=service.Network;n.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7799,"0.0.0.0");if(!(host?n.StartHost():n.StartClient()))throw new Exception("NGO start failed");return Task.FromResult<IPvPvESession>(new Lease(n,host));}
        }
        sealed class Lease:IPvPvESession,IPvPvEReopenableSession
        {
            readonly NetworkManager n;public string Id=>"phase23-native";public bool IsHost{get;}public int ParticipantCount=>n&&n.IsServer?n.ConnectedClients.Count:1;public event Action Closed{add{}remove{}}
            public Lease(NetworkManager n,bool host){this.n=n;IsHost=host;}public Task CloseAdmissionAsync()=>Task.CompletedTask;public Task OpenAdmissionAsync(){File.WriteAllText(Path.Combine(Arg("-dq-output"),"admission-open"),"ok");return Task.CompletedTask;}public Task LeaveAsync(){if(n&&n.IsListening)n.Shutdown();return Task.CompletedTask;}
        }
    }
}
#endif
