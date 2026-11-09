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
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

namespace DeadQuarantine.Development
{
    public sealed class PvPvEPhase12WildcardsDriver : MonoBehaviour
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
        void Awake(){var c=new GameObject("Phase12 fixture selection").AddComponent<ConfiguracionPartidaSeleccionada>();c.escenarioSeleccionado=ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;c.modoSeleccionado=ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;}
        void Start(){role=Arg("-dq-role");if(role==null){enabled=false;return;}mode=Arg("-dq-perk-mode")??"merchant";folder=Arg("-dq-output");Directory.CreateDirectory(folder);Application.runInBackground=true;PlayerPrefs.DeleteAll();StartCoroutine(Safe(Run()));}
        IEnumerator Safe(IEnumerator r){while(!finished){object current;try{if(!r.MoveNext())yield break;current=r.Current;}catch(Exception e){Complete(false,e.ToString());yield break;}if(current is IEnumerator child)yield return Safe(child);else yield return current;}}
        IEnumerator Until(Func<bool> test,string label,float seconds=35){float end=Time.realtimeSinceStartup+seconds;while(!test()){if(Time.realtimeSinceStartup>end)throw new TimeoutException(label);yield return null;}}
        IEnumerator Wait(Task task){yield return Until(()=>task.IsCompleted,"Network operation");if(task.IsFaulted)throw task.Exception;}
        void Write(string name,string value="ok")=>File.WriteAllText(Path.Combine(folder,name),value);
        bool Has(string name)=>File.Exists(Path.Combine(folder,name));
        IEnumerator Barrier(string key){Write(role+"-"+key);yield return Until(()=>Has((role=="host"?"client1":"host")+"-"+key),key);}
        void Check(bool condition,string label){if(!condition)throw new Exception(label);checks.Add(label);Save("progress-");}
        void Save(string prefix,bool ok=false,string error=""){Write(prefix+role+".json",JsonUtility.ToJson(new Result{ok=ok,role=role,mode=mode,error=error,checks=checks.ToArray(),metrics=metrics.ToArray()},true));}
        void Complete(bool ok,string error){if(finished)return;finished=true;Save("result-",ok,error);StartCoroutine(Quit(ok));}
        IEnumerator Quit(bool ok){if(mode=="pvp"&&service)yield return Wait(service.CancelAndLeaveAsync());else if(Network&&Network.IsListening)Network.Shutdown();yield return new WaitForSecondsRealtime(.3f);Application.Quit(ok?0:1);}
        void MetricValue(string name,float value){metrics.Add(new Metric{name=name,value=value});}
        static object Field(object obj,string name)=>obj.GetType().GetField(name,Fields).GetValue(obj);
        void Stamina(float value)=>typeof(PlayerMovement).GetProperty("CurrentStamina").GetSetMethod(true).Invoke(Movement,new object[]{value});
        void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        void Home(float z=-1){var cc=Local.GetComponent<CharacterController>();cc.enabled=false;Local.transform.SetPositionAndRotation(new Vector3(role=="host"?-2:2,.1f,z),Quaternion.identity);cc.enabled=true;Local.GetComponent<NetworkTransform>().Teleport(Local.transform.position,Local.transform.rotation,Local.transform.localScale);}
        IEnumerator Run()
        {
            FindFirstObjectByType<GestorEscenariosPartida>().ActivarEscenario(ConfiguracionPartidaSeleccionada.Escenario.Laboratorio);
            service=GetComponent<PvPvEMatchmaking>();
            if(mode=="pvp"){
                var config=Instantiate(Settings);config.MapVoteSeconds=600;service.ConfigureForTest(config,new DirectoryAdapter(service,PlayerPrefab,role=="host"));
                if(role!="host")yield return Until(()=>Has("host-ready"),"Host ready");yield return Wait(service.SearchAsync());if(role=="host")Write("host-ready");
                yield return Until(()=>service.Roster&&service.Roster.Members.Count==2,"PvPvE roster");
                if(Network.IsServer)foreach(var id in Network.ConnectedClientsIds.ToArray())Instantiate(PlayerPrefab).GetComponent<NetworkObject>().SpawnAsPlayerObject(id);
            }else{
                Network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7798,"0.0.0.0");
                if(role=="host"){Network.StartHost();Write("host-ready");}else{yield return Until(()=>Has("host-ready"),"Host ready");Network.StartClient();}
            }
            yield return Until(()=>Local&&Players.Length==2,"Two actual original players");yield return Until(()=>Local.GetComponent<MultiplayerPlayerSpawnAssigner>().IsAtSpawn.Value,"Spawn ready");
            Local.GetComponentInChildren<PauseController>(true).MostrarHUD();
            Movement.MovementLocked=false; // The isolated fixture omits GameStartCurtain/StoryIntroController.
            foreach(var b in Local.GetComponentsInChildren<MonoBehaviour>(true))if(new[]{"PlayerLook","PlayerCameraBob","PlayerDownedEffects"}.Contains(b.GetType().Name))b.enabled=false;
            keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var controls=(PlayerControls)Field(Movement,"controls");controls.Player.Move.ApplyBindingOverride(1,"<Keyboard>/w");
            Home();yield return new WaitForSecondsRealtime(.6f);
            Check(Local.Perks.Value==0&&Local.PerkReloadSpeed==1&&Local.PerkSwitchSpeed==1,"No bonuses without perks; original weapon defaults preserved");
            Check(Vendor.Offers.Count(o=>o.EsPerk)==5,"Existing merchant contains exactly the five backlog passives");
            Local.SolicitarDeslizamientoServerRpc(Vector3.forward);yield return new WaitForSecondsRealtime(.15f);Check(!Local.Deslizando,"Slide request without kneepads is rejected by server");
            if(mode=="pvp"){if(Arg("-dq-pick10")=="1")yield return Pick10();else yield return Pvp();}else{Check(!Local.EquipoDeClase.Value.Active&&!Local.GranadaPvPvEActiva,"Ordinary multiplayer retains original inventory policy");Check(Local.CapacidadGranadas==1&&Local.PuedeGuardarGranada("Granadas"),"Legacy merchant grenade capacity unchanged");}
            yield return Barrier("finished");Complete(true,"");
        }
        PvPvEClassDefinition Pick10Class(){
            var c=ClassFor(1);
            foreach(var kind in new[]{WeaponAttachmentKind.Scope,WeaponAttachmentKind.Muzzle}){
                var entry=Settings.Loadouts.attachments.entries.First(e=>Settings.Loadouts.attachments.Allows(c.primary,kind,e.id));c.primaryAttachments.Set(kind,entry.id);
            }
            return c;
        }
        IEnumerator Pick10(){
            var catalog=Settings.Loadouts;var c=Pick10Class();
            Check(catalog.Pick10Cost(catalog.Default(0))==4,"Each mandatory weapon and utility type costs exactly one point");
            Check(catalog.Pick10Cost(c)==10&&catalog.Validate(c,out _),"Ten-point class includes both primaries, two accessories, two perks, two utility types and both Wildcards");
            foreach(var secondary in new[]{false,true})foreach(WeaponAttachmentKind kind in Enum.GetValues(typeof(WeaponAttachmentKind))){
                var test=ClassFor(1);var gun=secondary?test.secondary:test.primary;
                var entry=catalog.attachments.entries.First(e=>catalog.attachments.Allows(gun,kind,e.id));
                if(secondary)test.secondaryAttachments.Set(kind,entry.id);else test.primaryAttachments.Set(kind,entry.id);
                Check(catalog.Pick10Cost(test)==9&&catalog.Validate(test,out _),"Each accessory slot costs one point on "+(secondary?"second":"first")+" primary / "+kind);
            }
            var extra=ClassFor(0);Check(catalog.Pick10Cost(extra)==6&&catalog.HealingCapacity(extra)>1&&catalog.GrenadeCapacity(extra)>1,"Extra quantities do not add per-unit costs");
            var illegal=c.Copy();illegal.secondaryAttachments.scope=catalog.attachments.entries.First(e=>catalog.attachments.Allows(illegal.secondary,WeaponAttachmentKind.Scope,e.id)).id;
            Check(catalog.Pick10Cost(illegal)==11&&!catalog.Validate(illegal,out var error)&&error.Contains("PICK 10"),"Eleventh point is rejected centrally");
            var tampered=JsonUtility.FromJson<PvPvEClassDefinition>(JsonUtility.ToJson(illegal).TrimEnd('}')+",\"pick10Points\":0}");
            Check(!catalog.Validate(tampered,out _),"A forged client points field cannot bypass derived cost");
            var store=new PvPvEClassStore(catalog,"pick10-"+role);Check(store.Save(0,c,out _),"Valid ten-point class persists");
            Check(!store.Save(0,illegal,out _)&&catalog.Pick10Cost(new PvPvEClassStore(catalog,"pick10-"+role).Get(0))==10,"Rejected edit leaves persistent class intact");
            yield return Barrier("pick10-apply");
            if(Network.IsServer){foreach(var player in Players){
                Check(player.AplicarWildcardsDeClaseServidor(c,true),"Server accepts canonical ten-point class");
                var equipment=player.EquipoDeClase.Value;var perks=player.Perks.Value;var healing=player.GetSlotCura(0);var grenades=player.SlotGranada.Value;
                Check(!player.AplicarWildcardsDeClaseServidor(illegal,true)&&!player.AplicarPerksDeClaseServidor(illegal)&&!player.AplicarCuracionDeClaseServidor(illegal,true),"All existing authoritative class entry points reject over-budget equipment");
                Check(player.EquipoDeClase.Value.Equals(equipment)&&player.Perks.Value==perks&&player.GetSlotCura(0).Equals(healing)&&player.SlotGranada.Value.Equals(grenades),"Rejected class cannot partially grant weapons, perks or supplies");
            }Write("pick10-ready");}
            yield return Until(()=>Has("pick10-ready")&&Players.All(player=>player.EquipoDeClase.Value.mask==12&&player.Perks.Value==9)&&Local.RevisionArmasAplicada==Local.EquipoDeClase.Value.revision&&!Switcher.IsSwitching,"Approved equipment synchronized Host + Client");
            if(!Network.IsServer)Check(!Local.AplicarWildcardsDeClaseServidor(c,true),"Client cannot directly grant a valid class either");
            Switcher.SelectCategory(WeaponSlotCategory.Secondary);yield return Until(()=>!Switcher.IsSwitching&&Local.GetComponent<NetworkWeaponPresentation>().Weapons[Switcher.CurrentWeaponIndex].Id==c.secondary,"Approved second primary still equips");
            yield return Barrier("pick10-second");yield return Until(()=>Players.All(player=>player.GetComponent<NetworkWeaponPresentation>().Weapons[player.GetComponent<NetworkWeaponPresentation>().EquippedIndex].Id==c.secondary),"Remote presentation retains the same approved second primary");
        }
        PvPvEClassDefinition ClassFor(int pass){
            var c=Settings.Loadouts.Default(0);c.primary="SemiRifle";c.secondary="AutoPistol";
            if(pass==0){c.wildcard1="wildcard.healing";c.wildcard2="wildcard.grenades";}
            else{c.wildcard1="wildcard.overkill";c.wildcard2="wildcard.greed";c.secondary="BurstRifle";c.perk1="perk.gloves";c.perk2="perk.holster";}
            return c;
        }
        IEnumerator Pvp()
        {
            var catalog=Settings.Loadouts;
            Check(catalog.wildcards.entries.Length==4,"Four configured Wildcards, two persisted slots per class");
            foreach(var a in new[]{"","wildcard.healing","wildcard.grenades","wildcard.overkill","wildcard.greed"})
            foreach(var b in new[]{"","wildcard.healing","wildcard.grenades","wildcard.overkill","wildcard.greed"}){
                var c=catalog.Default(0);c.wildcard1=a;c.wildcard2=b;
                Check(catalog.Validate(c,out _)==(a==""||a!=b),"Wildcard pair rule "+a+"/"+b);
            }
            var illegal=ClassFor(1);illegal.wildcard1="";Check(!catalog.Validate(illegal,out _),"Second primary requires Overkill");
            illegal=ClassFor(1);illegal.wildcard2="";Check(!catalog.Validate(illegal,out _),"Same-category perks require Perk Greed");
            illegal=ClassFor(1);illegal.secondary=illegal.primary;Check(!catalog.Validate(illegal,out _),"Overkill cannot duplicate the same weapon");
            illegal=ClassFor(1);illegal.perk2=illegal.perk1;Check(!catalog.Validate(illegal,out _),"Greed cannot duplicate a perk");
            illegal=ClassFor(0);illegal.wildcard2="wildcard.unknown";Check(!catalog.Validate(illegal,out _)&&catalog.Repair(illegal,0).wildcard2=="","Unknown saved IDs rejected and repaired");
            if(!Network.IsServer)Check(!Local.AplicarWildcardsDeClaseServidor(ClassFor(0)),"Client cannot grant Wildcards directly");
            for(int pass=0;pass<2;pass++){
                yield return Barrier("apply-"+pass);
                if(Network.IsServer){foreach(var p in Players){Check(p.AplicarWildcardsDeClaseServidor(ClassFor(pass),true),"Server applies validated class "+pass);typeof(PlayerHealth).GetField("nextRegenerationTime",Fields).SetValue(p,Time.time+100000f);}Write("applied-"+pass);}
                var c=ClassFor(pass);uint expectedMask=pass==0?3u:12u;
                yield return Until(()=>Has("applied-"+pass)&&Players.All(p=>p.EquipoDeClase.Value.mask==expectedMask)&&Local.RevisionArmasAplicada==Local.EquipoDeClase.Value.revision&&!Switcher.IsSwitching,"Class equipment synchronized and installed");
                Check(Local.CapacidadCuras==catalog.HealingCapacity(c)&&Local.GetSlotCura(0).cantidad==Local.CapacidadCuras,"Healing amount derives from server catalog plus Wildcard "+pass);
                Check(Local.CapacidadGranadas==catalog.GrenadeCapacity(c)&&Local.SlotGranada.Value.cantidad==Local.CapacidadGranadas,"Grenade count derives from server catalog plus Wildcard "+pass);
                Check(Players.All(p=>p.GranadaPvPvE.Value.itemId.ToString()==c.grenade&&p.TipoCuracionPvPvE==c.healing),"Both peers see the selected utility types "+pass);
                Check(!Local.PuedeGuardarGranada("Granadas")&&Local.EspaciosExtraAlMaximo(SlotInventarioExtra.Granadas),"Legacy merchant cannot bypass class grenade policy");
                if(pass==1){
                    Check(Local.PerkReloadSpeed==1.5f&&Local.PerkSwitchSpeed==1.25f,"Perk Greed enables both real same-category effects");
                    Check(Switcher.GetIndexForCategory(WeaponSlotCategory.Primary)!=Switcher.GetIndexForCategory(WeaponSlotCategory.Secondary),"Overkill retains two distinct inventory positions");
                    Switcher.SelectCategory(WeaponSlotCategory.Secondary);
                    yield return Until(()=>!Switcher.IsSwitching&&Local.GetComponent<NetworkWeaponPresentation>().Weapons[Switcher.CurrentWeaponIndex].Id==c.secondary,"Second primary equips through normal switcher");
                    yield return Barrier("secondary");
                    yield return Until(()=>Players.All(p=>p.GetComponent<NetworkWeaponPresentation>().Weapons[p.GetComponent<NetworkWeaponPresentation>().EquippedIndex].Id==c.secondary),"Remote third-person presentation receives second primary");
                    Switcher.UnlockWeapon("P90");yield return new WaitForSecondsRealtime(.2f);
                    Check(!Switcher.IsUnlocked("P90"),"Class weapon policy rejects unselected third weapon");
                    Switcher.SelectCategory(WeaponSlotCategory.Primary);yield return Until(()=>!Switcher.IsSwitching&&Local.GetComponent<NetworkWeaponPresentation>().Weapons[Switcher.CurrentWeaponIndex].Id==c.primary,"First primary remains selectable");
                }else{
                    var hud=Local.GetComponentInChildren<HUDController>(true);Capture("wildcards-supplies.png",hud.GetComponentInParent<Canvas>());
                    // Actual Q action, not a synthetic client-side inventory decrement.
                    yield return Barrier("damage");if(Network.IsServer){foreach(var p in Players)p.CurrentHealth.Value=1;Write("damaged");}
                    yield return Until(()=>Has("damaged")&&Local.CurrentHealth.Value==1,"Damage replicated");
                    Keys(Key.Q);yield return new WaitForSecondsRealtime(.15f);Keys();
                    yield return Until(()=>Local.GetSlotCura(0).cantidad==Local.CapacidadCuras-1,"Q consumes one extra-capacity healing charge on server");
                    Check(Local.CurrentHealth.Value==1+catalog.FindHealing(c.healing).healing,"Q uses existing healing amount");
                    int n=Local.CapacidadGranadas;
                    // Existing grenade RPC creates a real NGO projectile and consumes one server charge.
                    for(int i=0;i<n;i++){
                        if(i==0){Keys(Key.G);yield return new WaitForSecondsRealtime(.15f);Keys();}else Local.SolicitarLanzamientoGranadaServerRpc(Local.transform.position+Vector3.up*2+Vector3.forward*10,Vector3.forward*15);
                        int count=n-i-1;yield return Until(()=>Local.SlotGranada.Value.cantidad==count,"Authoritative grenade consumption "+i);
                    }
                    Local.SolicitarLanzamientoGranadaServerRpc(Local.transform.position+Vector3.up*2+Vector3.forward*10,Vector3.forward*15);
                    yield return new WaitForSecondsRealtime(.2f);Check(Local.SlotGranada.Value.Vacio&&Local.CapacidadGranadas==n,"Empty grenades retain capacity and cannot become negative");
                    Capture("wildcards-empty-grenades.png",hud.GetComponentInParent<Canvas>());
                    yield return Barrier("consumed");
                    if(Network.IsServer){foreach(var p in Players){Check(p.AplicarWildcardsDeClaseServidor(c),"Idempotent class apply accepted");Check(p.SlotGranada.Value.Vacio&&p.GetSlotCura(0).cantidad==p.CapacidadCuras-1,"Repeated apply does not duplicate utility charges");}Write("idempotent");}
                    yield return Until(()=>Has("idempotent"),"Idempotency results");
                }
                yield return Barrier("pass-"+pass);
            }
            // All three existing projectile prefabs must remain active on the receiving peer.
            for(int type=0;type<catalog.grenades.Length;type++){
                yield return Barrier("type-"+type);
                if(Network.IsServer){foreach(var p in Players){var c=ClassFor(0);c.grenade=catalog.grenades[type].id;Check(p.AplicarWildcardsDeClaseServidor(c,true),"Server configures grenade type "+type);}Write("type-ready-"+type);}
                yield return Until(()=>Has("type-ready-"+type)&&Local.SlotGranada.Value.itemId.ToString()==catalog.grenades[type].id,"Selected grenade type replicated");
                Local.SolicitarLanzamientoGranadaServerRpc(Local.transform.position+Vector3.up*2+Vector3.forward*15,Vector3.forward*15);
                yield return Until(()=>FindObjectsByType<GrenadeProjectile>(FindObjectsSortMode.None).Any(g=>g.IsSpawned&&g.gameObject.activeInHierarchy),"Active grenade NetworkBehaviour exists on receiving peer");
                yield return new WaitForSecondsRealtime(4.5f);yield return Barrier("detonated-"+type);
            }
            yield return Barrier("death");if(Network.IsServer){foreach(var p in Players)p.State.Value=PlayerHealth.PlayerState.Downed;Write("dead");}
            yield return Until(()=>Has("dead")&&Players.All(p=>!p.EquipoDeClase.Value.Active&&p.Perks.Value==0&&!p.GranadaPvPvEActiva),"Death removes synchronized Wildcard grants");
            yield return Barrier("nextlife");if(Network.IsServer){foreach(var p in Players){p.State.Value=PlayerHealth.PlayerState.Alive;Check(p.AplicarWildcardsDeClaseServidor(catalog.Default(0),true),"Next life accepts a class without Wildcards");}Write("alive");}
            yield return Until(()=>Has("alive")&&Local.EquipoDeClase.Value.Active&&Local.EquipoDeClase.Value.mask==0&&Local.RevisionArmasAplicada==Local.EquipoDeClase.Value.revision,"Next life restores ordinary class capacities");
            Check(Local.CapacidadCuras==catalog.FindHealing(catalog.Default(0).healing).carryCount&&Local.CapacidadGranadas==1,"Removing extra supplies restores base counts");
            yield return Barrier("end");if(Network.IsServer){PlayerHealth.FinPartidaPerksServidor();foreach(var p in Players)Check(!p.AplicarWildcardsDeClaseServidor(ClassFor(1)),"Ended match cannot grant equipment");Write("ended");}
            yield return Until(()=>Has("ended")&&!Local.EquipoDeClase.Value.Active&&!Local.GranadaPvPvEActiva,"Match end removes class policy");
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
            public Task<IPvPvESession> FindOrCreateAsync(PvPvENetworkSettings settings){var n=service.Network;n.AddNetworkPrefab(prefab);foreach(var grenade in service.GetComponent<PvPvEPhase12WildcardsDriver>().GrenadePrefabs)n.AddNetworkPrefab(grenade);n.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7799,"0.0.0.0");if(!(host?n.StartHost():n.StartClient()))throw new Exception("NGO start failed");return Task.FromResult<IPvPvESession>(new Lease(n,host));}
        }
        sealed class Lease:IPvPvESession
        {
            readonly NetworkManager n;public string Id=>"phase12-native";public bool IsHost{get;}public int ParticipantCount=>n&&n.IsServer?n.ConnectedClients.Count:1;public event Action Closed{add{}remove{}}
            public Lease(NetworkManager n,bool host){this.n=n;IsHost=host;}public Task CloseAdmissionAsync()=>Task.CompletedTask;public Task LeaveAsync(){if(n&&n.IsListening)n.Shutdown();return Task.CompletedTask;}
        }
    }
}
#endif
