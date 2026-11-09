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
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

namespace DeadQuarantine.Development
{
    public sealed class PvPvEPhase14InitialClassDriver : MonoBehaviour
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
        void Awake(){var c=new GameObject("Phase14 fixture selection").AddComponent<ConfiguracionPartidaSeleccionada>();c.escenarioSeleccionado=ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;c.modoSeleccionado=ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;}
        void Start(){role=Arg("-dq-role");if(role==null){enabled=false;return;}mode=Arg("-dq-mode")??"pvp";folder=Arg("-dq-output");Directory.CreateDirectory(folder);Application.runInBackground=true;PlayerPrefs.DeleteAll();StartCoroutine(Safe(Run()));}
        IEnumerator Safe(IEnumerator r){while(!finished){object current;try{if(!r.MoveNext())yield break;current=r.Current;}catch(Exception e){Complete(false,e.ToString());yield break;}if(current is IEnumerator child)yield return Safe(child);else yield return current;}}
        IEnumerator Until(Func<bool> test,string label,float seconds=35){float end=Time.realtimeSinceStartup+seconds;while(!test()){if(Time.realtimeSinceStartup>end)throw new TimeoutException(label);yield return null;}}
        IEnumerator Wait(Task task){yield return Until(()=>task.IsCompleted,"Network operation");if(task.IsFaulted)throw task.Exception;}
        void Write(string name,string value="ok")=>File.WriteAllText(Path.Combine(folder,name),value);
        bool Has(string name)=>File.Exists(Path.Combine(folder,name));
        IEnumerator Barrier(string key){Write(role+"-"+key);yield return Until(()=>Has((role=="host"?"client1":"host")+"-"+key),key);}
        void Check(bool condition,string label){if(!condition)throw new Exception(label);checks.Add(label);Save("progress-");}
        void Save(string prefix,bool ok=false,string error=""){Write(prefix+role+".json",JsonUtility.ToJson(new Result{ok=ok,role=role,mode=mode,error=error,checks=checks.ToArray(),metrics=metrics.ToArray()},true));}
        void Complete(bool ok,string error){if(finished)return;finished=true;Save("result-",ok,error);StartCoroutine(Quit(ok));}
        IEnumerator Quit(bool ok){if(mode=="pvp"&&service&&!service.CanNavigateAway)yield return Wait(service.CancelAndLeaveAsync());else if(Network&&Network.IsListening)Network.Shutdown();yield return new WaitForSecondsRealtime(.3f);Application.Quit(ok?0:1);}
        void MetricValue(string name,float value){metrics.Add(new Metric{name=name,value=value});}
        static object Field(object obj,string name)=>obj.GetType().GetField(name,Fields).GetValue(obj);
        void Stamina(float value)=>typeof(PlayerMovement).GetProperty("CurrentStamina").GetSetMethod(true).Invoke(Movement,new object[]{value});
        void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        void Home(float z=-1){var cc=Local.GetComponent<CharacterController>();cc.enabled=false;Local.transform.SetPositionAndRotation(new Vector3(role=="host"?-2:2,.1f,z),Quaternion.identity);cc.enabled=true;Local.GetComponent<NetworkTransform>().Teleport(Local.transform.position,Local.transform.rotation,Local.transform.localScale);}
        PvPvEInitialClassSelection Selection=>service.Roster.GetComponent<PvPvEInitialClassSelection>();
        PvPvEInitialClassView View=>PvPvEInitialClassView.Active;
        uint serial=100;Mouse mouse;
        PvPvEClassDefinition Definition(int index){
            var c=Settings.Loadouts.Default(index);c.name=role+" CLASE "+index;c.primary="SemiRifle";c.secondary="AutoPistol";
            if(index==1)c.primaryAttachments.scope=Attachment(c.primary,WeaponAttachmentKind.Scope);
            if(index>=2){c.perk1="perk.gloves";c.perk2="perk.boots";}
            if(index==3){c.wildcard1="wildcard.healing";c.wildcard2="wildcard.grenades";}
            if(index==4){c.secondary="BurstRifle";c.perk2="perk.holster";c.wildcard1="wildcard.overkill";c.wildcard2="wildcard.greed";c.primaryAttachments.scope=Attachment(c.primary,WeaponAttachmentKind.Scope);c.primaryAttachments.muzzle=Attachment(c.primary,WeaponAttachmentKind.Muzzle);}
            return c;
        }
        string Attachment(string gun,WeaponAttachmentKind kind)=>Settings.Loadouts.attachments.entries.First(e=>Settings.Loadouts.attachments.Allows(gun,kind,e.id)).id;
        IEnumerator Request(int index,string json,bool accepted,uint? epoch=null){
            yield return new WaitForSecondsRealtime(.18f);uint id=++serial;bool arrived=false,result=false;string error="";
            void Reply(uint e,uint r,bool a,string message){if(r!=id)return;arrived=true;result=a;error=message;}
            Selection.Reply+=Reply;Selection.ConfirmServerRpc(index,json,epoch??Selection.Epoch.Value,id);
            yield return Until(()=>arrived,"Targeted selection reply");Selection.Reply-=Reply;
            Check(result==accepted,"Selection RPC "+id+" accepted="+accepted+" "+error);
        }
        IEnumerator Run(){
            FindFirstObjectByType<GestorEscenariosPartida>().ActivarEscenario(ConfiguracionPartidaSeleccionada.Escenario.Laboratorio);
            CharacterSelection.SelectedIndex=role=="host"?1:2;service=GetComponent<PvPvEMatchmaking>();
            if(mode!="pvp"){
                Network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7798,"0.0.0.0");
                if(role=="host"){Network.StartHost();Write("host-ready");}else{yield return Until(()=>Has("host-ready"),"Ordinary host");Network.StartClient();}
                yield return Until(()=>Local&&Players.Length==2,"Ordinary players");yield return Until(()=>Local.GetComponent<MultiplayerPlayerSpawnAssigner>().IsAtSpawn.Value,"Ordinary spawn");
                Movement.MovementLocked=false;yield return new WaitForSecondsRealtime(.3f);
                Check(!PvPvEInitialClassView.BlocksLocalGameplay&&!PlayerInputGate.ActionsBlocked&&!Movement.MovementLocked,"Ordinary multiplayer retains its input and has no initial class screen");
                yield return Barrier("ordinary");Complete(true,"");yield break;
            }
            var config=Instantiate(Settings);config.MapVoteSeconds=3;config.PostVoteSeconds=1;service.ConfigureForTest(config,new DirectoryAdapter(service,PlayerPrefab,role=="host"));
            if(role!="host")yield return Until(()=>Has("host-ready"),"Host ready");yield return Wait(service.SearchAsync());if(role=="host")Write("host-ready");
            yield return Until(()=>service.Roster&&service.Roster.Members.Count==2&&Selection.IsSpawned,"Two real roster members");
            Check(!PvPvEInitialClassView.BlocksLocalGameplay,"Lobby without spawned map players does not open class selection");
            yield return Request(0,JsonUtility.ToJson(Definition(0)),false);Check(Selection.Confirmed.Count==0,"Class cannot be confirmed before map player exists");
            yield return Until(()=>service.Roster.GetComponent<PvPvEMapVoting>().Phase.Value==PvPvEVotePhase.MapSelected,"Existing map voting result");
            yield return Barrier("spawn");if(Network.IsServer)foreach(var client in Network.ConnectedClientsIds.ToArray())Instantiate(PlayerPrefab).GetComponent<NetworkObject>().SpawnAsPlayerObject(client);
            yield return Until(()=>Local&&Players.Length==2&&View&&View.IsShowing,"Selection automatically opens on map player spawn");
            yield return Until(()=>Local.GetComponent<MultiplayerPlayerSpawnAssigner>().IsAtSpawn.Value&&Switcher.IsInitialized&&!Switcher.IsSwitching,"Original player initialized");
            Movement.MovementLocked=false;Local.GetComponentInChildren<PauseController>(true).MostrarHUD();
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            Home();yield return new WaitForSecondsRealtime(.3f);
            Check(View.Store.Count==5&&View.Panel.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("SavedClass_"))==5,"Exactly five saved class cards, no duplicated class system");
            View.UseValidationProfile("initial-"+role);for(int i=0;i<5;i++)Check(View.Store.Save(i,Definition(i),out _),"Existing persistent class store seeded "+i);View.RefreshValidationClasses();
            for(int i=0;i<5;i++){View.Panel.transform.Find("SavedClass_"+i).GetComponent<Button>().onClick.Invoke();Check(View.SelectedIndex==i&&View.DisplayedClass.primary==Definition(i).primary,"Initial screen shows saved class "+i);}
            yield return Until(()=>View.PreviewsReady,"Two isolated weapon previews");
            Check(Cursor.visible&&Cursor.lockState==CursorLockMode.None,"Selection exposes the cursor");
            Check(PlayerInputGate.ActionsBlocked&&PlayerInputGate.MouseBlocked&&Movement.MovementLocked,"Initial screen blocks owner movement, look and actions");
            Local.GetComponentInChildren<PlayerLook>(true).enabled=true;Switcher.CurrentWeapon.InputLocked=false;var position=Local.transform.position;var rotation=Local.transform.rotation;int ammo=Switcher.CurrentWeapon.CurrentAmmo;
            Keys(Key.W,Key.LeftShift,Key.Space,Key.Q,Key.G);InputSystem.QueueStateEvent(mouse,new MouseState{buttons=3,delta=new Vector2(900,600)});yield return new WaitForSecondsRealtime(.35f);Keys();InputSystem.QueueStateEvent(mouse,new MouseState());
            var offset=Local.transform.position-position;offset.y=0;
            Check(offset.magnitude<.08f&&Quaternion.Angle(rotation,Local.transform.rotation)<.1f&&Switcher.CurrentWeapon.CurrentAmmo==ammo&&!Switcher.CurrentWeapon.IsAiming,"Real movement/mouse/fire/aim inputs cannot bypass selection screen");
            Keys(Key.Escape);yield return Until(()=>PauseController.LocalPlayerPaused,"Pause opens during class selection");Keys();yield return new WaitForSecondsRealtime(.2f);Check(!View.IsShowing&&PvPvEInitialClassView.BlocksLocalGameplay,"Pause uses its original UI while retaining selection input protection");Keys(Key.Escape);yield return Until(()=>!PauseController.LocalPlayerPaused,"Pause closes");Keys();yield return Until(()=>View.IsShowing&&View.PreviewsReady,"Initial screen and its isolated previews restore after pause");
            // A legacy over-budget profile remains visible but cannot be confirmed.
            var definitions=Enumerable.Range(0,5).Select(Definition).ToArray();var bad=Definition(4);
            foreach(WeaponAttachmentKind kind in Enum.GetValues(typeof(WeaponAttachmentKind))){bad.primaryAttachments.Set(kind,Attachment(bad.primary,kind));bad.secondaryAttachments.Set(kind,Attachment(bad.secondary,kind));}definitions[1]=bad;
            PlayerPrefs.SetString("DQ.PvPvE.Classes.v1.initial-"+role,"{\"version\":1,\"classes\":["+string.Join(",",definitions.Select(JsonUtility.ToJson))+"]}");PlayerPrefs.Save();View.RefreshValidationClasses();View.Choose(1);
            Check(!View.Panel.transform.Find("ConfirmInitialClass").GetComponent<Button>().interactable,"Over-budget saved class has a visible disabled confirmation");yield return Until(()=>View.PreviewsReady,"Invalid class preview");yield return new WaitForSecondsRealtime(.2f);CheckPreviewModels();Capture("initial-invalid.png",View.Panel.GetComponent<Canvas>());
            yield return Request(1,JsonUtility.ToJson(bad),false);yield return Request(-1,JsonUtility.ToJson(Definition(0)),false);yield return Request(5,JsonUtility.ToJson(Definition(0)),false);yield return Request(0,"{broken",false);yield return Request(0,new string('x',PvPvEInitialClassSelection.MaximumRequestBytes+1),false);
            var unknown=Definition(0);unknown.primary="weapon.unknown";yield return Request(0,JsonUtility.ToJson(unknown),false);
            var incompatible=Definition(2);incompatible.perk2="perk.holster";yield return Request(2,JsonUtility.ToJson(incompatible),false);
            yield return Request(0,JsonUtility.ToJson(Definition(0)),false,0);
            Check(Selection.Confirmed.Count==0,"Malformed, stale, over-budget and incompatible requests grant no selections");
            View.Panel.transform.Find("ResetInvalidClass").GetComponent<Button>().onClick.Invoke();Check(Settings.Loadouts.Validate(View.Store.Get(1),out _),"Invalid saved class can be reset with existing class defaults");
            int chosen=role=="host"?4:3;View.Choose(chosen);yield return Until(()=>View.PreviewsReady,"Selected equipment preview");yield return new WaitForSecondsRealtime(.2f);CheckPreviewModels();Capture("initial-select.png",View.Panel.GetComponent<Canvas>());
            yield return Until(()=>Local.GetComponent<PlayerCharacter>().CharacterIndex.Value==(role=="host"?1:2),"Original skin synchronized");
            yield return new WaitForSecondsRealtime(.18f);yield return Barrier("before-confirm");
            if(role!="host"){
                yield return Until(()=>Has("host-confirmed")&&Selection.TryGet(0,out _),"Host class confirmed first");
                Check(!Selection.TryGet(Network.LocalClientId,out _)&&View.IsShowing,"Slow participant remains unconfirmed and keeps its own selection screen");yield return new WaitForSecondsRealtime(1);
            }
            View.Panel.transform.Find("ConfirmInitialClass").GetComponent<Button>().onClick.Invoke();View.Confirm();
            yield return Until(()=>Selection.TryGet(Network.LocalClientId,out _),"Class approved by actual server RPC");
            if(role=="host"){Write("host-confirmed");Check(Selection.Confirmed.Count==1,"Host does not auto-confirm the other participant");}
            yield return Barrier("confirmed");yield return Until(()=>Selection.Confirmed.Count==2,"Both independent choices replicated");
            foreach(var entry in Selection.Confirmed){var c=JsonUtility.FromJson<PvPvEClassDefinition>(entry.DefinitionJson.ToString());Check(entry.ClassIndex==(entry.ClientId==0?4:3)&&Settings.Loadouts.Validate(c,out _),"Canonical server snapshot belongs to its actual sender "+entry.ClientId);}
            Check(View.IsShowing&&!View.Panel.transform.Find("ConfirmInitialClass").GetComponent<Button>().interactable&&PlayerInputGate.ActionsBlocked,"Accepted class waits without starting a match or releasing controls");
            Check(!Local.EquipoDeClase.Value.Active&&Local.Perks.Value==0,"Selection does not prematurely apply phase16 equipment");
            yield return Until(()=>View.PreviewsReady,"Approved preview");yield return new WaitForSecondsRealtime(.2f);CheckPreviewModels();Capture("initial-confirmed.png",View.Panel.GetComponent<Canvas>());
            var canonical=Enumerable.Range(0,Selection.Confirmed.Count).Select(i=>Selection.Confirmed[i]).ToArray();yield return Request(chosen,JsonUtility.ToJson(Definition(chosen)),true);yield return Request(0,JsonUtility.ToJson(Definition(0)),false);
            Check(Selection.Confirmed.Count==2&&Enumerable.Range(0,Selection.Confirmed.Count).Select(i=>Selection.Confirmed[i]).ToArray().SequenceEqual(canonical),"Duplicate confirmation is idempotent; post-confirm replacement cannot mutate snapshot");
            yield return Barrier("new-cycle");uint previous=Selection.Epoch.Value;if(Network.IsServer){Selection.BeginForMapServer();Write("cycle-reset");}
            yield return Until(()=>Has("cycle-reset")&&Selection.Epoch.Value!=previous&&Selection.Confirmed.Count==0,"Explicit future map boundary resets selections");
            yield return Request(0,JsonUtility.ToJson(Definition(0)),false,previous);Check(Selection.Confirmed.Count==0,"Prior-map request cannot confirm the new map");
            yield return Barrier("close-window");if(Network.IsServer){Selection.CloseSelectionServer();Write("window-closed");}
            yield return Until(()=>Has("window-closed")&&!View.IsShowing&&!PvPvEInitialClassView.BlocksLocalGameplay,"Explicit close restores input gate and removes selection UI");Check(!Movement.MovementLocked,"Underlying movement lock is preserved and restored");
            yield return Request(0,JsonUtility.ToJson(Definition(0)),false);
            yield return Barrier("reopen");if(Network.IsServer){Selection.BeginForMapServer();Write("window-reopened");}
            yield return Until(()=>Has("window-reopened")&&View.IsShowing,"Selection lifecycle can reopen once without duplicate canvases");
            yield return Request(chosen,JsonUtility.ToJson(Definition(chosen)),true);yield return Barrier("reconfirmed");yield return Until(()=>Selection.Confirmed.Count==2,"Selections re-established for disconnect test");
            Check(FindObjectsByType<PvPvEInitialClassView>(FindObjectsSortMode.None).Length==1,"Exactly one local initial selection view");
            if(role=="client1"){
                yield return Wait(service.CancelAndLeaveAsync());yield return Until(()=>!PvPvEInitialClassView.Active&&!PvPvEInitialClassView.BlocksLocalGameplay,"Disconnect destroys selection view and releases its gate");Write("client-left");Complete(true,"");
            }else{
                yield return Until(()=>Has("client-left")&&service.Roster.Members.Count==1&&Selection.Confirmed.Count==1,"Disconnected participant removed from confirmed classes");
                Check(Selection.TryGet(Network.LocalClientId,out _)&&!Selection.TryGet(1,out _),"Disconnect preserves host choice and removes departed choice");yield return Wait(service.CancelAndLeaveAsync());yield return Until(()=>!PvPvEInitialClassView.Active&&!PvPvEInitialClassView.BlocksLocalGameplay,"Host exit cleans initial selection state");Complete(true,"");
            }
        }
        void CheckPreviewModels(){foreach(var secondary in new[]{false,true}){string id=secondary?View.DisplayedClass.secondary:View.DisplayedClass.primary;var choice=secondary?View.DisplayedClass.secondaryAttachments:View.DisplayedClass.primaryAttachments;string node=secondary?"SecondaryPreview":"PrimaryPreview";var preview=View.Panel.transform.Find(node+"Frame/"+node).GetComponent<PvPvEWeaponPreview>();var holder=(GameObject)Field(preview,"holder");var attachments=holder.GetComponentInChildren<WeaponAttachments>(true);Check(holder.transform.GetChild(0).name=="Preview_"+id&&(!Settings.Loadouts.attachments.Eligible(id)||attachments&&attachments.ApprovedChoice.Equals(choice)),"Rendered preview model and accessories match selected "+id);}}
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
            public Task<IPvPvESession> FindOrCreateAsync(PvPvENetworkSettings settings){var n=service.Network;n.AddNetworkPrefab(prefab);foreach(var grenade in service.GetComponent<PvPvEPhase14InitialClassDriver>().GrenadePrefabs)n.AddNetworkPrefab(grenade);n.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7799,"0.0.0.0");if(!(host?n.StartHost():n.StartClient()))throw new Exception("NGO start failed");return Task.FromResult<IPvPvESession>(new Lease(n,host));}
        }
        sealed class Lease:IPvPvESession
        {
            readonly NetworkManager n;public string Id=>"phase14-native";public bool IsHost{get;}public int ParticipantCount=>n&&n.IsServer?n.ConnectedClients.Count:1;public event Action Closed{add{}remove{}}
            public Lease(NetworkManager n,bool host){this.n=n;IsHost=host;}public Task CloseAdmissionAsync()=>Task.CompletedTask;public Task LeaveAsync(){if(n&&n.IsListening)n.Shutdown();return Task.CompletedTask;}
        }
    }
}
#endif
