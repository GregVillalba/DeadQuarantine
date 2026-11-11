using UnityEngine;
using Unity.Netcode;

public class GameStartCurtain : MonoBehaviour
{
    private static GameObject cortinaPersistenteLobby;
    private static Sprite spriteCargaAsignado;

    [SerializeField] private Sprite imagenCargaLobby;

    public static void MostrarCortinaDesdeLobby()
    {
        MostrarCortinaPersistente();
    }

    public static void MostrarCortinaTransicion(string destino)
    {
        if (GestorEscenariosPartida.Instance != null)
            GestorEscenariosPartida.Instance.IniciarCargaMultiplayer();
        GameStartCurtain instancia = FindAnyObjectByType<GameStartCurtain>();
        if (instancia != null)
        {
            instancia.curtainHidden = false;
            instancia.mapaAditivoListoDetectado = false;
        }
        MostrarCortinaPersistente(destino);
    }

    private static void MostrarCortinaPersistente(string destino = null)
    {
        bool pantallaCooperativa = DeadQuarantine.PvPvE.PvPvELoadingScreen.BeginCooperative(
            NetworkBootstrap.Instance != null ? NetworkBootstrap.Instance.CooperativeLoadingSettings : null, destino);
        if (cortinaPersistenteLobby != null)
        {
            UnityEngine.UI.Image fondoExistente =
                cortinaPersistenteLobby.GetComponentInChildren<UnityEngine.UI.Image>(true);
            if (fondoExistente != null)
            {
                if (pantallaCooperativa) fondoExistente.color = Color.clear;
                else AsignarImagenCarga(fondoExistente);
            }
            cortinaPersistenteLobby.SetActive(true);
            return;
        }

        GameObject raiz = new GameObject("CortinaCargaMultiplayer");
        cortinaPersistenteLobby = raiz;
        Object.DontDestroyOnLoad(raiz);

        Canvas canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        UnityEngine.UI.CanvasScaler scaler =
            raiz.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1100f);
        scaler.matchWidthOrHeight = 0.5f;
        raiz.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject fondo = new GameObject("Fondo");
        fondo.transform.SetParent(raiz.transform, false);
        RectTransform rect = fondo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        UnityEngine.UI.Image imagen = fondo.AddComponent<UnityEngine.UI.Image>();
        if (pantallaCooperativa) imagen.color = Color.clear;
        else AsignarImagenCarga(imagen);
        imagen.raycastTarget = true;

