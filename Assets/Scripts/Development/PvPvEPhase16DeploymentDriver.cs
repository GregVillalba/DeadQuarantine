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
    public sealed class PvPvEPhase16DeploymentDriver : MonoBehaviour
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
        void OldAwake(){var c=new GameObject("Phase16 fixture selection").AddComponent<ConfiguracionPartidaSeleccionada>();c.escenarioSeleccionado=ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;c.modoSeleccionado=ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;}
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
        PvPvEInitialClassView View=>PvPvEInitialClassView.Active;
        int mapIndex;
        IEnumerator Run(){
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;keyboard=InputSystem.AddDevice<Keyboard>();service=FindFirstObjectByType<PvPvEMatchmaking>();
            service.Changed+=()=>Write("connection-"+role+".txt",service.State+" | "+service.Error+" | "+(Flow?Flow.Stage.Value.ToString():"no flow"));
            CharacterSelection.SelectedIndex=role=="host"?1:2;
            var config=Instantiate(service.Settings);mapIndex=int.TryParse(Arg("-dq-map"),out int index)?index:0;
            config.SpawnProtectionSeconds=8;config.MapVoteSeconds=12;config.PostVoteSeconds=1;config.MapLoadSeconds=120;config.PlayerPrepareSeconds=90;
            service.ConfigureForTest(config,new DirectoryAdapter(service,config.PlayerPrefab,role=="host"));
            var store=new PvPvEClassStore(config.Loadouts);
            for(int i=0;i<5;i++){
                var c=config.Loadouts.Default(i);c.name=role+" CLASE "+i;c.primary=i==1?(role=="host"?"P90":"MAC10"):"SemiRifle";c.secondary="AutoPistol";
                c.wildcard1="wildcard.healing";c.perk1=config.Loadouts.perks.entries[0].id;
                var scope=config.Loadouts.attachments.entries.FirstOrDefault(e=>e.kind==WeaponAttachmentKind.Scope&&e.weapons.Contains(c.primary));if(scope!=null)c.primaryAttachments.scope=scope.id;
                Check(store.Save(i,c,out var error),"Existing saved class valid "+i+" "+error);
            }
            var menu=service.GetComponent<PvPvEModeMenu>();var initial=menu.GetComponentInParent<Canvas>().transform.Find("menuPrincipalPanel");if(initial)initial.gameObject.SetActive(false);menu.OpenModes();menu.OpenPvPvE();
            if(role!="host")yield return Until(()=>Has("host-ready"),"Host ready",150);yield return Wait(service.SearchAsync());if(role=="host")Write("host-ready");
            yield return Until(()=>Flow&&Deployment&&service.Roster.Members.Count==2,"Production roster with both teams",150);
            Check(!PvPvEPreparationFlow.LocalControlsLocked,"Lobby retains normal controls");
            Deployment.AcknowledgeServerRpc(uint.MaxValue,ulong.MaxValue,0);
            var vote=service.Roster.GetComponent<PvPvEMapVoting>();yield return Until(()=>vote.Phase.Value==PvPvEVotePhase.Voting,"Vote open");vote.CastVoteServerRpc(mapIndex);
            yield return Until(()=>Local&&View&&View.IsShowing&&PvPvEMapContext.Active,"Original menu to real mode map",180);
            Check(PvPvEMapContext.Active.Scenario.TipoEscenario==config.Maps[mapIndex].Id,"Voted map activated");
            var markers=PvPvEMapContext.Active.Scenario.GetComponentsInChildren<PvPvETeamSpawnPoint>();
            Check(markers.Count(m=>m.Team==PvPvETeam.Alpha)>=5&&markers.Count(m=>m.Team==PvPvETeam.Bravo)>=5,"At least five accessible configured positions per team");
            View.Choose(role=="host"?2:4);yield return Until(()=>View.PreviewsReady,"Class previews",120);
            if(role!="host")yield return new WaitForSecondsRealtime(3);
            View.Confirm();yield return Until(()=>Flow.GetComponent<PvPvEInitialClassSelection>().TryGet(Network.LocalClientId,out _),"Initial canonical class confirmed",120);
            yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Countdown,"Placement and equipment acknowledgments unlock countdown",150);
            Check(Deployment.AllPlaced&&PvPvEPreparationFlow.LocalControlsLocked&&Movement.MovementLocked,"Both positioned and equipped before countdown; movement blocked");
            Check(Deployment.TryGet(Network.LocalClientId,out var own)&&Vector3.Distance(Local.transform.position,own.Position)<1,"Owner-authoritative transform reaches server-chosen position");
            Check(service.Roster.TryGetMember(Network.LocalClientId,out var member)&&markers.Any(m=>m.Team==member.Team&&Vector3.Distance(m.transform.position,own.Position)<.7f),"Spawn belongs to assigned typed team");
            Check(Players.Length==2&&Vector3.Distance(Players[0].transform.position,Players[1].transform.position)>config.SpawnSpacing,"No duplicate PlayerObjects or overlapping opposing spawns");
            Check(Local.EquipoDeClase.Value.Active&&Local.EquipoDeClase.Value.primary.ToString()=="SemiRifle"&&Switcher.ObtenerArmaEquipadaId()=="SemiRifle","Canonical primary and secondary equipped using original switcher");
            Check(Local.CuracionPvPvEActiva&&Local.SlotsCuras[0].cantidad==config.Loadouts.HealingCapacity(store.Get(2))&&Local.Perks.Value!=0,"Healing, wildcard capacity and passive perks applied by server");
            var attachments=Local.GetComponent<NetworkWeaponAttachments>();var presentation=Local.GetComponent<NetworkWeaponPresentation>();int weapon=Array.FindIndex(presentation.Weapons,w=>w.Id=="SemiRifle");
            Check(attachments.GetChoice(weapon).Equals(store.Get(2).primaryAttachments),"Approved class attachments synchronized");
            if(Network.IsServer){int before=Local.CurrentHealth.Value;Local.TakeDamage(1000,true);Check(Local.CurrentHealth.Value==before,"Server rejects damage during countdown");}
            Capture("countdown.png",Flow.GetComponent<PvPvEPreparationView>().Panel.GetComponent<Canvas>());
            yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Playing&&!Movement.MovementLocked&&Local.GetComponentInChildren<PlayerLook>(true).enabled,"START releases both movement and look",30);
            Check(!PlayerInputGate.ActionsBlocked&&!PvPvEPreparationFlow.LocalControlsLocked&&!Switcher.CurrentWeapon.InputLocked,"Combat input releases after all-ready server countdown");
            Check(Local.GetComponent<PlayerCharacter>().AppliedIndex==(role=="host"?1:2),"Correct independent skin preserved");
            yield return new WaitForSecondsRealtime(.2f);CaptureWorld("playing.png");
            if(Network.IsServer){int before=Local.CurrentHealth.Value;Local.TakeDamage(25,true);Check(Local.CurrentHealth.Value==before,"Initial brief protection blocks server damage");}
            yield return new WaitForSecondsRealtime(.25f);
            Keys();yield return null;Keys(Key.W);
            yield return Until(()=>Local&&Movement.MoveInput.sqrMagnitude>.5f,"Input after START: focus="+Application.isFocused+", enabled="+Movement.enabled+", locked="+Movement.MovementLocked+", map="+((PlayerControls)Field(Movement,"controls")).Player.Move.enabled+", raw="+((PlayerControls)Field(Movement,"controls")).Player.Move.ReadValue<Vector2>()+", gate="+PlayerInputGate.ActionsBlocked,3);
            Check(Movement.MoveInput.sqrMagnitude>.5f,"Input System accepts movement after START");Keys();yield return null;
            var pause=Local.GetComponentInChildren<PauseController>(true);pause.PausarJuego();yield return null;
            // Separate native processes share Windows PlayerPrefs; serialize profile writes in the fixture.
            if(role=="client1")yield return Until(()=>Has("host-class-pending"),"Host profile isolated");
            Check(store.Save(1,store.Get(1),out _),"Fixture restores this peer's own saved class before opening selector");
            var classView=Deployment.GetComponent<PvPvEDeploymentView>();
            pause.AbrirConfiguracion();yield return null;Check(!classView.Panel.activeInHierarchy&&PauseController.LocalPlayerPaused,"Class overlay leaves existing settings menu usable while retaining pause");
            pause.VolverDesdeConfiguracion();yield return null;Check(classView.Panel.activeInHierarchy,"Class options return only in pause home");classView.Choose(1);
            yield return Until(()=>Deployment.TryGet(Network.LocalClientId,out var row)&&row.PendingIndex==1,"Pause class change accepted for next life");
            Check(Switcher.ObtenerArmaEquipadaId()=="SemiRifle"&&Local.EquipoDeClase.Value.primary.ToString()=="SemiRifle","Pending class never replaces live equipment");if(role=="host")Write("host-class-pending");
            Capture("change-class.png",classView.Panel.GetComponent<Canvas>());pause.ReanudarJuego();
            yield return new WaitForSecondsRealtime(.3f);
            Deployment.ChangeClassServerRpc(9,"{}",selectionEpoch());yield return new WaitForSecondsRealtime(.3f);
            Check(Deployment.TryGet(Network.LocalClientId,out var pending)&&pending.PendingIndex==1,"Malformed class index cannot replace pending canonical class");
            Deployment.ChangeClassServerRpc(0,JsonUtility.ToJson(store.Get(0)),0);yield return new WaitForSecondsRealtime(.3f);
            Check(Deployment.TryGet(Network.LocalClientId,out pending)&&pending.PendingIndex==1,"Previous-cycle class request rejected");
            yield return Barrier("kill");
            ulong oldId=Local.NetworkObjectId;uint oldRevision=pending.Revision;
            if(Network.IsServer){foreach(var h in Players){Deployment.CancelProtectionServer(h.OwnerClientId);h.TakeDamage(99999,true);h.EliminatePlayer();}Write("killed");}
            yield return Until(()=>Deployment.TryGet(Network.LocalClientId,out var row)&&row.Life==PvPvELifeStage.Dead,"Server death starts synchronized ten-second respawn",30);
            Check(!Local.IsDowned&&PvPvEPreparationFlow.LocalControlsLocked,"PvPvE death bypasses cooperative downed/lives system and locks input");
            double respawnAt=GetRow().Due;
            yield return new WaitForSecondsRealtime(.3f);
            var death=Deployment.GetComponent<PvPvEDeathPresentation>();
            Check(death.FloatingCamera&&death.VisibleCorpses==2&&death.RagdollBodies>=20,"Both deaths create cosmetic ragdolls and owner floating camera");
            Capture("death.png",classView.Panel.GetComponent<Canvas>());CaptureWorld("ragdoll.png");
            yield return new WaitForSecondsRealtime(3.2f);
            Check(death.VisibleCorpses==0&&Local.NetworkObjectId==oldId&&GetRow().Life==PvPvELifeStage.Dead,"Corpses removed after three seconds; no early respawn");
            Deployment.AcknowledgeServerRpc(oldRevision,oldId,selectionEpoch());
            yield return Until(()=>Local&&Local.NetworkObjectId!=oldId&&GetRow().Life==PvPvELifeStage.Alive&&!Movement.MovementLocked,"Fresh player respawns safely with controls",90);
            Check(Network.ServerTime.Time>=respawnAt-.15&&Players.Length==2,"Ten-second deadline respected and each owner has exactly one player");
            Check(Local.IsAlive&&Local.CurrentHealth.Value==Local.MaxHealth&&Local.GetComponent<PlayerCharacter>().AppliedIndex==(role=="host"?1:2),"Respawn restores health/state while retaining selected skin");
            string expected=role=="host"?"P90":"MAC10";
            Check(Local.EquipoDeClase.Value.primary.ToString()==expected&&Switcher.ObtenerArmaEquipadaId()==expected&&GetRow().PendingIndex==-1,"Pending class becomes active exactly once on respawn (expected="+expected+", health="+Local.EquipoDeClase.Value.primary+", equipped="+Switcher.ObtenerArmaEquipadaId()+", pending="+GetRow().PendingIndex+")");
            Check(Switcher.CurrentWeapon.MunicionCompleta&&Local.SlotGranada.Value.cantidad==config.Loadouts.GrenadeCapacity(store.Get(1))&&Local.Perks.Value!=0&&Local.SlotsCuras[0].cantidad==config.Loadouts.HealingCapacity(store.Get(1)),"Fresh ammo, healing, grenade supply and perks replenished per life");
            yield return new WaitForSecondsRealtime(.2f);
            Check(!death.FloatingCamera&&!PlayerInputGate.ActionsBlocked,"Floating camera cleaned up and gameplay input restored");
            var weaponDef=Local.GetComponent<NetworkWeaponPresentation>();var current=Array.Find(weaponDef.Weapons,w=>w.Id==expected);int currentIndex=Array.IndexOf(weaponDef.Weapons,current);
            Check(Local.GetComponent<NetworkWeaponAttachments>().GetChoice(currentIndex).Equals(store.Get(1).primaryAttachments),"Respawn applies new primary attachments on both peers");
            // Exercise the existing shot presentation path, not a fabricated score or direct kill RPC.
            var origin=current.Source.AimOrigin;weaponDef.PublishShot(current.Source,new[]{origin+Local.transform.forward*20});
            yield return Until(()=>GetRow().ProtectedUntil==0,"Offensive action cancels brief respawn protection",20);
            if(Network.IsServer){int health=Local.CurrentHealth.Value;Local.TakeDamage(10,true);Check(Local.CurrentHealth.Value==health-10,"Damage accepted after offensive protection cancellation");}
            yield return Barrier("done");
            if(role=="client1"){yield return ExitAndVerify();Write("client-left");Complete(true,"");yield break;}
            yield return Until(()=>Has("client-left")&&Flow.Stage.Value==PvPvEPrepareStage.Failed&&Deployment.Players.Count==1,"Disconnected team removes deployment record without an infinite wait",30);
            Check(Deployment.Players.Count==1,"Departed player removed from deployment roster");yield return ExitAndVerify();Complete(true,"");
        }
        uint selectionEpoch()=>Flow.GetComponent<PvPvEInitialClassSelection>().Epoch.Value;
        PvPvEDeployment GetRow(){Deployment.TryGet(Network.LocalClientId,out var row);return row;}
        IEnumerator ExitAndVerify(){yield return Wait(service.CancelAndLeaveAsync());yield return Until(()=>!SceneManager.GetSceneByName(service.Settings.MatchScene).isLoaded,"Separate map unloaded",150);Check(!PvPvETeamDeployment.Active&&!PvPvEPreparationFlow.Active&&!PlayerInputGate.ActionsBlocked,"Exit cleans deployment, UI and input state");Check(service.CanNavigateAway,"Menu/session restored");}
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
            readonly NetworkManager n;public string Id=>"phase16-native";public bool IsHost{get;}public int ParticipantCount=>n&&n.IsServer?n.ConnectedClients.Count:1;public event Action Closed{add{}remove{}}
            public Lease(NetworkManager n,bool host){this.n=n;IsHost=host;}public Task CloseAdmissionAsync()=>Task.CompletedTask;public Task LeaveAsync(){if(n&&n.IsListening)n.Shutdown();return Task.CompletedTask;}
        }
    }
}
#endif
