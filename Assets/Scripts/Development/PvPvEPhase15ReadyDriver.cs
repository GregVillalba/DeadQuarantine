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
    public sealed class PvPvEPhase15ReadyDriver : MonoBehaviour
    {
        public PvPvENetworkSettings Settings; public GameObject PlayerPrefab; public GameObject[] GrenadePrefabs;
        string role, mode, folder; bool finished; Keyboard keyboard; PvPvEMatchmaking service;
        readonly List<string> checks=new(); readonly List<Metric> metrics=new();
        [Serializable] struct Metric { public string name; public float value; }
        [Serializable] sealed class Result {public bool ok;public string role,mode,error;public string[] checks;public Metric[] metrics;}
        NetworkManager Network=>NetworkManager.Singleton;
        PlayerHealth Local=>Network&&Network.LocalClient?.PlayerObject?Network.LocalClient.PlayerObject.GetComponent<PlayerHealth>():null;
        PlayerHealth[] Players=>FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None).Where(p=>p.IsSpawned).OrderBy(p=>p.OwnerClientId).ToArray();
        PlayerMovement Movement=>Local.GetComponent<PlayerMovement>();
        WeaponSwitcher Switcher=>Local.GetComponentInChildren<WeaponSwitcher>(true);
        NPCWeaponVendor Vendor=>FindFirstObjectByType<NPCWeaponVendor>();
        PassivePerkCatalog Catalog=>Settings.Loadouts.perks;
        static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static string Arg(string key){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null;}
        void OldAwake(){var c=new GameObject("Phase15 fixture selection").AddComponent<ConfiguracionPartidaSeleccionada>();c.escenarioSeleccionado=ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;c.modoSeleccionado=ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;}
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
        void Stamina(float value)=>typeof(PlayerMovement).GetProperty("CurrentStamina").GetSetMethod(true).Invoke(Movement,new object[]{value});
        void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        void Home(float z=-1){var cc=Local.GetComponent<CharacterController>();cc.enabled=false;Local.transform.SetPositionAndRotation(new Vector3(role=="host"?-2:2,.1f,z),Quaternion.identity);cc.enabled=true;Local.GetComponent<NetworkTransform>().Teleport(Local.transform.position,Local.transform.rotation,Local.transform.localScale);}
        PvPvEPreparationFlow Flow=>service&&service.Roster?service.Roster.GetComponent<PvPvEPreparationFlow>():null;
        PvPvEInitialClassView View=>PvPvEInitialClassView.Active;
        WeaponSwitcher held;int mapIndex;uint epoch;
        IEnumerator Run(){
            service=FindFirstObjectByType<PvPvEMatchmaking>();service.Changed+=()=>Write("connection-"+role+".txt",service.State+" | "+service.Error+" | roster="+(bool)service.Roster+" | flow="+(bool)Flow+" | members="+(service.Roster?service.Roster.Members.Count:0));
            CharacterSelection.SelectedIndex=role=="host"?1:2;
            var config=Instantiate(service.Settings);mapIndex=int.TryParse(Arg("-dq-map"),out int index)?index:0;
            config.MapVoteSeconds=12;config.PostVoteSeconds=1;config.MapLoadSeconds=120;config.PlayerPrepareSeconds=mode=="timeout"?8:60;config.PreMatchSeconds=5;
            service.ConfigureForTest(config,new DirectoryAdapter(service,config.PlayerPrefab,role=="host"));
            var store=new PvPvEClassStore(config.Loadouts);
            for(int i=0;i<5;i++){var c=config.Loadouts.Default(i);c.name=role+" CLASE "+i;c.primary="SemiRifle";c.secondary="AutoPistol";Check(store.Save(i,c,out _),"Five existing saved profiles available "+i);}
            var menu=service.GetComponent<PvPvEModeMenu>();var initial=menu.GetComponentInParent<Canvas>().transform.Find("menuPrincipalPanel");if(initial)initial.gameObject.SetActive(false);menu.OpenModes();menu.OpenPvPvE();
            if(role!="host")yield return Until(()=>Has("host-ready"),"Host ready",150);yield return Wait(service.SearchAsync());if(role=="host")Write("host-ready");Check(service.State==PvPvEConnectionState.Waiting,"Session connected: "+service.Error);
            yield return Until(()=>Flow&&service.Roster.Members.Count==2,"Original two-team roster",150);
            Check(!PvPvEPreparationFlow.LocalControlsLocked,"Preparation gate inactive in original lobby");
            var vote=service.Roster.GetComponent<PvPvEMapVoting>();yield return Until(()=>vote.Phase.Value==PvPvEVotePhase.Voting,"Vote open");vote.CastVoteServerRpc(mapIndex);
            // A client cannot declare initialization without a scene/player or forge an object token.
            Flow.ReportInitializedServerRpc(0,ulong.MaxValue);
            yield return Until(()=>Flow.Stage.Value!=PvPvEPrepareStage.Lobby,"Production voting starts NGO additive loading",180);
            Check(PvPvEPreparationFlow.LocalControlsLocked,"Loading begins with gameplay locked");
            yield return Until(()=>Local&&PvPvEMapContext.Active&&View&&View.IsShowing,"Real mode scene and initial selection loaded",180);
            epoch=Flow.GetComponent<PvPvEInitialClassSelection>().Epoch.Value;
            Check(SceneManager.GetSceneByName(config.MatchScene).isLoaded&&Local.gameObject.scene.name==config.MatchScene,"Player belongs to NGO-loaded mode scene");
            Check(PvPvEMapContext.Active.Scenario.TipoEscenario==config.Maps[mapIndex].Id&&Flow.Map.Value==config.Maps[mapIndex].Id,"Chosen scenario matches both original vote and active map");
            Check(Flow.TryGet(Network.LocalClientId,out var loaded)&&loaded.Has(PvPvEReadyFlags.Loaded),"Loaded flag comes from NGO scene completion");
            Check(FindObjectsByType<RoundManager>(FindObjectsSortMode.None).Length==0&&FindObjectsByType<ZombieAI>(FindObjectsSortMode.None).Length==0,"Cooperative round/enemy auto-start removed only in mode scene");
            Check(PvPvEPreparationFlow.LocalControlsLocked&&Movement.MovementLocked,"Controls remain locked across loading and class UI");
            if(mode=="timeout"&&role=="client1"){
                Write("client-idle");yield return Until(()=>service.CanNavigateAway,"Non-ready client is disconnected by bounded server deadline",150);Complete(true,"");yield break;
            }
            View.Choose(role=="host"?2:4);yield return Until(()=>View.PreviewsReady,"Initial class previews",120);yield return new WaitForSecondsRealtime(.2f);
            View.Confirm();yield return Until(()=>Flow.GetComponent<PvPvEInitialClassSelection>().TryGet(Network.LocalClientId,out _),"Initial class confirmed through production RPC",120);
            if(mode=="timeout"){
                yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Failed,"Timed-out participant cannot hold Ready forever",150);
                Check(!Flow.AllReady&&Flow.Participants.Count==1,"Timed-out participant removed instead of marked Ready");yield return ExitAndVerify();Complete(true,"");yield break;
            }
            if(mode=="disconnect"){
                yield return Barrier("disconnect");
                if(role=="client1"){yield return ExitAndVerify();Write("client-left");Complete(true,"");yield break;}
                yield return Until(()=>Has("client-left")&&Flow.Stage.Value==PvPvEPrepareStage.Failed,"Empty team aborts preparation",150);
                Check(Flow.Participants.Count==1&&!Flow.AllReady,"Departed player is not counted or awaited as Ready");yield return ExitAndVerify();Complete(true,"");yield break;
            }
            if(role=="client1"){
                Check(held&&!held.IsInitialized,"Actual owner weapon initialization is delayed, not a fake Ready value");
                yield return Until(()=>Flow.TryGet(0,out var host)&&host.Has(PvPvEReadyFlags.Ready),"Host Ready while client initializes",120);
                Flow.ReportInitializedServerRpc(0,Local.NetworkObjectId);Flow.ReportInitializedServerRpc(epoch,ulong.MaxValue);yield return new WaitForSecondsRealtime(.4f);
                Check(Flow.TryGet(Network.LocalClientId,out var own)&&!own.Has(PvPvEReadyFlags.Initialized)&&Flow.Stage.Value!=PvPvEPrepareStage.ReadyForPlacement,"Stale and foreign initialization tokens rejected");
                Write("client-held");yield return new WaitForSecondsRealtime(2);held.enabled=true;
            }else{
                yield return Until(()=>Has("client-held"),"Slow client reached class confirmation",150);
                Check(!Flow.AllReady&&Flow.Stage.Value!=PvPvEPrepareStage.ReadyForPlacement,"Host alone cannot finish preparation");
                Check(Flow.TryGet(1,out var client)&&client.Has(PvPvEReadyFlags.ClassConfirmed)&&!client.Has(PvPvEReadyFlags.Ready),"Class selection alone is not Ready");
                Check(!Flow.BeginCountdownAfterPlacementServer(),"Countdown cannot start before every participant is Ready");
            }
            yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.ReadyForPlacement&&Flow.AllReady,"All real participants loaded, initialized and selected",150);
            Check(Flow.Participants.Count==2&&Enumerable.Range(0,2).All(i=>Flow.Participants[i].Flags==(PvPvEReadyFlags)15),"Both peers see authoritative complete readiness");
            Check(Local.GetComponent<PlayerCharacter>().AppliedIndex==(role=="host"?1:2),"Lobby appearance survives loading independently of class");
            Check(!Local.EquipoDeClase.Value.Active&&PvPvEPreparationFlow.LocalControlsLocked,"Readiness hands off without applying phase16 loadouts or releasing combat");
            yield return new WaitForSecondsRealtime(.3f);Capture("all-ready.png",Flow.GetComponent<PvPvEPreparationView>().Panel.GetComponent<Canvas>());
            yield return Barrier("ready");
            // Test the future placement callback; no team-spawn or respawn algorithm is implemented.
            if(Network.IsServer){Check(Flow.BeginCountdownAfterPlacementServer(),"Trusted post-placement countdown callback works");Write("countdown");}
            yield return Until(()=>Has("countdown")&&Flow.Stage.Value==PvPvEPrepareStage.Countdown,"Synchronized countdown begins");
            if(role=="client1"){
                Flow.enabled=false;Local.GetComponent<PlayerCharacter>().CharacterIndex.Value=0;
                yield return Until(()=>Has("ready-revoked"),"Server revokes Ready after changed appearance",30);Flow.enabled=true;
            }else{
                yield return Until(()=>!Flow.AllReady&&Flow.Stage.Value==PvPvEPrepareStage.SelectingClasses,"Incorrect appearance cancels pre-match countdown",30);
                Check(!Flow.BeginCountdownAfterPlacementServer(),"Countdown cannot bypass revoked readiness");Write("ready-revoked");
            }
            yield return Until(()=>Flow.AllReady&&Flow.Stage.Value==PvPvEPrepareStage.ReadyForPlacement,"Approved appearance restores validated readiness",30);
            yield return Barrier("skin-restored");if(Network.IsServer){Check(Flow.BeginCountdownAfterPlacementServer(),"Countdown can retry only after everyone is valid again");Write("countdown-retry");}
            yield return Until(()=>Has("countdown-retry")&&Flow.Stage.Value==PvPvEPrepareStage.Countdown,"Final synchronized countdown");
            double deadline=Flow.Deadline.Value;Check(deadline>Network.ServerTime.Time&&PvPvEPreparationFlow.LocalControlsLocked,"Shared server deadline retains gameplay lock");
            yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Prepared,"Five-second server countdown completes",20);
            Check(Flow.AllReady&&PvPvEPreparationFlow.LocalControlsLocked,"Countdown does not prematurely start phase16 gameplay");
            yield return Barrier("exit");yield return ExitAndVerify();Complete(true,"");
        }
        IEnumerator ExitAndVerify(){
            yield return Wait(service.CancelAndLeaveAsync());yield return Until(()=>!SceneManager.GetSceneByName(service.Settings.MatchScene).isLoaded,"Mode scene unloaded on session exit",150);
            Check(!PvPvEPreparationFlow.Active&&!PvPvEPreparationFlow.LocalControlsLocked&&!PvPvEInitialClassView.Active,"Session exit restores input and destroys preparation UI");
            Check(service.CanNavigateAway,"Original menu/session controls restored");
        }
        sealed class DelayedPlayer:INetworkPrefabInstanceHandler {
            readonly GameObject prefab;readonly PvPvEPhase15ReadyDriver driver;
            public DelayedPlayer(GameObject prefab,PvPvEPhase15ReadyDriver driver){this.prefab=prefab;this.driver=driver;}
            public NetworkObject Instantiate(ulong owner,Vector3 position,Quaternion rotation){var go=UnityEngine.Object.Instantiate(prefab,position,rotation);if(owner==driver.Network.LocalClientId){driver.held=go.GetComponentInChildren<WeaponSwitcher>(true);driver.held.enabled=false;}return go.GetComponent<NetworkObject>();}
            public void Destroy(NetworkObject instance){if(instance)UnityEngine.Object.Destroy(instance.gameObject);}
        }
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
            public Task<IPvPvESession> FindOrCreateAsync(PvPvENetworkSettings settings){var n=service.Network;if(!host&&Arg("-dq-case")=="slow")n.PrefabHandler.AddHandler(prefab,new DelayedPlayer(prefab,UnityEngine.Object.FindFirstObjectByType<PvPvEPhase15ReadyDriver>()));n.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7799,"0.0.0.0");if(!(host?n.StartHost():n.StartClient()))throw new Exception("NGO start failed");return Task.FromResult<IPvPvESession>(new Lease(n,host));}
        }
        sealed class Lease:IPvPvESession
        {
            readonly NetworkManager n;public string Id=>"phase15-native";public bool IsHost{get;}public int ParticipantCount=>n&&n.IsServer?n.ConnectedClients.Count:1;public event Action Closed{add{}remove{}}
            public Lease(NetworkManager n,bool host){this.n=n;IsHost=host;}public Task CloseAdmissionAsync()=>Task.CompletedTask;public Task LeaveAsync(){if(n&&n.IsListening)n.Shutdown();return Task.CompletedTask;}
        }
    }
}
#endif
