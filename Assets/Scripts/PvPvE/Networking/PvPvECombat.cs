using System;
using System.Collections.Generic;
using System.Linq;
using DeadQuarantine.PvPvE.UI;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DeadQuarantine.PvPvE.Networking {
 public enum PvPvEDamageKind:byte {Bullet,Melee,Explosion,Fire,Environment,Zombie}
 public struct PvPvECombatStats:INetworkSerializable,IEquatable<PvPvECombatStats>{
  public ulong ClientId;public PvPvETeam Team;public double Points;public int Kills,Deaths,Assists;public double StunnedUntil;
  public void NetworkSerialize<T>(BufferSerializer<T> s) where T:IReaderWriter{s.SerializeValue(ref ClientId);s.SerializeValue(ref Team);s.SerializeValue(ref Points);s.SerializeValue(ref Kills);s.SerializeValue(ref Deaths);s.SerializeValue(ref Assists);s.SerializeValue(ref StunnedUntil);}
  public bool Equals(PvPvECombatStats o)=>ClientId==o.ClientId&&Team==o.Team&&Points==o.Points&&Kills==o.Kills&&Deaths==o.Deaths&&Assists==o.Assists&&StunnedUntil==o.StunnedUntil;
 }
 public struct PvPvEAmmo:INetworkSerializable,IEquatable<PvPvEAmmo>{
  public ulong ClientId;public uint Life;public int Weapon,Magazine,Reserve;public bool Reloading;
  public void NetworkSerialize<T>(BufferSerializer<T> s) where T:IReaderWriter{s.SerializeValue(ref ClientId);s.SerializeValue(ref Life);s.SerializeValue(ref Weapon);s.SerializeValue(ref Magazine);s.SerializeValue(ref Reserve);s.SerializeValue(ref Reloading);}
  public bool Equals(PvPvEAmmo o)=>ClientId==o.ClientId&&Life==o.Life&&Weapon==o.Weapon&&Magazine==o.Magazine&&Reserve==o.Reserve&&Reloading==o.Reloading;
 }
 public readonly struct PvPvEDamageRecord {
  public readonly ulong Source,Victim,ObjectId;public readonly int Amount;public readonly PvPvEDamageKind Kind;public readonly bool Headshot;public readonly float Distance;public readonly string Weapon;
  public PvPvEDamageRecord(ulong source,PlayerHealth target,int amount,PvPvEDamageKind kind,bool head,float distance,string weapon){Source=source;Victim=target.OwnerClientId;ObjectId=target.NetworkObjectId;Amount=amount;Kind=kind;Headshot=head;Distance=distance;Weapon=weapon;}
 }
 // Authority for PvP hits and individual/team score; existing weapons still drive firing/animation/audio.
 public sealed partial class PvPvECombat:NetworkBehaviour {
  public static PvPvECombat Active{get;private set;}
  public readonly NetworkList<PvPvECombatStats> Stats=new();public readonly NetworkList<PvPvEAmmo> Ammunition=new();
  public readonly NetworkVariable<int> AlphaKills=new(),BravoKills=new();
  public readonly NetworkVariable<bool> ScoreLocked=new();public readonly NetworkVariable<PvPvETeam> Winner=new();
  public event Action<PvPvEDamageRecord> ValidatedDamageServer,DamageAppliedServer,DeathResolvedServer;
  public event Action<double,double,double,bool> LocalElimination;
  public event Action<int> LocalAssist;
  public int AcceptedShots{get;private set;}public int RejectedShots{get;private set;}
  public string LastShotDiagnostic{get;private set;}
  [System.Diagnostics.Conditional("UNITY_EDITOR"),System.Diagnostics.Conditional("DEVELOPMENT_BUILD")] void DebugShot(string message){LastShotDiagnostic=message;}

  PvPvETeamRoster roster;PvPvETeamDeployment deployment;PvPvEPreparationFlow flow;PvPvEKillFeedback feedback;
  sealed class Life {public uint Revision,LastShot;public ulong ObjectId;public int Equipped=-1,Burst;public double NextShot,NextMelee;public readonly Dictionary<int,Reload> Reloads=new();}
  sealed class Reload {public double Next;public bool Active,Filled;}
  readonly Dictionary<ulong,Life> lives=new();readonly HashSet<(ulong,uint)> dead=new();
  PvPvEZombieDirector zombies;
  PvPvEDamageRecord? applying;
  (ZombieHealth Target,ulong Source,bool Head,float Distance,string Weapon)? zombieShot;
  readonly ContributionLedger contributions=new();
  bool applyingDamage;double nextContributionPrune;
  public int ContributionCount=>IsServer?contributions.EntryCount:0;
  public PvPvENetworkSettings Settings=>roster.Configuration;
  double Now=>NetworkManager.ServerTime.Time;
  public static bool LocalBlocked=>Active&&Active.IsClient&&(Active.ScoreLocked.Value||Active.TryStats(Active.NetworkManager.LocalClientId,out var row)&&row.StunnedUntil>Active.Now);
  public static bool LocalOrServerBlocked(PlayerHealth health)=>Active&&health&&Active.NetworkManager==health.NetworkManager&&(Active.ScoreLocked.Value||Active.TryStats(health.OwnerClientId,out var row)&&row.StunnedUntil>Active.Now);
  public bool TryStats(ulong id,out PvPvECombatStats row){foreach(var s in Stats)if(s.ClientId==id){row=s;return true;}row=default;return false;}
  int StatIndex(ulong id){for(int i=0;i<Stats.Count;i++)if(Stats[i].ClientId==id)return i;return -1;}
  int AmmoIndex(ulong id,int weapon){for(int i=0;i<Ammunition.Count;i++)if(Ammunition[i].ClientId==id&&Ammunition[i].Weapon==weapon)return i;return -1;}
  public bool TryAmmo(ulong id,int weapon,out PvPvEAmmo row){int i=AmmoIndex(id,weapon);row=i>=0?Ammunition[i]:default;return i>=0;}
  public bool IsPlaying=>IsSpawned&&flow&&flow.Stage.Value==PvPvEPrepareStage.Playing&&!ScoreLocked.Value&&!roster.Closing.Value;
  public bool Alive(PlayerHealth health)=>health&&health.IsSpawned&&health.IsAlive&&deployment.TryGet(health.OwnerClientId,out var row)&&row.ObjectId==health.NetworkObjectId&&row.Life==PvPvELifeStage.Alive&&row.Acknowledged;
  public override void OnNetworkSpawn(){Active=this;roster=GetComponent<PvPvETeamRoster>();deployment=GetComponent<PvPvETeamDeployment>();flow=GetComponent<PvPvEPreparationFlow>();zombies=gameObject.AddComponent<PvPvEZombieDirector>();InitializeRadar();InitializePresentation();if(IsClient)feedback=gameObject.AddComponent<PvPvEKillFeedback>();}
  void Update(){
   if(!IsSpawned)return;
   foreach(var health in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))if(health.IsSpawned&&PvPvEPreparationFlow.OwnsStagingPlayer(health.NetworkObject)&&!health.GetComponent<PvPvEHurtbox>())health.gameObject.AddComponent<PvPvEHurtbox>();
   if(!IsServer)return;
   TickMatchServer();UpdateRadarServer();UpdatePresentationServer();
   if(!IsPlaying)contributions.Clear();
   else if(Now>=nextContributionPrune){nextContributionPrune=Now+.5;contributions.Prune(Now,(id,revision)=>roster.Contains(id)&&deployment.TryGet(id,out var entry)&&entry.Revision==revision&&entry.Life!=PvPvELifeStage.Dead);}
   foreach(var member in roster.Members){int i=StatIndex(member.ClientId);if(i<0)Stats.Add(new PvPvECombatStats{ClientId=member.ClientId,Team=member.Team});else if(Stats[i].Team!=member.Team){var row=Stats[i];row.Team=member.Team;Stats[i]=row;}
    var health=Health(member.ClientId);if(!health)continue;EnsureLife(health);
    if(IsPlaying&&!health.IsAlive)RecordDeathServer(health);
    if(lives.TryGetValue(member.ClientId,out var life))foreach(var reload in life.Reloads.ToArray())TickReload(health,reload.Key,reload.Value);
   }
   for(int i=Stats.Count-1;i>=0;i--)if(!roster.Contains(Stats[i].ClientId)){ulong id=Stats[i].ClientId;Stats.RemoveAt(i);lives.Remove(id);contributions.ForgetPlayer(id);for(int j=Ammunition.Count-1;j>=0;j--)if(Ammunition[j].ClientId==id)Ammunition.RemoveAt(j);}
  }
  PlayerHealth Health(ulong id)=>NetworkManager.ConnectedClients.TryGetValue(id,out var client)&&client.PlayerObject?client.PlayerObject.GetComponent<PlayerHealth>():null;
  void EnsureLife(PlayerHealth health){
   if(!deployment.TryGet(health.OwnerClientId,out var row)||row.ObjectId!=health.NetworkObjectId)return;
   if(lives.TryGetValue(health.OwnerClientId,out var old)&&old.Revision==row.Revision&&old.ObjectId==row.ObjectId)return;
   var life=new Life{Revision=row.Revision,ObjectId=row.ObjectId};lives[health.OwnerClientId]=life;
   for(int i=Ammunition.Count-1;i>=0;i--)if(Ammunition[i].ClientId==health.OwnerClientId)Ammunition.RemoveAt(i);
   int stats=StatIndex(health.OwnerClientId);if(stats>=0){var s=Stats[stats];s.StunnedUntil=0;Stats[stats]=s;}
   var presentation=health.GetComponent<NetworkWeaponPresentation>();
   for(int i=0;i<presentation.Weapons.Length;i++)if(health.ArmaPermitidaPorClase(presentation.Weapons[i].Id)&&health.EquipoDeClase.Value.Active){var spec=Settings.Loadouts.Find(presentation.Weapons[i].Id);if(spec!=null)Ammunition.Add(new PvPvEAmmo{ClientId=health.OwnerClientId,Life=row.Revision,Weapon=i,Magazine=spec.magazine,Reserve=presentation.Weapons[i].Source.UsesLimitedReserve?presentation.Weapons[i].Source.ReserveCapacity:int.MaxValue});}
  }
  public bool ObserveStateServer(PlayerHealth health,int weapon,byte flags){
   if(!IsServer||!health||!health.IsSpawned||!health.ArmaPermitidaPorClase(health.GetComponent<NetworkWeaponPresentation>().Weapons[weapon].Id))return false;
   EnsureLife(health);if(!lives.TryGetValue(health.OwnerClientId,out var life))return true;
   bool switched=life.Equipped!=weapon;if(switched){foreach(var r in life.Reloads.Values)r.Active=false;for(int i=0;i<Ammunition.Count;i++)if(Ammunition[i].ClientId==health.OwnerClientId){var oldAmmo=Ammunition[i];oldAmmo.Reloading=false;Ammunition[i]=oldAmmo;}life.Equipped=weapon;life.Burst=0;}
   if(!IsPlaying||!Alive(health))return true;
   if(!life.Reloads.TryGetValue(weapon,out var reload))life.Reloads[weapon]=reload=new Reload();
   var spec=Spec(health,weapon);if(spec==null)return false;
   bool reloading=(flags&1)!=0;
   if(reloading&&!reload.Active){reload.Active=true;reload.Filled=false;float speed=Mathf.Max(.01f,health.GetComponent<PlayerCharacter>().ReloadSpeedMultiplier);int ammoIndex=AmmoIndex(health.OwnerClientId,weapon);if(ammoIndex<0)return false;var ammo=Ammunition[ammoIndex];float fill=spec.shellReload?spec.shellOpenSeconds:ammo.Magazine==0?spec.emptyReloadFillSeconds:spec.reloadFillSeconds;if(fill<=0)fill=ammo.Magazine==0?spec.emptyReloadSeconds:spec.reloadSeconds;reload.Next=Now+Math.Max(.1,fill/speed-Mathf.Clamp(Settings.ReloadTimingGrace,0,.25f));}
   TickReload(health,weapon,reload);
   if(!reloading)reload.Active=false;
   int index=AmmoIndex(health.OwnerClientId,weapon);if(index>=0){var ammo=Ammunition[index];ammo.Reloading=reload.Active;Ammunition[index]=ammo;}
   return true;
  }
  PvPvELoadoutCatalog.Gun Spec(PlayerHealth health,int weapon){var p=health.GetComponent<NetworkWeaponPresentation>();return weapon>=0&&weapon<p.Weapons.Length?Settings.Loadouts.Find(p.Weapons[weapon].Id):null;}
  void TickReload(PlayerHealth health,int weapon,Reload reload){
   if(!reload.Active||!IsPlaying||!Alive(health)||Now+.03<reload.Next)return;int index=AmmoIndex(health.OwnerClientId,weapon);if(index<0)return;
   var spec=Spec(health,weapon);var ammo=Ammunition[index];if(spec==null||ammo.Reserve<=0||ammo.Magazine>=spec.magazine)return;
   if(!spec.shellReload&&reload.Filled)return;
   int count=Math.Min(ammo.Reserve,spec.shellReload?1:spec.magazine-ammo.Magazine);ammo.Magazine+=count;if(ammo.Reserve!=int.MaxValue)ammo.Reserve-=count;ammo.Reloading=true;Ammunition[index]=ammo;reload.Filled=true;
   reload.Next=Now+Mathf.Max(.1f,spec.shellSeconds)/Mathf.Max(.01f,health.GetComponent<PlayerCharacter>().ReloadSpeedMultiplier);
  }
  public bool ResolveShotServer(PlayerHealth shooter,int weapon,uint sequence,Vector3 origin,Vector3[] targets,out Vector3[] resolved){
   resolved=targets;
   if(!IsServer||!IsPlaying||!Alive(shooter)||LocalOrServerBlocked(shooter)){RejectedShots++;return false;}
   EnsureLife(shooter);var presentation=shooter.GetComponent<NetworkWeaponPresentation>();var spec=Spec(shooter,weapon);
   if(spec==null||!lives.TryGetValue(shooter.OwnerClientId,out var life)||!shooter.ArmaPermitidaPorClase(spec.id)||!TryAmmo(shooter.OwnerClientId,weapon,out var ammo)||targets==null||targets.Length!=Mathf.Clamp(spec.pellets,1,16)||sequence<=life.LastShot||Now+.015<life.NextShot||ammo.Magazine<1||ammo.Reloading||!ValidOrigin(shooter,origin)){TryAmmo(shooter.OwnerClientId,weapon,out var rejectedAmmo);DebugShot("rejected: actor="+shooter.OwnerClientId+" seq="+sequence+" ammo="+rejectedAmmo.Magazine+" reloading="+rejectedAmmo.Reloading+" origin="+ValidOrigin(shooter,origin)+" time="+Now);RejectedShots++;return false;}
   if(life.Equipped>=0&&life.Equipped!=weapon){RejectedShots++;return false;}
   float rangeMultiplier=AttachmentRange(shooter,weapon);float effectiveRange=spec.range*rangeMultiplier;
   Vector3 average=Vector3.zero;foreach(var target in targets){var delta=target-origin;if(!Finite(target)||delta.sqrMagnitude<.001f||delta.magnitude>effectiveRange+5){RejectedShots++;return false;}average+=delta.normalized;}
   if(average.sqrMagnitude<.1f){RejectedShots++;return false;}
   foreach(var target in targets)if(Vector3.Angle(average,target-origin)>spec.maxSpread+5){RejectedShots++;return false;}
   life.LastShot=sequence;life.Equipped=weapon;life.NextShot=Now+Mathf.Max(.025f,spec.fireInterval);
   if(spec.burstCount>1&&++life.Burst>=spec.burstCount){life.Burst=0;life.NextShot=Now+Mathf.Max(spec.fireInterval,spec.burstCooldown);}
   ammo.Magazine--;Ammunition[AmmoIndex(shooter.OwnerClientId,weapon)]=ammo;deployment.CancelProtectionServer(shooter.OwnerClientId);AcceptedShots++;
   resolved=new Vector3[targets.Length];
   float multiplier=shooter.GetComponent<NetworkWeaponAttachments>()?.catalog is { } catalog?AttachmentDamage(shooter,weapon,catalog):1;
   for(int i=0;i<targets.Length;i++){
    Vector3 direction=(targets[i]-origin).normalized;resolved[i]=origin+direction*effectiveRange;
    if(!Trace(shooter,origin,direction,effectiveRange,out var hit,out var victim)){DebugShot("accepted miss: actor="+shooter.OwnerClientId+" origin="+origin+" target="+targets[i]+" direction="+direction);continue;}DebugShot("accepted collision: actor="+shooter.OwnerClientId+" collider="+hit.collider.name+" target="+(victim?victim.OwnerClientId.ToString():"world")+" origin="+origin+" point="+hit.point);resolved[i]=hit.point;
    if(!victim){ApplyZombieShot(shooter,hit,origin,direction,spec,multiplier,rangeMultiplier);continue;}var hurtbox=victim.GetComponent<PvPvEHurtbox>();bool head=hurtbox&&hit.point.y>hurtbox.HeadThreshold;
    float falloff=Mathf.Lerp(1,spec.minDamageMultiplier,Mathf.InverseLerp(spec.falloffStart*rangeMultiplier,Mathf.Max(spec.falloffStart+.01f,spec.falloffEnd)*rangeMultiplier,hit.distance));
    int damage=Mathf.Max(1,Mathf.RoundToInt(spec.damage/(float)Mathf.Max(1,spec.pellets)*multiplier*falloff*(head?Settings.HeadshotMultiplier:1)));
    ApplyDamageServer(shooter.OwnerClientId,victim,damage,PvPvEDamageKind.Bullet,head,hit.distance,spec.id);
   }
   return true;
  }
  // Same head/limb priority as Weapon's existing zombie hit resolution, using server physics.
  void ApplyZombieShot(PlayerHealth shooter,RaycastHit impact,Vector3 origin,Vector3 direction,PvPvELoadoutCatalog.Gun spec,float multiplier,float rangeMultiplier){
   var zombie=impact.collider.GetComponentInParent<ZombieHealth>();if(!zombie||!zombies.Owns(zombie))return;
   bool head=false;int variant=-1;float closest=float.PositiveInfinity;
   foreach(var hit in Physics.RaycastAll(origin,direction,spec.range*rangeMultiplier,~0,QueryTriggerInteraction.Collide)){
    var headbox=hit.collider.GetComponent<ZombieHeadHitbox>();if(headbox&&headbox.Owner==zombie)head=true;
    var limb=hit.collider.GetComponent<ZombieLimbHitbox>();if(limb&&limb.Owner==zombie&&hit.distance<=impact.distance+.45f&&hit.distance<closest){closest=hit.distance;variant=limb.HitVariant;}
   }
   float falloff=Mathf.Lerp(1,spec.minDamageMultiplier,Mathf.InverseLerp(spec.falloffStart*rangeMultiplier,Mathf.Max(spec.falloffStart+.01f,spec.falloffEnd)*rangeMultiplier,impact.distance));
   int amount=Mathf.Max(1,Mathf.RoundToInt(spec.damage/(float)Mathf.Max(1,spec.pellets)*multiplier*falloff));
   var previous=zombieShot;zombieShot=(zombie,shooter.OwnerClientId,head,impact.distance,spec.id);try{zombie.RecibirDisparoPvPvEServidor(amount,impact.point,impact.normal,shooter.OwnerClientId,head,variant);}finally{zombieShot=previous;}
  }
  float AttachmentRange(PlayerHealth health,int index){var source=health.GetComponent<NetworkWeaponAttachments>();return source&&source.catalog?source.catalog.Modifier(source.GetChoice(index),e=>e.range):1;}
  float AttachmentDamage(PlayerHealth health,int index,WeaponAttachmentCatalog catalog){float value=1;var choice=health.GetComponent<NetworkWeaponAttachments>().GetChoice(index);foreach(WeaponAttachmentKind kind in Enum.GetValues(typeof(WeaponAttachmentKind))){var e=catalog.Find(choice.Get(kind));if(e!=null&&!e.requiresBracing)value*=e.damage;}return value;}
  bool ValidOrigin(PlayerHealth shooter,Vector3 origin){
   if(!Finite(origin))return false;Vector3 eye=shooter.transform.position+Vector3.up*1.5f;var presentation=shooter.GetComponent<NetworkWeaponPresentation>();if(presentation.Weapons.Length>0)eye=presentation.Weapons[0].Source.AimOrigin;
   if(Vector3.Distance(eye,origin)>Settings.ShotOriginTolerance)return false;
   return !Physics.Linecast(eye,origin,~0,QueryTriggerInteraction.Ignore);
  }
  static bool Finite(Vector3 value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z);
  bool Trace(PlayerHealth shooter,Vector3 origin,Vector3 direction,float range,out RaycastHit impact,out PlayerHealth target){
   impact=default;target=null;
   foreach(var hit in Physics.RaycastAll(origin,direction,range,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance)){
    var player=hit.collider.GetComponentInParent<PlayerHealth>();if(player==shooter)continue;
    if(player){var hurtbox=player.GetComponent<PvPvEHurtbox>();if(!Alive(player)||hurtbox&&!hurtbox.Contains(hit.collider))continue;impact=hit;target=player;return true;}
    var zombie=hit.collider.GetComponentInParent<ZombieHealth>();if(zombie){if(zombie.IsDead)continue;if(!hit.collider.isTrigger||hit.collider.GetComponent<ZombieHeadHitbox>()||hit.collider.GetComponent<ZombieLimbHitbox>()){impact=hit;return true;}continue;}
    if(hit.collider.isTrigger||GetComponent<PvPvEDeathPresentation>()&&GetComponent<PvPvEDeathPresentation>().OwnsCollider(hit.collider))continue;
    impact=hit;return true;
   }
   return false;
  }
  public bool CanDamage(ulong source,PlayerHealth victim,bool utility=false){
   if(IsServer)TickMatchServer();
   if(!IsServer||!IsPlaying||!Alive(victim)||PvPvETeamDeployment.BlocksDamage(victim)||!roster.TryGetMember(source,out var from)||!roster.TryGetMember(victim.OwnerClientId,out var to))return false;
   if(source==victim.OwnerClientId)return !utility;
   return Settings.FriendlyFire||from.Team!=to.Team;
  }
  public bool ApplyDamageServer(ulong source,PlayerHealth victim,int amount,PvPvEDamageKind kind,bool head,float distance,string weapon){
   if(amount<=0||!CanDamage(source,victim))return false;
   var record=new PvPvEDamageRecord(source,victim,amount,kind,head,distance,weapon);var previous=applying;bool previousApplying=applyingDamage;applying=record;applyingDamage=true;
   long before=(long)victim.CurrentHealth.Value+victim.Armor.Value;
   try{
    ValidatedDamageServer?.Invoke(record);victim.TakeDamage(amount);
    int actual=(int)Math.Clamp(before-victim.CurrentHealth.Value-victim.Armor.Value,0,int.MaxValue);
    if(actual>0){var dealt=new PvPvEDamageRecord(source,victim,actual,kind,head,distance,weapon);applying=dealt;
     if(source!=victim.OwnerClientId&&roster.TryGetMember(source,out var from)&&roster.TryGetMember(victim.OwnerClientId,out var to)&&from.Team!=to.Team&&deployment.TryGet(victim.OwnerClientId,out var life))contributions.Record(victim.OwnerClientId,life.Revision,source,ContributionType.Damage,actual,Now,Settings.DamageAssistWindow);
     DamageAppliedServer?.Invoke(dealt);
     if(kind==PvPvEDamageKind.Bullet&&source!=victim.OwnerClientId&&roster.Contains(source))HitConfirmedClientRpc(!victim.IsAlive,head,new ClientRpcParams{Send=new ClientRpcSendParams{TargetClientIds=new[]{source}}});
    }
    applyingDamage=false;if(!victim.IsAlive)RecordDeathServer(victim);
   }finally{applying=previous;applyingDamage=previousApplying;}
   return true;
  }
  public void RecordDeathServer(PlayerHealth health){
   if(IsServer)TickMatchServer();
   if(applyingDamage&&applying.HasValue&&health&&applying.Value.ObjectId==health.NetworkObjectId)return;
   if(!IsServer||!IsPlaying||!health||health.IsAlive||!deployment.TryGet(health.OwnerClientId,out var life)||life.ObjectId!=health.NetworkObjectId||!dead.Add((health.OwnerClientId,life.Revision)))return;
   int victimIndex=StatIndex(health.OwnerClientId);if(victimIndex<0)return;var victim=Stats[victimIndex];victim.Deaths++;victim.StunnedUntil=0;Stats[victimIndex]=victim;
   var record=applying.HasValue&&applying.Value.ObjectId==health.NetworkObjectId?applying.Value:new PvPvEDamageRecord(ulong.MaxValue,health,0,PvPvEDamageKind.Environment,false,0,"");
   if(record.Source!=health.OwnerClientId&&roster.TryGetMember(record.Source,out var killer)&&killer.Team!=victim.Team){
    bool longshot=record.Kind==PvPvEDamageKind.Bullet&&record.Distance>=Settings.LongshotThreshold(Settings.Loadouts.Find(record.Weapon));var award=Settings.EliminationAward(false,record.Headshot,longshot);
    int killerIndex=StatIndex(record.Source);if(killerIndex>=0){var row=Stats[killerIndex];row.Kills++;row.Points+=award.Total;Stats[killerIndex]=row;}
    if(killer.Team==PvPvETeam.Alpha)AlphaKills.Value++;else BravoKills.Value++;
    AwardClientRpc(award.Base,award.Head,award.Long,false,new ClientRpcParams{Send=new ClientRpcSendParams{TargetClientIds=new[]{record.Source}}});
    foreach(ulong assister in contributions.Consume(health.OwnerClientId,life.Revision,record.Source,victim.Team,Now,Settings.MinimumAssistDamage,TeamOf)){
     int index=StatIndex(assister);if(index<0)continue;var row=Stats[index];row.Assists++;row.Points+=Mathf.Max(0,Settings.AssistScore);Stats[index]=row;AssistClientRpc(Mathf.Max(0,Settings.AssistScore),new ClientRpcParams{Send=new ClientRpcSendParams{TargetClientIds=new[]{assister}}});
    }
    if(Resolution.Value==PvPvEMatchResolution.Overtime||AlphaKills.Value>=Settings.KillLimit||BravoKills.Value>=Settings.KillLimit)FinishMatchServer(killer.Team,Resolution.Value==PvPvEMatchResolution.Overtime?"Muerte súbita.":"Objetivo alcanzado.");
   }
   contributions.Forget(health.OwnerClientId,life.Revision);
   DeathResolvedServer?.Invoke(record);
  }
  public void RecordZombieKillServer(ulong attacker,ZombieHealth zombie=null){
   if(IsServer)TickMatchServer();
   if(!IsServer||!IsPlaying||!roster.Contains(attacker))return;int index=StatIndex(attacker);if(index<0)return;
   bool contextual=zombieShot.HasValue&&zombieShot.Value.Target==zombie&&zombieShot.Value.Source==attacker;
   bool head=contextual&&zombieShot.Value.Head;bool far=contextual&&zombieShot.Value.Distance>=Settings.LongshotThreshold(Settings.Loadouts.Find(zombieShot.Value.Weapon));
   var award=Settings.EliminationAward(true,head,far);var row=Stats[index];row.Points+=award.Total;Stats[index]=row;
   AwardClientRpc(award.Base,award.Head,award.Long,true,new ClientRpcParams{Send=new ClientRpcSendParams{TargetClientIds=new[]{attacker}}});
  }
  public void ApplyZombieDamageServer(ZombieAI attacker,PlayerHealth victim,int amount){
   if(IsServer)TickMatchServer();
   if(!IsServer||!IsPlaying||!attacker||!zombies.Owns(attacker.GetComponent<ZombieHealth>())||!Alive(victim)||PvPvETeamDeployment.BlocksDamage(victim)||amount<=0||PvPvEZombieDirector.Blocked(attacker.transform.position+Vector3.up*.8f,victim.transform.position+Vector3.up))return;
   var previous=applying;applying=new PvPvEDamageRecord(ulong.MaxValue,victim,amount,PvPvEDamageKind.Zombie,false,0,"Zombie");try{victim.TakeDamage(amount);}finally{applying=previous;}
  }
  PvPvETeam TeamOf(ulong id)=>roster.TryGetMember(id,out var member)?member.Team:PvPvETeam.None;
  // Trusted server integration point, adapted from the former ScoreService.Utility.
  // Call only after a real effect has been applied; never exposed as a client RPC.
  public bool RecordAppliedUtilityServer(ulong source,PlayerHealth victim,ContributionType type,float duration,float intensity){
   if(!IsServer||!IsPlaying||!victim||!Alive(victim)||!float.IsFinite(duration)||!float.IsFinite(intensity)||duration<=0||intensity<=0||type==ContributionType.Damage||!Enum.IsDefined(typeof(ContributionType),type)||!CanDamage(source,victim,true)||TeamOf(source)==TeamOf(victim.OwnerClientId)||!deployment.TryGet(victim.OwnerClientId,out var life))return false;
   double window=Math.Min(duration,Settings.ContributionWindow(type));if(window<=0||!double.IsFinite(window))return false;
   contributions.Record(victim.OwnerClientId,life.Revision,source,type,0,Now,window,duration,Mathf.Clamp01(intensity));return true;
  }
  public int LocalConfirmedHits {get;private set;}
  [ClientRpc] void HitConfirmedClientRpc(bool killed,bool head,ClientRpcParams target=default){LocalConfirmedHits++;var player=NetworkManager.LocalClient?.PlayerObject;if(player)player.GetComponentInChildren<HUDController>(true)?.ShowHitMarker(killed,head);}
  [ClientRpc] void AssistClientRpc(int points,ClientRpcParams target=default)=>LocalAssist?.Invoke(points);
  [ClientRpc] void AwardClientRpc(double basis,double head,double far,bool zombie,ClientRpcParams target=default)=>LocalElimination?.Invoke(basis,head,far,zombie);
  [ServerRpc(RequireOwnership=false)] public void MeleeServerRpc(ulong objectId,Vector3 origin,Vector3 direction,ServerRpcParams rpc=default){
   var health=Health(rpc.Receive.SenderClientId);if(!health||health.NetworkObjectId!=objectId||!IsPlaying||!Alive(health)||LocalOrServerBlocked(health)||!Finite(direction)||direction.sqrMagnitude<.8f||direction.sqrMagnitude>1.2f||!ValidOrigin(health,origin))return;
   EnsureLife(health);if(!lives.TryGetValue(health.OwnerClientId,out var life)||Now<life.NextMelee)return;var melee=health.GetComponentInChildren<MeleeAttack>(true);if(!melee)return;life.NextMelee=Now+Mathf.Max(.1f,melee.PvPvECooldown);deployment.CancelProtectionServer(health.OwnerClientId);
   if(Trace(health,origin,direction,melee.PvPvERange,out var hit,out var victim)){if(victim)ApplyDamageServer(health.OwnerClientId,victim,melee.PvPvEDamage,PvPvEDamageKind.Melee,false,hit.distance,"Cuchillo");else{var zombie=hit.collider.GetComponentInParent<ZombieHealth>();if(zombie&&zombies.Owns(zombie))zombie.RecibirDanoServidor(melee.PvPvEDamage,hit.point,hit.normal,health.OwnerClientId);}}
  }
  bool Visible(Vector3 center,PlayerHealth target){var point=target.transform.position+Vector3.up*.9f;foreach(var hit in Physics.RaycastAll(center,point-center,Vector3.Distance(center,point),~0,QueryTriggerInteraction.Ignore))if(!hit.collider.GetComponentInParent<PlayerHealth>()&&!(GetComponent<PvPvEDeathPresentation>()&&GetComponent<PvPvEDeathPresentation>().OwnsCollider(hit.collider))&&hit.distance>.1f)return false;return true;}
  public void ExplosionServer(ulong source,Vector3 center,float radius,int damage){if(!IsServer||!IsPlaying||radius<=0)return;foreach(var member in roster.Members){var target=Health(member.ClientId);if(!target)continue;float distance=Vector3.Distance(center,target.transform.position+Vector3.up*.5f);if(distance<=radius&&Visible(center,target))ApplyDamageServer(source,target,Mathf.RoundToInt(damage*(1-distance/radius)),PvPvEDamageKind.Explosion,false,distance,"Granada");}}
  public void StunAreaServer(ulong source,Vector3 center,float radius,float seconds){if(!IsServer||!IsPlaying)return;foreach(var member in roster.Members){var target=Health(member.ClientId);if(!target||!CanDamage(source,target,true)||Vector3.Distance(center,target.transform.position+Vector3.up*.5f)>radius||!Visible(center,target))continue;int index=StatIndex(member.ClientId);if(index<0)continue;var row=Stats[index];row.StunnedUntil=Math.Max(row.StunnedUntil,Now+Mathf.Clamp(seconds,0,30));Stats[index]=row;RecordAppliedUtilityServer(source,target,ContributionType.Stun,Mathf.Clamp(seconds,0,30),1);}}
  public void BurnAreaServer(ulong source,Vector3 center,float radius,float height,float seconds,int damage,float interval){if(!IsServer||!IsPlaying)return;foreach(var member in roster.Members){var target=Health(member.ClientId);if(!target||!CanDamage(source,target))continue;var delta=target.transform.position-center;if(Mathf.Abs(delta.y)<=height&&new Vector2(delta.x,delta.z).magnitude<=radius&&Visible(center,target))target.Quemar(seconds,damage,interval,source);}}
  public Vector3 LocalTrailTarget(Vector3 origin,Vector3 target){if(!flow||flow.Stage.Value!=PvPvEPrepareStage.Playing)return target;var local=NetworkManager.LocalClient?.PlayerObject;var owner=local?local.GetComponent<PlayerHealth>():null;if(owner&&Trace(owner,origin,(target-origin).normalized,Vector3.Distance(origin,target),out var hit,out _))return hit.point;return target;}
  public override void OnNetworkDespawn(){DisposePresentation();DisposeRadar();if(zombies){zombies.Clear();Destroy(zombies);}if(feedback)Destroy(feedback);if(Active==this)Active=null;contributions.Clear();ValidatedDamageServer=null;DamageAppliedServer=null;DeathResolvedServer=null;LocalElimination=null;LocalAssist=null;base.OnNetworkDespawn();}
 }
}
