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
    public sealed class PvPvEPhase11PerksDriver : MonoBehaviour
    {
        public PvPvENetworkSettings Settings; public GameObject PlayerPrefab;
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
        void Awake(){var c=new GameObject("Phase11 fixture selection").AddComponent<ConfiguracionPartidaSeleccionada>();c.escenarioSeleccionado=ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;c.modoSeleccionado=ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;}
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
            if(Arg("-dq-shop-visual")=="1"){yield return ShopOnly();}else if(mode=="pvp"){yield return Pvp();}else{yield return Merchant();}
            yield return Barrier("finished");Complete(true,"");
        }
        IEnumerator Purchase(string id,bool expected,bool viaVendor=true)
        {
            bool received=false,success=false;var resources=Local.GetComponent<PlayerRecursos>();
            void Result(string item,bool value){if(item==id){received=true;success=value;}}
            resources.OnPurchaseResult+=Result;
            if(viaVendor)Vendor.RequestPurchase(id);else resources.ComprarPerkServerRpc(id);
            yield return Until(()=>received,"Purchase result "+id);resources.OnPurchaseResult-=Result;Check(success==expected,id+" purchase result = "+expected);
        }
        IEnumerator Equip(string id){Switcher.UnlockWeapon(id);yield return Until(()=>Switcher.CurrentWeapon.WeaponName!=null&&!Switcher.IsSwitching&&Local.GetComponent<NetworkWeaponPresentation>().Weapons[Switcher.CurrentWeaponIndex].Id==id,"Equip "+id);yield return new WaitForSecondsRealtime(.2f);}
        float reloadTime, switchTime, drainRate, regenRate; int reloadSequence;
        IEnumerator Reload()
        {
            yield return Barrier("reload-"+reloadSequence++);
            var w=Switcher.CurrentWeapon;w.RestaurarMunicion(w.CapacidadCargador-1,100);float start=Time.realtimeSinceStartup;
            w.SendMessage("OnReload",default(InputAction.CallbackContext));Check(w.IsReloading,"Reload starts through original controls");
            yield return new WaitForSecondsRealtime(.1f);
            float speed=Switcher.GetComponent<Animator>().speed;MetricValue("FP reload animator speed",speed);
            Check(Mathf.Abs(speed-w.PresentationReloadSpeed(false))<.02f,"Actual FP reload animator uses character x perk speed");
            if(Local.TienePerk(PassivePerk.Gloves)){
                var remote=Players.First(p=>!p.IsOwner);var presentation=remote.GetComponent<NetworkWeaponPresentation>();
                yield return Until(()=>presentation.BodyAnimator.GetBool("TP_IsReloading"),"Remote reload begins",3);
                var source=presentation.Weapons[presentation.EquippedIndex].Source;
                float expected=source.PresentationThirdPersonReloadSpeed(false)/source.PresentationCharacterReloadSpeed*remote.GetComponent<PlayerCharacter>().ReloadSpeedMultiplier;
                Check(Mathf.Abs(presentation.BodyAnimator.GetFloat("TP_ReloadSpeed")-expected)<.02f,"Remote TP reload uses the same synchronized gloves bonus");
            }
            yield return Until(()=>!w.IsReloading,"Reload finishes");reloadTime=Time.realtimeSinceStartup-start;
            Check(w.CurrentAmmo==w.CapacidadCargador,"Original reload preserves ammunition");
        }
        IEnumerator SwitchPair()
        {
            yield return Equip("AutoPistol");float start=Time.realtimeSinceStartup;Switcher.UnlockWeapon("SemiRifle");
            yield return Until(()=>Switcher.IsSwitching,"Switch begins");yield return new WaitForSecondsRealtime(.1f);
            float speed=Switcher.GetComponent<Animator>().speed;
            Check(Mathf.Abs(speed-Local.PerkSwitchSpeed)<.03f,"Actual holster/unholster speed reflects equipped holster only");
            yield return Until(()=>!Switcher.IsSwitching,"Switch finishes");switchTime=Time.realtimeSinceStartup-start;
            Check(Mathf.Abs(Switcher.GetComponent<Animator>().speed-1)<.02f,"Switch restores base animator speed before enabling weapon");
        }
        IEnumerator Drain()
        {
            Home(-3);Keys();yield return new WaitForSecondsRealtime(.25f);Stamina(4);float start=Time.realtimeSinceStartup,before=Movement.CurrentStamina;
            Keys(Key.W,Key.LeftShift);yield return new WaitForSecondsRealtime(.65f);Check(Movement.IsSprinting,"Original sprint input is active");drainRate=(before-Movement.CurrentStamina)/(Time.realtimeSinceStartup-start);Keys();yield return new WaitForSecondsRealtime(.2f);
        }
        IEnumerator Regen(){Keys();Stamina(1);float before=Movement.CurrentStamina,start=Time.realtimeSinceStartup;yield return new WaitForSecondsRealtime(.65f);regenRate=(Movement.CurrentStamina-before)/(Time.realtimeSinceStartup-start);}
        IEnumerator Merchant()
        {
            Check(!PvPvETeamRoster.Active,"Merchant test uses the ordinary multiplayer mode, not a fake PvPvE roster");
            yield return Purchase("perk.gloves",false,false);Check(Local.Perks.Value==0,"Insufficient resources cannot grant a passive");
            yield return Equip("AutoPistol");yield return Reload();yield return Reload();float baseReload=reloadTime;
            yield return SwitchPair();yield return SwitchPair();float baseSwitch=switchTime;yield return Equip("AutoPistol");
            yield return Drain();float baseDrain=drainRate;yield return Regen();float baseRegen=regenRate;
            Home();yield return new WaitForSecondsRealtime(.3f);yield return Barrier("funds");
            if(Network.IsServer){foreach(var p in Players)foreach(TipoRecurso type in Enum.GetValues(typeof(TipoRecurso)))p.GetComponent<PlayerRecursos>().SumarRecurso(type,100);Write("funds-ready");}
            yield return Until(()=>Has("funds-ready")&&Local.GetComponent<PlayerRecursos>().Cantidad(TipoRecurso.PilaDeLatas)==100,"Resources replicated");
            Home(40);yield return new WaitForSecondsRealtime(.3f);yield return Purchase("perk.gloves",false,false);Check(Local.Perks.Value==0,"Distant merchant request cannot grant a perk");Home();yield return new WaitForSecondsRealtime(.3f);yield return Purchase("perk.unknown",false,false);
            foreach(var e in Catalog.entries){
                var resources=Local.GetComponent<PlayerRecursos>();var before=Enum.GetValues(typeof(TipoRecurso)).Cast<TipoRecurso>().Select(resources.Cantidad).ToArray();
                yield return Purchase(e.id,true);yield return Until(()=>Local.TienePerk(e.perk),"Perk replicated "+e.id);
                foreach(TipoRecurso type in Enum.GetValues(typeof(TipoRecurso)))Check(resources.Cantidad(type)==before[(int)type]-Vendor.Offers.Single(o=>o.weaponId==e.id).Precio.Cantidad(type),"Server charges canonical merchant resources "+e.id+"/"+type);
                var owned=Local.Perks.Value;yield return Purchase(e.id,false,false);Check(Local.Perks.Value==owned,"Duplicate purchase cannot stack perk "+e.id);
                foreach(TipoRecurso type in Enum.GetValues(typeof(TipoRecurso)))Check(resources.Cantidad(type)==before[(int)type]-Vendor.Offers.Single(o=>o.weaponId==e.id).Precio.Cantidad(type),"Rejected duplicate does not charge resources "+e.id+"/"+type);
            }
            yield return Barrier("purchased");yield return Until(()=>Players.All(p=>p.Perks.Value==31),"Both peers see all five passives");
            Check(Local.PerkReloadSpeed==1.5f&&Mathf.Abs(Local.PerkSprintDrain-.7f)<.001f&&Mathf.Abs(Local.PerkStaminaRegen-1.15f)<.001f&&Local.PerkSwitchSpeed==1.25f,"Five canonical effects are derived from synchronized ownership");
            yield return Equip("AutoPistol");yield return Reload();MetricValue("reload baseline",baseReload);MetricValue("reload gloves",reloadTime);
            Check(Mathf.Abs(reloadTime/baseReload-1/1.5f)<.12f,"Gloves produce an actual reload duration near two thirds of baseline");
            yield return SwitchPair();MetricValue("switch baseline",baseSwitch);MetricValue("switch holster",switchTime);
            Check(Mathf.Abs(switchTime/baseSwitch-.8f)<.15f,"Holster reduces actual switch duration by approximately twenty percent");
            yield return Drain();MetricValue("drain ratio",drainRate/baseDrain);Check(Mathf.Abs(drainRate/baseDrain-.7f)<.13f,"Boots reduce measured running stamina consumption by thirty percent");
            yield return Regen();MetricValue("regen ratio",regenRate/baseRegen);Check(Mathf.Abs(regenRate/baseRegen-1.15f)<.09f,"Canteen increases measured stamina regeneration by fifteen percent");
            yield return Barrier("slide-ready");yield return Slide();yield return Barrier("slide-complete");
            Check(Players.All(p=>p.SlideUntil.Value>0),"Both peers received the server-approved slide state");Home();yield return new WaitForSecondsRealtime(.3f);
            yield return ShopVisual();var shop=Local.GetComponentInChildren<NPCShopUI>(true);yield return Barrier("life-down");
            if(Network.IsServer){foreach(var p in Players)p.State.Value=PlayerHealth.PlayerState.Downed;Write("downed");}
            yield return Until(()=>Has("downed")&&Local.IsDowned,"Downed");Check(Local.Perks.Value==31,"Ordinary multiplayer retains passives while downed");yield return Barrier("definitive");
            if(Network.IsServer){foreach(var p in Players)p.State.Value=PlayerHealth.PlayerState.Spectating;Write("spectating");}
            yield return Until(()=>Has("spectating")&&Local.Perks.Value==0,"Definitive death resets passives");
            foreach(var e in Catalog.entries)Check(!(bool)typeof(NPCShopUI).GetMethod("EstaComprada",Fields).Invoke(shop,new object[]{Vendor.Offers.First(o=>o.weaponId==e.id)}),"Definitive death re-enables merchant offer "+e.id);
            yield return Barrier("new-life");if(Network.IsServer){foreach(var p in Players)p.State.Value=PlayerHealth.PlayerState.Alive;Write("alive");}
            yield return Until(()=>Has("alive")&&Local.IsAlive,"New life");yield return Purchase("perk.gloves",true,false);
            yield return Barrier("dead-state");if(Network.IsServer){foreach(var p in Players)p.State.Value=PlayerHealth.PlayerState.Dead;Write("dead");}
            yield return Until(()=>Has("dead")&&Local.Perks.Value==0,"Singleplayer definitive-death state clears passives");Check(Local.PerkReloadSpeed==1&&Local.PerkSwitchSpeed==1,"Life reset restores base effects without modifying weapon data");
        }
        IEnumerator ShopOnly(){
            yield return Barrier("shop-funds");if(Network.IsServer){foreach(var p in Players)foreach(TipoRecurso t in Enum.GetValues(typeof(TipoRecurso)))p.GetComponent<PlayerRecursos>().SumarRecurso(t,100);Write("shop-funded");}
            yield return Until(()=>Has("shop-funded")&&Local.GetComponent<PlayerRecursos>().Cantidad(TipoRecurso.PilaDeLatas)==100,"Shop resources");
            foreach(var e in Catalog.entries)yield return Purchase(e.id,true);
            yield return Until(()=>Local.Perks.Value==31,"Five perk icons");yield return ShopVisual();
        }
        IEnumerator ShopVisual(){
            var hud=Local.GetComponentInChildren<HUDController>(true);hud.SendMessage("UpdatePerksHud");
            Check(hud.transform.Find("Pasivos").GetComponentsInChildren<UnityEngine.UI.Image>().Length==5,"Existing HUD displays the five acquired equipment icons");Capture("hud-passives.png",hud.GetComponentInParent<Canvas>());
            var shop=Local.GetComponentInChildren<NPCShopUI>(true);typeof(NPCShopUI).GetMethod("AbrirTienda",Fields).Invoke(shop,new object[]{Vendor});typeof(NPCShopUI).GetMethod("SeleccionarSeccion",Fields).Invoke(shop,new object[]{CategoriaTienda.Equipamiento});
            typeof(NPCShopUI).GetMethod("SeleccionarOferta",Fields).Invoke(shop,new object[]{Vendor.Offers.First(o=>o.perk==PassivePerk.Gloves)});
            var shopPanel=(GameObject)Field(shop,"shopPanel");
            yield return new WaitForSecondsRealtime(.7f);Check(shopPanel.activeInHierarchy,"Actual merchant panel is visible");Capture("merchant-passives.png",shopPanel.GetComponentInParent<Canvas>());
            foreach(var e in Catalog.entries)Check((bool)typeof(NPCShopUI).GetMethod("EstaComprada",Fields).Invoke(shop,new object[]{Vendor.Offers.First(o=>o.weaponId==e.id)}),"Merchant marks acquired perk bought "+e.id);
            shop.CerrarTienda();
        }
        IEnumerator Slide()
        {
            Home(-3);Keys();yield return new WaitForSecondsRealtime(.3f);Stamina(4);Keys(Key.W,Key.LeftShift);yield return new WaitForSecondsRealtime(.4f);
            float before=Movement.CurrentStamina;Keys(Key.W,Key.LeftShift,Key.LeftCtrl);
            yield return Until(()=>Local.Deslizando&&Movement.IsSliding,"Ctrl during sprint starts an approved slide",5);
            double end=Local.SlideUntil.Value;Vector3 start=Local.transform.position;Keys();yield return new WaitForSecondsRealtime(.2f);
            Check(Movement.IsCrouching&&Vector3.Distance(start,Local.transform.position)>.5f,"Slide moves the real CharacterController while retaining crouched clearance");
            Check(Movement.CurrentStamina<=before-Catalog.slideCost+.15f,"Slide spends stamina once on approval");
            Local.SolicitarDeslizamientoServerRpc(Vector3.forward);yield return new WaitForSecondsRealtime(.08f);Check(Local.SlideUntil.Value==end,"Repeated slide request is rejected during cooldown");
            yield return Until(()=>!Local.Deslizando,"Slide completes",3);Keys();
        }
        IEnumerator Pvp()
        {
            Check(service.Roster.CanStart,"Existing balanced PvPvE teams retained");yield return Barrier("apply");
            if(Network.IsServer){foreach(var p in Players){var c=Settings.Loadouts.Default(0);c.perk1="perk.gloves";c.perk2="perk.holster";Check(!p.AplicarPerksDeClaseServidor(c),"Server rejects two perks of one category");c.perk2="perk.boots";Check(p.AplicarPerksDeClaseServidor(c),"Server applies two valid perks from class without a purchase");}Write("class-ready");}
            yield return Until(()=>Has("class-ready")&&Players.All(p=>p.Perks.Value==3),"Both classes synchronized");
            Check(Local.PerkReloadSpeed==1.5f&&Local.PerkSprintDrain==.7f,"Both peers derive the selected class bonuses");
            if(!Network.IsServer)Check(!Local.AplicarPerksDeClaseServidor(Settings.Loadouts.Default(0)),"Client cannot assign class perks directly");
            yield return Purchase("perk.canteen",false,false);Check(Local.Perks.Value==3,"PvPvE cannot bypass class selection through merchant RPC");
            Check(!Vendor.Offers.First(o=>o.EsPerk).SeMuestraEnModo(ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia),"PvPvE merchant does not advertise purchasable class perks");
            yield return Barrier("pvp-death");if(Network.IsServer){foreach(var p in Players)p.State.Value=PlayerHealth.PlayerState.Downed;Write("pvp-dead");}
            yield return Until(()=>Has("pvp-dead")&&Players.All(p=>p.Perks.Value==0),"PvPvE death immediately removes effects");
            yield return Barrier("pvp-reapply");if(Network.IsServer){foreach(var p in Players){p.State.Value=PlayerHealth.PlayerState.Alive;var c=Settings.Loadouts.Default(0);c.perk1="perk.kneepads";c.perk2="perk.canteen";Check(p.AplicarPerksDeClaseServidor(c),"Next life can apply its selected class");}Write("pvp-alive");}
            yield return Until(()=>Has("pvp-alive")&&Players.All(p=>p.Perks.Value==20),"Next-life mask synchronized");
            yield return Barrier("match-end");if(Network.IsServer){PlayerHealth.FinPartidaPerksServidor();foreach(var p in Players)Check(!p.AplicarPerksDeClaseServidor(Settings.Loadouts.Default(0)),"Ended match rejects further perk assignments");Write("ended");}
            yield return Until(()=>Has("ended")&&Local.Perks.Value==0,"Match end removes all effects");Check(Local.PerkStaminaRegen==1,"End of match restores normal stamina regeneration");
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
            public Task<IPvPvESession> FindOrCreateAsync(PvPvENetworkSettings settings){var n=service.Network;n.AddNetworkPrefab(prefab);n.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7799,"0.0.0.0");if(!(host?n.StartHost():n.StartClient()))throw new Exception("NGO start failed");return Task.FromResult<IPvPvESession>(new Lease(n,host));}
        }
        sealed class Lease:IPvPvESession
        {
            readonly NetworkManager n;public string Id=>"phase11-native";public bool IsHost{get;}public int ParticipantCount=>n&&n.IsServer?n.ConnectedClients.Count:1;public event Action Closed{add{}remove{}}
            public Lease(NetworkManager n,bool host){this.n=n;IsHost=host;}public Task CloseAdmissionAsync()=>Task.CompletedTask;public Task LeaveAsync(){if(n&&n.IsListening)n.Shutdown();return Task.CompletedTask;}
        }
    }
}
#endif
