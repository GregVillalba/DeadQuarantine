#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.IO;using System.Collections;using System.Linq;using UnityEngine;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using Unity.Netcode.Components;
namespace DeadQuarantine.Development {public sealed partial class OriginalWeaponsSmokeDriver {
 public GameObject MerchantPrefab;
 IEnumerator ReviewMerchantFlashlight(){
  Application.runInBackground=true;var keys=InputSystem.AddDevice<Keyboard>();InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
  void ToggleF(bool down){InputSystem.EnableDevice(keys);keys.MakeCurrent();InputSystem.QueueStateEvent(keys,new KeyboardState(down?new[]{KeyCodeF}:Array.Empty<Key>()));InputSystem.Update();}
  Local.Switcher.UnlockWeapon("Rifle",true);yield return Until(()=>Local.Switcher.ObtenerArmaEquipadaId()=="Rifle"&&!Local.Switcher.IsSwitching,"Legacy compatible rifle ready");
  var cc=Local.GetComponent<CharacterController>();cc.enabled=false;var point=new Vector3(role=="host"?-1:1,.05f,0);Local.transform.SetPositionAndRotation(point,Quaternion.identity);Local.GetComponent<NetworkTransform>().Teleport(point,Quaternion.identity,Local.transform.localScale);
  var merchant=Instantiate(MerchantPrefab,new Vector3(0,0,2),Quaternion.identity);var vendor=merchant.GetComponentInChildren<NPCWeaponVendor>();merchant.transform.position+=new Vector3(0,0,2)-vendor.transform.position;var offer=vendor.Offers.Single(o=>o.esLinterna);var resources=Local.GetComponent<PlayerRecursos>();var light=Local.GetComponent<Flashlight>();var equipment=Local.GetComponent<NetworkWeaponAttachments>();
  bool? result=null;resources.OnPurchaseResult+=(id,ok)=>{if(id==offer.weaponId)result=ok;};
  Write(role+"-merchant-ready","ok");yield return Until(()=>File.Exists(PathCombine("host-merchant-ready"))&&File.Exists(PathCombine("client-merchant-ready")),"Both legacy owners ready");
  if(role=="host")foreach(var r in FindObjectsByType<PlayerRecursos>(FindObjectsSortMode.None)){r.PilaDeLatasNetwork.Value=0;r.CantimploraNetwork.Value=0;r.BidonNetwork.Value=0;}
  yield return new WaitForSecondsRealtime(.3f);Check(!light.Purchased&&!light.FlashlightAvailable,"Original modes do not grant a free flashlight");
  string weapon=Local.Switcher.ObtenerArmaEquipadaId();Check(equipment.Request(weapon,new WeaponAttachmentChoice{utility="Flashlight_01"}),"Legacy attachment equipped through existing network path");yield return Until(()=>equipment.GetChoice(Local.Switcher.CurrentWeaponIndex).utility=="Flashlight_01","Legacy attachment synchronized");
  ToggleF(true);yield return new WaitForSecondsRealtime(.2f);ToggleF(false);Check(!light.UtilityEnabled&&!light.BeamVisible,"Equipped but unpaid flashlight cannot turn on");
  vendor.RequestPurchase(offer.weaponId);yield return Until(()=>result.HasValue,"Merchant denied result");Check(result==false&&!light.Purchased,"Purchase without resources is denied by the server");Write(role+"-denied","ok");
  yield return Until(()=>File.Exists(PathCombine("host-denied"))&&File.Exists(PathCombine("client-denied")),"Both insufficient-funds checks");
  if(role=="host")foreach(var r in FindObjectsByType<PlayerRecursos>(FindObjectsSortMode.None))foreach(TipoRecurso kind in Enum.GetValues(typeof(TipoRecurso)))r.SumarRecurso(kind,20);
  yield return Until(()=>resources.PilaDeLatasNetwork.Value==20&&resources.BidonNetwork.Value==20,"Server resources synchronized");result=null;vendor.RequestPurchase(offer.weaponId);yield return Until(()=>result==true&&light.Purchased,"Actual merchant purchase succeeds");
  yield return Until(()=>resources.PilaDeLatasNetwork.Value==20-offer.precioPilaDeLatas&&resources.BidonNetwork.Value==20-offer.precioBidon,"Server charges actual merchant offer");
  Check(light.FlashlightAvailable,"Purchased flashlight becomes available");ToggleF(true);yield return new WaitForSecondsRealtime(.2f);ToggleF(false);yield return Until(()=>light.UtilityEnabled&&light.BeamVisible,"F enables the original purchased light");
  Write(role+"-light-on","ok");yield return Until(()=>File.Exists(PathCombine("host-light-on"))&&File.Exists(PathCombine("client-light-on")),"Both purchased lights enabled");yield return new WaitForSecondsRealtime(.2f);Check(Remote.GetComponent<Flashlight>().Purchased&&Remote.GetComponent<Flashlight>().BeamVisible,"Remote paid light is synchronized and visible");
  int cans=resources.PilaDeLatasNetwork.Value,fuel=resources.BidonNetwork.Value;result=null;vendor.RequestPurchase(offer.weaponId);yield return Until(()=>result.HasValue,"Duplicate merchant result");Check(result==false&&resources.PilaDeLatasNetwork.Value==cans&&resources.BidonNetwork.Value==fuel,"Duplicate equipped purchase never charges twice");
  Local.Switcher.UnlockWeapon("Escopeta",true);yield return Until(()=>Local.Switcher.ObtenerArmaEquipadaId()=="Escopeta"&&!Local.Switcher.IsSwitching,"Existing compatible shotgun selected");yield return new WaitForSecondsRealtime(.4f);result=null;vendor.RequestPurchase(offer.weaponId);yield return Until(()=>result==true&&light.FlashlightAvailable,"Owned flashlight equips on another gun");Check(resources.PilaDeLatasNetwork.Value==cans&&resources.BidonNetwork.Value==fuel,"Re-equipping an owned flashlight is free");
  Write(role+"-purchase-complete","ok");yield return Until(()=>File.Exists(PathCombine("host-purchase-complete"))&&File.Exists(PathCombine("client-purchase-complete")),"Both legacy purchase flows complete");
  if(Arg("-dq-original-score")=="1")yield return ReviewOriginalScoring();
  if(role=="host")foreach(var p in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))if(p.IsSpawned)p.State.Value=PlayerHealth.PlayerState.Spectating;
  yield return Until(()=>!light.Purchased&&!light.BeamVisible,"Definitive death clears purchase and original light");Write(role+"-death-checked","ok");yield return Until(()=>File.Exists(PathCombine("host-death-checked"))&&File.Exists(PathCombine("client-death-checked")),"Both life resets verified");
  InputSystem.RemoveDevice(keys);
 }
 const Key KeyCodeF=Key.F;
 string PathCombine(string file)=>System.IO.Path.Combine(folder,file);
}}
#endif
