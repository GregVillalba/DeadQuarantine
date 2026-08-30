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


    // ============================================================
    // AWAKE
    // ============================================================

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


    // ============================================================
    // START
    // ============================================================

    private async void Start()
    {
        await EnsureServicesInitialized();
    }


    // ============================================================
    // INICIALIZAR UNITY SERVICES
    // ============================================================

    private async Task EnsureServicesInitialized()
    {
        try
        {
            if (UnityServices.State !=
                ServicesInitializationState.Initialized)
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
            SessionOptions options =
                new SessionOptions
                {
                    MaxPlayers = maxConnections
                };

            currentSession =
                await MultiplayerService.Instance
                    .CreateSessionAsync(options);

            currentSession.PlayerJoined += OnPlayerJoined;
            currentSession.PlayerLeaving += OnPlayerLeft;

            CurrentJoinCode =
                currentSession.Code;

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

            currentSession.PlayerJoined += OnPlayerJoined;
            currentSession.PlayerLeaving += OnPlayerLeft;

            /*
             * IMPORTANTE:
             * Usamos el código REAL de la sesión.
             */
            CurrentJoinCode =
                currentSession.Code;

            Debug.Log(
                "[Network] Unido a la sala: " +
                CurrentJoinCode
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
    // EVENTOS DE JUGADORES
    // ============================================================

    private void OnPlayerJoined(string playerId)
    {
        Debug.Log(
            "[Network] PLAYER JOINED: " +
            playerId
        );

        Debug.Log(
            "[Network] Jugadores actuales: " +
            currentSession.Players.Count
        );
    }


    private void OnPlayerLeft(string playerId)
    {
        Debug.Log(
            "[Network] PLAYER LEFT: " +
            playerId
        );

        Debug.Log(
            "[Network] Jugadores actuales: " +
            currentSession.Players.Count
        );
    }


    // ============================================================
    // EMPEZAR PARTIDA
    // ============================================================

    public async Task StartMultiplayerGame()
    {
        Debug.Log(
            "[Network] ===== START MULTIPLAYER GAME ====="
        );

        // ============================================================
        // VALIDACIONES
        // ============================================================

        if (currentSession == null)
        {
            Debug.LogError(
                "[Network] currentSession es NULL."
            );

            return;
        }

        if (!currentSession.IsHost)
        {
            Debug.LogError(
                "[Network] Esta instancia no es el Host."
            );

            return;
        }

        if (!BothPlayersConnected())
        {
            Debug.LogError(
                "[Network] No hay dos jugadores."
            );

            return;
        }

        Debug.Log(
            "[Network] Validaciones correctas."
        );

        try
        {
            // ========================================================
            // ESTADO INICIAL DE LA RED
            // ========================================================

            Debug.Log(
                "[Network] Estado de red antes de Relay: " +
                currentSession.Network.State
            );


            // ========================================================
            // EVENTOS DE RED
            // ========================================================

            currentSession.Network.StateChanged += OnNetworkStateChanged;
            currentSession.Network.StartFailed += OnNetworkStartFailed;


            // ========================================================
            // INICIAR RELAY
            // ========================================================

            Debug.Log(
                "[Network] >>> INICIANDO RELAY <<<"
            );

            Task relayTask =
                currentSession
                    .AsHost()
                    .Network
                    .StartRelayNetworkAsync(
                        new RelayNetworkOptions()
                    );


            // Esperamos a que StartRelayNetworkAsync termine.
            await relayTask;


            Debug.Log(
                "[Network] >>> StartRelayNetworkAsync TERMINÓ <<<"
            );

            Debug.Log(
                "[Network] Estado de red: " +
                currentSession.Network.State
            );


            // ========================================================
            // ESPERAR A QUE LA RED ESTÉ STARTED
            // ========================================================

            float tiempoEsperado = 0f;

            while (
                currentSession.Network.State !=
                NetworkState.Started
            )
            {
                await Task.Delay(100);

                tiempoEsperado += 0.1f;

                Debug.Log(
                    "[Network] Estado de red: " +
                    currentSession.Network.State
                );

                if (tiempoEsperado >= 10f)
                {
                    Debug.LogError(
                        "[Network] La red no llegó a NetworkState.Started."
                    );

                    return;
                }
            }


            Debug.Log(
                "[Network] >>> RED INICIADA CORRECTAMENTE <<<"
            );


            // ========================================================
            // NETWORK MANAGER
            // ========================================================

            if (NetworkManager.Singleton == null)
            {
                Debug.LogError(
                    "[Network] NetworkManager.Singleton es NULL."
                );

                return;
            }

            Debug.Log(
                "[Network] NetworkManager encontrado."
            );

            Debug.Log(
                "[Network] IsListening: " +
                NetworkManager.Singleton.IsListening
            );


            // ========================================================
            // CARGAR ESCENA
            // ========================================================

            Debug.Log(
                "[Network] >>> CARGANDO MainSceneMultiPlayer <<<"
            );

            var resultado =
                NetworkManager.Singleton.SceneManager.LoadScene(
                    "MainSceneMultiPlayer",
                    LoadSceneMode.Single
                );

            Debug.Log(
                "[Network] Resultado LoadScene: " +
                resultado
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Network] EXCEPCIÓN EN StartMultiplayerGame:"
            );

            Debug.LogException(e);
        }
    }


    private void OnNetworkStateChanged(NetworkState state)
    {
        Debug.Log(
            "[Network] NetworkState cambió a: " +
            state
        );
    }

    private void OnNetworkStartFailed(SessionError error)
    {
        Debug.LogError(
            "[Network] ERROR iniciando Network: " +
            error
        );
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