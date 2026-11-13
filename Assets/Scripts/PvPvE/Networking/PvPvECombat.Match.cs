using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
namespace DeadQuarantine.PvPvE.Networking {
 public enum PvPvEMatchResolution:byte {Regulation,Overtime,Results,ReturningToLobby}
 public sealed partial class PvPvECombat {
  public readonly NetworkVariable<PvPvEMatchResolution> Resolution=new();
  public readonly NetworkVariable<double> ResultsDeadline=new();
  public readonly NetworkVariable<FixedString128Bytes> ResultReason=new();
  // Reused prior Finish/AddTeamKill/time-expiry rules; current Combat alone owns scoring.
  bool hostDeparturePending;
  public bool PrepareHostDepartureServer(){
   if(!IsServer||!IsPlaying||!roster.TryGetMember(NetworkManager.LocalClientId,out var host))return false;
   var opponent=host.Team==PvPvETeam.Alpha?PvPvETeam.Bravo:PvPvETeam.Alpha;
   FinishMatchServer(roster.Count(host.Team)==1&&roster.Count(opponent)>0?opponent:PvPvETeam.None,"El anfitrión abandonó la partida.");hostDeparturePending=true;return true;
  }
  void TickMatchServer(){
   if(!IsServer||!IsSpawned)return;
   if(Resolution.Value==PvPvEMatchResolution.Results){if(!hostDeparturePending&&Now>=ResultsDeadline.Value){Resolution.Value=PvPvEMatchResolution.ReturningToLobby;_ = flow.ReturnToLobbyServerAsync();}return;}
   if(!IsPlaying)return;
   if(MatchStartedAt.Value<0){MatchStartedAt.Value=Now;MatchDeadline.Value=Now+Math.Max(1,Settings.MatchDurationSeconds);}
   if(!roster.CanStart){ResolveEmptyTeamServer();return;}
   if(Resolution.Value==PvPvEMatchResolution.Regulation&&Now>=MatchDeadline.Value){
    if(AlphaKills.Value==BravoKills.Value)Resolution.Value=PvPvEMatchResolution.Overtime;
    else FinishMatchServer(AlphaKills.Value>BravoKills.Value?PvPvETeam.Alpha:PvPvETeam.Bravo,"Tiempo agotado.");
   }
  }
  public void ResolveEmptyTeamServer(){
   if(!IsServer||!IsPlaying||roster.CanStart)return;
   FinishMatchServer(roster.Count(PvPvETeam.Alpha)>0?PvPvETeam.Alpha:roster.Count(PvPvETeam.Bravo)>0?PvPvETeam.Bravo:PvPvETeam.None,"El equipo rival abandonó la partida.");
  }
  void FinishMatchServer(PvPvETeam winner,string reason){
   if(!IsServer||!IsPlaying)return;
   Winner.Value=winner;ResultReason.Value=new FixedString128Bytes(reason);MatchStoppedAt.Value=Now;
   ResultsDeadline.Value=Now+Math.Max(1,Settings.ResultsSeconds);ScoreLocked.Value=true;Resolution.Value=PvPvEMatchResolution.Results;zombies.Clear();contributions.Clear();
   if(winner!=PvPvETeam.None)AwardTeamVictoryServer(winner);
  }

  void AwardTeamVictoryServer(PvPvETeam winner)
  {
   var recipients = new List<ulong>();
   foreach (var member in roster.Members)
   {
    if (member.Team == winner && NetworkManager.ConnectedClients.ContainsKey(member.ClientId))
     recipients.Add(member.ClientId);
   }

   if (recipients.Count > 0)
   {
    TeamVictoryClientRpc(new ClientRpcParams
    {
     Send = new ClientRpcSendParams { TargetClientIds = recipients }
    });
   }
  }

  [ClientRpc]
  void TeamVictoryClientRpc(ClientRpcParams recipients = default)
  {
   var achievements = AchievementsManager.Instance;
   if (achievements != null &&
       !achievements.EstaDesbloqueado(AchievementId.VictoriaEnEquipo))
    achievements.Desbloquear(AchievementId.VictoriaEnEquipo);
  }
  public void ResetMatchServer(){
   if(!IsServer||flow.Stage.Value!=PvPvEPrepareStage.ReturningToLobby)return;
   hostDeparturePending=false;Stats.Clear();Ammunition.Clear();lives.Clear();dead.Clear();contributions.Clear();applying=null;applyingDamage=false;
   AlphaKills.Value=BravoKills.Value=0;Winner.Value=PvPvETeam.None;KillFeed.Clear();RadarReveals.Clear();feedSequence=0;
   MatchStartedAt.Value=MatchDeadline.Value=MatchStoppedAt.Value=-1;ResultsDeadline.Value=0;ResultReason.Value=default;Resolution.Value=PvPvEMatchResolution.Regulation;ScoreLocked.Value=false;
  }
 }
}
