using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
namespace DeadQuarantine.PvPvE.Networking {
 public struct PvPvEKillEntry:INetworkSerializable,IEquatable<PvPvEKillEntry>{
  public ulong Sequence,Killer,Victim;public FixedString64Bytes KillerName,VictimName,Weapon;
  public PvPvETeam KillerTeam,VictimTeam;public PvPvEDamageKind Kind;public bool Headshot;public double Expires;
  public void NetworkSerialize<T>(BufferSerializer<T> s)where T:IReaderWriter{s.SerializeValue(ref Sequence);s.SerializeValue(ref Killer);s.SerializeValue(ref Victim);s.SerializeValue(ref KillerName);s.SerializeValue(ref VictimName);s.SerializeValue(ref Weapon);s.SerializeValue(ref KillerTeam);s.SerializeValue(ref VictimTeam);s.SerializeValue(ref Kind);s.SerializeValue(ref Headshot);s.SerializeValue(ref Expires);}
  public bool Equals(PvPvEKillEntry e)=>Sequence==e.Sequence&&Killer==e.Killer&&Victim==e.Victim&&KillerName.Equals(e.KillerName)&&VictimName.Equals(e.VictimName)&&Weapon.Equals(e.Weapon)&&KillerTeam==e.KillerTeam&&VictimTeam==e.VictimTeam&&Kind==e.Kind&&Headshot==e.Headshot&&Expires==e.Expires;
 }
 public sealed partial class PvPvECombat {
  public readonly NetworkList<PvPvEKillEntry> KillFeed=new();
  public readonly NetworkVariable<double> MatchStartedAt=new(-1),MatchDeadline=new(-1),MatchStoppedAt=new(-1);
  PvPvEView matchView;ulong feedSequence;
  public double RemainingSeconds=>Resolution.Value==PvPvEMatchResolution.Overtime?0:MatchDeadline.Value<0?Settings.MatchDurationSeconds:Math.Max(0,MatchDeadline.Value-(MatchStoppedAt.Value>=0?MatchStoppedAt.Value:Now));
  void InitializePresentation(){if(IsServer)DeathResolvedServer+=PublishDeathFeed;if(IsClient){matchView=gameObject.AddComponent<PvPvEView>();matchView.Initialize(this);}}
  void UpdatePresentationServer(){

   for(int i=KillFeed.Count-1;i>=0;i--)if(KillFeed[i].Expires<=Now)KillFeed.RemoveAt(i);
  }
  static FixedString64Bytes FeedName(string value){var result=new FixedString64Bytes();var elements=System.Globalization.StringInfo.GetTextElementEnumerator(value??"");while(elements.MoveNext()){string element=elements.GetTextElement();if(char.IsControl(element[0]))continue;string next=result.ToString()+element;if(System.Text.Encoding.UTF8.GetByteCount(next)>60)break;result=new FixedString64Bytes(next);}return result;}
  // The existing death resolver deduplicates by life before this trusted server event.
  void PublishDeathFeed(PvPvEDamageRecord record){
   if(!IsServer||!IsSpawned||!roster.TryGetMember(record.Victim,out var victim))return;
   bool known=roster.TryGetMember(record.Source,out var killer);bool self=record.Source==record.Victim;
   string actor=self?"SUICIDIO":known?killer.Name.ToString():record.Kind==PvPvEDamageKind.Zombie?"ZOMBIE":"ENTORNO";
   string weapon=Settings.Loadouts.Find(record.Weapon)?.name??record.Weapon;
   if(string.IsNullOrEmpty(weapon))weapon=record.Kind==PvPvEDamageKind.Zombie?"ATAQUE":record.Kind==PvPvEDamageKind.Environment?"ENTORNO":record.Kind.ToString();
   var entry=new PvPvEKillEntry{Sequence=++feedSequence,Killer=record.Source,Victim=record.Victim,KillerName=FeedName(actor),VictimName=FeedName(victim.Name.ToString()),Weapon=FeedName(weapon),KillerTeam=known?killer.Team:PvPvETeam.None,VictimTeam=victim.Team,Kind=record.Kind,Headshot=record.Headshot,Expires=Now+Math.Max(1,Settings.KillFeedSeconds)};
   KillFeed.Add(entry);while(KillFeed.Count>Mathf.Clamp(Settings.KillFeedRows,1,10))KillFeed.RemoveAt(0);
  }
  void DisposePresentation(){if(IsServer)DeathResolvedServer-=PublishDeathFeed;if(matchView)Destroy(matchView);}
 }
}