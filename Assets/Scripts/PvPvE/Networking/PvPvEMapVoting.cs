using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DeadQuarantine.PvPvE.Networking
{
    public enum PvPvEVotePhase : byte { WaitingForTeams, Voting, VoteResult, MapSelected }
    public struct PvPvEBallot : INetworkSerializable, IEquatable<PvPvEBallot>
    {
        public ulong ClientId;
        public int MapIndex;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T:IReaderWriter {s.SerializeValue(ref ClientId);s.SerializeValue(ref MapIndex);}
        public bool Equals(PvPvEBallot other)=>ClientId==other.ClientId && MapIndex==other.MapIndex;
    }
    public static class PvPvEVoteRules
    {
        public static int SelectWinner(int[] counts,Func<int,int> randomIndex)
        {
            if(counts==null || counts.Length==0)throw new ArgumentException("No maps configured",nameof(counts));
            int maximum=0;var ties=new List<int>();
            for(int i=0;i<counts.Length;i++){if(counts[i]>maximum){maximum=counts[i];ties.Clear();}if(counts[i]==maximum)ties.Add(i);}
            return ties[randomIndex(ties.Count)];
        }
    }
    /// <summary>One server-owned vote per member; deadlines use NGO synchronized server time.</summary>
    public sealed class PvPvEMapVoting : NetworkBehaviour
    {
        [SerializeField] PvPvENetworkSettings settings;
        PvPvETeamRoster roster;
        bool membershipDirty;
        public readonly NetworkVariable<PvPvEVotePhase> Phase=new(PvPvEVotePhase.WaitingForTeams);
        public readonly NetworkVariable<double> Deadline=new(0);
        public readonly NetworkVariable<int> Winner=new(-1);
        public readonly NetworkList<PvPvEBallot> Ballots=new();
        public event Action Changed;
        public event Action<ConfiguracionPartidaSeleccionada.Escenario> SelectionReady;
        public double Remaining=>IsSpawned?Math.Max(0,Deadline.Value-NetworkManager.ServerTime.Time):0;
        public PvPvEMapOption SelectedMap=>Winner.Value>=0 && Winner.Value<settings.Maps.Length?settings.Maps[Winner.Value]:null;
        public int VoteOf(ulong client){foreach(var b in Ballots)if(b.ClientId==client)return b.MapIndex;return -1;}
        public int Count(int index){int count=0;foreach(var b in Ballots)if(b.MapIndex==index)count++;return count;}
        public override void OnNetworkSpawn()
        {
            roster=GetComponent<PvPvETeamRoster>();
            if(IsServer)settings=roster.Configuration;
            Phase.OnValueChanged+=PhaseChanged;Deadline.OnValueChanged+=DeadlineChanged;Winner.OnValueChanged+=WinnerChanged;Ballots.OnListChanged+=BallotChanged;
            if(IsServer){roster.Changed+=MembershipChanged;MembershipChanged();}
            Changed?.Invoke();
        }
        void PhaseChanged(PvPvEVotePhase oldValue,PvPvEVotePhase value)=>Changed?.Invoke();
        void DeadlineChanged(double oldValue,double value)=>Changed?.Invoke();
        void WinnerChanged(int oldValue,int value)=>Changed?.Invoke();
        void BallotChanged(NetworkListEvent<PvPvEBallot> value)=>Changed?.Invoke();
        void MembershipChanged()=>membershipDirty=true;
        void ReconcileMembership()
        {
            if(!IsServer)return;
            for(int i=Ballots.Count-1;i>=0;i--)if(!roster.Contains(Ballots[i].ClientId))Ballots.RemoveAt(i);
            var flow=GetComponent<PvPvEPreparationFlow>();if(flow&&flow.Stage.Value!=PvPvEPrepareStage.Lobby)return;
            if(roster.Closing.Value){ResetVote();return;}
            if(!roster.CanStart){ResetVote();return;}
            if(Phase.Value==PvPvEVotePhase.WaitingForTeams && settings.HasValidMaps)BeginVote();
        }
        void ResetVote(){if(Ballots.Count>0)Ballots.Clear();Deadline.Value=0;Winner.Value=-1;Phase.Value=PvPvEVotePhase.WaitingForTeams;}
        void BeginVote(){Ballots.Clear();Winner.Value=-1;Deadline.Value=NetworkManager.ServerTime.Time+Math.Max(1,settings.MapVoteSeconds);Phase.Value=PvPvEVotePhase.Voting;}
        [ServerRpc(RequireOwnership=false)]
        public void CastVoteServerRpc(int mapIndex,ServerRpcParams rpc=default)
        {
            ulong client=rpc.Receive.SenderClientId;
            if(roster.Closing.Value || Phase.Value!=PvPvEVotePhase.Voting || NetworkManager.ServerTime.Time>=Deadline.Value || !roster.Contains(client) || mapIndex<0 || mapIndex>=settings.Maps.Length)return;
            for(int i=0;i<Ballots.Count;i++)if(Ballots[i].ClientId==client){if(Ballots[i].MapIndex!=mapIndex)Ballots[i]=new PvPvEBallot{ClientId=client,MapIndex=mapIndex};return;}
            Ballots.Add(new PvPvEBallot{ClientId=client,MapIndex=mapIndex});
        }
        public void BeginNextLobbyCycle()
        {
            if(!IsServer || roster.Closing.Value || Phase.Value!=PvPvEVotePhase.MapSelected)return;
            ResetVote();if(roster.CanStart && settings.HasValidMaps)BeginVote();
        }
        void Update()
        {
            if(!IsSpawned || !IsServer)return;
            if(membershipDirty){membershipDirty=false;ReconcileMembership();}
            if(roster.Closing.Value || !roster.CanStart)return;
            double now=NetworkManager.ServerTime.Time;
            if(Phase.Value==PvPvEVotePhase.Voting && now>=Deadline.Value){
                var counts=new int[settings.Maps.Length];foreach(var b in Ballots)counts[b.MapIndex]++;
                Winner.Value=PvPvEVoteRules.SelectWinner(counts,n=>UnityEngine.Random.Range(0,n));
                Deadline.Value=now+Math.Max(1,settings.PostVoteSeconds);Phase.Value=PvPvEVotePhase.VoteResult;
            }else if(Phase.Value==PvPvEVotePhase.VoteResult && now>=Deadline.Value){
                Deadline.Value=0;Phase.Value=PvPvEVotePhase.MapSelected;
                // The map-loading phase consumes this result; no premature scene transition here.
                SelectionReady?.Invoke(SelectedMap.Id);
            }
        }
        public override void OnNetworkDespawn()
        {
            if(roster && IsServer)roster.Changed-=MembershipChanged;
            Phase.OnValueChanged-=PhaseChanged;Deadline.OnValueChanged-=DeadlineChanged;Winner.OnValueChanged-=WinnerChanged;Ballots.OnListChanged-=BallotChanged;
            base.OnNetworkDespawn();
        }
    }
}