        AudioListener.pause = true;
    }

    private static void AsignarImagenCarga(UnityEngine.UI.Image imagen)
    {
        if (spriteCargaAsignado == null)
            spriteCargaAsignado = Resources.Load<Sprite>("ImagenCargaSoldado");

        imagen.sprite = spriteCargaAsignado;
        if (imagen.sprite == null)
        {
            UnityEngine.UI.Image imagenOriginal = BuscarImagenCargaOriginal();
            if (imagenOriginal != null)
                imagen.sprite = imagenOriginal.sprite;
        }
        if (imagen.sprite != null)
        {
            imagen.type = UnityEngine.UI.Image.Type.Simple;
            imagen.preserveAspect = false;
            imagen.color = Color.white;
        }
        else
        {
            Debug.LogError("[GameStartCurtain] Falta asignar Imagen Carga Lobby en el Inspector.");
            imagen.color = Color.black;
        }

    }

    private static UnityEngine.UI.Image BuscarImagenCargaOriginal()
    {
        UnityEngine.UI.Image[] imagenes =
            Object.FindObjectsByType<UnityEngine.UI.Image>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (UnityEngine.UI.Image imagen in imagenes)
        {
            if (imagen != null && imagen.gameObject.name == "LoadingScreen" &&
                imagen.sprite != null)
            {
                return imagen;
            }
        }

        return null;
    }

    public static void CancelarCortinaDesdeLobby()
    {
        DeadQuarantine.PvPvE.PvPvELoadingScreen.CancelCooperative();
        if (cortinaPersistenteLobby != null)
        {
            cortinaPersistenteLobby.SetActive(false);
            Object.Destroy(cortinaPersistenteLobby);
            cortinaPersistenteLobby = null;
        }

        AudioListener.pause = false;
    }

    [Header("Cortina de carga")]
    [SerializeField] private GameObject curtainCanvas;

    [Header("Configuración")]
    [SerializeField] private int expectedPlayerCount = 2;

    private bool curtainHidden;

    private float proximoLog;

    private bool mapaAditivoListoDetectado;

    private void Awake()
    {
        if (imagenCargaLobby != null)
            spriteCargaAsignado = imagenCargaLobby;
        Debug.Log("[GameStartCurtain] Awake");

        OcultarRestosDelLobby();

        if (curtainCanvas != null)
            curtainCanvas.SetActive(!DeadQuarantine.PvPvE.PvPvELoadingScreen.CooperativeActive);

        BloquearJugadorLocalInmediatamente();

    }

    private void OcultarRestosDelLobby()
    {
        // LoadSceneMode.Single debería descargar NetworkLobby, pero durante la
        // transición de Netcode puede coexistir unos cuadros. Se oculta cualquier
        // resto visual antes de mostrar la escena jugable.
        MultiplayerLobbyController[] lobbies =
            FindObjectsByType<MultiplayerLobbyController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (MultiplayerLobbyController lobby in lobbies)
        {
            if (lobby == null)
                continue;

            Canvas canvas = lobby.GetComponentInParent<Canvas>(true);

            if (canvas != null)
                canvas.gameObject.SetActive(false);

            lobby.gameObject.SetActive(false);
        }

        Transform[] all =
            FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (Transform t in all)
        {
            if (t == null)
                continue;

            if (t.name == "salaEsperaPanel" ||
                t.name.StartsWith("LobbyCinematic"))
            {
                t.gameObject.SetActive(false);
            }
        }
    }

    private void BloquearJugadorLocalInmediatamente()
    {
        // Bloquea cualquier Player que ya haya sido creado por el spawner de esta
        // escena mientras terminan de sincronizarse host e invitados.
        foreach (PlayerMovement movimiento in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (movimiento.IsOwner)
                movimiento.MovementLocked = true;
        }

        foreach (PlayerLook look in FindObjectsByType<PlayerLook>(FindObjectsSortMode.None))
        {
            NetworkObject netObj = look.GetComponentInParent<NetworkObject>();

            if (netObj != null && netObj.IsOwner)
                look.enabled = false;
        }

        foreach (WeaponSwitcher switcher in FindObjectsByType<WeaponSwitcher>(FindObjectsSortMode.None))
        {
            NetworkObject netObj = switcher.GetComponentInParent<NetworkObject>();

            if (netObj != null && netObj.IsOwner && switcher.CurrentWeapon != null)
                switcher.CurrentWeapon.InputLocked = true;
        }
    }

    private void Update()
    {
        if (curtainHidden)
            return;


        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            Debug.LogError("[GameStartCurtain] La red no está activa: vuelvo al menú.");
            curtainHidden = true;
            CancelarCortinaDesdeLobby();

            if (NetworkBootstrap.Instance != null)
                _ = NetworkBootstrap.Instance.GoToMenuDueToDisconnect();

            return;
        }
        GestorEscenariosPartida gestor = GestorEscenariosPartida.Instance;
        if (gestor != null && gestor.UsaEscenasAditivasMultiplayer)
        {
            if (!gestor.MapaMultiplayerListo || !gestor.PresentacionLocalPreparada)
                return;

            if (!mapaAditivoListoDetectado)
            {
                mapaAditivoListoDetectado = true;
            }
        }

        if (!TodosListos())
            return;

        OcultarCortina();
    }

    private void LogEstadoJugadores()
    {
        foreach (var a in ObtenerAssignersDePlayersReales())
        {
            Debug.LogWarning(
                $"[GameStartCurtain] clientId={a.OwnerClientId} | " +
                $"spawnIndex={a.AssignedSpawnIndex.Value} | " +
                $"IsAtSpawn={a.IsAtSpawn.Value} | pos={a.transform.position}"
            );
        }
    }

    private MultiplayerPlayerSpawnAssigner[] ObtenerAssignersDePlayersReales()
    {
        MultiplayerPlayerSpawnAssigner[] todos =
            FindObjectsByType<MultiplayerPlayerSpawnAssigner>(
                FindObjectsSortMode.None);

        System.Collections.Generic.List<MultiplayerPlayerSpawnAssigner> reales =
            new System.Collections.Generic.List<MultiplayerPlayerSpawnAssigner>();

        foreach (MultiplayerPlayerSpawnAssigner a in todos)
        {
            if (a == null)
                continue;

            NetworkObject no = a.GetComponent<NetworkObject>();

            // MainSceneMultiPlayer tiene un NetworkObject de escena llamado
            // "multiplayer" con este componente, pero NO es un PlayerObject.
            if (no == null || !no.IsPlayerObject)
                continue;

            reales.Add(a);
        }

        return reales.ToArray();
    }

    private bool TodosListos()
    {
        // FindObjectsByType SÍ funciona igual en Host y en Cliente,
        // porque cada Player es un NetworkObject spawneado y visible
        // localmente en todas las máquinas — a diferencia de
        // ConnectedClientsList, que solo está completa en el servidor.
       /* MultiplayerPlayerSpawnAssigner[] assigners =
            FindObjectsByType<MultiplayerPlayerSpawnAssigner>(FindObjectsSortMode.None);

        if (assigners.Length < jugadoresEsperados)
            return false;

        foreach (MultiplayerPlayerSpawnAssigner assigner in assigners)
        {
            if (!assigner.IsAtSpawn.Value)
                return false;
        }

        if (Time.time >= proximoLog)
        {
            proximoLog = Time.time + 2f;

            string estados = "";

            foreach (MultiplayerPlayerSpawnAssigner a in assigners)
                estados += a.IsAtSpawn.Value + " ";

            Debug.Log(
                "[Curtain] Jugadores: " + assigners.Length + "/" + jugadoresEsperados +
                " | IsAtSpawn: " + estados
            );

        }*/

        MultiplayerPlayerSpawnAssigner[] assigners =
            ObtenerAssignersDePlayersReales();

        int jugadoresEsperados = expectedPlayerCount;

        if (NetworkBootstrap.Instance != null &&
            NetworkBootstrap.Instance.PlayerCount > 0)
        {
            jugadoresEsperados = NetworkBootstrap.Instance.PlayerCount;
        }

        // Log de diagnóstico: va ANTES de los return para verse siempre.
        if (Time.unscaledTime >= proximoLog)
        {
            proximoLog = Time.unscaledTime + 2f;

            string estados = "";

            foreach (MultiplayerPlayerSpawnAssigner a in assigners)
                estados += a.IsAtSpawn.Value + " ";

            Debug.Log(
                "[Curtain] Jugadores: " + assigners.Length + "/" + jugadoresEsperados +
                " | IsAtSpawn: " + estados
            );
        }

        if (assigners.Length < jugadoresEsperados)
            return false;

        foreach (MultiplayerPlayerSpawnAssigner assigner in assigners)
        {
            if (!assigner.IsAtSpawn.Value)
                return false;
        }

        return true;

       // return true;
    }

    private void OcultarCortina()
    {
        Debug.Log("[GameStartCurtain] OcultarCortina() ejecutado.");

        curtainHidden = true;
        DeadQuarantine.PvPvE.PvPvELoadingScreen.CancelCooperative();

        if (curtainCanvas != null)
            curtainCanvas.SetActive(false);

        if (cortinaPersistenteLobby != null)
        {
            cortinaPersistenteLobby.SetActive(false);
            Object.Destroy(cortinaPersistenteLobby);
            cortinaPersistenteLobby = null;
        }

        AudioListener.pause = false;

        NetworkObject jugadorLocal = NetworkManager.Singleton != null &&
            NetworkManager.Singleton.LocalClient != null
            ? NetworkManager.Singleton.LocalClient.PlayerObject : null;
        StoryIntroController intro = jugadorLocal != null
            ? jugadorLocal.GetComponentInChildren<StoryIntroController>(true) : null;
        if (intro == null || !intro.HistoriaActiva)
        {
            RevelarHUDParaJugadorLocal();
            DesbloquearJugadorLocal();
        }
    }

    private void DesbloquearJugadorLocal()
    {
        PlayerMovement[] movimientos = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        Debug.Log("[GameStartCurtain] PlayerMovement encontrados: " + movimientos.Length);

        foreach (PlayerMovement movimiento in movimientos)
        {
            if (movimiento.IsOwner)
            {
                movimiento.MovementLocked = false;
                Debug.Log("[GameStartCurtain] Movimiento desbloqueado.");
                break;
            }
        }

        PlayerLook[] looks = FindObjectsByType<PlayerLook>(FindObjectsSortMode.None);
        Debug.Log("[GameStartCurtain] PlayerLook encontrados: " + looks.Length);

        foreach (PlayerLook look in looks)
        {
            NetworkObject netObj = look.GetComponentInParent<NetworkObject>();

            if (netObj != null && netObj.IsOwner)
            {
                look.enabled = true;
                Debug.Log("[GameStartCurtain] Cámara desbloqueada. enabled = " + look.enabled);
                break;
            }
        }

        WeaponSwitcher[] switchers = FindObjectsByType<WeaponSwitcher>(FindObjectsSortMode.None);
        Debug.Log("[GameStartCurtain] WeaponSwitcher encontrados: " + switchers.Length);

        foreach (WeaponSwitcher switcher in switchers)
        {
            NetworkObject netObj = switcher.GetComponentInParent<NetworkObject>();

            if (netObj != null && netObj.IsOwner && switcher.CurrentWeapon != null)
            {
                switcher.CurrentWeapon.InputLocked = false;
                Debug.Log("[GameStartCurtain] Arma desbloqueada. InputLocked = " + switcher.CurrentWeapon.InputLocked);
                break;
            }
        }
    }

    private void RevelarHUDParaJugadorLocal()
    {
        PauseController[] controllers =
            FindObjectsByType<PauseController>(FindObjectsSortMode.None);

        foreach (PauseController controller in controllers)
        {
            if (controller.IsOwner)
            {
                controller.MostrarHUD();
                break;
            }
        }
    }
}
