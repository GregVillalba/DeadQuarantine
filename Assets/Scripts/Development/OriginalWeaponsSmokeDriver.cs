#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.AI;
namespace DeadQuarantine.Development
{
 public sealed class OriginalWeaponsSmokeDriver : MonoBehaviour
 {
  [Serializable] sealed class Result {public bool ok;public string role,error;public string[] checks;}
  public GameObject ZombiePrefab;
  string role,folder;
  readonly List<string> checks=new();
  NetworkManager Network=>NetworkManager.Singleton;
  NetworkWeaponPresentation Local=>Network.LocalClient?.PlayerObject?Network.LocalClient.PlayerObject.GetComponent<NetworkWeaponPresentation>():null;
  NetworkWeaponPresentation Remote=>FindObjectsByType<NetworkWeaponPresentation>(FindObjectsSortMode.None).FirstOrDefault(p=>p.IsSpawned&&!p.IsOwner);
  bool finished,sawRemoteFire,sawLocalBolt,sawLocalPartsBolt;
  static string Arg(string key){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null;}
  void Start() {role=Arg("-dq-weapons-role");folder=Arg("-dq-weapons-folder");if(string.IsNullOrEmpty(role)){enabled=false;return;}Directory.CreateDirectory(folder);StartCoroutine(Safe(Run()));}
  IEnumerator Safe(IEnumerator routine) {
   while(!finished){object current;try{if(!routine.MoveNext())yield break;current=routine.Current;}catch(Exception e){Complete(false,e.ToString());yield break;}if(current is IEnumerator nested)yield return Safe(nested);else yield return current;}
  }
  IEnumerator Until(Func<bool> condition,string message,float timeout=25) {float end=Time.realtimeSinceStartup+timeout;while(!condition()){if(Time.realtimeSinceStartup>end)throw new TimeoutException(message);yield return null;}}
  void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks.Add(text);Write("progress-"+role+".json",JsonUtility.ToJson(new Result{role=role,checks=checks.ToArray()},true));}
  void Write(string file,string value){string path=Path.Combine(folder,file);File.WriteAllText(path+".tmp",value);for(int i=0;;i++){try{if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);return;}catch(IOException)when(i<100){System.Threading.Thread.Sleep(10);}}}
  string Read(string file){using var stream=File.Open(Path.Combine(folder,file),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);using var reader=new StreamReader(stream);return reader.ReadToEnd();}
  void Complete(bool ok,string error){if(finished)return;finished=true;Write("result-"+role+".json",JsonUtility.ToJson(new Result{ok=ok,role=role,error=error,checks=checks.ToArray()},true));Application.Quit(ok?0:1);}
  void Place(bool occluded=false) {
   var local=Local;Vector3 p=role=="host"?new Vector3(0,.05f,0):new Vector3(0,.05f,7);
   if(occluded)p=role=="host"?new Vector3(10,.05f,-3):new Vector3(10,.05f,3);
   var controller=local.GetComponent<CharacterController>();controller.enabled=false;local.transform.SetPositionAndRotation(p,Quaternion.Euler(0,role=="host"?0:180,0));controller.enabled=true;
   var nt=local.GetComponent<NetworkTransform>();if(nt)nt.Teleport(p,local.transform.rotation,local.transform.localScale);
  }
  void Prepare() {
   foreach(var script in Local.GetComponentsInChildren<MonoBehaviour>(true))if(new[]{"PlayerMovement","PlayerLook","CameraRecoil","PlayerDownedEffects","PlayerCameraBob"}.Contains(script.GetType().Name))script.enabled=false;
   foreach(var weapon in Local.GetComponentsInChildren<Weapon>(true))weapon.InputLocked=false;
   Place();
  }
  void Update() {
   if(!Local)return;
   if(Remote && Remote.BodyAnimator){int layer=Remote.BodyAnimator.GetLayerIndex("Actions");if(layer>=0 && Remote.BodyAnimator.GetCurrentAnimatorStateInfo(layer).IsName("Fire"))sawRemoteFire=true;}
   var animator=Local.Switcher.GetComponent<Animator>();if(animator && Local.Switcher.CurrentWeaponIndex==2){int layer=animator.GetLayerIndex("Fire");if(layer>=0 && animator.GetCurrentAnimatorStateInfo(layer).IsName("Bolt"))sawLocalBolt=true;layer=animator.GetLayerIndex("HandGunADD");if(layer>=0 && animator.GetCurrentAnimatorStateInfo(layer).IsName("Bolt Action"))sawLocalPartsBolt=true;}
  }
  IEnumerator Run() {
   FindFirstObjectByType<GestorEscenariosPartida>().ActivarEscenario(ConfiguracionPartidaSeleccionada.Escenario.Laboratorio);
   var transport=Network.GetComponent<UnityTransport>();transport.SetConnectionData("127.0.0.1",7787,"0.0.0.0");
   if(role=="host")Network.StartHost();else{yield return new WaitForSeconds(1);Network.StartClient();}
   yield return Until(()=>Local&&Remote,"Both player objects did not spawn",35);yield return new WaitForSeconds(1);
   yield return Until(()=>Local.GetComponent<MultiplayerPlayerSpawnAssigner>().IsAtSpawn.Value,"Selected scenario spawn not acknowledged");
   var expectedSpawn=FindFirstObjectByType<MultiplayerPlayerSpawner>().GetSpawnPoint(role=="host"?0:1);
   Check(expectedSpawn && Vector3.Distance(Local.transform.position,expectedSpawn.position)<.7f,"Player starts at the selected scenario spawn, not the manual fallback");
   Prepare();yield return new WaitForSeconds(.6f);
   Check(Local.GetComponentInChildren<OwnerNetworkAnimator>(true).IsSpawned,"Owner rig initializes network animation");
   var start=Local.transform.position;Local.GetComponent<CharacterController>().Move(Vector3.right*.25f);
   Check(Vector3.Distance(start,Local.transform.position)>.20f,"Local controller moves without colliding with its own visual rig");Place();
   Local.ShowLocalBodyForTesting=true;yield return null;
   Check(Local.Body.GetComponentsInChildren<Renderer>(true).All(r=>!r.forceRenderingOff),"Inspector preview shows the local third-person body");
   Local.ShowLocalBodyForTesting=false;yield return null;
   Check(Local.Body.GetComponentsInChildren<Renderer>(true).All(r=>r.forceRenderingOff),"Disabling Inspector preview hides the local body again");
   Check(!Local.GetComponent<PlayerNameplate>().NameVisible,"Local player cannot see own name");
   Check(Remote.GetComponent<PlayerNameplate>().NameVisible,"Remote name remains visible");
   yield return Until(()=>Remote.VisibleWeapon,"Remote starting weapon missing");
   Check(Remote.VisibleWeapon.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh==Remote.Weapons[0].Model.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh,"Remote weapon reuses the first-person model");
   if(role=="host")yield return Host();else yield return Client();
  }
  IEnumerator Host() {
   Write("command.txt","FIRE");int old=Remote.RemoteShotCount;Local.Switcher.CurrentWeapon.SendMessage("TryFire");
   yield return Until(()=>Remote.RemoteShotCount>old,"Remote client shot effects never reached Host");
   Check(Remote.RemoteSoundEvents>0&&Remote.RemoteFlashEvents>0&&Remote.RemoteTrailEvents>0,"Remote Client sound, muzzle and trails reach Host");
   yield return Until(()=>File.Exists(Path.Combine(folder,"host-shot-seen.txt")),"Host effects never reached Client");
   Write("command.txt","OCCLUDE");Place(true);yield return new WaitForSeconds(1);
   Check(Remote.GetComponent<PlayerNameplate>().IsOccluded&&Remote.GetComponent<PlayerNameplate>().NameOpacity<.5f,"Name remains visible with reduced opacity through a wall");
   yield return Until(()=>File.Exists(Path.Combine(folder,"occlusion-client.txt")),"Client occlusion validation failed");
   Write("command.txt","POSITION");Place();yield return new WaitForSeconds(.6f);
   Check(!Remote.GetComponent<PlayerNameplate>().IsOccluded&&Remote.GetComponent<PlayerNameplate>().NameOpacity>.9f,"Name restores opacity outside obstruction");
   Local.Switcher.UnlockWeapon("Rifle");yield return Until(()=>Local.Switcher.CurrentWeaponIndex==1&&!Local.Switcher.IsSwitching,"Rifle switch failed");yield return new WaitForSeconds(.3f);
   Write("command.txt","RIFLE");
   var w=Local.Switcher.CurrentWeapon;w.InputLocked=false;w.SendMessage("TryFire");yield return new WaitForSeconds(.25f);
   yield return Until(()=>File.Exists(Path.Combine(folder,"rifle-client.txt")),"Remote rifle was not confirmed");Check(true,"Weapon switch and rifle animation replicate to remote Client");
   // The overlay camera stays horizontal while the world camera looks down.
   var field=typeof(Weapon).GetField("playerCamera",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);var camera=(Camera)field.GetValue(w);
   camera.transform.rotation=Quaternion.Euler(60,0,0);
   var events=Local.Switcher.GetComponent<WeaponAnimationEvents>();var eject=typeof(WeaponAnimationEvents).GetField("casingEjectPoint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(events) as Transform;
   w.PresentationPose(eject,out var expected,out _);var before=FindObjectsByType<Casing>(FindObjectsSortMode.None);events.OnEjectCasing();var casing=FindObjectsByType<Casing>(FindObjectsSortMode.None).FirstOrDefault(c=>!before.Contains(c));
   Check(casing&&Vector3.Distance(casing.transform.position,expected)<.01f,"Casing ejects at the visible weapon position while looking down");camera.transform.localRotation=Quaternion.identity;
   Local.Switcher.UnlockWeapon("Escopeta");yield return Until(()=>Local.Switcher.CurrentWeaponIndex==2&&!Local.Switcher.IsSwitching,"Shotgun switch failed");yield return new WaitForSeconds(.3f);
   w=Local.Switcher.CurrentWeapon;w.InputLocked=false;int initial=w.CurrentAmmo;
   for(int i=0;i<3;i++){
    int ammo=w.CurrentAmmo;w.SendMessage("TryFire");yield return Until(()=>w.CurrentAmmo==ammo-1,"Shotgun did not fire");
    if(i<2)yield return Until(()=>!w.IsChambering,"Shotgun bolt never completed",5);
   }
   w.SendMessage("OnReload",default(UnityEngine.InputSystem.InputAction.CallbackContext));
   Check(w.IsChambering&&!w.IsReloading,"Rapid reload waits for the shotgun bolt cycle");
   yield return Until(()=>w.IsReloading,"Queued shotgun reload never started",5);
   int prior=w.CurrentAmmo;w.AnimationAmmunitionFill();Check(w.CurrentAmmo==prior,"Magazine-fill event cannot instantly fill a multipart shotgun reload");
   var forestock=w.GetComponentsInChildren<Transform>(true).First(t=>t.name=="forestock");
   var fpAnimator=Local.Switcher.GetComponent<Animator>();var openState=fpAnimator.GetLayerIndex("HandGunADD");bool sampledOpen=false;float openPosition=0;
   int totalInserted=0;float end=Time.realtimeSinceStartup+18;
   while(w.IsReloading){if(Time.realtimeSinceStartup>end)throw new TimeoutException("Shotgun reload did not finish");if(fpAnimator.GetCurrentAnimatorStateInfo(openState).IsName("Reload Insert") && !fpAnimator.IsInTransition(openState)) {
     if(!sampledOpen){openPosition=forestock.localPosition.y;sampledOpen=true;}
     if(Mathf.Abs(forestock.localPosition.y-openPosition)>.02f)throw new Exception("Shotgun forestock closes during shell insertion");
    }
    int now=w.CurrentAmmo;CheckDelta(now-prior);totalInserted+=now-prior;prior=now;yield return null;}
   Check(sampledOpen && openPosition>-.30f,"Shotgun forestock stays open throughout shell insertion");
   Check(totalInserted==3&&w.CurrentAmmo==initial,"Shotgun reload inserts exactly one shell per animation event");
   Check(sawLocalBolt&&sawLocalPartsBolt,"Shotgun arm pump and actual weapon bolt both animate (arms="+sawLocalBolt+", parts="+sawLocalPartsBolt+")");
   Write("command.txt","SHOTGUN");yield return Until(()=>File.Exists(Path.Combine(folder,"shotgun-client.txt")),"Remote shotgun did not update");Check(true,"Remote Client receives the real shotgun model and controller");
   Capture("host-final.png");Write("command.txt","END");yield return Until(()=>File.Exists(Path.Combine(folder,"result-client.json")),"Client did not finish");Complete(true,"");
  }
  void CheckDelta(int delta){if(delta<0||delta>1)throw new InvalidOperationException("Shotgun ammo jumped by "+delta+" shells");}
  IEnumerator Client() {
   string last="";float end=Time.realtimeSinceStartup+120;
   while(!finished){if(Time.realtimeSinceStartup>end)throw new TimeoutException("Host validation did not finish");
    if(File.Exists(Path.Combine(folder,"result-host.json"))&&Read("result-host.json").Contains("\"ok\": false"))throw new Exception("Host validation failed");
    if(Remote.RemoteShotCount>0&&!File.Exists(Path.Combine(folder,"host-shot-seen.txt"))){Check(Remote.RemoteSoundEvents>0&&Remote.RemoteFlashEvents>0&&Remote.RemoteTrailEvents>0,"Host shot sound, muzzle and trail reach remote Client");Write("host-shot-seen.txt","ok");}
    string command=File.Exists(Path.Combine(folder,"command.txt"))?Read("command.txt"):"";
    if(command!=last){last=command;
     if(command=="FIRE")Local.Switcher.CurrentWeapon.SendMessage("TryFire");
     if(command=="OCCLUDE"){Place(true);yield return new WaitForSeconds(.8f);Check(Remote.GetComponent<PlayerNameplate>().IsOccluded&&Remote.GetComponent<PlayerNameplate>().NameOpacity<.5f,"Remote Client sees occluded name semi-transparent");Write("occlusion-client.txt","ok");Capture("name-through-wall.png");}
     if(command=="POSITION")Place();
     if(command=="RIFLE") {yield return Until(()=>Remote.EquippedIndex==1&&Remote.VisibleWeapon,"Remote rifle missing");yield return new WaitForSeconds(.6f);Check(sawRemoteFire,"Owner fire trigger is visible in remote third-person Animator");Write("rifle-client.txt","ok");Capture("remote-rifle.png");}
     if(command=="SHOTGUN"){yield return Until(()=>Remote.EquippedIndex==2&&Remote.VisibleWeapon,"Remote shotgun missing");Check(Remote.VisibleWeapon.name.Contains("Escopeta"),"Equipped shotgun uses matching mesh and animation override");Write("shotgun-client.txt","ok");Capture("remote-shotgun.png");}
     if(command=="END"){Complete(true,"");yield break;}
    }yield return null;
   }
  }
  void Capture(string name){var camera=Camera.main;if(!camera)return;var old=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1600,900,24);try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,name),image.EncodeToPNG());Destroy(image);}finally{camera.targetTexture=old;RenderTexture.active=active;rt.Release();Destroy(rt);}}
 }
}
#endif
