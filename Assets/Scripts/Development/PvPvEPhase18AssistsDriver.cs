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
    public sealed class PvPvEPhase18AssistsDriver : MonoBehaviour
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
        void OldAwake(){var c=new GameObject("Phase18 fixture selection").AddComponent<ConfiguracionPartidaSeleccionada>();c.escenarioSeleccionado=ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;c.modoSeleccionado=ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;}
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
        PvPvETeamDeployment Deployment=>service&&service.Roster?service.Roster.GetComponent<PvPvETeamDeployment>():null;
        PvPvECombat Combat=>service&&service.Roster?service.Roster.GetComponent<PvPvECombat>():null;
        PvPvEInitialClassView View=>PvPvEInitialClassView.Active;
        int who;GameObject wall;
        PlayerHealth Actor(int id)=>Players.FirstOrDefault(p=>p.OwnerClientId==(ulong)id);
        PvPvECombatStats Stats(int id){Combat.TryStats((ulong)id,out var row);return row;}
        IEnumerator Sync(string key){Write(role+"-"+key);yield return Until(()=>new[]{"host","client1","client2"}.All(r=>Has(r+"-"+key)),key,90);}
        IEnumerator Do(int id,string key,Action action){if(who==id){action();Write(key);}yield return Until(()=>Has(key),key,90);yield return new WaitForSecondsRealtime(.15f);}
        void Place(float x,float z){var cc=Local.GetComponent<CharacterController>();cc.enabled=false;Local.transform.SetPositionAndRotation(new Vector3(50000+x,.08f,z),Quaternion.identity);cc.enabled=true;Local.GetComponent<NetworkTransform>().Teleport(Local.transform.position,Local.transform.rotation,Local.transform.localScale);}
        void Layout(int arrangement){if(arrangement==0)Place(who==2?6:0,who==1?2:0);else if(arrangement==1)Place(who==2?0:0,who==1?4:who==2?2:0);else if(arrangement==2)Place(who==2?6:0,who==1?60:0);else Place(who==2?6:0,who==1?2:0);}
        void FireAt(Vector3 point){var look=Local.GetComponentInChildren<PlayerLook>(true);look.enabled=false;var camera=Local.GetComponentsInChildren<Camera>(true).First(c=>c.CompareTag("MainCamera"));camera.transform.rotation=Quaternion.LookRotation(point-camera.transform.position);FieldSet(Switcher.CurrentWeapon,"currentSpread",-4f);Switcher.CurrentWeapon.SendMessage("OnFireSemiAuto",default(UnityEngine.InputSystem.InputAction.CallbackContext));}
        static void FieldSet(object obj,string name,object value)=>obj.GetType().GetField(name,Fields).SetValue(obj,value);
        void RawShot(int index,uint sequence,Vector3 origin,Vector3[] targets)=>typeof(NetworkWeaponPresentation).GetMethod("ShotServerRpc",Fields).Invoke(Local.GetComponent<NetworkWeaponPresentation>(),new object[]{index,sequence,origin,targets});
        IEnumerator Run(){
            who=role=="host"?0:role=="client1"?1:2;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            service=FindFirstObjectByType<PvPvEMatchmaking>();CharacterSelection.SelectedIndex=who%3;
            var config=Instantiate(service.Settings);config.KillLimit=8;config.MapVoteSeconds=35;config.PostVoteSeconds=1;config.PreMatchSeconds=1;config.RespawnSeconds=1.5f;config.CorpseSeconds=.4f;config.SpawnProtectionSeconds=.3f;
            service.ConfigureForTest(config,new DirectoryAdapter(service,config.PlayerPrefab,who==0));var store=new PvPvEClassStore(config.Loadouts);
            for(int i=0;i<5;i++){var c=config.Loadouts.Default(i);c.primary="SemiRifle";c.secondary="AutoPistol";Check(store.Save(i,c,out _),"Existing valid class "+i);}
            var menu=service.GetComponent<PvPvEModeMenu>();var initial=menu.GetComponentInParent<Canvas>().transform.Find("menuPrincipalPanel");if(initial)initial.gameObject.SetActive(false);menu.OpenModes();menu.OpenPvPvE();
            if(who!=0)yield return Until(()=>Has("host-ready"),"Host ready",150);yield return Wait(service.SearchAsync());who=(int)Network.LocalClientId;Write(role+"-connected");if(who==0)Write("host-ready");
            yield return Until(()=>Combat&&service.Roster.Members.Count==3,"Three actual connected players",150);
            Check(service.Roster.Count(PvPvETeam.Alpha)==2&&service.Roster.Count(PvPvETeam.Bravo)==1,"Real 2v1 assignment");
            var voting=service.Roster.GetComponent<PvPvEMapVoting>();yield return Until(()=>voting.Phase.Value==PvPvEVotePhase.Voting,"Vote");voting.CastVoteServerRpc(0);
            yield return Until(()=>Local&&View&&View.IsShowing,"Mode scene and initial selection",180);yield return Until(()=>View.PreviewsReady,"Class previews",120);View.Choose(0);View.Confirm();
            yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Playing&&!PvPvEPreparationFlow.LocalControlsLocked,"Real deployed/equipped start",150);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Phase18 validation floor";floor.GetComponent<MeshRenderer>().sharedMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.15f,.19f,.2f)};floor.transform.position=new Vector3(50000,-.5f,30);floor.transform.localScale=new Vector3(30,1,150);
            Layout(0);yield return Sync("arena");yield return new WaitForSecondsRealtime(.7f);
            Check(Players.Length==3&&Combat.Stats.Count==3,"One original PlayerObject and persistent stats row per participant");

            yield return Duel(1,90);
            yield return Do(0,"damage-1",()=>FireAt(Actor(1).transform.position+Vector3.up));
            yield return Until(()=>Actor(1).CurrentHealth.Value==20,"Original gun deals actual 70 damage");
            yield return Kill(2,1);
            Check(Stats(0).Assists==1&&Stats(0).Points==50&&Stats(2).Kills==1&&Stats(2).Assists==0,"Damage assist goes to contributor, not killer");
            yield return Feedback(0,"damage-assist.png");
            yield return Duel(2,50);
            yield return Do(0,"stun-2",()=>Combat.StunAreaServer(2,Actor(1).transform.position,3,2));
            yield return Until(()=>Stats(1).StunnedUntil>Network.ServerTime.Time,"Real stun applied by authority");
            Check(Actor(1).CurrentHealth.Value==50,"Utility contributor causes zero damage");
            yield return Kill(0,2);
            Check(Stats(2).Assists==1&&Stats(2).Points==150&&Stats(0).Kills==1,"Remote zero-damage utility assist synchronized");
            yield return Feedback(2,"utility-assist.png");
            yield return Duel(3,50);
            yield return Do(0,"mixed-3",()=>{Damage(0,15);Combat.StunAreaServer(0,Actor(1).transform.position,3,2);Check(Combat.RecordAppliedUtilityServer(0,Actor(1),ContributionType.Flash,2,.4f),"Trusted generic flash registration accepts valid applied-effect contract");});
            yield return Kill(2,3);
            Check(Stats(0).Assists==2,"Damage plus multiple utility types award exactly one assist");
            yield return Duel(4,50);
            yield return Do(0,"small-4",()=>Damage(0,14));yield return Kill(2,4);
            Check(Stats(0).Assists==2,"Below-threshold damage gives no assist");
            yield return Duel(5,50);
            yield return Do(0,"expired-5",()=>{config.DamageAssistWindow=.2f;config.StunAssistWindow=.2f;Damage(0,20);Combat.StunAreaServer(0,Actor(1).transform.position,3,.2f);});
            yield return new WaitForSecondsRealtime(.7f);
            yield return Do(0,"restore-windows",()=>{config.DamageAssistWindow=10;config.StunAssistWindow=8;});yield return Kill(2,5);
            Check(Stats(0).Assists==2,"Expired damage and utility never award assist");
            yield return Duel(6,50);
            yield return Do(0,"armor-6",()=>{Actor(1).EquiparBlindaje(PiezaBlindaje.Chaleco,25);Damage(0,8);Damage(0,7);Check(Actor(1).CurrentHealth.Value==50&&Actor(1).Armor.Value==10,"Actual armor damage accumulates to minimum assist threshold");});
            yield return Kill(2,6);Check(Stats(0).Assists==3,"Cumulative armor-only damage qualifies once");
            yield return Duel(7,50);
            yield return Do(0,"environment-7",()=>{Damage(0,20);Actor(1).TakeDamage(99999,true);});
            yield return Until(()=>Stats(1).Deaths==7,"Environmental death recorded without killer");
            Check(Combat.AlphaKills.Value==6&&Stats(0).Assists==3,"Environmental death awards no kill or assist");
            yield return Duel(8,50);yield return Kill(2,7);
            Check(Stats(0).Assists==3,"Old-life contributions do not leak across respawn");
            yield return Duel(9,50);
            if(who!=0)Check(!Combat.RecordAppliedUtilityServer((ulong)who,Actor(1),ContributionType.Stun,2,1),"Client cannot directly register contribution");
            yield return Do(0,"invalid-final",()=>{
                Check(!Combat.RecordAppliedUtilityServer(1,Actor(1),ContributionType.Stun,2,1),"Self utility excluded");
                Check(!Combat.RecordAppliedUtilityServer(2,Actor(0),ContributionType.Stun,2,1),"Allied utility excluded");
                Check(!Combat.RecordAppliedUtilityServer(0,Actor(1),(ContributionType)255,2,1)&&!Combat.RecordAppliedUtilityServer(0,Actor(1),ContributionType.Damage,2,1)&&!Combat.RecordAppliedUtilityServer(0,Actor(1),ContributionType.Stun,float.NaN,1)&&!Combat.RecordAppliedUtilityServer(0,Actor(1),ContributionType.Stun,2,0),"Malformed utility rejected");
                Combat.StunAreaServer(0,Actor(1).transform.position,3,2);Damage(0,15);
            });
            yield return Kill(2,8);yield return Until(()=>Combat.ScoreLocked.Value,"Winning kill locks score");
            Check(Stats(0).Kills==1&&Stats(0).Assists==4&&Stats(0).Points==300,"Winning kill includes final assist before score lock");
            Check(Stats(2).Kills==7&&Stats(2).Assists==1&&Stats(2).Points==750&&Stats(1).Deaths==9,"All peer stats agree across nine deaths and respawns");
            Check(Combat.AlphaKills.Value==8&&Combat.BravoKills.Value==0&&Combat.Winner.Value==PvPvETeam.Alpha,"Only PvP kills change team objective; assists do not");
            yield return Do(0,"late-events",()=>{Combat.RecordDeathServer(Actor(1));Check(!Combat.RecordAppliedUtilityServer(0,Actor(1),ContributionType.Stun,2,1),"Post-match contribution rejected");Combat.ApplyDamageServer(0,Actor(2),9999,PvPvEDamageKind.Explosion,false,0,"late");});
            Check(Stats(0).Assists==4&&Stats(0).Points==300&&Stats(1).Deaths==9,"Duplicate death and post-lock damage cannot duplicate rewards");
            yield return Sync("done");
            if(who==2){yield return ExitAndVerify();Write("third-left");Complete(true,"");yield break;}
            yield return Until(()=>Has("third-left")&&Combat.Stats.Count==2,"Disconnected player removed from stats",30);Check(Combat.AlphaKills.Value==8,"Team total survives departure");Write("remaining-"+who);yield return Until(()=>Has("remaining-0")&&Has("remaining-1"),"Remaining peers ready to exit",30);yield return ExitAndVerify();Complete(true,"");
        }
        void Damage(ulong source,int amount){Check(Combat.ApplyDamageServer(source,Actor(1),amount,PvPvEDamageKind.Bullet,false,2,"controlled-fixture"),"Authority accepted controlled damage "+amount);}
        IEnumerator Duel(int sequence,int hp){
            yield return Until(()=>Actor(1)&&Actor(1).IsAlive&&Deployment.TryGet(1,out var row)&&row.Life==PvPvELifeStage.Alive,"Victim respawn for round "+sequence,40);
            Place(who==0?-4:0,who==1?2:0);yield return Sync("layout-"+sequence);yield return new WaitForSecondsRealtime(.6f);
            yield return Do(0,"hp-"+sequence,()=>Actor(1).SetMaxHealth(hp,true));
        }
        IEnumerator Kill(int killer,int total){
            yield return Do(killer,"kill-"+total,()=>FireAt(Actor(1).transform.position+Vector3.up));
            yield return Until(()=>Combat.AlphaKills.Value==total&&Stats(1).Deaths==(total>=7?total+1:total),"Valid kill and synchronized stats "+total,30);
            yield return Sync("score-"+total);
        }
        IEnumerator Feedback(int owner,string filename){if(who==owner){var feedback=Combat.GetComponent<PvPvEKillFeedback>();Check(feedback.Message=="+50  ASISTENCIA","Targeted +50 assist feedback");Capture(filename,feedback.Panel.GetComponent<Canvas>());CaptureWorld("world-"+filename);}yield return Sync("captured-"+filename);}
        IEnumerator ExitAndVerify(){yield return Wait(service.CancelAndLeaveAsync());yield return Until(()=>!SceneManager.GetSceneByName(service.Settings.MatchScene).isLoaded,"Map unloaded",120);Check(!PvPvECombat.Active&&!PvPvEPreparationFlow.LocalControlsLocked,"Combat authority and input state cleaned on exit");}
        void CaptureWorld(string name){
            var camera=Deployment.GetComponent<PvPvEDeathPresentation>().FloatingCamera;
            if(!camera)camera=Local.GetComponentsInChildren<Camera>(true).FirstOrDefault(c=>c.isActiveAndEnabled&&c.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()?.renderType!=UnityEngine.Rendering.Universal.CameraRenderType.Overlay);
            Check(camera&&camera.isActiveAndEnabled,"Active gameplay/death camera available for "+name);
            var target=new RenderTexture(1280,720,24);var active=RenderTexture.active;
            try{RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,role+"-"+name),image.EncodeToPNG());Destroy(image);}
            finally{RenderTexture.active=active;target.Release();Destroy(target);}
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
            public Task<IPvPvESession> FindOrCreateAsync(PvPvENetworkSettings settings){var n=service.Network;n.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7799,"0.0.0.0");if(!(host?n.StartHost():n.StartClient()))throw new Exception("NGO start failed");return Task.FromResult<IPvPvESession>(new Lease(n,host));}
        }
        sealed class Lease:IPvPvESession
        {
            readonly NetworkManager n;public string Id=>"phase18-native";public bool IsHost{get;}public int ParticipantCount=>n&&n.IsServer?n.ConnectedClients.Count:1;public event Action Closed{add{}remove{}}
            public Lease(NetworkManager n,bool host){this.n=n;IsHost=host;}public Task CloseAdmissionAsync()=>Task.CompletedTask;public Task LeaveAsync(){if(n&&n.IsListening)n.Shutdown();return Task.CompletedTask;}
        }
    }
}
#endif
