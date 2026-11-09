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
    public sealed class PvPvEPhase24ReviewDriver : MonoBehaviour
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
            for(int i=0;i<5;i++){var c=config.Loadouts.Default(i);c.wildcard1=config.Loadouts.wildcards.entries.First(e=>e.kind==PvPvEWildcard.ExtraHealing).id;c.primary="SemiRifle";c.secondary="AutoPistol";c.perk1=config.Loadouts.perks.entries[0].id;c.perk2=config.Loadouts.perks.entries.First(p=>p.category!=config.Loadouts.perks.entries[0].category).id;c.secondaryAttachments.muzzle=config.Loadouts.attachments.entries.First(e=>e.suppressed&&e.kind==WeaponAttachmentKind.Muzzle&&e.weapons.Contains("AutoPistol")).id;Check(store.Save(i,c,out _),"Existing valid class "+i);}
            var menu=service.GetComponent<PvPvEModeMenu>();var initial=menu.GetComponentInParent<Canvas>().transform.Find("menuPrincipalPanel");if(initial)initial.gameObject.SetActive(false);menu.OpenModes();menu.OpenPvPvE();
            if(who!=0)yield return Until(()=>Has("host-ready"),"Host ready",150);yield return Wait(service.SearchAsync());who=(int)Network.LocalClientId;Write(role+"-connected");if(who==0)Write("host-ready");
            yield return Until(()=>Combat&&service.Roster.Members.Count==3,"Three actual connected players",150);
            yield return ReviewLobbyAppearance("initial-lobby-world.png");
            Check(service.Roster.Count(PvPvETeam.Alpha)==2&&service.Roster.Count(PvPvETeam.Bravo)==1,"Real 2v1 assignment");
            var voting=service.Roster.GetComponent<PvPvEMapVoting>();yield return Until(()=>voting.Phase.Value==PvPvEVotePhase.Voting,"Vote");voting.CastVoteServerRpc(int.Parse(Arg("-dq-map")??"1"));
            yield return Until(()=>Local&&View&&View.IsShowing,"Mode scene and initial selection",180);yield return Until(()=>View.PreviewsReady,"Class previews",120);Check(PvPvEMinimap.Active&&!PvPvEMinimap.Active.Shown,"Minimap hidden during initial class selection");View.Choose(0);View.Confirm();
            yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Playing&&!PvPvEPreparationFlow.LocalControlsLocked,"Real deployed/equipped start",150);



            yield return ReviewEquipment();
            Check(Players.All(player=>service.Roster.TryGetMember(player.OwnerClientId,out var member)&&member.CharacterIndex==(int)player.OwnerClientId%3&&player.GetComponent<PlayerCharacter>().AppliedIndex==member.CharacterIndex),"All three chosen variants match replicated gameplay models");
            CaptureWorld("gameplay-map.png");
            if(mode=="map"){yield return Sync("map-visual");yield return ExitAndVerify();Complete(true,"");yield break;}
            yield return Until(()=>Hud&&Hud.MatchText.Contains("ALPHA")&&Combat.MatchDeadline.Value>0,"HUD initialized with server clock");
            Check(!Hud.ScoreboardVisible,"Scoreboard closed by default");double remaining=Combat.RemainingSeconds;yield return new WaitForSecondsRealtime(1.1f);Check(Combat.RemainingSeconds<remaining&&Combat.RemainingSeconds>0,"Header clock counts synchronized time without client timer");
            Check(Hud.MatchText.Contains("OBJETIVO 50"),"HUD displays actual configured PvP objective");ValidateExistingHud();CaptureAllHud("hud-live-map.png");
            yield return Do(0,"spawn-npc",()=>{var c=service.Settings;c.MaxZombiesAlive=1;c.WalkerChance=0;c.RunnerChance=0;c.DogChance=1;});
            yield return Until(()=>FindObjectsByType<ZombieHealth>(FindObjectsSortMode.None).Any(z=>z.IsSpawned&&!z.IsDead),"Existing authoritative NPC for cause-of-death test",40);
            var npc=FindObjectsByType<ZombieHealth>(FindObjectsSortMode.None).First(z=>z.IsSpawned&&!z.IsDead);
            yield return Do(0,"freeze-npc",()=>{service.Settings.MaxZombiesAlive=0;npc.GetComponent<ZombieAI>().Aturdir(30);});
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Phase24 controlled validation arena";floor.GetComponent<Renderer>().sharedMaterial=GraphicsSettings.currentRenderPipeline.defaultMaterial;floor.transform.position=new Vector3(50000,-.5f,30);floor.transform.localScale=new Vector3(30,1,150);
            Position();yield return Sync("arena");yield return new WaitForSecondsRealtime(.6f);yield return Do(0,"hp",()=>{foreach(var p in Players)p.SetMaxHealth(100,true);Actor(1).SetMaxHealth(90,true);});
            yield return Do(0,"friendly-fire",()=>{int hp=Actor(2).CurrentHealth.Value;Check(!Combat.ApplyDamageServer(0,Actor(2),50,PvPvEDamageKind.Bullet,false,0,"SemiRifle")&&Actor(2).CurrentHealth.Value==hp,"Friendly fire rejected by server");Check(!Combat.RecordAppliedUtilityServer(0,Actor(2),ContributionType.Stun,2,1),"Allied negative utility rejected by server");});
            yield return Do(1,"forged-shot",()=>{var origin=Switcher.CurrentWeapon.AimOrigin;RawShot(Switcher.CurrentWeaponIndex,123,origin+Vector3.right*100,new[]{origin+Vector3.up*50});Local.GetComponent<NetworkWeaponAttachments>().PublishShotVisibility(Switcher.CurrentWeaponIndex);});
            yield return new WaitForSecondsRealtime(.3f);Check(Actor(1).CurrentHealth.Value==90&&!Combat.IsRadarRevealed(Actor(1)),"Forged shot and client-only visibility event cannot damage or reveal");
            CursorLockMode beforeCursor=Cursor.lockState;yield return OpenBoard();Check(Cursor.lockState==beforeCursor&&!PlayerInputGate.MenuOpen,"TAB does not unlock cursor or open gameplay menu");
            Check(!Hud.CanvasRoot.transform.Find("MatchHUD").gameObject.activeSelf,"Match header hidden behind scoreboard");Check(Hud.VisibleRows(0)==2&&Hud.VisibleRows(1)==1,"Two team tables show actual 2v1 participants");Check(Hud.DisplayedIds(PvPvETeam.Alpha).SequenceEqual(new ulong[]{0,2}),"Initial tied order deterministic by participant ID");
            Capture("scoreboard-initial.png",Hud.CanvasRoot.GetComponent<Canvas>());
            yield return Do(0,"contribute",()=>FireAt(Actor(1).transform.position+Vector3.up));yield return Until(()=>Actor(1).CurrentHealth.Value==20,"Existing host gun contributes actual damage");
            if(who!=0){var remote=Actor(0).GetComponent<NetworkWeaponPresentation>();yield return Until(()=>remote.RemoteShotCount>0&&remote.RemoteSoundEvents>0&&remote.RemoteTrailEvents>0,"Accepted remote shot sound and trail received");Check(remote.VisibleWeapon&&remote.VisibleMuzzle,"Remote uses equipped world model and muzzle");}
            CaptureRemote("third-person.png");
            var firstVictim=Actor(1);yield return Do(2,"pvp-kill",()=>FireAt(Actor(1).transform.position+Vector3.up));
            yield return Until(()=>Stats(2).Kills==1&&Stats(0).Assists==1&&Combat.KillFeed.Count==1,"Kill assist stats and server feed synchronized");
            yield return Do(0,"duplicate",()=>Combat.RecordDeathServer(firstVictim));Check(Combat.KillFeed.Count==1,"Duplicate death produces one feed entry");
            yield return Until(()=>Hud.Cell(0,0,1)=="100"&&Hud.Cell(0,1,1)=="50"&&Hud.Cell(1,0,3)=="1","Held scoreboard updates live kills deaths points assists");
            Check(Hud.DisplayedIds(PvPvETeam.Alpha).SequenceEqual(new ulong[]{2,0})&&Hud.Cell(0,0,2)=="1"&&Hud.Cell(0,1,4)=="1","Recovered ordering and all five columns match authority");
            var entry=Combat.KillFeed[0];Check(entry.Killer==2&&entry.Victim==1&&entry.Weapon.ToString()==config.Loadouts.Find("SemiRifle").name,"Feed has server killer victim and actual weapon");Capture("scoreboard-live.png",Hud.CanvasRoot.GetComponent<Canvas>());
            yield return CloseBoard();yield return Until(()=>Hud.VisibleFeedCount==1,"Killfeed visible outside scoreboard");Capture("killfeed-pvp.png",Hud.CanvasRoot.GetComponent<Canvas>());
            yield return Until(()=>Actor(1)&&Combat.Alive(Actor(1)),"Victim respawns through original deployment",35);if(who==1)Position();yield return Sync("respawn-one");
            ValidateExistingHud();CaptureAllHud("hud-complete.png");
            yield return Until(()=>Combat.KillFeed.Count==0&&Hud.VisibleFeedCount==0,"Feed expires from synchronized server deadline",10);yield return Sync("feed-expired");
            yield return Do(0,"zombie-cause",()=>{config.KillFeedSeconds=12;var agent=npc.GetComponent<UnityEngine.AI.NavMeshAgent>();agent.enabled=false;npc.transform.position=Actor(0).transform.position+Vector3.forward*1.2f;npc.GetComponent<NetworkTransform>().Teleport(npc.transform.position,npc.transform.rotation,npc.transform.localScale);Combat.ApplyZombieDamageServer(npc.GetComponent<ZombieAI>(),Actor(0),99999);});
            yield return Until(()=>Stats(0).Deaths==1&&Combat.KillFeed.Count==1,"Zombie death synchronized in feed");Check(Combat.KillFeed[0].Kind==PvPvEDamageKind.Zombie&&Combat.KillFeed[0].KillerName.ToString()=="ZOMBIE"&&Combat.AlphaKills.Value==1&&Combat.BravoKills.Value==0,"Zombie attacker shown without team kill");
            yield return Until(()=>Hud.VisibleFeedCount==1&&Hud.FeedLine(0).Contains("ZOMBIE"),"Zombie feed presented");Capture("killfeed-zombie.png",Hud.CanvasRoot.GetComponent<Canvas>());
            yield return Until(()=>Actor(0)&&Combat.Alive(Actor(0)),"Host respawn",35);if(who==0)Position();yield return Sync("host-back");
            yield return Do(0,"suicide",()=>Combat.ApplyDamageServer(1,Actor(1),99999,PvPvEDamageKind.Explosion,false,0,"Granada"));yield return Until(()=>Stats(1).Deaths==2&&Combat.KillFeed.Count==2,"Self damage death resolved");
            Check(Combat.KillFeed[1].KillerName.ToString()=="SUICIDIO"&&Combat.AlphaKills.Value==1,"Suicide labeled without team score");
            yield return Do(0,"environment-two",()=>Actor(2).TakeDamage(99999,true));yield return Until(()=>Stats(2).Deaths==1&&Combat.KillFeed.Count==3,"Environmental death resolved");
            yield return Do(0,"environment-zero",()=>Actor(0).TakeDamage(99999,true));yield return Until(()=>Stats(0).Deaths==2&&Combat.KillFeed.Count==3&&Combat.KillFeed[0].Sequence==3,"Configured feed cap removes oldest entry");
            Check(Combat.KillFeed[2].KillerName.ToString()=="ENTORNO"&&Stats(0).Points==50&&Stats(2).Points==100,"Environment does not invent a killer or extra points");
            yield return OpenBoard();yield return Until(()=>Hud.Cell(0,0,3)=="1"&&Hud.Cell(0,1,3)=="2","Scoreboard works while owner dead and after replacements");Capture("scoreboard-deaths.png",Hud.CanvasRoot.GetComponent<Canvas>());
            foreach(var dimensions in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,800)}){Screen.SetResolution(dimensions.x,dimensions.y,false);yield return Until(()=>Screen.width==dimensions.x&&Screen.height==dimensions.y,"Test resolution");yield return new WaitForSecondsRealtime(.2f);CheckBounds();Capture("scoreboard-"+dimensions.x+"x"+dimensions.y+".png",Hud.CanvasRoot.GetComponent<Canvas>());}
            yield return Sync("leave-ready");
            if(who==2){yield return ExitAndVerify();Write("third-left");Check(!PvPvEView.Active,"View cleaned when client leaves with TAB held");Complete(true,"");yield break;}
            yield return Until(()=>Has("third-left")&&!service.Roster.Contains(2),"Departed participant");yield return Until(()=>Hud.VisibleRows(0)==1&&Hud.VisibleRows(1)==1,"Held scoreboard removes disconnected row");Check(Hud.DisplayedIds(PvPvETeam.Alpha).SequenceEqual(new ulong[]{0}),"No disconnected ghost row");
            yield return CloseBoard();yield return Do(0,"stop",()=>Combat.MatchDeadline.Value=Network.ServerTime.Time+.2);
            yield return Until(()=>Combat.ScoreLocked.Value&&Combat.Resolution.Value==PvPvEMatchResolution.Results&&Hud.ScoreboardVisible,"Authoritative timed result after integration exercise");
            Check(Combat.Winner.Value==PvPvETeam.Alpha,"PvP score alone determines winner after PvE assists and environmental deaths");Capture("integration-result.png",Hud.CanvasRoot.GetComponent<Canvas>());
            Write("remaining-"+who);yield return Until(()=>Has("remaining-0")&&Has("remaining-1"),"Peers observed result");
            yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Lobby&&!SceneManager.GetSceneByName(config.MatchScene).isLoaded,"Same-session integrated return",180);yield return new WaitForSecondsRealtime(.3f);
            Check(!Combat.ScoreLocked.Value&&Combat.MatchDeadline.Value<0&&Combat.KillFeed.Count==0&&Deployment.Players.Count==0,"Full integration cleans clock feed and player deployment");Check(!Hud.CanvasRoot.activeSelf&&!PvPvEPreparationFlow.LocalControlsLocked,"Gameplay HUD and gates cleared in restored lobby");
            yield return Until(()=>service.GetComponent<PvPvELobbyView>().IsShowing,"3D lobby returns",40);yield return ReviewLobbyAppearance("restored-lobby-world.png");
            if(cloud){if(who==0){var query=QueryRoom(config,true);yield return Wait(query);Check(query.Result>0&&service.SessionId!="phase24-native","Real Services room is discoverable after synchronized return");Write("cloud-open-verified");}else yield return Until(()=>Has("cloud-open-verified"),"Host checked published room",40);}
            yield return ExitAndVerify();if(cloud&&who==0){var query=QueryRoom(config,false);yield return Wait(query);Check(query.Result==0,"Real Services test room removed after host exit");}yield return Until(()=>!PvPvEView.Active,"HUD canvas disposed");Check(!PvPvEView.Active,"HUD and TAB do not leak into original menu");Complete(true,"");yield break;
        }

        static async Task<int> QueryRoom(PvPvENetworkSettings config,bool open){int count=0;for(int i=0;i<10;i++){var result=await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions{FilterOptions=PvPvEUnitySessions.Query(config).Filters});count=result.Sessions.Count;if(open?count>0:count==0)return count;await Task.Delay(1000);}return count;}
        IEnumerator ReviewEquipment(){
            var c=new PvPvEClassStore(service.Settings.Loadouts).Get(0);var catalog=service.Settings.Loadouts;Check(catalog.Validate(c,out _)&&catalog.Pick10Cost(c)<=10,"Saved Pick10 class remains valid at spawn");
            Check(Local.EquipoDeClase.Value.Active&&catalog.HasWildcard(c,PvPvEWildcard.ExtraHealing)&&Local.CapacidadCuras==catalog.HealingCapacity(c),"Wildcard healing capacity applied with existing loadout");
            Check(Local.SlotGranada.Value.cantidad==catalog.GrenadeCapacity(c)&&Local.Perks.Value!=0,"Grenade inventory and passive class perks provisioned");
            var presentation=Local.GetComponent<NetworkWeaponPresentation>();int secondary=Array.FindIndex(presentation.Weapons,w=>w.Id==c.secondary);
            Check(Local.GetComponent<NetworkWeaponAttachments>().GetChoice(secondary).muzzle==c.secondaryAttachments.muzzle,"Canonical secondary accessory survives loading");
            if(who!=0)Check(!Local.AplicarCuracionDeClaseServidor(c,true),"Client cannot grant class healing supplies");
            ((InputAction)Field(Local,"accionSlot1")).ApplyBindingOverride(0,"<Keyboard>/q");int count=Local.GetSlotCura(0).cantidad;
            yield return Do(0,"injury",()=>{foreach(var player in Players){FieldSet(player,"nextRegenerationTime",Time.time+300);player.CurrentHealth.Value=1;}});yield return Until(()=>Local.CurrentHealth.Value==1,"Controlled server injury");
            Keys(Key.Q);yield return new WaitForSecondsRealtime(.12f);Keys();yield return Until(()=>Local.CurrentHealth.Value>1&&Local.GetSlotCura(0).cantidad==count-1,"Real owner healing input consumes one server-approved supply");
            Check(Local.CurrentHealth.Value==Math.Min(Local.MaxHealth,1+catalog.FindHealing(c.healing).healing),"Healing clamps canonical amount");yield return Sync("healing-reviewed");
            Check(Players.All(player=>player.GetSlotCura(0).cantidad==count-1),"All three peers see depleted healing inventory");
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
