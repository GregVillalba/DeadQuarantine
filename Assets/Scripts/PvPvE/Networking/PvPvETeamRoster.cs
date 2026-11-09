using System;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DeadQuarantine.PvPvE.Networking
{
    public enum PvPvETeam : byte { None, Alpha, Bravo }
    public struct PvPvEMember : INetworkSerializable,IEquatable<PvPvEMember>
    {
        public ulong ClientId;
        public FixedString64Bytes Name;
        public PvPvETeam Team;
        public int CharacterIndex;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T:IReaderWriter {s.SerializeValue(ref ClientId);s.SerializeValue(ref Name);s.SerializeValue(ref Team);s.SerializeValue(ref CharacterIndex);}
        public bool Equals(PvPvEMember other)=>ClientId==other.ClientId && Name.Equals(other.Name) && Team==other.Team && CharacterIndex==other.CharacterIndex;
    }
    public static class PvPvETeamRules
    {
        public static PvPvETeam Choose(int alpha,int bravo,int capacity)
        {
            if(alpha>=capacity && bravo>=capacity)return PvPvETeam.None;
            return alpha<=bravo && alpha<capacity?PvPvETeam.Alpha:PvPvETeam.Bravo;
        }
    }
    /// <summary>The server owns membership and teams. Clients have no team-assignment RPC.</summary>
    public sealed class PvPvETeamRoster : NetworkBehaviour
    {
        public static PvPvETeamRoster Active {get;private set;}
        public static event Action ActiveChanged;
        public readonly NetworkList<PvPvEMember> Members=new();
        public readonly NetworkVariable<bool> Closing=new(false);
        public void BeginClose(){if(IsServer)Closing.Value=true;}
        void ClosingChanged(bool oldValue,bool value)=>Changed?.Invoke();
        public event Action Changed;
        public bool RebalanceWhileWaiting=true;
        [SerializeField] PvPvENetworkSettings settings;
        public PvPvENetworkSettings Configuration=>settings;
        Func<ulong,string> approvedName;
        Func<ulong,int> approvedCharacter;
        public void Initialize(PvPvENetworkSettings config,Func<ulong,string> name,Func<ulong,int> character=null){settings=config;approvedName=name;approvedCharacter=character;}
        public bool TryGetMember(ulong client,out PvPvEMember member){foreach(var m in Members)if(m.ClientId==client){member=m;return true;}member=default;return false;}
        [ServerRpc(RequireOwnership=false)]
        public void SetCharacterServerRpc(int index,ServerRpcParams rpc=default)
        {
            var flow=GetComponent<PvPvEPreparationFlow>();if(Closing.Value || flow&&flow.Stage.Value!=PvPvEPrepareStage.Lobby || !settings.IsValidCharacter(index))return;
            for(int i=0;i<Members.Count;i++){var m=Members[i];if(m.ClientId!=rpc.Receive.SenderClientId)continue;if(m.CharacterIndex!=index){m.CharacterIndex=index;Members[i]=m;}return;}
        }
        public int Count(PvPvETeam team){int n=0;foreach(var member in Members)if(member.Team==team)n++;return n;}
        public bool CanStart=>Count(PvPvETeam.Alpha)>0 && Count(PvPvETeam.Bravo)>0;
        public bool Contains(ulong client){foreach(var member in Members)if(member.ClientId==client)return true;return false;}
        public override void OnNetworkSpawn()
        {
            Active=this;Members.OnListChanged+=OnListChanged;Closing.OnValueChanged+=ClosingChanged;
            if(IsServer){NetworkManager.OnClientConnectedCallback+=Add;NetworkManager.OnClientDisconnectCallback+=Remove;foreach(var client in NetworkManager.ConnectedClientsIds)Add(client);}
            ActiveChanged?.Invoke();Changed?.Invoke();
        }
        void OnListChanged(NetworkListEvent<PvPvEMember> change)=>Changed?.Invoke();
        void Add(ulong client)
        {
            if(!IsServer || Contains(client))return;
            var team=PvPvETeamRules.Choose(Count(PvPvETeam.Alpha),Count(PvPvETeam.Bravo),settings.TeamCapacity);
            if(team==PvPvETeam.None){if(client!=NetworkManager.LocalClientId)NetworkManager.DisconnectClient(client,"La sala está llena.");return;}
            Members.Add(new PvPvEMember{ClientId=client,Name=new FixedString64Bytes(PvPvEHello.CleanName(approvedName?.Invoke(client))),Team=team,CharacterIndex=approvedCharacter?.Invoke(client)??0});
        }
        void Remove(ulong client)
        {
            if(!IsServer)return;
            for(int i=Members.Count-1;i>=0;i--)if(Members[i].ClientId==client)Members.RemoveAt(i);
            if(!RebalanceWhileWaiting)return;
            RebalanceLobbyServer();
        }
        public void RebalanceLobbyServer(){
            if(!IsServer||!RebalanceWhileWaiting)return;
            while(Math.Abs(Count(PvPvETeam.Alpha)-Count(PvPvETeam.Bravo))>1){var from=Count(PvPvETeam.Alpha)>Count(PvPvETeam.Bravo)?PvPvETeam.Alpha:PvPvETeam.Bravo;for(int i=Members.Count-1;i>=0;i--)if(Members[i].Team==from){var member=Members[i];member.Team=from==PvPvETeam.Alpha?PvPvETeam.Bravo:PvPvETeam.Alpha;Members[i]=member;break;}}
        }
        public override void OnNetworkDespawn()
        {
            Members.OnListChanged-=OnListChanged;Closing.OnValueChanged-=ClosingChanged;
            if(IsServer){NetworkManager.OnClientConnectedCallback-=Add;NetworkManager.OnClientDisconnectCallback-=Remove;}
            if(Active==this){Active=null;ActiveChanged?.Invoke();}base.OnNetworkDespawn();
        }
    }
}
