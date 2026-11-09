#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace DeadQuarantine.Development
{
 public sealed partial class Phase6WeaponsSmokeDriver
 {
  static readonly string[] Phase7Ids={"AutoPistol","SemiRifle","BurstRifle","P90","Vector","MAC10","Handgun01","Handgun02","SMG02","SMG05","Sniper01","Sniper02","Sniper03"};
  readonly List<string> phase7Issues=new();
  void Note7(bool ok,string message){if(!ok){phase7Issues.Add(message);Write("issues-"+role+".txt",string.Join("\n",phase7Issues));}else Check(true,message);}
  bool phase7;Keyboard testKeyboard;int emptyReloadIndex7=-1;
  readonly HashSet<int> emptyReloadSeen7=new(),emptyReloadLost7=new();
  readonly HashSet<int> localHolster7=new(),localDraw7=new(),remoteHolster7=new(),remoteDraw7=new();
  static object Field7(object obj,string name)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
  void Keys7(params Key[] keys){if(testKeyboard==null)testKeyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(keys));}
  void Update(){
   if(!phase7||!Local||!Remote)return;
   var state=((NetworkVariable<NetworkWeaponPresentation.State>)Field7(Remote,"state")).Value;
   if(state.Weapon<3||(state.Flags&1)==0)emptyReloadIndex7=-1;
   else if(emptyReloadIndex7==state.Weapon&&(state.Flags&16)==0)emptyReloadLost7.Add(state.Weapon);
   else if((state.Flags&16)!=0){emptyReloadIndex7=state.Weapon;emptyReloadSeen7.Add(state.Weapon);}
   Observe7(Local.Switcher.GetComponent<Animator>(),"Holster",Local.Switcher.CurrentWeaponIndex,localHolster7,localDraw7);
   Observe7(Remote.BodyAnimator,"WeaponSwitch",Remote.EquippedIndex,remoteHolster7,remoteDraw7);
  }
  static void Observe7(Animator animator,string layerName,int index,HashSet<int> holsters,HashSet<int> draws){
   if(!animator||index<3)return;int layer=animator.GetLayerIndex(layerName);if(layer<0)return;
   var state=animator.GetCurrentAnimatorStateInfo(layer);if(state.IsName("Holster"))holsters.Add(index);if(state.IsName("Unholster"))draws.Add(index);
  }
  IEnumerator Locomotion7(string id){
   var movement=Local.GetComponent<PlayerMovement>();var controls=(PlayerControls)Field7(movement,"controls");controls.asset.RemoveAllBindingOverrides();movement.MovementLocked=false;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
   Vector3 start=Local.transform.position;Quaternion rotation=Local.transform.rotation;movement.enabled=true;
   Keys7(Key.W,Key.A);yield return new WaitForSeconds(.45f);
   var animator=Local.Switcher.GetComponent<Animator>();
   Check(Vector3.Distance(start,Local.transform.position)>.2f&&animator.GetFloat("Speed")>.2f,id+" actual walking input drives FP locomotion (distance="+Vector3.Distance(start,Local.transform.position)+", speed="+animator.GetFloat("Speed")+", input="+controls.Player.Move.ReadValue<Vector2>()+")");
   Keys7(Key.W,Key.A,Key.LeftShift);yield return new WaitForSeconds(.6f);
   Check(movement.IsSprinting&&animator.GetBool("Running"),id+" actual sprint input drives the correct FP running state");
   yield return Until(()=>Remote.BodyAnimator.GetBool("TP_IsSprinting"),id+" remote sprint synchronization",5);
   Check(true,id+" TP sprint visible on peer");CaptureRemote(id+"-sprint");yield return Barrier(id+"-sprint-observed");
   Keys7();yield return new WaitForSeconds(.25f);Keys7(Key.LeftCtrl);yield return new WaitForSeconds(.35f);
   Check(movement.IsCrouching,id+" crouch input changes stance");yield return Until(()=>Remote.BodyAnimator.GetBool("TP_IsCrouching"),id+" remote crouch synchronization",5);
   CaptureRemote(id+"-crouch");yield return Barrier(id+"-crouch-observed");Keys7();yield return new WaitForSeconds(.3f);
   if(movement.IsCrouching){Keys7(Key.LeftCtrl);yield return new WaitForSeconds(.1f);Keys7();yield return new WaitForSeconds(.3f);}
   movement.enabled=false;var controller=Local.GetComponent<CharacterController>();controller.enabled=false;Local.transform.SetPositionAndRotation(start,rotation);controller.enabled=true;Local.GetComponent<NetworkTransform>().Teleport(start,rotation,Local.transform.localScale);
   yield return new WaitForSeconds(.3f);yield return Barrier(id+"-locomotion");
  }
  IEnumerator CheckSight7(string id,Weapon w){
   var marker=(Transform)Field7(w,"adsSightReference");var camera=(Camera)Field7(w,"weaponCamera");Vector3 point=default,front=default;bool sampled=false;
   Action<ScriptableRenderContext,Camera> probe=(context,rendered)=>{if(rendered==camera){point=camera.WorldToViewportPoint(marker.position);front=camera.WorldToViewportPoint(marker.position-WeaponAttachments.Find(w.transform,"root").up*.4f);sampled=true;}};
   RenderPipelineManager.beginCameraRendering+=probe;
   try{Capture(role+"-"+id+"-ads.png");yield return Until(()=>sampled,id+" overlay camera render sample",5);}finally{RenderPipelineManager.beginCameraRendering-=probe;}
   Capture(role+"-"+id+"-ads.png");
   var rig=Local.Switcher.GetComponent<Animator>();var lines=new List<string>{"viewport="+point+" camera="+camera.name+" enabled="+camera.enabled+" root="+w.transform.localPosition+" rotation="+w.transform.localEulerAngles};
   for(int i=0;i<rig.layerCount;i++)lines.Add(rig.GetLayerName(i)+" weight="+rig.GetLayerWeight(i)+" time="+rig.GetCurrentAnimatorStateInfo(i).normalizedTime+" clips="+string.Join(",",rig.GetCurrentAnimatorClipInfo(i).Select(c=>c.clip.name+":"+c.weight)));
   var physical=marker.parent;
   lines.Add("physical="+physical.position+" forward="+(-physical.up)+" camForward="+camera.transform.forward+" cameraPosition="+camera.transform.position+" rigRotation="+rig.transform.rotation.eulerAngles+" aim="+rig.GetFloat("Aiming"));
   Write(role+"-"+id+"-pose.txt",string.Join("\n",lines));
   Note7(Vector2.Distance(new Vector2(front.x,front.y),Vector2.one*.5f)<.04f,id+" ADS barrel axis also aligns with the optical centre ("+front+")");
   Note7(marker.name=="DQ_OpticalSight"&&point.z>0&&Vector2.Distance(new Vector2(point.x,point.y),Vector2.one*.5f)<.018f,id+" optical ADS reference aligns with the camera centre ("+point+")");
  }
  IEnumerator InputAndSwitch7(string id,Weapon w,int index){
   var controls=(PlayerControls)Field7(w,"controls");controls.Player.Fire.ApplyBindingOverride("<Keyboard>/f12");
   bool automatic=(bool)Field7(w,"isAutomatic");int prior=w.CurrentAmmo,shots=Remote.RemoteShotCount,eject=Remote.EjectCount;
   w.SendMessage("OnAimStarted",default(InputAction.CallbackContext));yield return new WaitForSeconds(.65f);
   yield return Barrier(id+"-input-start");Keys7(Key.F12);yield return new WaitForSeconds(.35f);Keys7();w.SendMessage("OnAimCanceled",default(InputAction.CallbackContext));yield return new WaitForSeconds(.2f);
   int spent=prior-w.CurrentAmmo;Check(automatic?spent>=2:spent==(w.UsesBurstFire?3:1),id+" held Input System fire respects automatic / semi / burst mode");
   Write(role+"-"+id+"-input-rounds",spent.ToString());yield return Barrier(id+"-input-count");
   int peer=int.Parse(File.ReadAllText(Path.Combine(folder,(role=="host"?"client":"host")+"-"+id+"-input-rounds")));
   yield return new WaitForSeconds(.8f);
   Note7(Remote.RemoteShotCount-shots==peer,id+" peer rounds: expected="+peer+" actual="+(Remote.RemoteShotCount-shots));
   Note7(Remote.EjectCount-eject==peer,id+" peer casings: expected="+peer+" actual="+(Remote.EjectCount-eject));
   yield return Until(()=>!w.IsChambering,id+" input bolt completes",5);
   w.SendMessage("OnReload",default(InputAction.CallbackContext));yield return Until(()=>!w.IsReloading,id+" input reload finishes",15);
   yield return new WaitForSeconds(.2f);w.SendMessage("TryFire");yield return new WaitForSeconds(.15f);
   w.SendMessage("OnReload",default(InputAction.CallbackContext));yield return new WaitForSeconds(.1f);
   Local.Switcher.RequestEquip(0);yield return Until(()=>Local.Switcher.CurrentWeaponIndex==0&&!Local.Switcher.IsSwitching,id+" holster while reloading");
   Check(!w.IsReloading,id+" changing weapons cancels its interrupted reload");
   Local.Switcher.RequestEquip(index);yield return Until(()=>Local.Switcher.CurrentWeaponIndex==index&&!Local.Switcher.IsSwitching,id+" re-equip after interrupted reload");
   yield return Barrier(id+"-switch");yield return new WaitForSeconds(.3f);
   Check(localHolster7.Contains(index)&&localDraw7.Contains(index),id+" FP holster / unholster states play");
   Check(remoteHolster7.Contains(index)&&remoteDraw7.Contains(index),id+" TP holster / unholster states play on peer");
   w.InputLocked=false;w.SendMessage("OnReload",default(InputAction.CallbackContext));yield return Until(()=>!w.IsReloading,id+" recovery reload",15);
   yield return new WaitForSeconds(.2f);
  }
 }
}
#endif
