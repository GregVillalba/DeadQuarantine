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
  Check(DisponibilidadTienda.UsaLinternaGratuita,"Traditional mode enables the included flashlight");
  Check(!DisponibilidadTienda.SeMuestraEnPartidaActual(offer),"Merchant hides the included flashlight");
  yield return Until(()=>light.FlashlightAvailable&&equipment.GetChoice(Local.Switcher.CurrentWeaponIndex).utility=="Flashlight_01","Starter weapon has included flashlight");
  Check(!light.Purchased&&!light.BeamVisible,"Included flashlight begins off and costs no resources");
  int cans=resources.PilaDeLatasNetwork.Value,fuel=resources.BidonNetwork.Value;
  bool? result=null;
  resources.OnPurchaseResult+=(id,ok)=>{if(id==offer.weaponId)result=ok;};
  vendor.RequestPurchase(offer.weaponId);
  yield return Until(()=>result.HasValue,"Merchant responds to old flashlight offer");
  Check(result==false&&resources.PilaDeLatasNetwork.Value==cans&&resources.BidonNetwork.Value==fuel,"Removed offer cannot charge resources");
  ToggleF(true);yield return new WaitForSecondsRealtime(.2f);ToggleF(false);
  yield return Until(()=>light.UtilityEnabled&&light.BeamVisible,"F enables the included light");
  Write(role+"-light-on","ok");
  yield return Until(()=>File.Exists(PathCombine("host-light-on"))&&File.Exists(PathCombine("client-light-on")),"Both included lights enabled");
  yield return Until(()=>Remote.GetComponent<Flashlight>().BeamVisible,"Remote included beam is visible");
  Local.Switcher.UnlockWeapon("Escopeta",true);
  yield return Until(()=>Local.Switcher.ObtenerArmaEquipadaId()=="Escopeta"&&!Local.Switcher.IsSwitching,"Switch to next weapon");
  yield return Until(()=>light.FlashlightAvailable&&equipment.GetChoice(Local.Switcher.CurrentWeaponIndex).utility=="Flashlight_01","Next weapon has included flashlight");
  Write(role+"-purchase-complete","ok");
  yield return Until(()=>File.Exists(PathCombine("host-purchase-complete"))&&File.Exists(PathCombine("client-purchase-complete")),"Both peers changed weapons");
  if(Arg("-dq-original-score")=="1")yield return ReviewOriginalScoring();
  if(role=="host")foreach(var p in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))if(p.IsSpawned)p.State.Value=PlayerHealth.PlayerState.Spectating;
  yield return Until(()=>!light.BeamVisible,"Definitive death switches off the light");
  Write(role+"-death-checked","ok");
  yield return Until(()=>File.Exists(PathCombine("host-death-checked"))&&File.Exists(PathCombine("client-death-checked")),"Both life resets verified");
  InputSystem.RemoveDevice(keys);
 }
 const Key KeyCodeF=Key.F;
 string PathCombine(string file)=>System.IO.Path.Combine(folder,file);
}}
#endif
