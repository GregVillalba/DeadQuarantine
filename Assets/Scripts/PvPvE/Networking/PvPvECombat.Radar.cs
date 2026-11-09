using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
namespace DeadQuarantine.PvPvE.Networking {
 public struct PvPvERadarReveal:INetworkSerializable,IEquatable<PvPvERadarReveal>{
  public ulong ClientId,ObjectId;public uint Life;public double Until;
  public void NetworkSerialize<T>(BufferSerializer<T> s)where T:IReaderWriter{s.SerializeValue(ref ClientId);s.SerializeValue(ref ObjectId);s.SerializeValue(ref Life);s.SerializeValue(ref Until);}
  public bool Equals(PvPvERadarReveal r)=>ClientId==r.ClientId&&ObjectId==r.ObjectId&&Life==r.Life&&Until==r.Until;
 }
 public sealed partial class PvPvECombat {
  public readonly NetworkList<PvPvERadarReveal> RadarReveals=new();
  readonly Dictionary<ulong,NetworkWeaponAttachments> radarSources=new();
  PvPvEMinimap minimap;
  void InitializeRadar(){if(IsClient){minimap=gameObject.AddComponent<PvPvEMinimap>();minimap.Initialize(this);}}
  void UpdateRadarServer(){
   foreach(var key in radarSources.Keys.ToArray()){var source=radarSources[key];var current=Health(key);if(!roster.Contains(key)||!source||!current||source.gameObject!=current.gameObject){if(source)source.ShotVisibility-=RadarShot;radarSources.Remove(key);}}
   foreach(var member in roster.Members){if(radarSources.ContainsKey(member.ClientId))continue;var health=Health(member.ClientId);var source=health?health.GetComponent<NetworkWeaponAttachments>():null;if(source){source.ShotVisibility+=RadarShot;radarSources.Add(member.ClientId,source);}}
   for(int i=RadarReveals.Count-1;i>=0;i--){var row=RadarReveals[i];var health=Health(row.ClientId);if(!IsPlaying||!Alive(health)||row.ObjectId!=health.NetworkObjectId||!deployment.TryGet(row.ClientId,out var life)||life.Revision!=row.Life||row.Until<=Now)RadarReveals.RemoveAt(i);}
  }
  // Published only by the existing accepted-shot path. There is no reveal RPC for clients.
  void RadarShot(ulong sender,string weapon,bool audible){
   if(!IsServer||!IsPlaying||!audible||!radarSources.TryGetValue(sender,out var source))return;
   var health=Health(sender);if(!Alive(health)||source.gameObject!=health.gameObject||LocalOrServerBlocked(health))return;
   var presentation=health.GetComponent<NetworkWeaponPresentation>();int index=Array.FindIndex(presentation.Weapons,d=>d.Id==weapon);if(index<0)return;
   var choice=source.GetChoice(index);
   foreach(WeaponAttachmentKind kind in Enum.GetValues(typeof(WeaponAttachmentKind)))if(source.catalog.Find(choice.Get(kind))?.suppressed==true)return;
   if(!deployment.TryGet(sender,out var life))return;
   var reveal=new PvPvERadarReveal{ClientId=sender,ObjectId=health.NetworkObjectId,Life=life.Revision,Until=Now+Mathf.Max(.1f,Settings.EnemyShotRevealSeconds)};
   for(int i=0;i<RadarReveals.Count;i++)if(RadarReveals[i].ClientId==sender){RadarReveals[i]=reveal;return;}
   RadarReveals.Add(reveal);
  }
  public bool IsRadarRevealed(PlayerHealth target){
   if(!IsPlaying||!Alive(target)||!deployment.TryGet(target.OwnerClientId,out var life))return false;
   foreach(var r in RadarReveals)if(r.ClientId==target.OwnerClientId&&r.ObjectId==target.NetworkObjectId&&r.Life==life.Revision&&r.Until>Now)return true;
   return false;
  }
  void DisposeRadar(){foreach(var source in radarSources.Values)if(source)source.ShotVisibility-=RadarShot;radarSources.Clear();if(minimap)Destroy(minimap);}
 }
}