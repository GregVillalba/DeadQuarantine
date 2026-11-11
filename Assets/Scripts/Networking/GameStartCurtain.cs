using UnityEngine;
using Unity.Netcode;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;
using static DeadQuarantine.PvPvE.PvPvEUIFactory;

public class GameStartCurtain : MonoBehaviour
{
    private static GameObject cortinaPersistenteLobby;
    private static Sprite spriteCargaAsignado;
    private static Image fondoPersistente;
    private static Button botonSalir;
    private static TextMeshProUGUI etiquetaSalir;
    private static TextMeshProUGUI avisoCarga;
    private static bool anfitrionDesconectado;
    private static bool saliendoAlMenu;

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
            instancia.BloquearJugadorLocalInmediatamente();
        }
        MostrarCortinaPersistente(destino);
    }

    private static void MostrarCortinaPersistente(string destino = null)
    {
        if (anfitrionDesconectado || NetworkBootstrap.Instance?.IsLeavingIntentionally == true)
            return;

        bool pantallaCooperativa = DeadQuarantine.PvPvE.PvPvELoadingScreen.BeginCooperative(
            NetworkBootstrap.Instance != null ? NetworkBootstrap.Instance.CooperativeLoadingSettings : null, destino);
        if (cortinaPersistenteLobby != null)
        {
            if (pantallaCooperativa) fondoPersistente.color = Color.clear;
            else AsignarImagenCarga(fondoPersistente);
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

        fondoPersistente = fondo.AddComponent<Image>();
        if (pantallaCooperativa) fondoPersistente.color = Color.clear;
        else AsignarImagenCarga(fondoPersistente);

        RectTransform boton = Panel(raiz.transform, "Salir de la partida", 0, 0, 1, 1, 1f);
        Place(boton, new Vector2(.76f, .89f), new Vector2(.97f, .96f));
        Image imagenBoton = boton.GetComponent<Image>();
        imagenBoton.raycastTarget = true;
        botonSalir = boton.gameObject.AddComponent<Button>();
        botonSalir.targetGraphic = imagenBoton;
        StyleButton(botonSalir);
        etiquetaSalir = Text(boton, "Etiqueta", "Salir de la partida", 0, 0, 1, 1, null, 26);
        Place(etiquetaSalir.rectTransform, Vector2.zero, Vector2.one);
        etiquetaSalir.alignment = TextAlignmentOptions.Center;
        botonSalir.onClick.AddListener(SalirDesdeCortina);

        RectTransform aviso = Panel(raiz.transform, "Aviso de desconexión", 0, 0, 1, 1, 1f);
        Place(aviso, new Vector2(.17f, .49f), new Vector2(.83f, .61f));
        avisoCarga = Text(aviso, "Mensaje", "", 0, 0, 1, 1, null, 30);
        Place(avisoCarga.rectTransform, new Vector2(.03f, .06f), new Vector2(.97f, .94f));
        avisoCarga.alignment = TextAlignmentOptions.Center;
        aviso.gameObject.SetActive(false);

        AudioListener.pause = true;
    }

    public static void MantenerEntradaDeCarga()
    {
        if (cortinaPersistenteLobby == null || !cortinaPersistenteLobby.activeInHierarchy)
            return;

        if (EventSystem.current == null)
        {
            GameObject entradaCarga = new GameObject("Entrada de pantalla de carga",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            entradaCarga.transform.SetParent(cortinaPersistenteLobby.transform, false);
            entradaCarga.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void AvisarSalidaDeCompanero()
    {
        if (cortinaPersistenteLobby == null || anfitrionDesconectado)
            return;

        avisoCarga.text = "Un compañero abandonó la partida.";
        avisoCarga.transform.parent.gameObject.SetActive(true);
    }

    public static bool AvisarDesconexionDelHost()
    {
        if (cortinaPersistenteLobby == null)
            return false;

        anfitrionDesconectado = true;
        botonSalir.interactable = true;
        avisoCarga.text = "Se perdió la conexión con el anfitrión. La partida se cerró.";
        avisoCarga.transform.parent.gameObject.SetActive(true);
        etiquetaSalir.text = "Volver al menú";
        AudioListener.pause = false;
        return true;
    }

    public static void MostrarSalidaAlMenu()
    {
        saliendoAlMenu = true;
        DeadQuarantine.PvPvE.PvPvELoadingScreen.CancelOriginal();
        if (cortinaPersistenteLobby == null)
            return;

        fondoPersistente.sprite = null;
        fondoPersistente.color = Color.black;
        botonSalir.gameObject.SetActive(false);
        avisoCarga.text = "Volviendo al menú…";
        avisoCarga.transform.parent.gameObject.SetActive(true);
    }

    private static void SalirDesdeCortina()
    {
        NetworkBootstrap network = NetworkBootstrap.Instance;
        if (network == null)
            return;

        botonSalir.interactable = false;
        etiquetaSalir.text = "Saliendo…";
        _ = anfitrionDesconectado ? network.GoToMenuDueToDisconnect() : network.SalirDuranteCarga();
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
        if (!anfitrionDesconectado && !saliendoAlMenu)
            CerrarCortinaPersistente();
    }

    public static void CerrarCortinaPersistente()
    {
        anfitrionDesconectado = false;
        saliendoAlMenu = false;
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


    private void Awake()
    {
        if (saliendoAlMenu)
        {
            if (curtainCanvas != null)
                curtainCanvas.SetActive(false);
            return;
        }

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
        if (curtainHidden || anfitrionDesconectado ||
            NetworkBootstrap.Instance?.IsLeavingIntentionally == true)
            return;


        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            Debug.LogError("[GameStartCurtain] La red no está activa: vuelvo al menú.");
            curtainHidden = true;
            if (!AvisarDesconexionDelHost() && NetworkBootstrap.Instance != null)
                _ = NetworkBootstrap.Instance.GoToMenuDueToDisconnect();

            return;
        }
        GestorEscenariosPartida gestor = GestorEscenariosPartida.Instance;
        string mapaAditivo = null;
        if (gestor != null && gestor.UsaEscenasAditivasMultiplayer)
        {
            if (!gestor.MapaMultiplayerListo || !gestor.PresentacionLocalPreparada)
                return;

            mapaAditivo = gestor.NombreMapaMultiplayerActivo;
        }

        if (!TodosListos(mapaAditivo))
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
        NetworkManager red = NetworkManager.Singleton;
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
            if (no == null || !no.IsSpawned || !no.IsPlayerObject)
                continue;
            if (red != null && red.IsConnectedClient && !SigueConectado(red, no.OwnerClientId))
                continue;

            reales.Add(a);
        }

        return reales.ToArray();
    }

    private bool TodosListos(string mapaAditivo)
    {
        MultiplayerPlayerSpawnAssigner[] assigners =
            ObtenerAssignersDePlayersReales();

        int jugadoresEsperados = expectedPlayerCount;

        NetworkManager red = NetworkManager.Singleton;
        if (red != null && red.IsConnectedClient)
            jugadoresEsperados = Mathf.Max(1, red.ConnectedClientsIds.Count);

        // Log de diagnóstico: va ANTES de los return para verse siempre.
        if (Time.unscaledTime >= proximoLog)
        {
            proximoLog = Time.unscaledTime + 2f;

            string estados = "";

            foreach (MultiplayerPlayerSpawnAssigner a in assigners)
            {
                bool listo = mapaAditivo == null
                    ? a.IsAtSpawn.Value
                    : a.ConfirmoSpawnEnEscena(mapaAditivo);
                estados += listo + " ";
            }

            Debug.Log(
                "[Curtain] Jugadores: " + assigners.Length + "/" + jugadoresEsperados +
                " | Mapa: " + mapaAditivo + " | Listos: " + estados
            );
        }

        if (assigners.Length < jugadoresEsperados)
            return false;

        foreach (MultiplayerPlayerSpawnAssigner assigner in assigners)
        {
            bool listo = mapaAditivo == null
                ? assigner.IsAtSpawn.Value
                : assigner.ConfirmoSpawnEnEscena(mapaAditivo);
            if (!listo)
                return false;
        }

        return true;
    }

    private static bool SigueConectado(NetworkManager red, ulong clientId)
    {
        foreach (ulong conectado in red.ConnectedClientsIds)
            if (conectado == clientId)
                return true;
        return false;
    }

    private void OcultarCortina()
    {
        Debug.Log("[GameStartCurtain] OcultarCortina() ejecutado.");

        curtainHidden = true;
        if (curtainCanvas != null)
            curtainCanvas.SetActive(false);
        CerrarCortinaPersistente();

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
