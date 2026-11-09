using System;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using DeadQuarantine.PvPvE.UI;
namespace DeadQuarantine.PvPvE.Networking
{
 public struct PvPvEConfirmedClass : INetworkSerializable,IEquatable<PvPvEConfirmedClass> {
  public ulong ClientId;public uint Epoch;public int ClassIndex;public FixedString4096Bytes DefinitionJson;
  public void NetworkSerialize<T>(BufferSerializer<T> s) where T:IReaderWriter {s.SerializeValue(ref ClientId);s.SerializeValue(ref Epoch);s.SerializeValue(ref ClassIndex);s.SerializeValue(ref DefinitionJson);}
  public bool Equals(PvPvEConfirmedClass other)=>ClientId==other.ClientId&&Epoch==other.Epoch&&ClassIndex==other.ClassIndex&&DefinitionJson.Equals(other.DefinitionJson);
 }
 // Stores the initial choice only. Scene loading/Ready and equipment application belong to phases 15/16.
 public sealed class PvPvEInitialClassSelection:NetworkBehaviour {
  public const int MaximumRequestBytes=3072;
  public readonly NetworkList<PvPvEConfirmedClass> Confirmed=new();
  public readonly NetworkVariable<uint> Epoch=new(1);
  public readonly NetworkVariable<bool> SelectionOpen=new(true);
  PvPvETeamRoster roster;PvPvEMapVoting voting;PvPvEPreparationFlow preparation;PvPvEInitialClassView view;
  readonly System.Collections.Generic.Dictionary<ulong,float> nextRequest=new();
  public event Action Changed;
  public event Action<uint,uint,bool,string> Reply;
  public PvPvELoadoutCatalog Catalog=>roster?roster.Configuration.Loadouts:null;
  public bool TryGet(ulong client,out PvPvEConfirmedClass confirmed){foreach(var entry in Confirmed)if(entry.ClientId==client&&entry.Epoch==Epoch.Value){confirmed=entry;return true;}confirmed=default;return false;}
  public bool TryGetConfirmedClass(ulong client,out PvPvEClassDefinition definition){definition=null;if(!TryGet(client,out var entry))return false;definition=JsonUtility.FromJson<PvPvEClassDefinition>(entry.DefinitionJson.ToString());return true;}
  public bool HasPlayer(ulong client){
   if(!IsSpawned||!NetworkManager)return false;
   var player=IsServer?(NetworkManager.ConnectedClients.TryGetValue(client,out var c)?c.PlayerObject:null):(client==NetworkManager.LocalClientId?NetworkManager.LocalClient?.PlayerObject:null);
   return player&&player.IsSpawned&&player.OwnerClientId==client&&player.GetComponent<PlayerHealth>();
  }
  public bool CanSelect(ulong client)=>IsSpawned&&SelectionOpen.Value&&Epoch.Value!=0&&roster&&!roster.Closing.Value&&roster.Contains(client)&&voting&&voting.Phase.Value==PvPvEVotePhase.MapSelected&&HasPlayer(client)&&(!preparation||preparation.AllowsSelection(client));
  public bool ShouldShowLocal=>IsClient&&NetworkManager&&CanSelect(NetworkManager.LocalClientId);
  public override void OnNetworkSpawn(){
   roster=GetComponent<PvPvETeamRoster>();voting=GetComponent<PvPvEMapVoting>();preparation=GetComponent<PvPvEPreparationFlow>();if(preparation)preparation.Changed+=Notify;
   Confirmed.OnListChanged+=ListChanged;Epoch.OnValueChanged+=EpochChanged;SelectionOpen.OnValueChanged+=OpenChanged;roster.Changed+=RosterChanged;voting.Changed+=Notify;
   if(IsClient)view=gameObject.AddComponent<PvPvEInitialClassView>();Notify();
  }
  void Notify()=>Changed?.Invoke();
  void ListChanged(NetworkListEvent<PvPvEConfirmedClass> change)=>Notify();
  void EpochChanged(uint before,uint after)=>Notify();void OpenChanged(bool before,bool after)=>Notify();
  void RosterChanged(){
   if(IsServer){foreach(var client in new System.Collections.Generic.List<ulong>(nextRequest.Keys))if(!roster.Contains(client))nextRequest.Remove(client);for(int i=Confirmed.Count-1;i>=0;i--)if(!roster.Contains(Confirmed[i].ClientId)){nextRequest.Remove(Confirmed[i].ClientId);Confirmed.RemoveAt(i);}}
   Notify();
  }
  // Trusted map-cycle boundaries for the future loading/match flow. They do not start a match.
  public bool BeginForMapServer(){if(!IsServer||!IsSpawned||roster.Closing.Value)return false;Confirmed.Clear();nextRequest.Clear();Epoch.Value=Epoch.Value==uint.MaxValue?1:Epoch.Value+1;SelectionOpen.Value=true;return true;}
  public bool CloseSelectionServer(){if(!IsServer||!IsSpawned)return false;SelectionOpen.Value=false;return true;}
  [ServerRpc(RequireOwnership=false)]
  public void ConfirmServerRpc(int classIndex,string definitionJson,uint epoch,uint requestId,ServerRpcParams rpc=default){
   ulong sender=rpc.Receive.SenderClientId;
   if(nextRequest.TryGetValue(sender,out float until)&&Time.unscaledTime<until){Answer(sender,epoch,requestId,false,"Esperá un momento antes de reintentar.");return;}
   nextRequest[sender]=Time.unscaledTime+.15f;
   if(epoch!=Epoch.Value||!CanSelect(sender)){Answer(sender,epoch,requestId,false,"La selección de esta partida no está disponible.");return;}
   if(classIndex<0||classIndex>=5||string.IsNullOrEmpty(definitionJson)||Encoding.UTF8.GetByteCount(definitionJson)>MaximumRequestBytes){Answer(sender,epoch,requestId,false,"Clase inválida.");return;}
   PvPvEClassDefinition definition;
   try{definition=JsonUtility.FromJson<PvPvEClassDefinition>(definitionJson);}catch(ArgumentException){Answer(sender,epoch,requestId,false,"No se pudo leer la clase.");return;}
   if(!Catalog.Validate(definition,out string error)){Answer(sender,epoch,requestId,false,error);return;}
   string canonical=JsonUtility.ToJson(definition);
   if(Encoding.UTF8.GetByteCount(canonical)>MaximumRequestBytes){Answer(sender,epoch,requestId,false,"Clase demasiado extensa.");return;}
   if(TryGet(sender,out var existing)){bool same=existing.ClassIndex==classIndex&&existing.DefinitionJson.ToString()==canonical;Answer(sender,epoch,requestId,same,same?"":"Ya confirmaste tu clase inicial.");return;}
   Confirmed.Add(new PvPvEConfirmedClass{ClientId=sender,Epoch=Epoch.Value,ClassIndex=classIndex,DefinitionJson=new FixedString4096Bytes(canonical)});
   Answer(sender,epoch,requestId,true,"");
  }
  void Answer(ulong client,uint epoch,uint request,bool accepted,string error){
   if(!NetworkManager.ConnectedClients.ContainsKey(client))return;
   ReplyClientRpc(epoch,request,accepted,error,new ClientRpcParams{Send=new ClientRpcSendParams{TargetClientIds=new[]{client}}});
  }
  [ClientRpc] void ReplyClientRpc(uint epoch,uint request,bool accepted,string error,ClientRpcParams target=default)=>Reply?.Invoke(epoch,request,accepted,error);
  public override void OnNetworkDespawn(){
   Confirmed.OnListChanged-=ListChanged;Epoch.OnValueChanged-=EpochChanged;SelectionOpen.OnValueChanged-=OpenChanged;
   if(preparation)preparation.Changed-=Notify;if(roster)roster.Changed-=RosterChanged;if(voting)voting.Changed-=Notify;
   if(view){view.DisposeView();Destroy(view);}nextRequest.Clear();Changed=null;Reply=null;base.OnNetworkDespawn();
  }
 }
}
