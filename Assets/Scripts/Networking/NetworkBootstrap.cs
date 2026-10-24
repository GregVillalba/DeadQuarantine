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
    // Máximo de jugadores por sala. Es una constante (y no un campo del Inspector) a propósito: la escena tenía guardado
    // "maxConnections: 2" y eso pisaba el valor del script.
    public const int MaxPlayersPerRoom = 4;

    // Propiedad de sesión con la que el anfitrión avisa que cierra la sala.
    public const string CerradaProperty = "SalaCerrada";

    public enum JoinFailReason { None, SessionFull, NotFound, Other }

    /// <summary>Por qué falló el último StartClient (para mostrar "Sala llena", etc.).</summary>
    public JoinFailReason LastJoinFailReason { get; private set; }

    public static NetworkBootstrap Instance { get; private set; }

    public string CurrentJoinCode { get; private set; }

    public event Action OnGuestLeft; // lo recibe el HOST cuando el invitado se va
    public event Action OnHostLeft;  // lo recibe el INVITADO cuando el host se va

    public bool IsHost =>
        currentSession != null && currentSession.IsHost;

    public int PlayerCount =>
        currentSession != null
            ? currentSession.Players.Count
            : 0;

    private ISession currentSession;

    public ISession CurrentSession => currentSession;

    private bool isIntentionalLeave;


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

        // Que el latido de la sala siga aunque la ventana pierda el foco (si no, parecería que el jugador se fue).
        Application.runInBackground = true;

        // Al cerrar el juego, salir de la sala antes (si no, el jugador queda "fantasma" en la sala).
        Application.wantsToQuit += OnWantsToQuit;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Application.wantsToQuit -= OnWantsToQuit;
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

   // private async Task EnsureServicesInitialized()
   public async Task<bool> EnsureServicesInitialized() 
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
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Network] Error inicializando servicios: " +
                e.Message
            );
            return false;
        }
    }


    // ============================================================
    // CREAR SALA
    // ============================================================

    public async Task<string> StartHost()
    {
        isIntentionalLeave = false;

        if (!await EnsureServicesInitialized())
            return null;

        try
        {
            SessionOptions options =
                new SessionOptions
                {
                    MaxPlayers = MaxPlayersPerRoom
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

            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

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
        isIntentionalLeave = false;
        LastJoinFailReason = JoinFailReason.None;

        if (!await EnsureServicesInitialized())
        {
            LastJoinFailReason = JoinFailReason.Other;
            return false;
        }

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
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Network] Error al unirse: " +
                e.Message
            );

            LastJoinFailReason = Classify(e);

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
            // ESPERAR A QUE LOS INVITADOS SE CONECTEN A NETCODE
            // ========================================================
            // Si se carga la escena antes de que el invitado termine de
            // conectarse, la recibe dos veces ("Server Scene Handle already exist").

            int jugadoresEsperados = currentSession.Players.Count;
            float esperaClientes = 0f;

            while (
                NetworkManager.Singleton.ConnectedClientsIds.Count <
                jugadoresEsperados
            )
            {
                await Task.Delay(100);

                esperaClientes += 0.1f;

                if (esperaClientes >= 15f)
                {
                    // Si ya hay al menos 2 conectados se sigue con los que están (alguien pudo haberse ido).
                    if (NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
                    {
                        Debug.LogWarning(
                            "[Network] No todos los invitados se conectaron: sigo con los que están."
                        );

                        break;
                    }

                    Debug.LogError(
                        "[Network] Los invitados no se conectaron a tiempo."
                    );

                    return;
                }
            }

            // Margen para que el invitado termine de sincronizar la escena actual.
            await Task.Delay(1500);


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

   /* private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return;

        // Solo nos interesa cuando el que se desconectó somos nosotros mismos
        // (por ejemplo, porque el Host cerró la partida).
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        if (isIntentionalLeave)
        {
            isIntentionalLeave = false;
            return;
        }

        Debug.LogWarning("[Network] Se perdió la conexión con el Host. Volviendo al menú.");

        _ = GoToMenuDueToDisconnect();
    }*/

    private void OnClientDisconnected(ulong clientId)
{
    if (NetworkManager.Singleton == null)
        return;

    // HOST: se fue otro jugador
    if (IsHost)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId && !isIntentionalLeave)
            OnGuestLeft?.Invoke();

        return;
    }

    // INVITADO: solo nos interesa cuando el desconectado somos nosotros
    if (clientId != NetworkManager.Singleton.LocalClientId)
        return;

    if (isIntentionalLeave)
    {
        isIntentionalLeave = false;
        return;
    }

    Debug.LogWarning("[Network] Se perdió la conexión con el Host.");

    // Si hay un cartel escuchando, él decide cuándo salir; si no, vuelve al menú como antes.
    if (OnHostLeft != null)
        OnHostLeft.Invoke();
    else
        _ = GoToMenuDueToDisconnect();
}

    //private async Task GoToMenuDueToDisconnect()
    public async Task GoToMenuDueToDisconnect()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        try
        {
            if (currentSession != null)
                await currentSession.LeaveAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Network] Error al abandonar la sesión tras desconexión: " + e.Message);
        }

        currentSession = null;
        CurrentJoinCode = null;

        SceneManager.LoadScene("PantallasUI");
    }

    // ============================================================
    // CERRAR SALA (HOST) Y CERRAR EL JUEGO
    // ============================================================

    /// <summary>
    /// El anfitrión cierra la sala: primero avisa a los demás (propiedad "SalaCerrada"), les da un instante para
    /// enterarse y después borra la sesión.
    /// </summary>
    public async Task CerrarSalaComoHost()
    {
        if (currentSession == null || !currentSession.IsHost)
            return;

        try
        {
            var host = currentSession.AsHost();

            host.SetProperty(CerradaProperty, new SessionProperty("1"));
            await host.SavePropertiesAsync();

            await Task.Delay(1200);

            await host.DeleteAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Network] Error cerrando la sala: " + e.Message);
        }

        currentSession = null;
        CurrentJoinCode = null;
    }

    private bool quitAllowed;
    private bool quitInProgress;

    // Se llama cuando se intenta cerrar el juego (o salir de Play en el editor).
    private bool OnWantsToQuit()
    {
        if (quitAllowed || currentSession == null)
            return true;

        if (!quitInProgress)
        {
            quitInProgress = true;
            _ = LeaveThenQuit();
        }

        return false; // se pospone el cierre hasta haber salido de la sala
    }

    private async Task LeaveThenQuit()
    {
        try
        {
            Task salir = IsHost
                ? CerrarSalaComoHost()
                : currentSession.LeaveAsync();

            // Tope de 4 segundos para no trabar el cierre si no hay internet.
            await Task.WhenAny(salir, Task.Delay(4000));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Network] Error saliendo de la sala al cerrar: " + e.Message);
        }

        currentSession = null;
        quitAllowed = true;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Traduce el error de unirse a una causa entendible (sala llena, no existe...).
    private static JoinFailReason Classify(Exception e)
    {
        string text = Describe(e);

        if (text.Contains("full"))
            return JoinFailReason.SessionFull;

        if (text.Contains("not found") || text.Contains("notfound") || text.Contains("404"))
            return JoinFailReason.NotFound;

        return JoinFailReason.Other;
    }

    private static string Describe(Exception e)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (Exception ex = e; ex != null; ex = ex.InnerException)
        {
            sb.Append(ex.GetType().Name).Append(' ').Append(ex.Message).Append(' ');

            // SessionException.Error / LobbyServiceException.Reason traen la causa exacta (ej: SessionFull, LobbyFull).
            foreach (string propName in new[] { "Error", "Reason", "ErrorCode" })
            {
                var prop = ex.GetType().GetProperty(propName);

                if (prop == null)
                    continue;

                object value = prop.GetValue(ex);

                if (value != null)
                    sb.Append(value).Append(' ');
            }
        }

        return sb.ToString().ToLowerInvariant();
    }

    public void NotifyIntentionalLeave()
    {
        isIntentionalLeave = true;
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