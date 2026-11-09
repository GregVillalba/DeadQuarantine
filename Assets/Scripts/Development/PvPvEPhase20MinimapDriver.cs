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
    public sealed class PvPvEPhase20MinimapDriver : MonoBehaviour
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
        void OldAwake(){var c=new GameObject("Phase20 fixture selection").AddComponent<ConfiguracionPartidaSeleccionada>();c.escenarioSeleccionado=ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;c.modoSeleccionado=ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;}
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
            var config=Instantiate(service.Settings);config.MaxZombiesAlive=0;config.ZombieSpawnInterval=.3f;config.KillLimit=50;config.MapVoteSeconds=35;config.PostVoteSeconds=1;config.PreMatchSeconds=1;config.RespawnSeconds=1.5f;config.CorpseSeconds=.4f;config.SpawnProtectionSeconds=.3f;
            service.ConfigureForTest(config,new DirectoryAdapter(service,config.PlayerPrefab,who==0));var store=new PvPvEClassStore(config.Loadouts);
            for(int i=0;i<5;i++){var c=config.Loadouts.Default(i);c.primary="SemiRifle";c.secondary="AutoPistol";c.secondaryAttachments.muzzle=config.Loadouts.attachments.entries.First(e=>e.suppressed&&e.kind==WeaponAttachmentKind.Muzzle&&e.weapons.Contains("AutoPistol")).id;Check(store.Save(i,c,out _),"Existing valid class "+i);}
            var menu=service.GetComponent<PvPvEModeMenu>();var initial=menu.GetComponentInParent<Canvas>().transform.Find("menuPrincipalPanel");if(initial)initial.gameObject.SetActive(false);menu.OpenModes();menu.OpenPvPvE();
            if(who!=0)yield return Until(()=>Has("host-ready"),"Host ready",150);yield return Wait(service.SearchAsync());who=(int)Network.LocalClientId;Write(role+"-connected");if(who==0)Write("host-ready");
            yield return Until(()=>Combat&&service.Roster.Members.Count==3,"Three actual connected players",150);
            Check(service.Roster.Count(PvPvETeam.Alpha)==2&&service.Roster.Count(PvPvETeam.Bravo)==1,"Real 2v1 assignment");
            var voting=service.Roster.GetComponent<PvPvEMapVoting>();yield return Until(()=>voting.Phase.Value==PvPvEVotePhase.Voting,"Vote");voting.CastVoteServerRpc(int.Parse(Arg("-dq-map")??"1"));
            yield return Until(()=>Local&&View&&View.IsShowing,"Mode scene and initial selection",180);yield return Until(()=>View.PreviewsReady,"Class previews",120);Check(PvPvEMinimap.Active&&!PvPvEMinimap.Active.Shown,"Minimap hidden during initial class selection");View.Choose(0);View.Confirm();
            yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Playing&&!PvPvEPreparationFlow.LocalControlsLocked,"Real deployed/equipped start",150);


            yield return Until(()=>Radar&&Radar.Shown,"Minimap appears only after actual deployed Playing",30);
            var map=config.Maps[int.Parse(Arg("-dq-map")??"1")];
            Check(Radar.Terrain.texture==map.Minimap&&map.Minimap.width>=1024&&map.Minimap.filterMode==FilterMode.Point,"Current map uses sharp baked texture without runtime RenderTexture");
            Check(map.MinimapMin.x<Local.transform.position.x&&map.MinimapMax.x>Local.transform.position.x&&map.MinimapMin.y<Local.transform.position.z&&map.MinimapMax.y>Local.transform.position.z,"Original map spawn lies inside regenerated map bounds");
            CheckCenter();Capture("map-initial.png",Radar.Panel.GetComponentInParent<Canvas>());
            if(mode=="map"){yield return Finish();yield break;}
            if(who==0)Write("anchor",JsonUtility.ToJson(new Point{value=Local.transform.position}));yield return Until(()=>Has("anchor"),"Anchor");anchor=JsonUtility.FromJson<Point>(File.ReadAllText(Path.Combine(folder,"anchor"))).value;
            Position(anchor+(who==1?new Vector3(5,0,-2):who==2?new Vector3(-4,0,3):Vector3.zero));yield return Sync("layout");yield return new WaitForSecondsRealtime(.7f);
            yield return Until(()=>Radar.TryMarker((ulong)who,out _,out _,out _),"Own marker");
            Check(Radar.TryMarker((ulong)who,out var own,out _,out var ownColor)&&own.magnitude<1&&ownColor==PvPvEMinimap.AllyColor,"Own orientation marker centered and blue");
            if(who!=1){ulong ally=who==0?2UL:0UL;Check(Radar.TryMarker(ally,out _,out _,out var blue)&&blue==PvPvEMinimap.AllyColor,"Allies always blue without firing");Check(!Radar.TryMarker(1,out _,out _,out _),"Enemy hidden before firing");}
            else Check(!Radar.TryMarker(0,out _,out _,out _)&&!Radar.TryMarker(2,out _,out _,out _),"Both opposing players hidden to Bravo");
            yield return Do(1,"invalid-shot",()=>{var origin=Switcher.CurrentWeapon.AimOrigin;RawShot(Switcher.CurrentWeaponIndex,123,origin+Vector3.right*100,new[]{origin+Vector3.up*50});Local.GetComponent<NetworkWeaponAttachments>().PublishShotVisibility(Switcher.CurrentWeaponIndex);});
            Check(!Combat.IsRadarRevealed(Actor(1)),"Rejected shot and local presentation event cannot forge a reveal");
            yield return Do(1,"normal-shot",()=>FireAt(Local.transform.position+Vector3.up*100));
            yield return Until(()=>Combat.IsRadarRevealed(Actor(1)),"Server accepted unsuppressed shot replicates reveal");
            if(who!=1){yield return Until(()=>Radar.TryMarker(1,out _,out _,out _),"Enemy dot rendered");Check(Radar.TryMarker(1,out _,out var edge,out var color)&&!edge&&color==PvPvEMinimap.EnemyColor,"Visible enemy is a red dot within radar");}
            else Check(Radar.TryMarker(1,out _,out _,out var color)&&color==PvPvEMinimap.AllyColor,"Shooter stays blue on own minimap");
            if(who==0)Capture("enemy-dot.png",Radar.Panel.GetComponentInParent<Canvas>());yield return Sync("dot-captured");
            yield return Do(1,"move-far",()=>Position(anchor+new Vector3(80,0,80)));yield return new WaitForSecondsRealtime(.6f);
            yield return Do(1,"far-shot",()=>FireAt(Local.transform.position+Vector3.up*100));yield return Until(()=>Combat.IsRadarRevealed(Actor(1)),"Far reveal");
            if(who!=1){yield return Until(()=>Radar.TryMarker(1,out _,out var edge,out _)&&edge,"Red border arrow");Check(Radar.TryMarker(1,out var point,out var edge,out var color)&&edge&&color==PvPvEMinimap.EnemyColor&&point.x>0&&point.y>0,"Out-of-range red arrow points towards enemy northeast");if(who==0)Capture("enemy-border.png",Radar.Panel.GetComponentInParent<Canvas>());}
            yield return Until(()=>!Combat.IsRadarRevealed(Actor(1)),"Reveal expires on synchronized server clock",10);if(who!=1)yield return Until(()=>!Radar.TryMarker(1,out _,out _,out _),"Expired enemy marker disappears");
            yield return Sync("expired");
            if(who==1){int index=Array.FindIndex(Local.GetComponent<NetworkWeaponPresentation>().Weapons,w=>w.Id=="AutoPistol");Switcher.RequestEquip(index);yield return Until(()=>Switcher.CurrentWeaponIndex==index&&!Switcher.IsSwitching&&Switcher.CurrentWeapon.ShotIsSuppressed,"Existing compatible silencer equipped normally",30);Write("suppressed-ready");}
            yield return Until(()=>Has("suppressed-ready"),"Suppressed weapon ready",45);yield return new WaitForSecondsRealtime(.5f);
            if(who==0)Write("accepted-before",Combat.AcceptedShots.ToString());yield return Until(()=>Has("accepted-before"),"Accepted counter");
            yield return Do(1,"silent-shot",()=>FireAt(Local.transform.position+Vector3.up*100));
            if(who==0){yield return Until(()=>Combat.AcceptedShots>int.Parse(File.ReadAllText(Path.Combine(folder,"accepted-before"))),"Actual suppressed shot accepted by server",15);Write("silent-accepted");}
            yield return Until(()=>Has("silent-accepted"),"Suppressed shot acceptance",20);Check(!Combat.IsRadarRevealed(Actor(1)),"Approved compatible silencer does not reveal shooter");
            if(who!=1)Check(!Radar.TryMarker(1,out _,out _,out _),"Suppressed enemy has no red point or border arrow");
            yield return Sync("silent-checked");
            yield return Do(1,"return-near",()=>Position(anchor+new Vector3(5,0,-2)));yield return new WaitForSecondsRealtime(.6f);
            yield return Do(0,"host-shot",()=>FireAt(Local.transform.position+Vector3.up*100));yield return Until(()=>Combat.IsRadarRevealed(Actor(0)),"Host shot replicated");
            if(who==1){yield return Until(()=>Radar.TryMarker(0,out _,out _,out var color)&&color==PvPvEMinimap.EnemyColor,"Client sees host as revealed enemy");Capture("client-enemy.png",Radar.Panel.GetComponentInParent<Canvas>());}
            if(who==2)Check(Radar.TryMarker(0,out _,out _,out var color)&&color==PvPvEMinimap.AllyColor,"An allied shot never changes allied marker to red");
            ulong first=Actor(0).NetworkObjectId;yield return Do(0,"environment-death",()=>Local.TakeDamage(99999,true));
            yield return Until(()=>!Combat.IsRadarRevealed(Actor(0)),"Death clears reveal");if(who==0)yield return Until(()=>!Radar.Shown,"Local death hides minimap");else yield return Until(()=>!Radar.TryMarker(0,out _,out _,out _),"Dead player has no ghost marker");
            yield return Until(()=>Actor(0)&&Actor(0).NetworkObjectId!=first&&Combat.Alive(Actor(0)),"Original respawn replaces owner object",35);
            if(who==0){Position(anchor);yield return Until(()=>Radar.Shown,"Minimap restored after respawn");}yield return Sync("respawned");
            Check(!Combat.IsRadarRevealed(Actor(0)),"Old-life reveal never survives respawn");yield return Do(0,"new-life-shot",()=>FireAt(Local.transform.position+Vector3.up*100));yield return Until(()=>Combat.IsRadarRevealed(Actor(0)),"Accepted-shot hook rebinds to new PlayerObject");
            foreach(var dimensions in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,800)}){Screen.SetResolution(dimensions.x,dimensions.y,false);yield return Until(()=>Screen.width==dimensions.x&&Screen.height==dimensions.y,"Requested test resolution",15);yield return new WaitForSecondsRealtime(.2f);CheckCenter();if(who==0)Capture("scale-"+dimensions.x+"x"+dimensions.y+".png",Radar.Panel.GetComponentInParent<Canvas>());}
            yield return Sync("done");
            if(who==2){yield return ExitAndVerify();Write("third-left");Check(!PvPvEMinimap.Active,"Minimap disposed on client leave");Complete(true,"");yield break;}
            yield return Until(()=>Has("third-left")&&!service.Roster.Contains(2),"Disconnected ally removed");yield return new WaitForSecondsRealtime(.2f);Check(!Radar.TryMarker(2,out _,out _,out _),"Disconnected player leaves no stale marker");
            yield return Finish(false);
        }
        [Serializable] struct Point {public Vector3 value;}Vector3 anchor;
        PvPvEMinimap Radar=>PvPvEMinimap.Active;
        void Position(Vector3 point){Local.GetComponent<PlayerMovement>().enabled=false;var cc=Local.GetComponent<CharacterController>();cc.enabled=false;Local.transform.SetPositionAndRotation(point,Quaternion.identity);Local.GetComponent<NetworkTransform>().Teleport(point,Quaternion.identity,Local.transform.localScale);}
        void CheckCenter(){Canvas.ForceUpdateCanvases();var rect=(RectTransform)Radar.Panel.transform;var center=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(new Vector3(0,-rect.rect.height*.5f)));Check(Mathf.Abs(center.x-Screen.width*.5f)<2&&center.y>Screen.height*.6f,"Minimap centered at top at "+Screen.width+"x"+Screen.height);}
        IEnumerator Finish(bool three=true){
            if(three)yield return Sync("map-complete");else{Write("remaining-"+who);yield return Until(()=>Has("remaining-0")&&Has("remaining-1"),"Remaining peers ready");}
            yield return Do(0,"score-lock",()=>Combat.ScoreLocked.Value=true);yield return Until(()=>!Radar.Shown&&Combat.RadarReveals.Count==0,"Score lock hides map and clears reveals");
            if(three){yield return Sync("map-locked");if(who==2){yield return ExitAndVerify();Write("third-left");Check(!PvPvEMinimap.Active,"Minimap disposed");Complete(true,"");yield break;}yield return Until(()=>Has("third-left"),"Third peer exit");Write("remaining-"+who);yield return Until(()=>Has("remaining-0")&&Has("remaining-1"),"Peers exit");}
            yield return ExitAndVerify();yield return Until(()=>!PvPvEMinimap.Active,"Minimap canvas cleaned after exit");Check(!PvPvEMinimap.Active,"No minimap leaks back to original menu");Complete(true,"");
        }
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
            readonly NetworkManager n;public string Id=>"phase20-native";public bool IsHost{get;}public int ParticipantCount=>n&&n.IsServer?n.ConnectedClients.Count:1;public event Action Closed{add{}remove{}}
            public Lease(NetworkManager n,bool host){this.n=n;IsHost=host;}public Task CloseAdmissionAsync()=>Task.CompletedTask;public Task LeaveAsync(){if(n&&n.IsListening)n.Shutdown();return Task.CompletedTask;}
        }
    }
}
#endif
