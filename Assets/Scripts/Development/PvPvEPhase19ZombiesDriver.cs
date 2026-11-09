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
    public sealed class PvPvEPhase19ZombiesDriver : MonoBehaviour
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
        void OldAwake(){var c=new GameObject("Phase19 fixture selection").AddComponent<ConfiguracionPartidaSeleccionada>();c.escenarioSeleccionado=ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;c.modoSeleccionado=ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;}
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
            for(int i=0;i<5;i++){var c=config.Loadouts.Default(i);c.primary="SemiRifle";c.secondary="AutoPistol";Check(store.Save(i,c,out _),"Existing valid class "+i);}
            var menu=service.GetComponent<PvPvEModeMenu>();var initial=menu.GetComponentInParent<Canvas>().transform.Find("menuPrincipalPanel");if(initial)initial.gameObject.SetActive(false);menu.OpenModes();menu.OpenPvPvE();
            if(who!=0)yield return Until(()=>Has("host-ready"),"Host ready",150);yield return Wait(service.SearchAsync());who=(int)Network.LocalClientId;Write(role+"-connected");if(who==0)Write("host-ready");
            yield return Until(()=>Combat&&service.Roster.Members.Count==3,"Three actual connected players",150);
            Check(service.Roster.Count(PvPvETeam.Alpha)==2&&service.Roster.Count(PvPvETeam.Bravo)==1,"Real 2v1 assignment");
            var voting=service.Roster.GetComponent<PvPvEMapVoting>();yield return Until(()=>voting.Phase.Value==PvPvEVotePhase.Voting,"Vote");voting.CastVoteServerRpc(int.Parse(Arg("-dq-map")??"1"));
            yield return Until(()=>Local&&View&&View.IsShowing,"Mode scene and initial selection",180);yield return Until(()=>View.PreviewsReady,"Class previews",120);View.Choose(0);View.Confirm();
            yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Playing&&!PvPvEPreparationFlow.LocalControlsLocked,"Real deployed/equipped start",150);

            yield return Sync("playing");
            Check(FindObjectsByType<ZombieHealth>(FindObjectsSortMode.None).Length==0,"No zombie before Playing or while disabled");
            yield return Do(0,"enable",()=>{foreach(var player in Players)player.SetMaxHealth(2000,true);config.MaxZombiesAlive=3;config.WalkerChance=80;config.RunnerChance=15;config.DogChance=5;});
            yield return Until(()=>Zombies.Length==3,"Existing zones and NavMesh support three replicated zombies",45);
            yield return new WaitForSecondsRealtime(1);Check(Zombies.Length==3,"Configured alive cap respected");
            if(who==0){Check(Director.AliveCount==3,"Server owns only three instances");Write("spawn-ids",string.Join(",",Zombies.Select(z=>z.NetworkObjectId)));foreach(var z in Zombies){Check(UnityEngine.AI.NavMesh.SamplePosition(z.transform.position,out var h,2,UnityEngine.AI.NavMesh.AllAreas),"Spawn on active original NavMesh");Check(Players.All(player=>Vector3.Distance(player.transform.position,z.transform.position)>=config.ZombieSafeDistance-3),"Spawn avoids all players");}Write("snapshot-ready");}
            yield return Until(()=>Has("snapshot-ready"),"Spawn snapshot");Check(string.Join(",",Zombies.Select(z=>z.NetworkObjectId))==File.ReadAllText(Path.Combine(folder,"spawn-ids")),"Identical authoritative zombie network IDs on each peer");
            if(who!=0)Check(Zombies.All(z=>!z.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled),"Client does not simulate zombie navigation");
            yield return Sync("snapshot");
            foreach(var kind in new[]{PvPvEZombieKind.Walker,PvPvEZombieKind.Runner,PvPvEZombieKind.Dog}){
                yield return Do(0,"clear-"+kind,()=>{config.MaxZombiesAlive=0;Director.Clear();});yield return Until(()=>Zombies.Length==0,"Clear replicated zombies");yield return Sync("clear-"+kind);
                yield return Do(0,"type-"+kind,()=>{config.WalkerChance=kind==PvPvEZombieKind.Walker?1:0;config.RunnerChance=kind==PvPvEZombieKind.Runner?1:0;config.DogChance=kind==PvPvEZombieKind.Dog?1:0;config.MaxZombiesAlive=1;});
                yield return Until(()=>Zombies.Length==1&&Zombies[0].GetComponent<ZombieAI>().PvPvEKind==kind,"Forced configured kind replicated: "+kind,40);var zombie=Zombies[0];var ai=zombie.GetComponent<ZombieAI>();
                Check(ai.IsRunning==(kind!=PvPvEZombieKind.Walker),"Existing SetRunning presentation matches "+kind);
                yield return Until(()=>!ai.EstaApareciendo,"Original emergence animation completes",20);if(who==0){config.MaxZombiesAlive=0;ai.Aturdir(20);}
                yield return Until(()=>ai.EstaAturdido,"Real utility replicated on zombie");yield return Sync("stun-"+kind);
                if(who==0){var agent=zombie.GetComponent<UnityEngine.AI.NavMeshAgent>();var before=zombie.CurrentHealth;zombie.TakeDamage(99999,zombie.transform.position,Vector3.up);Check(zombie.CurrentHealth==before,"Unvalidated legacy damage RPC cannot kill mode zombie");
                    // Place an actual network zombie near the remote shooter on original navigation.
                    var shooter=Actor(1);Vector3 destination=default;bool found=false;for(int i=0;i<16;i++){var direction=Quaternion.Euler(0,i*22.5f,0)*shooter.transform.forward;var desired=shooter.transform.position+direction*4;if(UnityEngine.AI.NavMesh.SamplePosition(desired,out var hit,1,UnityEngine.AI.NavMesh.AllAreas)&&!PvPvEZombieDirector.Blocked(shooter.GetComponentsInChildren<Camera>(true).First(c=>c.CompareTag("MainCamera")).transform.position,hit.position+Vector3.up*(kind==PvPvEZombieKind.Dog?.4f:1.6f))&&(kind==PvPvEZombieKind.Dog||!PvPvEZombieDirector.Blocked(shooter.GetComponentsInChildren<Camera>(true).First(c=>c.CompareTag("MainCamera")).transform.position,hit.position+Vector3.up))){destination=hit.position;found=true;break;}}Check(found,"Test firing position lies on visible original map navigation");agent.Warp(destination);zombie.GetComponent<NetworkTransform>().Teleport(zombie.transform.position,zombie.transform.rotation,zombie.transform.localScale);Write("placed-"+kind);}
                yield return Until(()=>Has("placed-"+kind),"Place actual zombie");yield return new WaitForSecondsRealtime(.7f);
                yield return Do(1,"untrusted-"+kind,()=>zombie.TakeDamage(99999,zombie.transform.position,Vector3.up));Check(zombie.CurrentHealth==zombie.MaximumHealth,"Remote client cannot forge damage against "+kind);
                if(who==1){var eye=Local.GetComponentsInChildren<Camera>(true).First(c=>c.CompareTag("MainCamera")).transform.position;Write("shot-diagnostics-"+kind,string.Join("\n",Physics.RaycastAll(eye,(ShotPoint(zombie,kind)-eye).normalized,Vector3.Distance(eye,ShotPoint(zombie,kind))+1,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance).Select(h=>h.distance+" "+h.collider.name+" "+h.collider.GetComponentInParent<ZombieHealth>()?.name)));CaptureWorld("before-"+kind+".png");}
                double beforePoints=Stats(1).Points;
                yield return Do(1,"shot-"+kind,()=>FireAt(ShotPoint(zombie,kind)));
                yield return Until(()=>zombie.CurrentHealth<zombie.MaximumHealth,"Original gun reduces synchronized zombie health",20);
                if(kind==PvPvEZombieKind.Runner){int expected=Mathf.Max(0,zombie.MaximumHealth-zombie.GetComponent<ZombieHeadshot>().CalcularDano(config.Loadouts.Find("SemiRifle").damage,zombie.MaximumHealth,zombie.MaximumHealth));Check(zombie.CurrentHealth==expected,"Server preserves existing headshot calculation through body collider");}
                if(!zombie.IsDead){Check(zombie.CurrentHealth>0,"Original humanoid health requires another body shot");yield return new WaitForSecondsRealtime(.45f);yield return Do(1,"second-shot-"+kind,()=>FireAt(kind==PvPvEZombieKind.Dog?ShotPoint(zombie,kind):zombie.transform.position+Vector3.up));}
                yield return Until(()=>zombie.IsDead&&Stats(1).Points==beforePoints+config.ZombieKillScore,"Remote existing weapon kills "+kind+" and grants individual zombie points",25);
                Check(Stats(1).Kills==0&&Combat.AlphaKills.Value==0&&Combat.BravoKills.Value==0,"Zombie kill never changes PvP kills or team score");
                if(who==0){Director.ZombieDied(zombie,1);Check(Stats(1).Points==beforePoints+config.ZombieKillScore,"Duplicate zombie death awards no extra points");}
                if(who==1){var feedback=Combat.GetComponent<PvPvEKillFeedback>();Check(feedback.Message=="+10  ZOMBIE","Confirmed zombie feedback on remote killer");Capture("zombie-"+kind+".png",feedback.Panel.GetComponent<Canvas>());CaptureWorld("world-"+kind+".png");}
                yield return Sync("killed-"+kind);
            }
            yield return Do(0,"attack-prepare",()=>{Director.Clear();config.MaxZombiesAlive=1;config.WalkerChance=0;config.DogChance=1;});yield return Until(()=>Zombies.Length==1,"Dog for actual AI attack");
            var attacker=Zombies[0];Vector3 bravoGoal=Actor(1).transform.position;
            yield return Do(0,"attack-position",()=>{config.MaxZombiesAlive=0;var p=Actor(1);p.SetMaxHealth(1,true);var agent=attacker.GetComponent<UnityEngine.AI.NavMeshAgent>();agent.Warp(AttackPosition(p));attacker.transform.rotation=Quaternion.LookRotation(p.transform.position-attacker.transform.position);attacker.GetComponent<NetworkTransform>().Teleport(attacker.transform.position,attacker.transform.rotation,attacker.transform.localScale);});
            yield return Until(()=>Stats(1).Deaths==1,"Existing AI attacks and kills Bravo player",35);
            yield return new WaitForSecondsRealtime(.3f);Check(Vector3.Distance(attacker.transform.position,bravoGoal)<3,"Existing navigation movement replicated to clients");
            Check(Combat.AlphaKills.Value==0&&Combat.BravoKills.Value==0&&Stats(0).Points==0,"Zombie killing player grants death and no team kill or opposing points");
            yield return Until(()=>Actor(1)&&Actor(1).IsAlive&&Deployment.TryGet(1,out var row)&&row.Life==PvPvELifeStage.Alive,"Zombie victim uses existing respawn",35);
            yield return Do(0,"nearest-alpha",()=>{var health=Actor(0);foreach(var player in Players.Where(p=>p.OwnerClientId!=1))player.SetMaxHealth(1,true);var agent=attacker.GetComponent<UnityEngine.AI.NavMeshAgent>();agent.Warp(AttackPosition(health));attacker.transform.rotation=Quaternion.LookRotation(health.transform.position-attacker.transform.position);attacker.GetComponent<NetworkTransform>().Teleport(attacker.transform.position,attacker.transform.rotation,attacker.transform.localScale);});
            yield return Until(()=>Stats(0).Deaths+Stats(2).Deaths>=1,"Same existing AI retargets and kills nearest Alpha player",35);Check(Stats(0).Kills==0&&Stats(1).Kills==0,"Zombies hostile to both teams");
            yield return Do(0,"lock",()=>Combat.ScoreLocked.Value=true);yield return Until(()=>Zombies.Length==0,"Score lock despawns all mode zombies");yield return new WaitForSecondsRealtime(1);Check(Zombies.Length==0,"No further spawn after lock");
            yield return Sync("done");
            if(who==2){yield return ExitAndVerify();Write("third-left");Complete(true,"");yield break;}
            yield return Until(()=>Has("third-left"),"Third exited");Write("remaining-"+who);yield return Until(()=>Has("remaining-0")&&Has("remaining-1"),"Exit ready");yield return ExitAndVerify();Check(!PvPvEZombieDirector.Active,"Director cleaned on exit");Complete(true,"");
        }
        Vector3 ShotPoint(ZombieHealth zombie,PvPvEZombieKind kind){if(kind==PvPvEZombieKind.Runner){var head=zombie.GetComponentInChildren<ZombieHeadHitbox>(true);if(head)return head.GetComponent<Collider>().bounds.center;}if(kind==PvPvEZombieKind.Dog){var collider=zombie.GetComponent<Collider>();var bounds=collider.bounds;var eye=Local.GetComponentsInChildren<Camera>(true).First(c=>c.CompareTag("MainCamera")).transform.position;foreach(float y in new[]{.65f,.8f,.5f})foreach(float x in new[]{0f,.3f,-.3f}){var point=bounds.center+new Vector3(bounds.extents.x*x,bounds.extents.y*(y*2-1),0);var direction=(point-eye).normalized;if(collider.Raycast(new Ray(eye,direction),out var hit,50))return hit.point+direction*.05f;}throw new Exception("Dog fixture could not find a real collider surface");}return zombie.transform.position+Vector3.up;}
        Vector3 AttackPosition(PlayerHealth player){for(int i=0;i<16;i++){var desired=player.transform.position+Quaternion.Euler(0,i*22.5f,0)*player.transform.forward*5;if(UnityEngine.AI.NavMesh.SamplePosition(desired,out var hit,1,UnityEngine.AI.NavMesh.AllAreas)&&!PvPvEZombieDirector.Blocked(hit.position+Vector3.up*.8f,player.transform.position+Vector3.up)){return hit.position;}}throw new Exception("No reachable visible attack fixture point");}
        PvPvEZombieDirector Director=>PvPvEZombieDirector.Active;
        ZombieHealth[] Zombies=>FindObjectsByType<ZombieHealth>(FindObjectsSortMode.None).Where(z=>z.IsSpawned&&!z.IsDead).OrderBy(z=>z.NetworkObjectId).ToArray();
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
            readonly NetworkManager n;public string Id=>"phase19-native";public bool IsHost{get;}public int ParticipantCount=>n&&n.IsServer?n.ConnectedClients.Count:1;public event Action Closed{add{}remove{}}
            public Lease(NetworkManager n,bool host){this.n=n;IsHost=host;}public Task CloseAdmissionAsync()=>Task.CompletedTask;public Task LeaveAsync(){if(n&&n.IsListening)n.Shutdown();return Task.CompletedTask;}
        }
    }
}
#endif
