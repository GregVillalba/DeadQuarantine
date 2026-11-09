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
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;

namespace DeadQuarantine.Development
{
    public sealed class PvPvEPhase25AccessoriesDriver : MonoBehaviour
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
        void OldAwake(){var c=new GameObject("Phase24 fixture selection").AddComponent<ConfiguracionPartidaSeleccionada>();c.escenarioSeleccionado=ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;c.modoSeleccionado=ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;}
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
            who=role=="host"?0:role=="client1"?1:2;keyboard=InputSystem.AddDevice<Keyboard>();PlayerIdentity.SetUsername(role=="host"?"HostQA":role=="client1"?"BravoQA":"AlphaQA");InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            service=FindFirstObjectByType<PvPvEMatchmaking>();CharacterSelection.SelectedIndex=who%3;
            var config=Instantiate(service.Settings);config.ResultsSeconds=6;config.MatchDurationSeconds=600;config.KillFeedRows=3;config.KillFeedSeconds=3;config.MaxZombiesAlive=0;config.ZombieSpawnInterval=.3f;config.KillLimit=50;config.MapVoteSeconds=35;config.PostVoteSeconds=1;config.PreMatchSeconds=1;config.RespawnSeconds=1.5f;config.CorpseSeconds=.4f;config.SpawnProtectionSeconds=.3f;
            bool cloud=Arg("-dq-cloud")=="1";
            if(cloud){config.SessionPool="dq.review24."+Path.GetFileName(folder);config.MapVoteSeconds=90;yield return Wait(UnityServices.InitializeAsync(new InitializationOptions().SetProfile("r24-"+role+"-"+Path.GetFileName(folder).Substring(0,8))));}
            service.ConfigureForTest(config,cloud?new PvPvEUnitySessions():new DirectoryAdapter(service,config.PlayerPrefab,who==0));var store=new PvPvEClassStore(config.Loadouts);
            for(int i=0;i<5;i++){var c=config.Loadouts.Default(i);c.primary=who==1?"Escopeta":"Rifle";c.secondary="Handgun";c.wildcard1=c.wildcard2=c.perk1=c.perk2="";c.primaryAttachments=new WeaponAttachmentChoice{scope="Scope_01",muzzle="Muzzle_Silencer_01"};c.secondaryAttachments=new WeaponAttachmentChoice{scope="Scope_02",muzzle="Muzzle_Silencer_01"};Check(store.Save(i,c,out var error),"Protected weapon accessories accepted by saved Pick10 class: "+error);}

