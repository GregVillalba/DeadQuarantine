using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeadQuarantine.PvPvE.UI;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeadQuarantine.PvPvE.Networking
{
 public enum PvPvELifeStage:byte { Equipping, Alive, Dead }
 public struct PvPvEDeployment:INetworkSerializable,IEquatable<PvPvEDeployment> {
  public ulong ClientId,ObjectId;public uint Revision;public PvPvELifeStage Life;
  public bool Acknowledged;public Vector3 Position;public Quaternion Rotation;
  public double Due,ProtectedUntil;public int ClassIndex,PendingIndex;
  public FixedString4096Bytes Definition;
  public void NetworkSerialize<T>(BufferSerializer<T> s) where T:IReaderWriter {
   s.SerializeValue(ref ClientId);s.SerializeValue(ref ObjectId);s.SerializeValue(ref Revision);s.SerializeValue(ref Life);
   s.SerializeValue(ref Acknowledged);s.SerializeValue(ref Position);s.SerializeValue(ref Rotation);s.SerializeValue(ref Due);s.SerializeValue(ref ProtectedUntil);
   s.SerializeValue(ref ClassIndex);s.SerializeValue(ref PendingIndex);s.SerializeValue(ref Definition);
  }
  public bool Equals(PvPvEDeployment o)=>ClientId==o.ClientId&&ObjectId==o.ObjectId&&Revision==o.Revision&&Life==o.Life&&Acknowledged==o.Acknowledged&&Position==o.Position&&Rotation==o.Rotation&&Due==o.Due&&ProtectedUntil==o.ProtectedUntil&&ClassIndex==o.ClassIndex&&PendingIndex==o.PendingIndex&&Definition.Equals(o.Definition);
 }

 // Owns the life cycle, not weapons, combat scoring or the zombie simulation.
 public sealed class PvPvETeamDeployment:NetworkBehaviour {
  public static PvPvETeamDeployment Active{get;private set;}
  public readonly NetworkList<PvPvEDeployment> Players=new();
  PvPvEPreparationFlow flow;PvPvETeamRoster roster;PvPvEInitialClassSelection selection;
  PvPvEDeploymentView view;PvPvEDeathPresentation death;
  uint sequence,localRevision,releasedRevision;ulong localObject;float nextAck;bool applied;
  readonly Dictionary<ulong,double> requests=new();
  readonly Dictionary<ulong,FixedString4096Bytes> pendingClasses=new();
  readonly Dictionary<ulong,Vector3> lastSpawns=new();
  readonly Dictionary<ulong,double> combatUntil=new();
  public event Action<bool,string> ClassReply;
  public PvPvENetworkSettings Settings=>roster.Configuration;
  public bool AllPlaced=>Players.Count==roster.Members.Count&&Players.Count>0&&Enumerable.Range(0,Players.Count).All(i=>Players[i].Acknowledged&&Players[i].Life==PvPvELifeStage.Alive);
  public static bool LocalBlocked=>Active&&Active.IsClient&&(!Active.TryGet(Active.NetworkManager.LocalClientId,out var row)||!row.Acknowledged||row.Life!=PvPvELifeStage.Alive);
  public bool TryGet(ulong id,out PvPvEDeployment row){foreach(var p in Players)if(p.ClientId==id){row=p;return true;}row=default;return false;}
  int Index(ulong id){for(int i=0;i<Players.Count;i++)if(Players[i].ClientId==id)return i;return -1;}
  NetworkObject Player(ulong id)=>NetworkManager.ConnectedClients.TryGetValue(id,out var c)?c.PlayerObject:null;
  public override void OnNetworkSpawn(){Active=this;flow=GetComponent<PvPvEPreparationFlow>();roster=GetComponent<PvPvETeamRoster>();selection=GetComponent<PvPvEInitialClassSelection>();if(IsClient){view=gameObject.AddComponent<PvPvEDeploymentView>();death=gameObject.AddComponent<PvPvEDeathPresentation>();}}
  void Update(){
   if(!IsSpawned)return;
   if(IsServer)for(int i=Players.Count-1;i>=0;i--)if(!roster.Contains(Players[i].ClientId)){ulong id=Players[i].ClientId;requests.Remove(id);pendingClasses.Remove(id);lastSpawns.Remove(id);combatUntil.Remove(id);Players.RemoveAt(i);}
   if(!flow.InPreparation||flow.Stage.Value is PvPvEPrepareStage.Failed or PvPvEPrepareStage.ReturningToLobby)return;
   if(IsClient)UpdateOwner();
   if(!IsServer||roster.Closing.Value)return;
   if(flow.Stage.Value==PvPvEPrepareStage.ReadyForPlacement){
    if(!flow.BeginPlacementServer())return;
    Players.Clear();
    foreach(var member in roster.Members){
     if(!selection.TryGet(member.ClientId,out var c)||!Deploy(member.ClientId,c.ClassIndex,c.DefinitionJson)){flow.FailDeploymentServer("No hay un spawn accesible o equipamiento válido para ambos equipos.");return;}
    }
   }
   if(flow.Stage.Value==PvPvEPrepareStage.PlacingTeams&&AllPlaced)flow.BeginCountdownAfterPlacementServer();
   if(flow.Stage.Value==PvPvEPrepareStage.Prepared&&AllPlaced)flow.StartPlacedPlayersServer();
   bool playing=flow.Stage.Value==PvPvEPrepareStage.Playing&&(!PvPvECombat.Active||!PvPvECombat.Active.ScoreLocked.Value);
   for(int i=0;i<Players.Count;i++){
    var row=Players[i];var obj=Player(row.ClientId);if(!obj)continue;var health=obj.GetComponent<PlayerHealth>();double now=NetworkManager.ServerTime.Time;
    if(flow.Stage.Value==PvPvEPrepareStage.Playing&&row.Life==PvPvELifeStage.Alive&&!health.IsAlive){
     row.Life=PvPvELifeStage.Dead;row.Acknowledged=false;row.Due=now+Math.Max(1,Settings.RespawnSeconds);row.ProtectedUntil=0;Players[i]=row;
    }else if(playing&&row.Life==PvPvELifeStage.Dead&&now>=row.Due){
     bool pending=pendingClasses.TryGetValue(row.ClientId,out var next);int classIndex=pending?row.PendingIndex:row.ClassIndex;var json=pending?next:row.Definition;
     // A new instance restores all life-local state through the original Player's initialization.
     obj.Despawn(true);
     if(!Deploy(row.ClientId,classIndex,json)){flow.FailDeploymentServer("No se pudo encontrar una reaparición accesible. Salí de la sala y reintentá.");return;}
     pendingClasses.Remove(row.ClientId);
    }else if(row.Life==PvPvELifeStage.Equipping&&!row.Acknowledged&&now>=row.Due){
     if(row.ClientId==NetworkManager.LocalClientId)flow.FailDeploymentServer("El anfitrión no terminó de equiparse.");else NetworkManager.DisconnectClient(row.ClientId,"No se pudo preparar tu equipamiento a tiempo.");
    }
    if(playing&&row.Life==PvPvELifeStage.Alive&&row.ProtectedUntil>0&&row.ProtectedUntil<double.MaxValue&&now>=row.ProtectedUntil){row.ProtectedUntil=0;Players[i]=row;}
    if(playing&&row.Life==PvPvELifeStage.Alive&&row.ProtectedUntil==double.MaxValue){row.ProtectedUntil=now+Settings.SpawnProtectionSeconds;Players[i]=row;}
   }
  }
  bool Deploy(ulong id,int classIndex,FixedString4096Bytes json){
   var definition=JsonUtility.FromJson<PvPvEClassDefinition>(json.ToString());
   if(!Settings.Loadouts.Validate(definition,out _)||!TryChooseSpawn(id,out var position,out var rotation))return false;
   var obj=Player(id);
   if(!obj){var go=Instantiate(Settings.PlayerPrefab,position,rotation);SceneManager.MoveGameObjectToScene(go,PvPvEMapContext.Active.gameObject.scene);obj=go.GetComponent<NetworkObject>();obj.SpawnAsPlayerObject(id,true);}
   var health=obj.GetComponent<PlayerHealth>();
   if(!health.AplicarWildcardsDeClaseServidor(definition,true)||!obj.GetComponent<NetworkWeaponAttachments>().ApplyClassServer(definition))return false;
   if(++sequence==0)sequence=1;
   var row=new PvPvEDeployment{ClientId=id,ObjectId=obj.NetworkObjectId,Revision=sequence,Life=PvPvELifeStage.Equipping,Position=position,Rotation=rotation,Due=NetworkManager.ServerTime.Time+Settings.DeploymentSeconds,ClassIndex=classIndex,PendingIndex=-1,Definition=json};
   int index=Index(id);if(index<0)Players.Add(row);else Players[index]=row;lastSpawns[id]=position;return true;
  }
  public bool TryChooseSpawn(ulong id,out Vector3 position,out Quaternion rotation){
   position=default;rotation=Quaternion.identity;
   if(!roster.TryGetMember(id,out var member)||!PvPvEMapContext.Active||!PvPvEMapContext.Active.Scenario)return false;
   var points=PvPvEMapContext.Active.Scenario.GetComponentsInChildren<PvPvETeamSpawnPoint>(false).Where(p=>p.Team==member.Team&&p.Map==flow.Map.Value).ToArray();
   float best=float.NegativeInfinity;
   foreach(var point in points){
    Vector3 p=point.transform.position;
    if(!NavMesh.SamplePosition(p,out var hit,.6f,NavMesh.AllAreas)||Vector3.Distance(p,hit.position)>.6f||!ClearCapsule(hit.position))continue;
    p=hit.position+Vector3.up*.08f;float score=0,minEnemy=1000;bool occupied=false;
    foreach(var other in roster.Members){
     if(other.ClientId==id)continue;var obj=Player(other.ClientId);if(!obj||!obj.GetComponent<PlayerHealth>().IsAlive)continue;
     Vector3 otherPosition=TryGet(other.ClientId,out var deployed)&&deployed.Life==PvPvELifeStage.Equipping?deployed.Position:obj.transform.position;
     float distance=Vector3.Distance(p,otherPosition);if(distance<Settings.SpawnSpacing){occupied=true;break;}
     if(other.Team!=member.Team){minEnemy=Mathf.Min(minEnemy,distance);Vector3 ray=otherPosition-p;if(!Physics.RaycastAll(p+Vector3.up*1.5f,ray.normalized,Mathf.Max(0,ray.magnitude-.6f),~0,QueryTriggerInteraction.Ignore).Any(h=>!h.collider.GetComponentInParent<PlayerHealth>()))score-=80;if(combatUntil.TryGetValue(other.ClientId,out double until)&&until>NetworkManager.ServerTime.Time)score-=30f/(1+distance);}
    }
    if(occupied)continue;
    score+=Mathf.Min(100,minEnemy);if(minEnemy<Settings.SafeSpawnDistance)score-=200;
    if(lastSpawns.TryGetValue(id,out var last)&&Vector3.Distance(last,p)<2)score-=15;
    if(score>best){best=score;position=p;rotation=point.transform.rotation;}
   }
   return best>float.NegativeInfinity;
  }
  public static bool ClearCapsule(Vector3 foot)=>!Physics.CheckCapsule(foot+Vector3.up*.5f,foot+Vector3.up*1.5f,.32f,~0,QueryTriggerInteraction.Ignore);
  void UpdateOwner(){
   var player=NetworkManager.LocalClient?.PlayerObject;if(!player)return;
   var health=player.GetComponent<PlayerHealth>();var character=player.GetComponent<PlayerCharacter>();
   if(roster.TryGetMember(NetworkManager.LocalClientId,out var member)&&character.CharacterIndex.Value!=member.CharacterIndex)character.CharacterIndex.Value=member.CharacterIndex;
   if(!TryGet(NetworkManager.LocalClientId,out var row)||row.ObjectId!=player.NetworkObjectId)return;
   if(localRevision!=row.Revision||localObject!=row.ObjectId){localRevision=row.Revision;localObject=row.ObjectId;applied=false;}
   var switcher=player.GetComponentInChildren<WeaponSwitcher>(true);
   if(row.Life==PvPvELifeStage.Equipping&&!applied&&switcher&&switcher.IsInitialized&&!switcher.IsSwitching&&health.EquipoDeClase.Value.Active&&character.AppliedIndex==member.CharacterIndex){
    var c=JsonUtility.FromJson<PvPvEClassDefinition>(row.Definition.ToString());
    var presentation=player.GetComponent<NetworkWeaponPresentation>();var data=new List<ArmaSaveData>();
    foreach(var id in new[]{c.primary,c.secondary}){var d=Array.Find(presentation.Weapons,w=>w.Id==id);if(d==null)return;d.Source.CancelReload();d.Source.RestaurarMunicion();data.Add(new ArmaSaveData{identificador=id,casillero=data.Count,municionCargador=d.Source.CurrentAmmo,municionReserva=d.Source.ReserveAmmo,cargador=d.Source.GetComponent<WeaponAttachments>() is { } attachments?Mathf.Max(0,attachments.MagazineIndex):-1});}
    if(!switcher.RestaurarArmas(data,c.primary))return;
    // Reuse the normal weapon state; no animation/pose/timing data is changed.
    foreach(var d in presentation.Weapons){if(d.Source.GetComponent<WeaponAttachments>() is { } a&&a.HasRuntimeCatalog)a.ApplyApproved(d.Id==c.primary?c.primaryAttachments:d.Id==c.secondary?c.secondaryAttachments:default);}
    switcher.ResetBaseLayerToIdle();switcher.CurrentWeapon.InputLocked=false;
    var controller=player.GetComponent<CharacterController>();bool enabledBefore=controller&&controller.enabled;if(controller)controller.enabled=false;
    player.transform.SetPositionAndRotation(row.Position,row.Rotation);player.GetComponent<NetworkTransform>().Teleport(row.Position,row.Rotation,player.transform.localScale);
    if(controller)controller.enabled=enabledBefore;player.GetComponent<PlayerMovement>().ResetMovementState();
    // Develop ignores fall damage until its spawn flag is set; team placement owns this teleport.
    var spawnAssigner=player.GetComponent<MultiplayerPlayerSpawnAssigner>();if(spawnAssigner&&spawnAssigner.IsOwner)spawnAssigner.IsAtSpawn.Value=true;
    applied=true;
   }
   if(applied&&!row.Acknowledged&&row.Life==PvPvELifeStage.Equipping&&Time.unscaledTime>=nextAck){nextAck=Time.unscaledTime+.4f;AcknowledgeServerRpc(row.Revision,row.ObjectId,selection.Epoch.Value);}
   if(row.Life==PvPvELifeStage.Alive&&row.Acknowledged&&flow.Stage.Value==PvPvEPrepareStage.Playing&&releasedRevision!=row.Revision){
    var movement=player.GetComponent<PlayerMovement>();movement.enabled=true;movement.ResetMovementState();movement.MovementLocked=false;
    var look=player.GetComponentInChildren<PlayerLook>(true);if(look){look.Girar(0);look.enabled=true;}
    // Both class weapons were locked while staging. Release the secondary as well;
    // otherwise its first equip after start/respawn cannot fire or reload.
    foreach(var definition in player.GetComponent<NetworkWeaponPresentation>().Weapons)
     if(definition.Source&&health.ArmaPermitidaPorClase(definition.Id))definition.Source.InputLocked=false;
    player.GetComponentInChildren<PauseController>(true)?.MostrarHUD();
    releasedRevision=row.Revision;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
   }
  }
  [ServerRpc(RequireOwnership=false)] public void AcknowledgeServerRpc(uint revision,ulong objectId,uint epoch,ServerRpcParams rpc=default){
   int index=Index(rpc.Receive.SenderClientId);if(index<0||epoch!=selection.Epoch.Value)return;
   var row=Players[index];var obj=Player(row.ClientId);
   if(row.Revision!=revision||row.ObjectId!=objectId||row.Life!=PvPvELifeStage.Equipping||!obj||obj.NetworkObjectId!=objectId||!obj.GetComponent<PlayerHealth>().IsAlive||Vector3.Distance(obj.transform.position,row.Position)>1||!roster.TryGetMember(row.ClientId,out var member)||obj.GetComponent<PlayerCharacter>().AppliedIndex!=member.CharacterIndex)return;
   row.Acknowledged=true;row.Life=PvPvELifeStage.Alive;row.Due=0;row.ProtectedUntil=flow.Stage.Value==PvPvEPrepareStage.Playing?NetworkManager.ServerTime.Time+Settings.SpawnProtectionSeconds:double.MaxValue;Players[index]=row;
  }
  [ServerRpc(RequireOwnership=false)] public void ChangeClassServerRpc(int index,string json,uint epoch,ServerRpcParams rpc=default){
   ulong sender=rpc.Receive.SenderClientId;bool accepted=false;string error="La clase no se puede cambiar ahora.";
   if(flow.Stage.Value==PvPvEPrepareStage.Playing&&epoch==selection.Epoch.Value&&roster.Contains(sender)&&TryGet(sender,out var row)&&(!requests.TryGetValue(sender,out double due)||NetworkManager.ServerTime.Time>=due)){
    requests[sender]=NetworkManager.ServerTime.Time+.2;
    if(index>=0&&index<5&&!string.IsNullOrEmpty(json)&&Encoding.UTF8.GetByteCount(json)<=PvPvEInitialClassSelection.MaximumRequestBytes){
     PvPvEClassDefinition c=null;try{c=JsonUtility.FromJson<PvPvEClassDefinition>(json);}catch(ArgumentException){}
     if(Settings.Loadouts.Validate(c,out error)){pendingClasses[sender]=new FixedString4096Bytes(JsonUtility.ToJson(c));row.PendingIndex=index;Players[Index(sender)]=row;accepted=true;error="La nueva clase se aplicará en el próximo respawn.";}
    }
   }
   ClassReplyClientRpc(accepted,error,new ClientRpcParams{Send=new ClientRpcSendParams{TargetClientIds=new[]{sender}}});
  }
  [ClientRpc] void ClassReplyClientRpc(bool accepted,string message,ClientRpcParams target=default)=>ClassReply?.Invoke(accepted,message);
  public static bool BlocksDamage(PlayerHealth health){
   var service=Active;if(!service||!health||!PvPvEPreparationFlow.OwnsStagingPlayer(health.NetworkObject))return false;
   return PvPvECombat.Active&&PvPvECombat.Active.ScoreLocked.Value||service.flow.Stage.Value!=PvPvEPrepareStage.Playing||!service.TryGet(health.OwnerClientId,out var row)||row.Life!=PvPvELifeStage.Alive||!row.Acknowledged||row.ProtectedUntil>service.NetworkManager.ServerTime.Time;
  }
  public static bool AllowsOffense(PlayerHealth health){var service=Active;if(PvPvECombat.LocalOrServerBlocked(health))return false;return !service||!PvPvEPreparationFlow.OwnsStagingPlayer(health.NetworkObject)||service.flow.Stage.Value==PvPvEPrepareStage.Playing&&service.TryGet(health.OwnerClientId,out var row)&&row.Life==PvPvELifeStage.Alive&&row.Acknowledged&&health.IsAlive;}
  [ServerRpc(RequireOwnership=false)] public void OffensiveActionServerRpc(ServerRpcParams rpc=default){CancelProtectionServer(rpc.Receive.SenderClientId);}
  public void CancelProtectionServer(ulong id){if(!IsServer||flow.Stage.Value!=PvPvEPrepareStage.Playing)return;int i=Index(id);if(i<0)return;var row=Players[i];if(row.Life!=PvPvELifeStage.Alive)return;combatUntil[id]=NetworkManager.ServerTime.Time+3;if(row.ProtectedUntil!=0){row.ProtectedUntil=0;Players[i]=row;}}
  public void ResetLobbyServer(){if(!IsServer)return;Players.Clear();requests.Clear();pendingClasses.Clear();lastSpawns.Clear();combatUntil.Clear();}
  public override void OnNetworkDespawn(){if(view){view.DisposeView();Destroy(view);}if(death)Destroy(death);if(Active==this)Active=null;ClassReply=null;base.OnNetworkDespawn();}
 }
}
