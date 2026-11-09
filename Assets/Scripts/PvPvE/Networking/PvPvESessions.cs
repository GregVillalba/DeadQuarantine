using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;

namespace DeadQuarantine.PvPvE.Networking
{
    public interface IPvPvESession
    {
        string Id {get;}
        bool IsHost {get;}
        int ParticipantCount {get;}
        Task CloseAdmissionAsync();
        event Action Closed;
        Task LeaveAsync();
    }
    public interface IPvPvEReopenableSession { Task OpenAdmissionAsync(); }
    public interface IPvPvESessions
    {
        Task InitializeAsync();
        Task<IPvPvESession> FindOrCreateAsync(PvPvENetworkSettings settings);
    }
    public sealed class PvPvEUnitySessions : IPvPvESessions
    {
        public async Task InitializeAsync()
        {
            var bootstrap=NetworkBootstrap.Instance;
            if(!bootstrap){var go=new UnityEngine.GameObject("NetworkBootstrap");bootstrap=go.AddComponent<NetworkBootstrap>();}
            if(bootstrap.CurrentSession!=null)throw new InvalidOperationException("Salí de la sala cooperativa antes de buscar una partida PvPvE.");
            if(!await bootstrap.EnsureServicesInitialized())throw new InvalidOperationException("No se pudo conectar con Unity Services. Revisá tu conexión e intentá nuevamente.");
        }
        public static QuickJoinOptions Query(PvPvENetworkSettings settings)=>new QuickJoinOptions {
            Timeout=TimeSpan.FromSeconds(settings.SearchSeconds),CreateSession=true,
            Filters=new List<FilterOption>{new(FilterField.StringIndex1,settings.SessionPool,FilterOperation.Equal),new(FilterField.StringIndex2,settings.Compatibility,FilterOperation.Equal),new(FilterField.AvailableSlots,"0",FilterOperation.Greater),new(FilterField.IsLocked,"false",FilterOperation.Equal)}
        };
        public static SessionOptions Options(PvPvENetworkSettings settings)=>new SessionOptions {
            Type="dq-pvpve",Name="PvPvE Team Deathmatch",MaxPlayers=settings.Capacity,IsPrivate=false,IsLocked=false,
            SessionProperties=new Dictionary<string,SessionProperty>{{"dq_mode",new(settings.SessionPool,VisibilityPropertyOptions.Public,PropertyIndex.String1)},{"dq_protocol",new(settings.Compatibility,VisibilityPropertyOptions.Public,PropertyIndex.String2)}}
        }.WithRelayNetwork();
        public async Task<IPvPvESession> FindOrCreateAsync(PvPvENetworkSettings settings)
        {
            // Services performs an atomic quick-join and retries full/disappearing sessions.
            // If no compatible session exists, it creates a public Relay session automatically.
            var session=await MultiplayerService.Instance.MatchmakeSessionAsync(Query(settings),Options(settings));
            if(session.MaxPlayers!=settings.Capacity || !session.Properties.TryGetValue("dq_mode",out var mode) || mode.Value!=settings.SessionPool || !session.Properties.TryGetValue("dq_protocol",out var protocol) || protocol.Value!=settings.Compatibility) {
                await session.LeaveAsync();throw new InvalidOperationException("La sala recibida no es compatible con esta modalidad.");
            }
            return new UnitySession(session,settings.ShutdownSeconds);
        }
        sealed class UnitySession : IPvPvESession,IPvPvEReopenableSession
        {
            readonly ISession session;
            readonly float shutdownSeconds;
            public string Id=>session.Id;
            public bool IsHost=>session.IsHost;
            public int ParticipantCount=>session.Players.Count;
            public async Task CloseAdmissionAsync(){if(session.IsHost && session.State==SessionState.Connected){var host=session.AsHost();host.IsLocked=true;await host.SavePropertiesAsync();}}
            public async Task OpenAdmissionAsync(){if(session.IsHost&&session.State==SessionState.Connected){var host=session.AsHost();host.IsLocked=false;await host.SavePropertiesAsync();}}
            public event Action Closed;
            public UnitySession(ISession value,float timeout){session=value;shutdownSeconds=timeout;session.RemovedFromSession+=Removed;session.StateChanged+=StateChanged;}
            void Removed()=>Closed?.Invoke();
            void StateChanged(SessionState state){if(state==SessionState.Deleted||state==SessionState.Disconnected)Closed?.Invoke();}
            public async Task LeaveAsync()
            {
                session.RemovedFromSession-=Removed;session.StateChanged-=StateChanged;
                // Ending the host's room prevents implicit migration to an unprepared new host.
                try {if(session.State!=SessionState.Deleted && session.State!=SessionState.Disconnected){if(session.IsHost)await session.AsHost().DeleteAsync();else await session.LeaveAsync();}}
                catch(SessionException e) when(e.Error==SessionError.SessionNotFound || session.State==SessionState.Deleted || session.State==SessionState.Disconnected) { /* The remote host already removed this session. */ }
                // RemovedFromSession invokes asynchronous SDK cleanup. Do not reuse this Type
                // or restore its NetworkManager while that cleanup still owns the old session.
                var timer=System.Diagnostics.Stopwatch.StartNew();
                while(MultiplayerService.Instance.Sessions.TryGetValue(session.Type,out var registered) && ReferenceEquals(registered,session)) {
                    if(timer.Elapsed.TotalSeconds>shutdownSeconds)throw new TimeoutException("Unity Services todavía está liberando la sala anterior. Reintentá salir.");
                    await Task.Delay(50);
                }
            }
        }
    }
}
