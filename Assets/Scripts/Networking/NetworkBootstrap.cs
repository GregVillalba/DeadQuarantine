using System;
using System.Threading.Tasks;

using Unity.Netcode;

using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkBootstrap : MonoBehaviour
{
    [SerializeField] private int maxConnections = 2;

    public static NetworkBootstrap Instance { get; private set; }

    public string CurrentJoinCode { get; private set; }

    public bool IsHost =>
        currentSession != null && currentSession.IsHost;

    public int PlayerCount =>
        currentSession != null
            ? currentSession.Players.Count
            : 0;

    private ISession currentSession;

    public ISession CurrentSession => currentSession;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        await EnsureServicesInitialized();
    }

    private async Task EnsureServicesInitialized()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance
                    .SignInAnonymouslyAsync();
            }

            Debug.Log(
                "[Network] Servicios listos. PlayerId: " +
                AuthenticationService.Instance.PlayerId
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Network] Error inicializando servicios: " +
                e.Message
            );
        }
    }

    // ============================================================
    // CREAR SALA
    // ============================================================

    public async Task<string> StartHost()
    {
        try
        {
            // IMPORTANTE:
            // No ponemos WithRelayNetwork() acá.
            // La sesión se crea primero y Relay se inicia
            // cuando ambos jugadores estén listos.

            SessionOptions options = new SessionOptions
            {
                MaxPlayers = maxConnections
            };

            currentSession =
                await MultiplayerService.Instance
                    .CreateSessionAsync(options);

            CurrentJoinCode = currentSession.Code;

            Debug.Log(
                "[Network] Sala creada. Código: " +
                CurrentJoinCode
            );

            return CurrentJoinCode;
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Network] Error creando sala: " +
                e.Message
            );

            return null;
        }
    }

    // ============================================================
    // UNIRSE
    // ============================================================

    public async Task<bool> StartClient(string joinCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                Debug.LogError(
                    "[Network] Código vacío."
                );

                return false;
            }

            currentSession =
                await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(joinCode);

            CurrentJoinCode = joinCode;

            Debug.Log(
                "[Network] Unido a la sala: " +
                joinCode
            );

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Network] Error al unirse: " +
                e.Message
            );

            return false;
        }
    }

    // ============================================================
    // JUGADORES
    // ============================================================

    public bool BothPlayersConnected()
    {
        return currentSession != null &&
               currentSession.Players.Count >= 2;
    }

    // ============================================================
    // READY
    // ============================================================

    public async Task SetReady(bool ready)
    {
        if (currentSession == null)
            return;

        // Usamos el constructor simple para evitar
        // el problema con VisibilityOptions.

        PlayerProperty property =
            new PlayerProperty(
                ready ? "true" : "false"
            );

        currentSession.CurrentPlayer.SetProperty(
            "ready",
            property
        );

        await currentSession.SaveCurrentPlayerDataAsync();

        Debug.Log(
            "[Network] Ready = " + ready
        );
    }

    public bool IsPlayerReady(IReadOnlyPlayer player)
    {
        if (player.Properties.TryGetValue(
            "ready",
            out PlayerProperty property))
        {
            return property.Value == "true";
        }

        return false;
    }

    public bool BothPlayersReady()
    {
        if (currentSession == null)
            return false;

        if (currentSession.Players.Count < 2)
            return false;

        foreach (var player in currentSession.Players)
        {
            if (!IsPlayerReady(player))
                return false;
        }

        return true;
    }

    // ============================================================
    // EMPEZAR PARTIDA
    // ============================================================

    public async Task StartMultiplayerGame()
    {
        if (!IsHost)
        {
            Debug.LogWarning(
                "[Network] Solo el Host puede comenzar."
            );

            return;
        }

        if (!BothPlayersConnected())
        {
            Debug.LogWarning(
                "[Network] No hay dos jugadores."
            );

            return;
        }

        if (!BothPlayersReady())
        {
            Debug.LogWarning(
                "[Network] Los dos jugadores no están listos."
            );

            return;
        }

        Debug.Log(
            "[Network] Ambos jugadores listos."
        );

        try
        {
            /*
             * En esta versión de Multiplayer Services,
             * StartRelayNetworkAsync requiere RelayNetworkOptions.
             *
             * Usamos la configuración por defecto.
             */

            await currentSession
                .AsHost()
                .Network
                .StartRelayNetworkAsync(
                    new RelayNetworkOptions()
                );

            Debug.Log(
                "[Network] Relay iniciado."
            );

            await Task.Yield();

            Debug.Log(
                "[Network] Cargando MainSceneMultiPlayer..."
            );

            NetworkManager.Singleton.SceneManager.LoadScene(
                "MainSceneMultiPlayer",
                LoadSceneMode.Single
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Network] Error iniciando partida: " +
                e.Message
            );
        }
    }

    // ============================================================
    // SALIR
    // ============================================================

    public async Task LeaveSession()
    {
        try
        {
            if (currentSession != null)
            {
                await currentSession.LeaveAsync();
                currentSession = null;
            }

            CurrentJoinCode = null;

            Debug.Log(
                "[Network] Sesión abandonada."
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Network] Error saliendo de la sesión: " +
                e.Message
            );
        }
    }
}