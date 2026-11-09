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
namespace DeadQuarantine.Development
{
 [DefaultExecutionOrder(-10000)]
 public sealed partial class Phase6WeaponsSmokeDriver:MonoBehaviour
 {
  static readonly string[] Ids={"Handgun01","Handgun02","SMG02","SMG05","Sniper01","Sniper02","Sniper03"};
  string role,folder;bool finished;readonly List<string> checks=new();
  NetworkManager Network=>NetworkManager.Singleton;
  NetworkWeaponPresentation Local=>Network.LocalClient?.PlayerObject?Network.LocalClient.PlayerObject.GetComponent<NetworkWeaponPresentation>():null;
  NetworkWeaponPresentation Remote=>FindObjectsByType<NetworkWeaponPresentation>(FindObjectsSortMode.None).FirstOrDefault(p=>p.IsSpawned&&!p.IsOwner);
  [Serializable] class Result {public bool ok;public string role,error;public string[] checks;}
  static string Arg(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:null;}
  void Awake(){
   var selected=new GameObject("Phase 6 validation selection").AddComponent<ConfiguracionPartidaSeleccionada>();
   selected.escenarioSeleccionado=ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;
   selected.modoSeleccionado=ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;
  }
  void Start(){phase7=Arg("-dq-weapons-phase")=="optics"||Arg("-dq-weapons-phase")=="pistols"||Arg("-dq-weapons-phase")=="poses"||Arg("-dq-weapons-phase")=="25"||Arg("-dq-weapons-phase")=="25a"||Arg("-dq-weapons-phase")=="7"||Arg("-dq-weapons-phase")=="8"||Arg("-dq-weapons-phase")=="9";Application.runInBackground=true;role=Arg("-dq-weapons-role");folder=Arg("-dq-weapons-folder");if(string.IsNullOrEmpty(role)){enabled=false;return;}Directory.CreateDirectory(folder);StartCoroutine(Safe(Run()));}
  IEnumerator Safe(IEnumerator r){while(!finished){object current;try{if(!r.MoveNext())yield break;current=r.Current;}catch(Exception e){Complete(false,e.ToString());yield break;}if(current is IEnumerator nested)yield return Safe(nested);else yield return current;}}
  IEnumerator Until(Func<bool> condition,string message,float timeout=30){float end=Time.realtimeSinceStartup+timeout;while(!condition()){if(Time.realtimeSinceStartup>end)throw new TimeoutException(message);yield return null;}}
  void Write(string name,string value){string p=Path.Combine(folder,name);try{File.WriteAllText(p+".tmp",value);if(File.Exists(p))File.Replace(p+".tmp",p,null);else File.Move(p+".tmp",p);}catch(IOException)when(name.StartsWith("progress-",StringComparison.Ordinal)){File.Delete(p+".tmp"); /* A progress viewer may briefly hold the previous file. Final results retain every check. */}}
  void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);Write("progress-"+role+".json",JsonUtility.ToJson(new Result{role=role,checks=checks.ToArray()},true));}
  void Complete(bool ok,string error){if(finished)return;finished=true;Write("result-"+role+".json",JsonUtility.ToJson(new Result{role=role,ok=ok,error=error,checks=checks.ToArray()},true));Network.Shutdown();Application.Quit(ok?0:1);}
  IEnumerator Barrier(string key){Write(role+"-"+key,"ok");yield return Until(()=>File.Exists(Path.Combine(folder,(role=="host"?"client":"host")+"-"+key)),"Peer barrier "+key);}
  IEnumerator Run(){
   FindFirstObjectByType<GestorEscenariosPartida>().ActivarEscenario(ConfiguracionPartidaSeleccionada.Escenario.Laboratorio);
   Network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",7796,"0.0.0.0");
   if(role=="host")Network.StartHost();else{yield return new WaitForSeconds(1);Network.StartClient();}
   yield return Until(()=>Local&&Remote,"Two actual player objects");yield return new WaitForSeconds(1);
   yield return Until(()=>Local.GetComponent<MultiplayerPlayerSpawnAssigner>().IsAtSpawn.Value,"Spawn acknowledged");
   foreach(var behaviour in Local.GetComponentsInChildren<MonoBehaviour>(true))if(new[]{"PlayerMovement","PlayerLook","CameraRecoil","PlayerDownedEffects","PlayerCameraBob"}.Contains(behaviour.GetType().Name))behaviour.enabled=false;
   var controller=Local.GetComponent<CharacterController>();controller.enabled=false;Local.transform.SetPositionAndRotation(role=="host"?new Vector3(0,.1f,0):new Vector3(0,.1f,5),Quaternion.Euler(0,role=="host"?0:180,0));controller.enabled=true;
   Local.GetComponent<NetworkTransform>().Teleport(Local.transform.position,Local.transform.rotation,Local.transform.localScale);
   Check(Local.Weapons.Length==16,"Sixteen distinct eligible pack weapons; launchers excluded");
   if(Arg("-dq-weapons-phase")=="optics"){yield return VerifyOpticPresentation();yield break;}
   if(Arg("-dq-weapons-phase")=="pistols"){yield return VerifyPistolPresentation();yield break;}
   if(Arg("-dq-weapons-phase")=="poses"){yield return VerifyWeaponPoses();yield break;}
   if(Arg("-dq-weapons-phase")=="25"){yield return Animations25();yield break;}
   if(Arg("-dq-weapons-phase")=="25a"){yield return Attachments25();yield break;}
   if(Arg("-dq-weapons-phase")=="9"){yield return Animations9();yield break;}
   if(Arg("-dq-weapons-phase")=="8"){yield return Attachments8();yield break;}
   var selectedIds=phase7?Phase7Ids:Ids;
   for(int i=0;i<selectedIds.Length;i++){
    string id=selectedIds[i];int index=Array.FindIndex(Local.Weapons,w=>w.Id==id);Check(index>=(phase7?3:9),id+" has its stable non-protected slot");
    Local.Switcher.UnlockWeapon(id);yield return Until(()=>Local.Switcher.CurrentWeaponIndex==index&&!Local.Switcher.IsSwitching,id+" equip");
    var w=Local.Switcher.CurrentWeapon;w.InputLocked=false;yield return new WaitForSeconds(.35f);
    yield return Barrier(id+"-equipped");yield return Until(()=>Remote.EquippedIndex==index&&Remote.VisibleWeapon,id+" remote equip");
    Check(Remote.VisibleWeapon.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh==Local.Weapons[index].Model.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh,id+" FP/TP mesh and remote equipment agree");
    if(phase7)yield return Locomotion7(id);
    Capture(role+"-"+id+"-hip.png");CaptureRemote(id);
    int rounds=w.UsesBurstFire?3:1;int ammo=w.CurrentAmmo,shots=Remote.RemoteShotCount,flash=Remote.RemoteFlashEvents,sound=Remote.RemoteSoundEvents,trails=Remote.RemoteTrailEvents,eject=Remote.EjectCount;
    yield return Barrier(id+"-fire");w.SendMessage("TryFire");
    yield return Until(()=>w.CurrentAmmo==ammo-rounds,id+" consumes one bullet");yield return Until(()=>Remote.RemoteShotCount>=shots+rounds,id+" remote shot");
    Check(Remote.RemoteFlashEvents>flash&&Remote.RemoteSoundEvents>sound&&Remote.RemoteTrailEvents>trails,id+" real remote muzzle, sound and bullet trail");
    yield return Until(()=>!w.IsChambering,id+" bolt completes",5);yield return Until(()=>Remote.EjectCount>eject,id+" ejection event");
    w.SendMessage("OnAimStarted",default(UnityEngine.InputSystem.InputAction.CallbackContext));yield return new WaitForSeconds(.8f);
    Check(w.IsAiming,id+" ADS enabled");if(phase7)yield return CheckSight7(id,w);Capture(role+"-"+id+"-ads.png");
    w.SendMessage("OnAimCanceled",default(UnityEngine.InputSystem.InputAction.CallbackContext));yield return new WaitForSeconds(.25f);
    w.SendMessage("OnReload",default(UnityEngine.InputSystem.InputAction.CallbackContext));Check(w.IsReloading,id+" reload starts");
    int reserve=w.ReserveAmmo,prior=w.CurrentAmmo;bool inserted=false;
    float end=Time.realtimeSinceStartup+15;
    while(w.IsReloading){if(Time.realtimeSinceStartup>end)throw new TimeoutException(id+" reload");if(id=="Sniper01"&&w.CurrentAmmo!=prior){Check(w.CurrentAmmo-prior==1,id+" inserts one cartridge per event");inserted=true;prior=w.CurrentAmmo;}yield return null;}
    Check(w.CurrentAmmo==ammo&&w.ReserveAmmo==reserve-rounds,id+" reload conserves reserve ammunition");if(id=="Sniper01")Check(inserted,id+" multipart reload completed through animation events");
    if(phase7)yield return InputAndSwitch7(id,w,index);
    // Empty reload is a separate path; exercise it for every newly integrated weapon.
    while(w.CurrentAmmo>0){
     int before=w.CurrentAmmo;w.SendMessage("TryFire");yield return Until(()=>w.CurrentAmmo==before-rounds,id+" magazine shot");
     if(w.CurrentAmmo>0)yield return new WaitForSeconds(id=="Sniper01"?2.15f:w.UsesBurstFire?(float)Field7(w,"burstCooldown")+.05f:w.PresentationFireInterval+.035f);
    }
    w.SendMessage("OnReload",default(UnityEngine.InputSystem.InputAction.CallbackContext));
    if(id=="Sniper01")Check(w.IsChambering&&!w.IsReloading,id+" rapid reload queues behind the bolt");
    yield return Until(()=>w.IsReloading,id+" empty reload starts",5);
    reserve=w.ReserveAmmo;prior=w.CurrentAmmo;int insertedCount=0;end=Time.realtimeSinceStartup+20;
    while(w.IsReloading){
     if(Time.realtimeSinceStartup>end)throw new TimeoutException(id+" empty reload");
     if(id=="Sniper01"&&w.CurrentAmmo!=prior){Check(w.CurrentAmmo-prior==1,id+" empty reload adds exactly one cartridge");insertedCount++;prior=w.CurrentAmmo;}
     CaptureReload(id);
     yield return null;
    }
    Check(w.CurrentAmmo==ammo&&w.ReserveAmmo==reserve-ammo,id+" empty reload restores capacity and consumes exact reserve");
    if(id=="Sniper01")Check(insertedCount==ammo,id+" empty magazine reload uses five distinct insert events");
    yield return Barrier(id+"-done");if(phase7)Check(emptyReloadSeen7.Contains(index)&&!emptyReloadLost7.Contains(index),id+" empty-reload selection and speed remain frozen on peer until completion");
   }
   yield return Barrier("complete");Complete(!phase7||phase7Issues.Count==0,phase7?string.Join("\n",phase7Issues):"");
  }
  void CaptureRemote(string id){
   var go=new GameObject("Phase 6 remote view");var camera=go.AddComponent<Camera>();camera.nearClipPlane=.01f;camera.farClipPlane=30;camera.fieldOfView=45;camera.cullingMask=1<<Remote.Body.gameObject.layer;
   camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.10f,.12f);
   camera.transform.position=Remote.Body.position+Remote.Body.rotation*new Vector3(2.3f,1.5f,2.3f);camera.transform.LookAt(Remote.Body.position+Vector3.up*1.05f);
   var rt=new RenderTexture(1000,900,24);var active=RenderTexture.active;
   try{camera.targetTexture=rt;if(phase7)UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=rt});else camera.Render();RenderTexture.active=rt;var img=new Texture2D(1000,900,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1000,900),0,0);img.Apply();File.WriteAllBytes(Path.Combine(folder,role+"-"+id+"-tp.png"),img.EncodeToPNG());Destroy(img);}
   finally{RenderTexture.active=active;camera.targetTexture=null;rt.Release();Destroy(rt);Destroy(go);}
  }
  void CaptureReload(string id){
   var animator=Local.Switcher.GetComponent<Animator>();int layer=animator.GetLayerIndex("Reload");
   if(layer<0||animator.IsInTransition(layer))return;
   var state=animator.GetCurrentAnimatorStateInfo(layer);if(state.normalizedTime<.45f||state.normalizedTime>.7f)return;
   string stage=state.IsName("Reload_Insert")||state.IsName("Reload_Insert 2")?"Insert":state.IsName("Reload_Open")?"Open":state.IsName("Reload_Close")?"Close":"Magazine";
   string name=role+"-"+id+"-reload-"+stage+".png";if(!File.Exists(Path.Combine(folder,name)))Capture(name);
  }
  void Capture(string name){var camera=Camera.main;if(!camera)return;var old=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);try{camera.targetTexture=rt;if(phase7)UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=rt});else camera.Render();RenderTexture.active=rt;var img=new Texture2D(1280,720,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1280,720),0,0);img.Apply();File.WriteAllBytes(Path.Combine(folder,name),img.EncodeToPNG());Destroy(img);}finally{camera.targetTexture=old;RenderTexture.active=active;rt.Release();Destroy(rt);}}
 }
}
#endif