            var menu=service.GetComponent<PvPvEModeMenu>();var initial=menu.GetComponentInParent<Canvas>().transform.Find("menuPrincipalPanel");if(initial)initial.gameObject.SetActive(false);menu.OpenModes();menu.OpenPvPvE();
            if(who!=0)yield return Until(()=>Has("host-ready"),"Host ready",150);yield return Wait(service.SearchAsync());who=(int)Network.LocalClientId;Write(role+"-connected");if(who==0)Write("host-ready");
            yield return Until(()=>Combat&&service.Roster.Members.Count==3,"Three actual connected players",150);
            yield return ReviewLobbyAppearance("initial-lobby-world.png");
            Check(service.Roster.Count(PvPvETeam.Alpha)==2&&service.Roster.Count(PvPvETeam.Bravo)==1,"Real 2v1 assignment");
            var voting=service.Roster.GetComponent<PvPvEMapVoting>();yield return Until(()=>voting.Phase.Value==PvPvEVotePhase.Voting,"Vote");voting.CastVoteServerRpc(int.Parse(Arg("-dq-map")??"1"));
            yield return Until(()=>Local&&View&&View.IsShowing,"Mode scene and initial selection",180);yield return Until(()=>View.PreviewsReady,"Class previews",120);Check(PvPvEMinimap.Active&&!PvPvEMinimap.Active.Shown,"Minimap hidden during initial class selection");View.Choose(0);View.Confirm();
            yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Playing&&!PvPvEPreparationFlow.LocalControlsLocked,"Real deployed/equipped start",150);



            yield return ValidateClass25();yield return Sync("equipped-all");
            foreach(var category in new[]{WeaponSlotCategory.Primary,WeaponSlotCategory.Secondary}){
                Switcher.SelectCategory(category);yield return Until(()=>!Switcher.IsSwitching,"Equipped class category",20);yield return new WaitForSecondsRealtime(.4f);yield return Sync("category-"+category);
                var w=Switcher.CurrentWeapon;var nwa=Local.GetComponent<NetworkWeaponAttachments>();var choice=nwa.GetChoice(Switcher.CurrentWeaponIndex);Check(choice.scope== (category==WeaponSlotCategory.Primary?"Scope_01":"Scope_02")&&w.ShotIsSuppressed,"Canonical class accessory effects apply on "+Switcher.ObtenerArmaEquipadaId());
                foreach(var remote in Players.Where(p=>p!=Local)){var view=remote.GetComponent<NetworkWeaponPresentation>();yield return Until(()=>view.VisibleWeapon&&view.VisibleWeapon.GetComponent<WeaponAttachments>()&&view.VisibleWeapon.GetComponent<WeaponAttachments>().ApprovedChoice.scope==choice.scope,"Other peers see class attachments",25);Check(WeaponAttachments.Find(view.VisibleWeapon.transform,"DQ_ATT_Muzzle_Silencer_01"),"Remote silencer is mounted on actual world model");}
                CaptureWorld("class-"+category+".png");CaptureRemote("class-"+category+"-TP.png");
                int ammo=w.CurrentAmmo,reserve=w.ReserveAmmo;w.SendMessage("TryFire");yield return Until(()=>w.CurrentAmmo<ammo,"Canonical weapon fires",8);yield return Until(()=>!w.IsChambering,"Chamber after shot",8);yield return new WaitForSecondsRealtime(.5f);int spent=w.UsesLimitedReserve?w.CapacidadCargador-w.CurrentAmmo:0;w.SendMessage("OnReload",default(InputAction.CallbackContext));yield return Until(()=>!w.IsReloading,"Canonical reload",25);Check(w.CurrentAmmo==w.CapacidadCargador&&w.ReserveAmmo==reserve-spent,"Protected weapon reload conserves ammunition with class accessories (mag="+w.CurrentAmmo+", capacity="+w.CapacidadCargador+", reserve="+w.ReserveAmmo+", expected="+(reserve-spent)+", limited="+w.UsesLimitedReserve+")");
                yield return Sync("fired-"+category);
            }
            yield return Do(0,"environmental-respawn",()=>Combat.ApplyDamageServer(1,Actor(1),99999,PvPvEDamageKind.Environment,false,0,"Entorno"));yield return Until(()=>Stats(1).Deaths==1,"Server death registered");yield return Until(()=>Actor(1)&&Combat.Alive(Actor(1)),"Respawn",40);yield return Sync("respawn-done");yield return ValidateClass25();
            Keys(Key.Tab);yield return Until(()=>Hud.ScoreboardVisible,"Scoreboard still opens");Capture("class-scoreboard.png",Hud.CanvasRoot.GetComponent<Canvas>());Keys();yield return Until(()=>!Hud.ScoreboardVisible,"Scoreboard closes");
            yield return Sync("verified");yield return ExitAndVerify();Complete(true,"");
        }
        IEnumerator ValidateClass25(){
            var c=new PvPvEClassStore(service.Settings.Loadouts).Get(0);Check(service.Settings.Loadouts.Validate(c,out _)&&service.Settings.Loadouts.Pick10Cost(c)<=10,"Saved protected gun class remains legal");var nwa=Local.GetComponent<NetworkWeaponAttachments>();var np=Local.GetComponent<NetworkWeaponPresentation>();
            foreach(var pair in new[]{(c.primary,c.primaryAttachments),(c.secondary,c.secondaryAttachments)}){int index=Array.FindIndex(np.Weapons,w=>w.Id==pair.Item1);yield return Until(()=>nwa.GetChoice(index).Equals(pair.Item2),"Server deployment restores full canonical accessory choice",25);}
        }
        IEnumerator ReviewLobbyAppearance(string name){
            var lobby=service.GetComponent<PvPvELobbyView>();yield return Until(()=>lobby&&lobby.IsShowing&&LobbyModelsReady(lobby),"Lobby visual variants finish building",60);yield return new WaitForSecondsRealtime(.2f);Check(LobbyModelsReady(lobby),"Lobby models built for every authoritative variant");CaptureLobby(name);
        }
        bool LobbyModelsReady(PvPvELobbyView lobby){var actors=lobby.Stage.GetComponents<LobbyCinematicPlayerDisplay>();var members=Enumerable.Range(0,service.Roster.Members.Count).Select(i=>service.Roster.Members[i]).OrderBy(m=>m.Team).ThenBy(m=>m.ClientId).ToArray();return actors.Length>=members.Length&&members.Select((m,i)=>actors[i].IsVisible&&actors[i].BuiltCharacterIndex==m.CharacterIndex&&m.CharacterIndex==(int)m.ClientId%3).All(value=>value);}
        void CaptureRemote(string name){var actor=Actor(who==1?0:1);var camera=new GameObject("Review remote character camera").AddComponent<Camera>();camera.transform.position=actor.transform.position+new Vector3(2,1.8f,3);camera.transform.LookAt(actor.transform.position+Vector3.up);camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.cullingMask=Local.GetComponentsInChildren<Camera>(true).First(c=>c.CompareTag("MainCamera")).cullingMask;CaptureCamera(camera,name);Destroy(camera.gameObject);}
        void CaptureLobby(string name){var stage=service.GetComponent<PvPvELobbyView>().Stage;var camera=stage.GetComponentsInChildren<Camera>(true).First(c=>c.isActiveAndEnabled);CaptureCamera(camera,name);}
        void CaptureCamera(Camera camera,string name){var target=new RenderTexture(1280,720,24);var active=RenderTexture.active;try{RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(folder,role+"-"+name),texture.EncodeToPNG());Destroy(texture);}finally{RenderTexture.active=active;target.Release();Destroy(target);}}
        PvPvEView Hud=>PvPvEView.Active;
        void Position(){Local.GetComponent<PlayerMovement>().enabled=false;Local.GetComponent<CharacterController>().enabled=false;Local.transform.SetPositionAndRotation(new Vector3(50000+(who==0?-4:0),.08f,who==1?2:0),Quaternion.identity);Local.GetComponent<NetworkTransform>().Teleport(Local.transform.position,Quaternion.identity,Local.transform.localScale);}
        IEnumerator OpenBoard(){Keys(Key.Tab);yield return Until(()=>Hud.ScoreboardVisible&&Hud.VisibleRows(0)==service.Roster.Count(PvPvETeam.Alpha)&&Hud.VisibleRows(1)==service.Roster.Count(PvPvETeam.Bravo),"Held TAB opens current team tables",10);yield return new WaitForSecondsRealtime(.15f);}
        IEnumerator CloseBoard(){Keys();yield return Until(()=>!Hud.ScoreboardVisible,"Released TAB closes scoreboard",10);}
        void ValidateExistingHud(){var hud=Local.GetComponentInChildren<HUDController>(true);Check(hud&&hud.isActiveAndEnabled,"Original life stamina ammo inventory HUD reused");var rounds=(TMPro.TMP_Text)Field(hud,"roundsText");Check(!rounds.gameObject.activeInHierarchy,"Legacy round counter hidden only in PvPvE");var perks=(RectTransform)Field(hud,"perksRow");Check(perks&&perks.gameObject.activeInHierarchy&&perks.Cast<Transform>().Count(t=>t.gameObject.activeSelf)==CountBits(Local.Perks.Value),"Original active perk icons match class");var score=Local.GetComponentsInChildren<ScoreHUD>(true).First(s=>s.gameObject.activeInHierarchy);Check(((TMPro.TMP_Text)Field(score,"textoPuntaje")).text==Stats(who).Points.ToString(),"Original points display bound to authoritative PvPvE stats");}
        static int CountBits(uint value){int n=0;while(value!=0){n+=(int)(value&1);value>>=1;}return n;}
        void CheckBounds(){Canvas.ForceUpdateCanvases();var board=Hud.CanvasRoot.transform.Find("Scoreboard") as RectTransform;var corners=new Vector3[4];board.GetWorldCorners(corners);Check(corners.All(c=>{var p=RectTransformUtility.WorldToScreenPoint(null,c);return p.x>=-1&&p.x<=Screen.width+1&&p.y>=-1&&p.y<=Screen.height+1;}),"Complete five-column scoreboard fits "+Screen.width+"x"+Screen.height);}
        void CaptureAllHud(string name){
            var original=Local.GetComponentInChildren<HUDController>(true).GetComponentInParent<Canvas>();var maps=PvPvEMinimap.Active.Panel.GetComponentInParent<Canvas>();var list=new[]{original.rootCanvas,maps.rootCanvas,Hud.CanvasRoot.GetComponent<Canvas>()}.Distinct().ToArray();
            var modes=list.Select(c=>c.renderMode).ToArray();var cameras=list.Select(c=>c.worldCamera).ToArray();var planes=list.Select(c=>c.planeDistance).ToArray();var camera=new GameObject("Phase24 UI validation").AddComponent<Camera>();camera.transform.position=new Vector3(90000,0,-10);camera.orthographic=true;camera.nearClipPlane=.1f;camera.farClipPlane=5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.04f,.045f);var target=new RenderTexture(1600,900,24);var active=RenderTexture.active;
            try{foreach(var canvas in list){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}Canvas.ForceUpdateCanvases();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,role+"-"+name),image.EncodeToPNG());Destroy(image);}
            finally{for(int i=0;i<list.Length;i++){list[i].renderMode=modes[i];list[i].worldCamera=cameras[i];list[i].planeDistance=planes[i];}RenderTexture.active=active;target.Release();Destroy(target);Destroy(camera.gameObject);}
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
            readonly NetworkManager n;public string Id=>"phase24-native";public bool IsHost{get;}public int ParticipantCount=>n&&n.IsServer?n.ConnectedClients.Count:1;public event Action Closed{add{}remove{}}
            public Lease(NetworkManager n,bool host){this.n=n;IsHost=host;}public Task CloseAdmissionAsync()=>Task.CompletedTask;public Task LeaveAsync(){if(n&&n.IsListening)n.Shutdown();return Task.CompletedTask;}
        }
    }
}
#endif
