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
    public sealed class PvPvEPhase17CombatDriver : MonoBehaviour
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
        void OldAwake(){var c=new GameObject("Phase17 fixture selection").AddComponent<ConfiguracionPartidaSeleccionada>();c.escenarioSeleccionado=ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;c.modoSeleccionado=ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;}
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
            var config=Instantiate(service.Settings);config.KillLimit=2;config.MapVoteSeconds=35;config.PostVoteSeconds=1;config.PreMatchSeconds=1;config.RespawnSeconds=1.5f;config.CorpseSeconds=.4f;config.SpawnProtectionSeconds=.3f;
            service.ConfigureForTest(config,new DirectoryAdapter(service,config.PlayerPrefab,who==0));var store=new PvPvEClassStore(config.Loadouts);
            for(int i=0;i<5;i++){var c=config.Loadouts.Default(i);c.primary="SemiRifle";c.secondary="AutoPistol";Check(store.Save(i,c,out _),"Existing valid class "+i);}
            var menu=service.GetComponent<PvPvEModeMenu>();var initial=menu.GetComponentInParent<Canvas>().transform.Find("menuPrincipalPanel");if(initial)initial.gameObject.SetActive(false);menu.OpenModes();menu.OpenPvPvE();
            if(who!=0)yield return Until(()=>Has("host-ready"),"Host ready",150);yield return Wait(service.SearchAsync());who=(int)Network.LocalClientId;Write(role+"-connected");if(who==0)Write("host-ready");
            yield return Until(()=>Combat&&service.Roster.Members.Count==3,"Three actual connected players",150);
            Check(service.Roster.Count(PvPvETeam.Alpha)==2&&service.Roster.Count(PvPvETeam.Bravo)==1,"Real 2v1 assignment");
            var voting=service.Roster.GetComponent<PvPvEMapVoting>();yield return Until(()=>voting.Phase.Value==PvPvEVotePhase.Voting,"Vote");voting.CastVoteServerRpc(0);
            yield return Until(()=>Local&&View&&View.IsShowing,"Mode scene and initial selection",180);yield return Until(()=>View.PreviewsReady,"Class previews",120);View.Choose(0);View.Confirm();
            yield return Until(()=>Flow.Stage.Value==PvPvEPrepareStage.Playing&&!PvPvEPreparationFlow.LocalControlsLocked,"Real deployed/equipped start",150);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Phase17 validation floor";floor.GetComponent<MeshRenderer>().sharedMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.15f,.19f,.2f)};floor.transform.position=new Vector3(50000,-.5f,30);floor.transform.localScale=new Vector3(30,1,150);
            Layout(0);yield return Sync("arena");yield return new WaitForSecondsRealtime(.7f);
            Check(Players.Length==3&&Combat.Stats.Count==3,"One original PlayerObject and persistent stats row per participant");
            yield return Do(0,"health",()=>{foreach(var p in Players)p.SetMaxHealth(500,true);});
            yield return Do(0,"grenade",()=>Local.SolicitarLanzamientoGranadaServerRpc(Local.transform.position+Vector3.forward*.8f+Vector3.up*.2f,Vector3.zero));
            yield return Until(()=>FindObjectsByType<GrenadeProjectile>(FindObjectsSortMode.None).Length>0,"Actual existing network grenade created",30);
            yield return Until(()=>FindObjectsByType<GrenadeProjectile>(FindObjectsSortMode.None).Length==0,"Actual grenade detonated",30);yield return new WaitForSecondsRealtime(.3f);
            Check(Actor(1).CurrentHealth.Value<500&&Actor(2).CurrentHealth.Value==500,"Real explosion damages enemy but never allied player (HP="+string.Join(",",Players.Select(h=>h.OwnerClientId+":"+h.CurrentHealth.Value+"@"+h.transform.position))+")");
            Check(Combat.AlphaKills.Value==0&&Combat.BravoKills.Value==0,"Nonlethal explosion awards no team kill");
            yield return Do(0,"reset-health",()=>{foreach(var p in Players)p.SetMaxHealth(100,true);Combat.StunAreaServer(0,Actor(1).transform.position,10,1.3f);});
            yield return Until(()=>Stats(1).StunnedUntil>Network.ServerTime.Time,"Authoritative enemy stun",20);
            Check(Stats(0).StunnedUntil==0&&Stats(2).StunnedUntil==0,"Allied/self negative utility excluded");
            if(who==1){Check(PvPvEPreparationFlow.LocalControlsLocked&&PlayerInputGate.ActionsBlocked,"Enemy stun blocks movement and combat");int ammo=Switcher.CurrentWeapon.CurrentAmmo;FireAt(Actor(0).transform.position+Vector3.up);yield return null;Check(Switcher.CurrentWeapon.CurrentAmmo==ammo,"Stunned owner cannot fire original weapon");}
            yield return Until(()=>Stats(1).StunnedUntil<=Network.ServerTime.Time,"Stun expires",10);yield return Sync("stun-done");
            yield return Do(0,"burn-health",()=>{foreach(var h in Players)h.SetMaxHealth(500,true);Combat.BurnAreaServer(0,Actor(1).transform.position,10,2,.7f,7,.2f);});
            yield return new WaitForSecondsRealtime(.9f);Check(Actor(1).CurrentHealth.Value<500&&Actor(2).CurrentHealth.Value==500,"Attributed burn damages enemies and excludes allies");
            yield return Do(0,"melee-health",()=>{foreach(var h in Players)h.SetMaxHealth(100,true);});
            yield return Do(0,"melee",()=>{var camera=Local.GetComponentsInChildren<Camera>(true).First(c=>c.CompareTag("MainCamera"));Local.GetComponentInChildren<PlayerLook>(true).enabled=false;camera.transform.rotation=Quaternion.LookRotation(Actor(1).transform.position+Vector3.up-camera.transform.position);Local.GetComponentInChildren<MeleeAttack>(true).OnMeleeHit();});
            yield return Until(()=>Actor(1).CurrentHealth.Value<100,"Existing knife event applies authoritative melee damage",20);Check(Actor(2).CurrentHealth.Value==100,"Melee does not damage allies");
            yield return Do(0,"melee-reset",()=>{foreach(var h in Players)h.SetMaxHealth(100,true);});
            Layout(1);yield return Sync("friendly-layout");yield return new WaitForSecondsRealtime(.3f);
            yield return Do(0,"friendly-shot",()=>FireAt(Actor(1).transform.position+Vector3.up));
            Check(Actor(2).CurrentHealth.Value==100&&Actor(1).CurrentHealth.Value==100,"Real shot stops at ally without friendly fire or penetrating to enemy");
            Check(Combat.AlphaKills.Value==0&&Stats(0).Points==0,"Allied contact gives no kill or points");
            Layout(0);wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Phase17 validation wall";wall.GetComponent<MeshRenderer>().sharedMaterial=floor.GetComponent<MeshRenderer>().sharedMaterial;wall.transform.position=new Vector3(50000,1,1);wall.transform.localScale=new Vector3(3,3,.2f);yield return Sync("wall-layout");yield return new WaitForSecondsRealtime(.3f);
            yield return Do(0,"wall-shot",()=>FireAt(Actor(1).transform.position+Vector3.up));
            Check(Actor(1).CurrentHealth.Value==100,"Server ray respects wall occlusion");Destroy(wall);yield return Sync("wall-removed");
            if(who==0){var nwp=Local.GetComponent<NetworkWeaponPresentation>();int index=Switcher.CurrentWeaponIndex;var origin=Switcher.CurrentWeapon.AimOrigin;RawShot(index,uint.MaxValue,origin+Vector3.right*20,new[]{origin+Vector3.forward*50});RawShot(index,0,origin,new[]{origin+Vector3.forward*50});RawShot(index,44,origin,new[]{new Vector3(float.NaN,0,0)});RawShot(index,45,origin,new[]{origin+Vector3.forward*50,origin+Vector3.forward*50});}
            yield return new WaitForSecondsRealtime(.4f);Check(Actor(1).CurrentHealth.Value==100&&Stats(0).Points==0,"Forged origin, replay, NaN and wrong pellet count cannot damage or score");
            if(who==0){int index=Switcher.CurrentWeaponIndex;Combat.TryAmmo(0,index,out var ammo);int before=ammo.Magazine;var nwp=Local.GetComponent<NetworkWeaponPresentation>();uint sequence=(uint)Field(nwp,"shotSequence");var origin=Switcher.CurrentWeapon.AimOrigin;RawShot(index,sequence+1,origin,new[]{origin+Vector3.up*30});RawShot(index,sequence+2,origin,new[]{origin+Vector3.up*30});FieldSet(nwp,"shotSequence",sequence+2);yield return new WaitForSecondsRealtime(.2f);Check(Combat.TryAmmo(0,index,out ammo)&&ammo.Magazine==before-1,"Back-to-back forged fire cannot bypass authoritative cadence");Write("cadence-done");}
            else yield return Until(()=>Has("cadence-done"),"Cadence validation",30);
            Layout(2);yield return Sync("long-layout");yield return Do(0,"low-health",()=>Actor(1).SetMaxHealth(20,true));yield return new WaitForSecondsRealtime(.3f);
            ulong first=Actor(1).NetworkObjectId;yield return Do(0,"headshot",()=>FireAt(Actor(1).transform.position+Vector3.up*1.55f));
            yield return Until(()=>Stats(0).Kills==1&&Stats(1).Deaths==1&&Combat.AlphaKills.Value==1,"Authoritative lethal headshot scores",30);
            Check(Stats(0).Points==config.KillScore&&Combat.BravoKills.Value==0,"Individual points and team PvP kills are separate");
            if(who==0){Check(Combat.GetComponent<PvPvEKillFeedback>().Message.Contains("HEADSHOT")&&Combat.GetComponent<PvPvEKillFeedback>().Message.Contains("LONGSHOT"),"Confirmed headshot/longshot feedback uses authoritative hit");Capture("feedback.png",Combat.GetComponent<PvPvEKillFeedback>().Panel.GetComponent<Canvas>());CaptureWorld("combat.png");}
            yield return Until(()=>Actor(1)&&Actor(1).NetworkObjectId!=first&&Actor(1).IsAlive&&Deployment.TryGet(1,out var ready)&&ready.Life==PvPvELifeStage.Alive,"PvP death goes through existing respawn",30);yield return Sync("enemy-respawn");
            Check(Stats(1).Deaths==1&&Stats(0).Points==100,"Statistics persist across PlayerObject replacement");
            Layout(0);yield return Sync("environment-layout");yield return new WaitForSecondsRealtime(.5f);
            yield return Do(0,"environment",()=>Actor(2).TakeDamage(99999,true));
            yield return Until(()=>Stats(2).Deaths==1,"Environmental death recorded",20);
            Check(Combat.AlphaKills.Value==1&&Combat.BravoKills.Value==0&&Stats(0).Points==100,"Environmental death grants no opposing kill or points");
            yield return Until(()=>Actor(2)&&Actor(2).IsAlive&&Deployment.TryGet(2,out var row)&&row.Life==PvPvELifeStage.Alive,"Third participant respawn",30);yield return Sync("environment-respawn");Layout(0);yield return Sync("duel-layout");yield return new WaitForSecondsRealtime(.5f);
            yield return Do(0,"host-low",()=>Local.SetMaxHealth(40,true));yield return Do(1,"client-kill",()=>FireAt(Actor(0).transform.position+Vector3.up));
            yield return Until(()=>Stats(1).Kills==1&&Stats(0).Deaths==1&&Combat.BravoKills.Value==1,"Client-owned original weapon damages host through server",30);
            yield return Until(()=>Actor(0)&&Actor(0).IsAlive&&Deployment.TryGet(0,out var row)&&row.Life==PvPvELifeStage.Alive,"Host respawn",30);yield return Sync("host-respawn");Layout(0);yield return Sync("ammo-layout");yield return new WaitForSecondsRealtime(.5f);
            Check(Stats(0).Kills==1&&Stats(0).Deaths==1&&Stats(0).Points==100,"Killer statistics also survive own respawn");
            if(who==1){Switcher.CurrentWeapon.SendMessage("OnReload",default(UnityEngine.InputSystem.InputAction.CallbackContext));yield return Until(()=>Combat.TryAmmo(1,Switcher.CurrentWeaponIndex,out var a)&&a.Reloading,"Remote owner's actual reload observed",20);yield return Until(()=>Combat.TryAmmo(1,Switcher.CurrentWeaponIndex,out var a)&&!a.Reloading&&a.Magazine==Switcher.CurrentWeapon.MaxAmmo&&Switcher.CurrentWeapon.CurrentAmmo==Switcher.CurrentWeapon.MaxAmmo,"Client reload animation and authoritative magazine agree",30);Check(Combat.TryAmmo(1,Switcher.CurrentWeaponIndex,out var reloaded)&&reloaded.Magazine==10,"Client reload restores existing magazine without timing desync");Write("remote-reload-done");}
            else yield return Until(()=>Has("remote-reload-done"),"Remote reload test",60);
            if(who==0){
                int count=Switcher.CurrentWeapon.CurrentAmmo;for(int i=0;i<count;i++){FireAt(Local.transform.position+Vector3.up*100);yield return new WaitForSecondsRealtime(.4f);}
                yield return Until(()=>Combat.TryAmmo(0,Switcher.CurrentWeaponIndex,out var a)&&a.Magazine==0,"Server ammunition drained by actual shots",20);
                Check(Switcher.CurrentWeapon.CurrentAmmo==0,"Original first-person magazine matches authoritative ledger");
                uint sequence=(uint)Field(Local.GetComponent<NetworkWeaponPresentation>(),"shotSequence");RawShot(Switcher.CurrentWeaponIndex,sequence+1,Switcher.CurrentWeapon.AimOrigin,new[]{Switcher.CurrentWeapon.AimOrigin+Vector3.up*30});yield return new WaitForSecondsRealtime(.3f);
                Check(Combat.TryAmmo(0,Switcher.CurrentWeaponIndex,out var empty)&&empty.Magazine==0,"Forged shot cannot bypass empty magazine");
                Switcher.CurrentWeapon.SendMessage("OnReload",default(UnityEngine.InputSystem.InputAction.CallbackContext));yield return Until(()=>Combat.TryAmmo(0,Switcher.CurrentWeaponIndex,out var a)&&a.Reloading,"Original reload observed by authority",20);Switcher.CurrentWeapon.CancelReload();
                yield return Until(()=>Combat.TryAmmo(0,Switcher.CurrentWeaponIndex,out var a)&&!a.Reloading,"Cancelled reload",20);Check(Combat.TryAmmo(0,Switcher.CurrentWeaponIndex,out empty)&&empty.Magazine==0,"Cancelled early reload cannot refill ammo");
                yield return new WaitForSecondsRealtime(.2f);Switcher.CurrentWeapon.SendMessage("OnReload",default(UnityEngine.InputSystem.InputAction.CallbackContext));yield return Until(()=>Combat.TryAmmo(0,Switcher.CurrentWeaponIndex,out var a)&&a.Magazine==Switcher.CurrentWeapon.MaxAmmo&&!a.Reloading&&Switcher.CurrentWeapon.CurrentAmmo==Switcher.CurrentWeapon.MaxAmmo,"Completed original reload replenishes magazine with bounded reserve",30);Write("ammo-done");
            }else yield return Until(()=>Has("ammo-done"),"Host ammo authority tests",90);
            yield return Sync("final-layout");if(who==2)Place(0,0);if(who==0)Place(6,0);if(who==1)Place(0,2);yield return Sync("final-positions");yield return new WaitForSecondsRealtime(.3f);
            yield return Do(0,"final-low",()=>Actor(1).SetMaxHealth(30,true));yield return Do(2,"final-kill",()=>FireAt(Actor(1).transform.position+Vector3.up));
            yield return Until(()=>Combat.ScoreLocked.Value,"Kill-limit score lock",30);
            Check(Combat.AlphaKills.Value==2&&Combat.BravoKills.Value==1&&Combat.Winner.Value==PvPvETeam.Alpha,"Only valid enemy kills reach configured limit");
            Check(Stats(2).Kills==1&&Stats(2).Points==100&&Stats(2).Deaths==1,"Third sender receives own points; no attribution to host");
            int kills=Combat.AlphaKills.Value,deaths=Stats(1).Deaths;double points=Stats(2).Points;
            if(who==0){Actor(1).EliminatePlayer();Combat.ApplyDamageServer(0,Actor(2),99999,PvPvEDamageKind.Explosion,false,0,"invalid-after-limit");}
            yield return new WaitForSecondsRealtime(.3f);Check(Combat.AlphaKills.Value==kills&&Stats(2).Points==points&&Stats(1).Deaths==deaths,"Duplicate death/late damage cannot modify locked score");
            Check(PvPvEPreparationFlow.LocalControlsLocked,"Limit freezes gameplay pending phase23 results");
            yield return Sync("done");
            if(who==2){yield return ExitAndVerify();Write("third-left");Complete(true,"");yield break;}
            yield return Until(()=>Has("third-left")&&Combat.Stats.Count==2,"Disconnected player removed from stats",30);Check(Combat.AlphaKills.Value==2&&Combat.BravoKills.Value==1,"Team totals survive disconnected contributor");Write("remaining-"+who);yield return Until(()=>Has("remaining-0")&&Has("remaining-1"),"Remaining peers ready to exit",30);yield return ExitAndVerify();Complete(true,"");
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
            readonly NetworkManager n;public string Id=>"phase17-native";public bool IsHost{get;}public int ParticipantCount=>n&&n.IsServer?n.ConnectedClients.Count:1;public event Action Closed{add{}remove{}}
            public Lease(NetworkManager n,bool host){this.n=n;IsHost=host;}public Task CloseAdmissionAsync()=>Task.CompletedTask;public Task LeaveAsync(){if(n&&n.IsListening)n.Shutdown();return Task.CompletedTask;}
        }
    }
}
#endif
