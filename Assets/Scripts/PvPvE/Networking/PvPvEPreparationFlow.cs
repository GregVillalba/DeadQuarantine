using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadQuarantine.PvPvE.UI;
namespace DeadQuarantine.PvPvE.Networking
{
 public enum PvPvEPrepareStage:byte {Lobby,LoadingMap,SelectingClasses,ReadyForPlacement,Countdown,Prepared,Failed,PlacingTeams,Playing,ReturningToLobby}
 [Flags] public enum PvPvEReadyFlags:byte {None=0,Loaded=1,Initialized=2,ClassConfirmed=4,Ready=8}
 public struct PvPvEParticipant:INetworkSerializable,IEquatable<PvPvEParticipant>{
  public ulong ClientId;public PvPvEReadyFlags Flags;public double Due;
  public void NetworkSerialize<T>(BufferSerializer<T> s) where T:IReaderWriter{s.SerializeValue(ref ClientId);s.SerializeValue(ref Flags);s.SerializeValue(ref Due);}
  public bool Equals(PvPvEParticipant p)=>ClientId==p.ClientId&&Flags==p.Flags&&Due==p.Due;
  public bool Has(PvPvEReadyFlags flag)=>(Flags&flag)==flag;
 }
 // Loading/Ready coordinator. Team deployment owns placement, equipment and the life cycle.
 public sealed partial class PvPvEPreparationFlow:NetworkBehaviour {
  public static PvPvEPreparationFlow Active {get;private set;}
  public readonly NetworkVariable<PvPvEPrepareStage> Stage=new(PvPvEPrepareStage.Lobby);
  public readonly NetworkVariable<ConfiguracionPartidaSeleccionada.Escenario> Map=new();
  public readonly NetworkVariable<double> Deadline=new();
  public readonly NetworkVariable<FixedString512Bytes> Failure=new();
  public readonly NetworkList<PvPvEParticipant> Participants=new();
  PvPvETeamRoster roster;PvPvEMapVoting voting;PvPvEInitialClassSelection selection;PvPvEPreparationView view;PvPvELoadingScreen loadingScreen;
  readonly Dictionary<GameObject,bool> menuRoots=new();Scene menuScene;float nextCheck;bool disposed,localReported;
  float nextLocalReport;
  readonly Dictionary<ulong,(PlayerMovement movement,CharacterController controller,bool movementEnabled,bool controllerEnabled)> frozen=new();
  uint reportedEpoch;bool hadSelection;ConfiguracionPartidaSeleccionada.Escenario oldMap;ConfiguracionPartidaSeleccionada.ModoJuego oldMode;
  public event Action Changed;
  public PvPvENetworkSettings Settings=>roster.Configuration;
  public bool InPreparation=>IsSpawned&&Stage.Value!=PvPvEPrepareStage.Lobby;
  public static bool LocalControlsLocked=>Active&&Active.IsClient&&Active.InPreparation&&!Active.roster.Closing.Value&&(Active.Stage.Value!=PvPvEPrepareStage.Playing||PvPvETeamDeployment.LocalBlocked||PvPvECombat.LocalBlocked);
  public static bool OwnsStagingPlayer(NetworkObject player)=>Active&&player&&Active.NetworkManager==player.NetworkManager&&Active.InPreparation&&player.gameObject.scene.name==Active.Settings.MatchScene;
  public bool TryGet(ulong id,out PvPvEParticipant participant){foreach(var p in Participants)if(p.ClientId==id){participant=p;return true;}participant=default;return false;}
  public bool AllowsSelection(ulong client)=>(Stage.Value is PvPvEPrepareStage.LoadingMap or PvPvEPrepareStage.SelectingClasses)&&TryGet(client,out var p)&&p.Has(PvPvEReadyFlags.Loaded);
  public bool AllReady=>Participants.Count>0&&Participants.Count==roster.Members.Count&&roster.CanStart&&Enumerable.Range(0,Participants.Count).All(i=>Participants[i].Has(PvPvEReadyFlags.Ready));
  public string Status{
   get{int loaded=0,ready=0;foreach(var p in Participants){if(p.Has(PvPvEReadyFlags.Loaded))loaded++;if(p.Has(PvPvEReadyFlags.Ready))ready++;}
    return Stage.Value switch{
     PvPvEPrepareStage.LoadingMap=>$"CARGANDO MAPA · {loaded}/{Participants.Count} cargados · {ready}/{Participants.Count} listos",
     PvPvEPrepareStage.SelectingClasses=>$"ESPERANDO JUGADORES · {ready}/{Participants.Count} listos",
     PvPvEPrepareStage.ReadyForPlacement=>"TODOS LOS JUGADORES ESTÁN LISTOS",
     PvPvEPrepareStage.PlacingTeams=>"PREPARANDO EQUIPOS",
     PvPvEPrepareStage.Countdown=>Math.Max(1,Math.Ceiling(Deadline.Value-NetworkManager.ServerTime.Time)).ToString(),
     PvPvEPrepareStage.Prepared=>"START",PvPvEPrepareStage.Failed=>Failure.Value.ToString(),_=>""};}
  }
  public override void OnNetworkSpawn(){
   Active=this;roster=GetComponent<PvPvETeamRoster>();voting=GetComponent<PvPvEMapVoting>();selection=GetComponent<PvPvEInitialClassSelection>();menuScene=SceneManager.GetActiveScene();
   hadSelection=ConfiguracionPartidaSeleccionada.Instancia;if(hadSelection){oldMap=ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado;oldMode=ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado;}
   Stage.OnValueChanged+=StageChanged;Map.OnValueChanged+=MapChanged;Deadline.OnValueChanged+=TimeChanged;Failure.OnValueChanged+=FailureChanged;Participants.OnListChanged+=ParticipantsChanged;
   roster.Changed+=MembershipChanged;NetworkManager.SceneManager.OnSceneEvent+=SceneEvent;
   if(IsServer)voting.SelectionReady+=MapSelected;if(IsClient){loadingScreen=gameObject.AddComponent<PvPvELoadingScreen>();view=gameObject.AddComponent<PvPvEPreparationView>();}Notify();
  }
  void Notify()=>Changed?.Invoke();void StageChanged(PvPvEPrepareStage before,PvPvEPrepareStage after){Notify();if(IsClient){if(after==PvPvEPrepareStage.Lobby){view?.RestoreLobbyInput();GetComponent<PvPvEDeathPresentation>()?.ClearForLobby();RestoreStaging();RestoreMenu();localReported=false;reportedEpoch=0;nextLocalReport=0;}else HideMenu();}}
  void MapChanged(ConfiguracionPartidaSeleccionada.Escenario a,ConfiguracionPartidaSeleccionada.Escenario b)=>Notify();
  void TimeChanged(double a,double b)=>Notify();void FailureChanged(FixedString512Bytes a,FixedString512Bytes b)=>Notify();void ParticipantsChanged(NetworkListEvent<PvPvEParticipant> e)=>Notify();
  void HideMenu(){
   if(menuRoots.Count!=0||!menuScene.IsValid()||!menuScene.isLoaded)return;
   var networkRoot=NetworkManager.transform.root.gameObject;
   foreach(var root in menuScene.GetRootGameObjects()){if(root==networkRoot||root==transform.root.gameObject)continue;menuRoots[root]=root.activeSelf;root.SetActive(false);}
  }
  void RestoreMenu(){
   if(menuScene.IsValid()&&menuScene.isLoaded)SceneManager.SetActiveScene(menuScene);
   foreach(var pair in menuRoots)if(pair.Key)pair.Key.SetActive(pair.Value);menuRoots.Clear();
   if(hadSelection&&ConfiguracionPartidaSeleccionada.Instancia){ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado=oldMap;ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado=oldMode;}
  }
  void MapSelected(ConfiguracionPartidaSeleccionada.Escenario map){if(!IsServer||Stage.Value!=PvPvEPrepareStage.Lobby)return;_ = LoadMapAsync(map);}
  async Task LoadMapAsync(ConfiguracionPartidaSeleccionada.Escenario map){
   try{
    if(!roster.CanStart||!Settings.PlayerPrefab||string.IsNullOrWhiteSpace(Settings.MatchScene)||!Application.CanStreamedLevelBeLoaded(Settings.MatchScene)){Fail("No se pudo cargar el mapa configurado.");return;}
    Map.Value=map;roster.RebalanceWhileWaiting=false;selection.BeginForMapServer();Participants.Clear();
    foreach(var member in roster.Members)Participants.Add(new PvPvEParticipant{ClientId=member.ClientId,Due=NetworkManager.ServerTime.Time+Settings.MapLoadSeconds});
    Deadline.Value=NetworkManager.ServerTime.Time+Settings.MapLoadSeconds;Stage.Value=PvPvEPrepareStage.LoadingMap;
    await PvPvEMatchmaking.Active.CloseAdmissionForMapAsync();
    while(!disposed&&IsSpawned&&Stage.Value==PvPvEPrepareStage.LoadingMap){
     if(!roster.CanStart){Fail("Un equipo quedó vacío antes de comenzar.");return;}
     var result=NetworkManager.SceneManager.LoadScene(Settings.MatchScene,LoadSceneMode.Additive);
     if(result==SceneEventProgressStatus.Started)return;
     if(result!=SceneEventProgressStatus.SceneEventInProgress)throw new InvalidOperationException("NGO: "+result);
     if(NetworkManager.ServerTime.Time>=Deadline.Value)throw new TimeoutException("La carga no pudo comenzar.");await Task.Delay(100);
    }
   }catch(Exception){if(!disposed&&IsSpawned)Fail("No se pudo preparar el mapa. Salí de la sala y reintentá.");}
  }
  void SceneEvent(SceneEvent e){
   if(!IsServer||!InPreparation||e.SceneName!=Settings.MatchScene)return;
   if(Stage.Value==PvPvEPrepareStage.ReturningToLobby){if(e.SceneEventType==SceneEventType.UnloadEventCompleted){if(e.ClientsThatTimedOut!=null)foreach(var id in e.ClientsThatTimedOut)RemoveTimedOut(id,"No se pudo regresar al lobby a tiempo.");returnUnloadDone=true;}return;}
   if(e.SceneEventType==SceneEventType.LoadComplete)MarkLoaded(e.ClientId);
   if(e.SceneEventType==SceneEventType.LoadEventCompleted){
    if(e.ClientsThatCompleted!=null)foreach(var id in e.ClientsThatCompleted)MarkLoaded(id);
    if(e.ClientsThatTimedOut!=null)foreach(var id in e.ClientsThatTimedOut)RemoveTimedOut(id,"No se pudo cargar el mapa a tiempo.");
   }
  }
  void MarkLoaded(ulong id){
   for(int i=0;i<Participants.Count;i++){var p=Participants[i];if(p.ClientId!=id||p.Has(PvPvEReadyFlags.Loaded))continue;p.Flags|=PvPvEReadyFlags.Loaded;p.Due=NetworkManager.ServerTime.Time+Settings.PlayerPrepareSeconds;Participants[i]=p;return;}
  }
  void EnsurePlayer(PvPvEParticipant participant){
   if(!participant.Has(PvPvEReadyFlags.Loaded)||!NetworkManager.ConnectedClients.TryGetValue(participant.ClientId,out var client)||client.PlayerObject)return;
   var context=PvPvEMapContext.Active;if(!context||!context.Scenario||context.Scenario.TipoEscenario!=Map.Value)return;
   var point=context.Scenario.SpawnJugador;if(!point){Fail("El escenario no tiene un punto de preparación válido.");return;}
   // Temporary initialization position; phase16 owns team placement and safe respawn selection.
   var player=Instantiate(Settings.PlayerPrefab,point.position+Vector3.up*.2f,point.rotation);
   SceneManager.MoveGameObjectToScene(player,context.gameObject.scene);player.GetComponent<NetworkObject>().SpawnAsPlayerObject(participant.ClientId,true);
  }
  void Update(){
   if(!IsSpawned||disposed)return;
   if(IsClient){MaintainStagingLock();ReportLocalInitialization();}
   if(!IsServer||!InPreparation||Stage.Value==PvPvEPrepareStage.Playing||Stage.Value==PvPvEPrepareStage.ReturningToLobby||Stage.Value==PvPvEPrepareStage.Failed||Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+.1f;
   if(!roster.CanStart){Fail("Un equipo quedó vacío antes de comenzar.");return;}
   for(int i=Participants.Count-1;i>=0;i--){
    var p=Participants[i];EnsurePlayer(p);
    bool initialized=p.Has(PvPvEReadyFlags.Initialized)&&ServerPlayerValid(p.ClientId,out _);
    bool chosen=selection.TryGetConfirmedClass(p.ClientId,out var definition)&&Settings.Loadouts.Validate(definition,out _);
    var flags=p.Flags&(PvPvEReadyFlags.Loaded|PvPvEReadyFlags.Initialized);if(chosen)flags|=PvPvEReadyFlags.ClassConfirmed;
    if(p.Has(PvPvEReadyFlags.Loaded)&&initialized&&chosen)flags|=PvPvEReadyFlags.Ready;
    if(flags!=p.Flags){p.Flags=flags;Participants[i]=p;}
    if(!p.Has(PvPvEReadyFlags.Ready)&&NetworkManager.ServerTime.Time>=p.Due)RemoveTimedOut(p.ClientId,"Un jugador no terminó de prepararse a tiempo.");
   }
   if(Stage.Value==PvPvEPrepareStage.Failed)return;
   if(AllReady){if(Stage.Value is PvPvEPrepareStage.LoadingMap or PvPvEPrepareStage.SelectingClasses){Stage.Value=PvPvEPrepareStage.ReadyForPlacement;Deadline.Value=0;}}
   else if(Stage.Value is PvPvEPrepareStage.ReadyForPlacement or PvPvEPrepareStage.PlacingTeams or PvPvEPrepareStage.Countdown or PvPvEPrepareStage.Prepared){Stage.Value=PvPvEPrepareStage.SelectingClasses;Deadline.Value=0;}
   else if(Stage.Value==PvPvEPrepareStage.LoadingMap&&Enumerable.Range(0,Participants.Count).All(i=>Participants[i].Has(PvPvEReadyFlags.Loaded)))Stage.Value=PvPvEPrepareStage.SelectingClasses;
   if(Stage.Value==PvPvEPrepareStage.Countdown&&NetworkManager.ServerTime.Time>=Deadline.Value){Stage.Value=PvPvEPrepareStage.Prepared;Deadline.Value=0;}
  }
  bool ServerPlayerValid(ulong id,out NetworkObject player){
   player=null;if(!NetworkManager.ConnectedClients.TryGetValue(id,out var client))return false;player=client.PlayerObject;
   if(!player||!player.IsSpawned||player.OwnerClientId!=id||player.gameObject.scene.name!=Settings.MatchScene||!roster.TryGetMember(id,out var member))return false;
   var health=player.GetComponent<PlayerHealth>();var character=player.GetComponent<PlayerCharacter>();
   return health&&health.IsSpawned&&health.IsAlive&&character&&character.IsSpawned&&character.CharacterIndex.Value==member.CharacterIndex&&character.AppliedIndex==member.CharacterIndex;
  }
  void MaintainStagingLock(){
   if(Stage.Value is PvPvEPrepareStage.Countdown or PvPvEPrepareStage.Prepared or PvPvEPrepareStage.Playing){RestoreStaging();return;}
   var player=NetworkManager.LocalClient?.PlayerObject;if(!player||!OwnsStagingPlayer(player))return;
   var movement=player.GetComponent<PlayerMovement>();var controller=player.GetComponent<CharacterController>();
   if(!frozen.ContainsKey(player.NetworkObjectId))frozen[player.NetworkObjectId]=(movement,controller,movement&&movement.enabled,controller&&controller.enabled);
   if(movement)movement.enabled=false;if(controller)controller.enabled=false;
  }
  void RestoreStaging(){foreach(var entry in frozen.Values){if(entry.movement)entry.movement.enabled=entry.movementEnabled;if(entry.controller)entry.controller.enabled=entry.controllerEnabled;}frozen.Clear();}
  void ReportLocalInitialization(){
   var player=NetworkManager.LocalClient?.PlayerObject;if(!player||!OwnsStagingPlayer(player)||!roster.TryGetMember(NetworkManager.LocalClientId,out var member))return;
   var character=player.GetComponent<PlayerCharacter>();if(!character||!character.IsSpawned)return;
   if(character.CharacterIndex.Value!=member.CharacterIndex){character.CharacterIndex.Value=member.CharacterIndex;return;}
   var switcher=player.GetComponentInChildren<WeaponSwitcher>(true);var health=player.GetComponent<PlayerHealth>();
   if(character.AppliedIndex!=member.CharacterIndex||!health||!health.IsSpawned||!switcher||!switcher.IsInitialized||switcher.IsSwitching)return;
   if(Time.unscaledTime<nextLocalReport)return;
   if(!TryGet(NetworkManager.LocalClientId,out var participant)||!participant.Has(PvPvEReadyFlags.Loaded)||participant.Has(PvPvEReadyFlags.Initialized))return;
   nextLocalReport=Time.unscaledTime+.5f;localReported=true;reportedEpoch=selection.Epoch.Value;ReportInitializedServerRpc(reportedEpoch,player.NetworkObjectId);
  }
  [ServerRpc(RequireOwnership=false)] public void ReportInitializedServerRpc(uint epoch,ulong objectId,ServerRpcParams rpc=default){
   ulong id=rpc.Receive.SenderClientId;if(!InPreparation||Stage.Value==PvPvEPrepareStage.Failed||epoch!=selection.Epoch.Value||!TryGet(id,out var entry)||!entry.Has(PvPvEReadyFlags.Loaded)||!ServerPlayerValid(id,out var player)||player.NetworkObjectId!=objectId)return;
   for(int i=0;i<Participants.Count;i++)if(Participants[i].ClientId==id){entry.Flags|=PvPvEReadyFlags.Initialized;Participants[i]=entry;return;}
  }
  void MembershipChanged(){
   if(!IsServer||!InPreparation)return;
   if(Stage.Value==PvPvEPrepareStage.Playing){GetComponent<PvPvECombat>().ResolveEmptyTeamServer();return;}
   if(Stage.Value==PvPvEPrepareStage.ReturningToLobby)return;
   for(int i=Participants.Count-1;i>=0;i--)if(!roster.Contains(Participants[i].ClientId))Participants.RemoveAt(i);
   foreach(var member in roster.Members)if(!TryGet(member.ClientId,out _)&&member.ClientId!=NetworkManager.LocalClientId)NetworkManager.DisconnectClient(member.ClientId,"La partida ya está en preparación.");
   if(!roster.Closing.Value&&!roster.CanStart)Fail("Un equipo quedó vacío antes de comenzar.");
  }
  void RemoveTimedOut(ulong id,string reason){if(id==NetworkManager.LocalClientId){Fail(reason);return;}if(NetworkManager.ConnectedClients.ContainsKey(id))NetworkManager.DisconnectClient(id,reason);}
  void Fail(string reason){if(!IsServer||disposed||!IsSpawned)return;Failure.Value=new FixedString512Bytes(reason);Stage.Value=PvPvEPrepareStage.Failed;Deadline.Value=0;}
  // Phase16 calls this only AFTER its team placement. Preparation never auto-starts gameplay.
  public bool BeginCountdownAfterPlacementServer(){if(!IsServer||Stage.Value!=PvPvEPrepareStage.PlacingTeams||!AllReady||!GetComponent<PvPvETeamDeployment>().AllPlaced)return false;Deadline.Value=NetworkManager.ServerTime.Time+Math.Max(1,Settings.PreMatchSeconds);Stage.Value=PvPvEPrepareStage.Countdown;return true;}
  public bool BeginPlacementServer(){if(!IsServer||Stage.Value!=PvPvEPrepareStage.ReadyForPlacement||!AllReady)return false;Stage.Value=PvPvEPrepareStage.PlacingTeams;selection.CloseSelectionServer();return true;}
  public void FailDeploymentServer(string reason){if(IsServer)Fail(reason);}
  public bool StartPlacedPlayersServer(){if(!IsServer||Stage.Value!=PvPvEPrepareStage.Prepared||!AllReady||!GetComponent<PvPvETeamDeployment>().AllPlaced)return false;Stage.Value=PvPvEPrepareStage.Playing;return true;}
  public static async Task ExitToMenuAsync(){
   var service=PvPvEMatchmaking.Active;if(!service)return;Time.timeScale=1;AudioListener.pause=false;
   service.GetComponent<PvPvELobbyView>()?.LeaveSearchLobby();await service.CancelAndLeaveAsync();if(service&&service.CanNavigateAway)service.GetComponent<PvPvEModeMenu>()?.OpenPvPvE();
  }
  public override void OnNetworkDespawn(){
   disposed=true;if(NetworkManager&&NetworkManager.SceneManager!=null)NetworkManager.SceneManager.OnSceneEvent-=SceneEvent;RestoreStaging();roster.Changed-=MembershipChanged;voting.SelectionReady-=MapSelected;
   Stage.OnValueChanged-=StageChanged;Map.OnValueChanged-=MapChanged;Deadline.OnValueChanged-=TimeChanged;Failure.OnValueChanged-=FailureChanged;Participants.OnListChanged-=ParticipantsChanged;
   if(loadingScreen){loadingScreen.DisposeView();Destroy(loadingScreen);}if(view){view.DisposeView();Destroy(view);}RestoreMenu();if(Active==this)Active=null;Changed=null;base.OnNetworkDespawn();
  }
 }
}
