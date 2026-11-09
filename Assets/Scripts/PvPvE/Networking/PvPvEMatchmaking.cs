using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace DeadQuarantine.PvPvE.Networking
{
    public enum PvPvEConnectionState { Idle,Searching,Connecting,Waiting,Leaving,Failed }

    public sealed class PvPvEMatchmaking : MonoBehaviour
    {
        public static PvPvEMatchmaking Active {get;private set;}
        [SerializeField] PvPvENetworkSettings settings;
        public PvPvENetworkSettings Settings=>settings;
        public PvPvEConnectionState State {get;private set;}
        public string Error {get;private set;}="";
        public string SessionId=>session?.Id;
        public bool IsHost=>session!=null && session.IsHost;
        public NetworkManager Network=>scope?.Network;
        public PvPvETeamRoster Roster {get;private set;}
        public bool CanNavigateAway=>scope==null && session==null && !InProgress(searchTask) && !InProgress(leaveTask);
        public event Action Changed;
        IPvPvESessions directory;
        IPvPvESession session;
        PvPvENetworkScope scope;
        CancellationTokenSource cancellation;
        Task searchTask,leaveTask,closingSession;
        bool networkFailed,quitPending,quitAllowed;
        static bool InProgress(Task task)=>task!=null&&!task.IsCompleted;
        public static bool Owns(NetworkManager manager)=>Active && manager && Active.Network==manager;

        void Awake()
        {
            Active=this;directory=new PvPvEUnitySessions();
            PvPvETeamRoster.ActiveChanged+=RosterChanged;
            Application.wantsToQuit+=CheckQuit;
        }
        public Task SearchAsync()
        {
            if(InProgress(searchTask))return searchTask;
            if(InProgress(leaveTask)||scope!=null||session!=null)return leaveTask??Task.CompletedTask;
            cancellation?.Dispose();cancellation=new CancellationTokenSource();networkFailed=false;Error="";
            GetComponent<DeadQuarantine.PvPvE.UI.PvPvELobbyView>()?.BeginSearching();
            SetState(PvPvEConnectionState.Searching);
            searchTask=SearchCoreAsync(cancellation.Token);_ = NotifyAfterAsync(searchTask);return searchTask;
        }
        async Task SearchCoreAsync(CancellationToken token)
        {
            try {
                await directory.InitializeAsync();token.ThrowIfCancellationRequested();
                scope=new PvPvENetworkScope(settings,PlayerIdentity.GetUsername());
                scope.LocalDisconnected+=LostConnection;scope.TransportFailed+=LostConnection;
                // SDK quick-join has no cancellation-token overload. Keep this operation owned
                // until its response arrives, then leave any late result before accepting another search.
                session=await directory.FindOrCreateAsync(settings);
                session.Closed+=LostConnection;token.ThrowIfCancellationRequested();
                SetState(PvPvEConnectionState.Connecting);
                float end=Time.realtimeSinceStartup+settings.ConnectionSeconds;
                RosterChanged();
                while(!Network || !Network.IsConnectedClient || !Roster || !Roster.Contains(Network.LocalClientId)) {
                    token.ThrowIfCancellationRequested();
                    if(networkFailed)throw new InvalidOperationException("La conexión con la sala se interrumpió.");
                    if(Time.realtimeSinceStartup>end)throw new TimeoutException("La sala no respondió a tiempo. Intentá buscar nuevamente.");
                    await Task.Delay(50);
                }
                token.ThrowIfCancellationRequested();if(Roster.Closing.Value)throw new InvalidOperationException("La sala se está cerrando. Volvé a buscar una partida.");SetState(PvPvEConnectionState.Waiting);
            }
            catch(OperationCanceledException){if(await TryCleanupAsync())SetState(PvPvEConnectionState.Idle);}
            catch(Exception e){
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(e);
#endif
                await TryCleanupAsync();Error=string.IsNullOrEmpty(Error)?"No se pudo entrar a una partida. "+e.Message:Error;SetState(PvPvEConnectionState.Failed);}
        }
        public Task CloseAdmissionForMapAsync(){if(scope==null||session==null||!session.IsHost)throw new InvalidOperationException("No hay una sala autoritativa activa.");scope.StopAccepting();return session.CloseAdmissionAsync();}
        public async Task OpenAdmissionForLobbyAsync(){
            if(scope==null||session==null||!session.IsHost)throw new InvalidOperationException("No hay una sala activa.");
            if(session is IPvPvEReopenableSession reopen){var task=reopen.OpenAdmissionAsync();if(await Task.WhenAny(task,Task.Delay(TimeSpan.FromSeconds(settings.ConnectionSeconds)))!=task)throw new TimeoutException("La sala no pudo reabrirse.");await task;}
            scope.StartAccepting();
        }
        public Task CancelAndLeaveAsync()
        {
            if(InProgress(leaveTask))return leaveTask;
            leaveTask=LeaveCoreAsync();_ = NotifyAfterAsync(leaveTask);return leaveTask;
        }
        async Task LeaveCoreAsync()
        {
            cancellation?.Cancel();SetState(PvPvEConnectionState.Leaving);
            var combat=Roster?Roster.GetComponent<PvPvECombat>():null;
            if(combat&&Network&&Network.IsServer&&combat.PrepareHostDepartureServer())await Task.Delay(TimeSpan.FromSeconds(Math.Max(.5,settings.ResultsSeconds)));
            if(InProgress(searchTask))await searchTask;
            if(await TryCleanupAsync()){Error="";SetState(PvPvEConnectionState.Idle);}
        }
        async Task<bool> TryCleanupAsync()
        {
            try {
                if(scope!=null){scope.StopAccepting();scope.LocalDisconnected-=LostConnection;scope.TransportFailed-=LostConnection;}
                if(session!=null) {
                    session.Closed-=LostConnection;
                    if(session.IsHost && Roster && scope!=null && scope.Network && scope.Network.IsServer) {
                        await session.CloseAdmissionAsync();Roster.BeginClose();float closeDeadline=Time.realtimeSinceStartup+settings.ShutdownSeconds;
                        while(scope.Network && scope.Network.IsListening && (scope.Network.ConnectedClients.Count>1 || session.ParticipantCount>1) && Time.realtimeSinceStartup<closeDeadline)await Task.Delay(50);
                    }
                    closingSession??=session.LeaveAsync();
                    if(await Task.WhenAny(closingSession,Task.Delay(TimeSpan.FromSeconds(settings.ShutdownSeconds)))!=closingSession) {
                        if(scope!=null){await scope.StopAndRestoreAsync();scope=null;}
                        if(await Task.WhenAny(closingSession,Task.Delay(1000))!=closingSession)throw new TimeoutException("La sala todavía se está cerrando. Reintentá salir.");
                    }
                    await closingSession;session=null;closingSession=null;
                }
                if(scope!=null){await scope.StopAndRestoreAsync();scope=null;}
                await PvPvEMapContext.UnloadAfterLeaveAsync(settings.MatchScene);DetachRoster();return true;
            }
            catch(Exception e){if(closingSession?.IsFaulted==true)closingSession=null;Error="No se pudo cerrar la conexión. "+e.Message;SetState(PvPvEConnectionState.Failed);return false;}
        }
        void LostConnection()
        {
            networkFailed=true;
            if(State==PvPvEConnectionState.Waiting)_=HandleDisconnectAsync();
        }
        async Task HandleDisconnectAsync(){bool completed=Roster&&Roster.GetComponent<PvPvECombat>()&&Roster.GetComponent<PvPvECombat>().ScoreLocked.Value;await CancelAndLeaveAsync();if(CanNavigateAway){Error=completed?"Partida finalizada. Estás en el lobby; podés volver a buscar.":"Se perdió la conexión con la sala. Podés volver a buscar.";GetComponent<DeadQuarantine.PvPvE.UI.PvPvELobbyView>()?.BeginSearching();}SetState(PvPvEConnectionState.Failed);}
        void RosterChanged()
        {
            DetachRoster();var active=PvPvETeamRoster.Active;
            if(active && scope!=null && active.NetworkManager==scope.Network){Roster=active;Roster.Changed+=RosterUpdated;var flow=Roster.GetComponent<PvPvEPreparationFlow>();if(flow)flow.Changed+=Notify;}
            Notify();
        }
        void RosterUpdated(){if(Roster && Roster.Closing.Value && Network && !Network.IsServer && State==PvPvEConnectionState.Waiting)LostConnection();Notify();}
        void DetachRoster(){if(Roster){Roster.Changed-=RosterUpdated;var flow=Roster.GetComponent<PvPvEPreparationFlow>();if(flow)flow.Changed-=Notify;}Roster=null;}
        async Task NotifyAfterAsync(Task task){await task;await Task.Yield();Notify();}
        void Notify()=>Changed?.Invoke();
        void SetState(PvPvEConnectionState state){State=state;Notify();}
        bool CheckQuit(){if(quitAllowed||CanNavigateAway)return true;if(!quitPending){quitPending=true;_=QuitAsync();}return false;}
        async Task QuitAsync(){await Task.WhenAny(CancelAndLeaveAsync(),Task.Delay(TimeSpan.FromSeconds(settings.ShutdownSeconds)));quitAllowed=true;Application.Quit();}
        void OnDestroy()
        {
            Application.wantsToQuit-=CheckQuit;PvPvETeamRoster.ActiveChanged-=RosterChanged;DetachRoster();Changed=null;
            if(Active==this)Active=null;cancellation?.Cancel();
            if(scope!=null||session!=null||InProgress(searchTask))_=CancelAndLeaveAsync();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Explicit development-only injection; production UI always uses Unity Services + Relay.
        public void ConfigureForTest(PvPvENetworkSettings config,IPvPvESessions sessions){if(!CanNavigateAway)throw new InvalidOperationException("Connection already active");settings=config;directory=sessions;}
#endif
    }
}
