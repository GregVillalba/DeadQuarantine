using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine.SceneManagement;
namespace DeadQuarantine.PvPvE.Networking {
 public sealed partial class PvPvEPreparationFlow {
  bool returnUnloadDone;
  public async Task ReturnToLobbyServerAsync(){
   if(!IsServer||Stage.Value!=PvPvEPrepareStage.Playing)return;
   Stage.Value=PvPvEPrepareStage.ReturningToLobby;returnUnloadDone=false;Deadline.Value=NetworkManager.ServerTime.Time+Settings.MapLoadSeconds;
   try{
    foreach(var client in NetworkManager.ConnectedClients.Values.ToArray())if(client.PlayerObject)client.PlayerObject.Despawn(true);
    var scene=SceneManager.GetSceneByName(Settings.MatchScene);
    if(scene.IsValid()&&scene.isLoaded){
     SceneEventProgressStatus result;
     do{result=NetworkManager.SceneManager.UnloadScene(scene);if(result==SceneEventProgressStatus.SceneEventInProgress)await Task.Delay(100);EnsureReturnActive();}while(result==SceneEventProgressStatus.SceneEventInProgress);
     if(result!=SceneEventProgressStatus.Started)throw new InvalidOperationException("NGO unload: "+result);
     while(!returnUnloadDone){EnsureReturnActive();await Task.Delay(50);}
    }
    EnsureReturnActive();await PvPvEMatchmaking.Active.OpenAdmissionForLobbyAsync();EnsureReturnActive();GetComponent<PvPvECombat>().ResetMatchServer();GetComponent<PvPvETeamDeployment>().ResetLobbyServer();
    Participants.Clear();selection.BeginForMapServer();selection.CloseSelectionServer();Map.Value=ConfiguracionPartidaSeleccionada.Escenario.Ninguno;
    roster.RebalanceWhileWaiting=true;Deadline.Value=0;Failure.Value=default;Stage.Value=PvPvEPrepareStage.Lobby;roster.RebalanceLobbyServer();voting.BeginNextLobbyCycle();
   }catch(Exception){if(!disposed&&IsSpawned)Fail("No se pudo volver al lobby. Salí de la sala y reintentá.");}
  }
  void EnsureReturnActive(){if(disposed||!IsSpawned||Stage.Value!=PvPvEPrepareStage.ReturningToLobby)throw new OperationCanceledException();if(NetworkManager.ServerTime.Time>=Deadline.Value)throw new TimeoutException();}
 }
}
