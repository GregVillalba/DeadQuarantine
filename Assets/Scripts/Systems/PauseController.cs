using System.Linq;
using UnityEngine.UI;
using DeadQuarantine.PvPvE;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections;
using TMPro;
using System.Collections.Generic;

public class PauseController : NetworkBehaviour
{
    [Header("Pausa")]
    [SerializeField] private GameObject popupMenuHome;

    [Header("Configuraciones (dentro de la pausa)")]
    [SerializeField] private ConfiguracionesPanelController configuraciones;
    [SerializeField] private OpcionesUI opcionesControles;

    [Tooltip("Menú de configuración actual que se abre desde la pausa.")]
    [SerializeField] private GameObject menuConfiguracion;

    [Header("HUD del jugador")]
    [SerializeField] private GameObject hud;

    [Header("Controles del jugador")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private NPCShopUI shopUI;

    [Header("Vida del jugador")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Historia (script separado)")]
    [SerializeField] private StoryIntroController storyIntro;

    [Header("Botón Reiniciar")]
    [SerializeField] private GameObject botonReiniciar;

    [Header("Fin de Ronda")]
    [SerializeField] private PantallaFinPartida victoriaSingle;
    [SerializeField] private PantallaFinPartida victoriaMulti;
    [SerializeField] private PantallaFinPartida derrotaSingle;
    [SerializeField] private PantallaFinPartida derrotaMulti;

    [SerializeField] private PlayerScore playerScore;

    [Header("Valoración")]
    [SerializeField] private GameObject popupValoracion;

    [Header("Ronda")]
    [SerializeField] private GameObject panelRonda;
    [SerializeField] private float duracionPanelRonda = 3f;
    [SerializeField] private TextMeshProUGUI textoRonda;

    [Header("Fase de compras")]
    [SerializeField] private GameObject panelFaseCompras;
    [SerializeField] private float duracionPanelFaseCompras = 3f;
    [SerializeField] private TextMeshProUGUI textoFaseCompras;
    [SerializeField] private string mensajeFaseCompras = "Tiempo para comprar habilitado";

    [Header("Aviso: compañero abandonó")]
    [SerializeField] private GameObject panelCompaneroAbandono;
    [SerializeField] private float duracionPanelCompaneroAbandono = 4f;
    [SerializeField] private TextMeshProUGUI textoCompaneroAbandono;
    [SerializeField] private string formatoCompaneroAbandono = "{0} abandonó la partida";
    

    private readonly List<(string linea, float vence)> avisosAbandono = new List<(string linea, float vence)>();

    [Header("Guardar Score (Modo Supervivencia)")]
    [SerializeField] private GuardarScoreCartelUI cartelGuardarScore;
    [SerializeField] private UnityEngine.UI.Button botonVolverAlMenuDerrota;

    private bool estaPausado;
    public bool EstaPausado => estaPausado;
    public GameObject PauseHomePanel => popupMenuHome;
    public bool IsPauseHomeVisible => estaPausado && popupMenuHome != null && popupMenuHome.activeInHierarchy;

    public static bool LocalPlayerPaused { get; private set; }
    private static int resumeFrame = -100;

    private PantallaFinPartida victoriaActiva;
    private PantallaFinPartida derrotaActiva;

    public static bool MouseInputBlocked =>
        LocalPlayerPaused || Time.frameCount - resumeFrame <= 2;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        LocalPlayerPaused = false;
        resumeFrame = -100;
    }

    private bool movementWasLocked;
    private bool lookWasEnabled;
    private bool weaponWasLocked;
    private PlayerHealth.PlayerState stateAtPause;

    private bool finDeRondaActivo;
    private Coroutine ocultarPanelRondaCoroutine;
    private Coroutine ocultarPanelFaseComprasCoroutine;
    private Coroutine ocultarPanelCompaneroCoroutine;
    

    // Estado del minijuego arcade
    private bool minijuegoActivo;

    public NetworkVariable<bool> EnMinijuegoNet = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private void Awake()
    {
        playerScore = transform.root.GetComponentInChildren<PlayerScore>();

        if (popupMenuHome != null) popupMenuHome.SetActive(false);
        if (menuConfiguracion != null) menuConfiguracion.SetActive(false);
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (playerLook == null) playerLook = GetComponentInChildren<PlayerLook>(true);
        if (weaponSwitcher == null) weaponSwitcher = GetComponentInChildren<WeaponSwitcher>(true);
        if (shopUI == null) shopUI = GetComponentInChildren<NPCShopUI>(true);
        if (shopUI == null) shopUI = transform.root.GetComponentInChildren<NPCShopUI>(true);
        if (storyIntro == null) storyIntro = GetComponent<StoryIntroController>();

        foreach (var p in new[] { victoriaSingle, victoriaMulti, derrotaSingle, derrotaMulti })
        {
            if (p != null) p.gameObject.SetActive(false);
        }

        if (popupValoracion != null) popupValoracion.SetActive(false);
        if (panelRonda != null) panelRonda.SetActive(false);
        if (panelFaseCompras != null) panelFaseCompras.SetActive(false);
        if (hud != null) hud.SetActive(false);
        if (panelCompaneroAbandono != null) panelCompaneroAbandono.SetActive(false);

        if (cartelGuardarScore == null)
            cartelGuardarScore = transform.root.GetComponentInChildren<GuardarScoreCartelUI>(true);

        if (cartelGuardarScore != null)
            cartelGuardarScore.gameObject.SetActive(false);

        ConectarBotonVolverAlMenuDerrota();
        ResolverMenuConfiguracion();
        ConectarBotonConfiguracionRuntime();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        ActualizarBotonReiniciar();
        RefreshPausePresentation();

        if (EsSinglePlayer())
            return;

        if (NetworkBootstrap.Instance != null)
        {
            NetworkBootstrap.Instance.OnGuestLeft += MostrarAvisoCompaneroAbandono;
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (NetworkBootstrap.Instance != null)
        {
            NetworkBootstrap.Instance.OnGuestLeft -= MostrarAvisoCompaneroAbandono;
        }

        if (estaPausado)
            LocalPlayerPaused = false;

        if (EsSinglePlayer())
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        if (minijuegoActivo && IsOwner && IsSpawned)
        {
            SolicitarEstadoMinijuego(false);
        }
    }

    // =========================================================
    // ENTRAR / SALIR DEL MINIJUEGO ARCADE
    // =========================================================

    public void EntrarMinijuego()
    {
        if (!IsOwner || minijuegoActivo) return;

        minijuegoActivo = true;

        SolicitarEstadoMinijuego(true);

        // Ocultar el HUD completo en la pantalla
        if (hud != null)
            hud.SetActive(false);

        stateAtPause = playerHealth != null
            ? playerHealth.State.Value
            : PlayerHealth.PlayerState.Alive;

        movementWasLocked = playerMovement != null && playerMovement.MovementLocked;
        lookWasEnabled = playerLook != null && playerLook.enabled;
        weaponWasLocked = weaponSwitcher != null &&
                          weaponSwitcher.CurrentWeapon != null &&
                          weaponSwitcher.CurrentWeapon.InputLocked;

        BloquearJugador();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void SalirMinijuego()
    {
        if (!IsOwner || !minijuegoActivo) return;

        minijuegoActivo = false;

        SolicitarEstadoMinijuego(false);

        // Volver a mostrar el HUD al salir
        if (hud != null)
            hud.SetActive(true);

        RestaurarEstadoJugador();
        HabilitarJugador();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void RegistrarPartidaArcade()
    {
        if (IsOwner)
            AchievementsManager.Instance?.Desbloquear(AchievementId.CampeonDesconectado);
    }

    private void SolicitarEstadoMinijuego(bool activo)
    {
        if (!IsSpawned)
            return;

        if (IsServer)
            EnMinijuegoNet.Value = activo;
        else
            CambiarEstadoMinijuegoServerRpc(activo);
    }

    [ServerRpc]
    private void CambiarEstadoMinijuegoServerRpc(bool activo)
    {
        EnMinijuegoNet.Value = activo;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [System.NonSerialized] public int DebugUpdates,DebugEscapePresses,DebugPauseCalls,DebugResumeCalls;
#endif
    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugUpdates++;if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)DebugEscapePresses++;
#endif
        if (!IsOwner)
            return;

        if (DeadQuarantine.PvPvE.UI.PvPvEClassEditor.BlocksPauseInput) return;
        bool historiaActiva = storyIntro != null && storyIntro.HistoriaActiva;
        bool tiendaActiva = shopUI != null && shopUI.EstaAbierta;

        if (historiaActiva || finDeRondaActivo || tiendaActiva || minijuegoActivo)
        {
            if (finDeRondaActivo && Keyboard.current != null)
            {
                if (victoriaActiva != null && victoriaActiva.gameObject.activeSelf &&
                    Keyboard.current.sKey.wasPressedThisFrame)
                {
                    OnSiguienteRondaVictoriaPresionado();
                }
            }
            return;
        }

        bool reasignandoTecla = (opcionesControles != null && opcionesControles.ReasignandoTecla)
                                || EstaReasignandoControlEnMenuConfiguracion();

        if (!reasignandoTecla && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (menuConfiguracion != null && menuConfiguracion.activeSelf)
            {
                VolverDesdeConfiguracion();
                return;
            }

            if (estaPausado)
                ReanudarJuego();
            else
                PausarJuego();
        }

        ActualizarCursor();
    }

    // =========================================================
    // PAUSA GENERAL
    // =========================================================

    public void PausarJuego()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugPauseCalls++;
#endif
        bool historiaActiva = storyIntro != null && storyIntro.HistoriaActiva;
        if (!IsOwner || estaPausado || historiaActiva) return;

        estaPausado = true;
        LocalPlayerPaused = true;

        stateAtPause = playerHealth != null
            ? playerHealth.State.Value
            : PlayerHealth.PlayerState.Alive;

        movementWasLocked = playerMovement != null && playerMovement.MovementLocked;
        lookWasEnabled = playerLook != null && playerLook.enabled;
        weaponWasLocked = weaponSwitcher != null &&
                          weaponSwitcher.CurrentWeapon != null &&
                          weaponSwitcher.CurrentWeapon.InputLocked;

        ResolverMenuConfiguracion();

        if (menuConfiguracion != null) menuConfiguracion.SetActive(false);
        if (popupMenuHome != null) popupMenuHome.SetActive(true);

        BloquearJugador();

        if (EsSinglePlayer())
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ReanudarJuego()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugResumeCalls++;
#endif
        if (!IsOwner || !estaPausado) return;

        estaPausado = false;
        LocalPlayerPaused = false;
        resumeFrame = Time.frameCount;

        if (opcionesControles != null) opcionesControles.Cerrar();
        if (configuraciones != null) configuraciones.CerrarTodo();
        if (menuConfiguracion != null) menuConfiguracion.SetActive(false);
        if (popupMenuHome != null) popupMenuHome.SetActive(false);

        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

        ConfiguracionesJuego.AplicarCambios();

        RestaurarEstadoJugador();
        HabilitarJugador();

        if (EsSinglePlayer())
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void BloquearJugador()
    {
        if (playerMovement != null) playerMovement.MovementLocked = true;
        if (playerLook != null) playerLook.enabled = false;
        if (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null)
            weaponSwitcher.CurrentWeapon.InputLocked = true;
    }

    private void HabilitarJugador()
    {
        if (playerHealth != null && !playerHealth.IsAlive) return;

        if (playerMovement != null) playerMovement.MovementLocked = false;
        if (playerLook != null) playerLook.enabled = true;
        if (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null)
            weaponSwitcher.CurrentWeapon.InputLocked = false;
    }

    private void RestaurarEstadoJugador()
    {
        if (!EsSinglePlayer() && playerHealth != null && playerHealth.State.Value != stateAtPause)
            return;

        if (playerMovement != null) playerMovement.MovementLocked = movementWasLocked;
        if (playerLook != null) playerLook.enabled = lookWasEnabled;
        if (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null)
            weaponSwitcher.CurrentWeapon.InputLocked = weaponWasLocked;
    }

    private bool EsSinglePlayer()
    {
        if (DeadQuarantine.PvPvE.Networking.PvPvEPreparationFlow.Active) return false;
        return SceneManager.GetActiveScene().name == "MainSceneSinglePlayer";
    }

    private bool EsModoSupervivencia()
    {
        return (RoundManager.Instance != null && RoundManager.Instance.IsInfiniteMode) ||
               (ConfiguracionPartidaSeleccionada.Instancia != null &&
                ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado == ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia);
    }

    private PantallaFinPartida ElegirPantallaVictoria() => EsSinglePlayer() ? victoriaSingle : victoriaMulti;
    private PantallaFinPartida ElegirPantallaDerrota() => EsSinglePlayer() ? derrotaSingle : derrotaMulti;

    private void ActualizarBotonReiniciar()
    {
        if (botonReiniciar != null) botonReiniciar.SetActive(EsSinglePlayer());
    }

    public void ReiniciarJuego()
    {
        if (!IsOwner || !EsSinglePlayer()) return;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("MainSceneSinglePlayer");
    }

    // Shared home presentation for singleplayer, cooperative and PvPvE; original actions remain on this controller.
    public void RefreshPausePresentation(Button classButton=null)
    {
        ActualizarBotonReiniciar();
        if(!popupMenuHome)return;var home=popupMenuHome.transform;
        // Older player variants do not always reference the visible Restart button in botonReiniciar.
        // Apply the same singleplayer restriction to the actual home actions as well.
        if(!EsSinglePlayer())foreach(var action in home.GetComponentsInChildren<Button>(true)){
            var caption=action.GetComponentInChildren<TMP_Text>(true);
            if(caption&&caption.text.Trim().StartsWith("Reiniciar",System.StringComparison.OrdinalIgnoreCase))action.gameObject.SetActive(false);
        }
        if(!classButton)return;
        var parent=classButton.transform.parent;
        var buttons=parent.GetComponentsInChildren<Button>(true).Where(b=>b.transform.parent==parent&&b!=classButton&&b.gameObject.activeSelf).OrderByDescending(b=>((RectTransform)b.transform).anchoredPosition.y).ToList();
        if(buttons.Count==0)return;classButton.transform.SetSiblingIndex(buttons[0].transform.GetSiblingIndex()+1);
        var layout=parent.GetComponent<LayoutGroup>();if(layout&&layout.enabled){LayoutRebuilder.MarkLayoutForRebuild((RectTransform)parent);return;}
        float top=((RectTransform)buttons[0].transform).anchoredPosition.y;
        float bottom=((RectTransform)buttons[buttons.Count-1].transform).anchoredPosition.y;
        if(buttons.Count==1)bottom=top-((RectTransform)buttons[0].transform).rect.height*1.2f;
        float step=buttons.Count>1?Mathf.Abs(top-((RectTransform)buttons[1].transform).anchoredPosition.y):Mathf.Abs(top-bottom);
        if(step<1)step=((RectTransform)buttons[0].transform).rect.height*1.2f;
        buttons.Insert(1,classButton);
        for(int i=0;i<buttons.Count;i++){var rect=(RectTransform)buttons[i].transform;var pos=rect.anchoredPosition;pos.y=top-i*step;rect.anchoredPosition=pos;}
    }

    public void Salir()
    {
        if (!IsOwner) return;
        if (DeadQuarantine.PvPvE.Networking.PvPvEPreparationFlow.Active) { _ = DeadQuarantine.PvPvE.Networking.PvPvEPreparationFlow.ExitToMenuAsync(); return; }

        if (!EsSinglePlayer() && NetworkBootstrap.Instance != null)
        {
            _ = NetworkBootstrap.Instance.SalirDePartidaCooperativa();
            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("PantallasUI");
    }

    private void ActualizarCursor()
    {
        if (!IsOwner) return;

        if (estaPausado || finDeRondaActivo || DeadQuarantine.PvPvE.UI.PvPvEInitialClassView.BlocksLocalGameplay)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    private void ResolverMenuConfiguracion()
    {
        if (menuConfiguracion != null) return;

        Transform[] objetos = transform.root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < objetos.Length; i++)
        {
            Transform objeto = objetos[i];
            if (objeto == null) continue;

            if (objeto.name == "ConfiguracionesPanelPausa")
            {
                menuConfiguracion = objeto.gameObject;
                return;
            }
        }
    }

    private void ConectarBotonConfiguracionRuntime()
    {
        if (popupMenuHome == null) return;

        UnityEngine.UI.Button[] botones = popupMenuHome.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        for (int i = 0; i < botones.Length; i++)
        {
            UnityEngine.UI.Button boton = botones[i];
            if (boton == null) continue;

            bool esBotonConfiguracion = boton.name == "BotonConfiguracion";
            TMP_Text textoBoton = boton.GetComponentInChildren<TMP_Text>(true);

            if (!esBotonConfiguracion && textoBoton != null)
            {
                string texto = textoBoton.text != null ? textoBoton.text.Trim().ToUpperInvariant() : string.Empty;
                esBotonConfiguracion = texto == "CONFIGURACIÓN" || texto == "CONFIGURACION";
            }

            if (!esBotonConfiguracion) continue;

            boton.onClick.RemoveListener(AbrirConfiguracion);
            boton.onClick.AddListener(AbrirConfiguracion);
            return;
        }
    }

    public void AbrirConfiguracion()
    {
        ResolverMenuConfiguracion();
        if (!IsOwner || !estaPausado || menuConfiguracion == null) return;

        if (configuraciones != null) configuraciones.CerrarTodo();
        if (popupMenuHome != null) popupMenuHome.SetActive(false);

        menuConfiguracion.SetActive(true);
        menuConfiguracion.transform.SetAsLastSibling();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void VolverDesdeConfiguracion()
    {
        if (!IsOwner || !estaPausado) return;

        ResolverMenuConfiguracion();
        if (menuConfiguracion != null) menuConfiguracion.SetActive(false);
        if (configuraciones != null) configuraciones.CerrarTodo();
        if (popupMenuHome != null) popupMenuHome.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private bool EstaReasignandoControlEnMenuConfiguracion()
    {
        if (menuConfiguracion == null || !menuConfiguracion.activeInHierarchy) return false;

        MonoBehaviour[] behaviours = menuConfiguracion.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null) continue;

            var property = behaviour.GetType().GetProperty("IsRebinding");
            if (property == null || property.PropertyType != typeof(bool) || !property.CanRead) continue;

            try
            {
                if ((bool)property.GetValue(behaviour)) return true;
            }
            catch { }
        }

        return false;
    }

    public void MostrarHUD()
    {
        if (hud != null) hud.SetActive(true);
    }

    private void ConectarBotonVolverAlMenuDerrota()
    {
        if (botonVolverAlMenuDerrota == null && derrotaActiva != null)
            botonVolverAlMenuDerrota = derrotaActiva.BotonVolver;

        if (botonVolverAlMenuDerrota != null)
        {
            botonVolverAlMenuDerrota.onClick.RemoveAllListeners();
            botonVolverAlMenuDerrota.onClick.AddListener(OnClickBotonVolverAlMenuDerrota);
        }
    }

    public void MostrarVictoria()
    {
        if (!IsOwner) return;

        finDeRondaActivo = true;
        if (hud != null) hud.SetActive(false);
        BloquearJugador();

        victoriaActiva = ElegirPantallaVictoria();
        if (victoriaActiva != null)
        {
            victoriaActiva.gameObject.SetActive(true);
            victoriaActiva.Mostrar(playerScore);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void OnSiguienteRondaVictoriaPresionado()
    {
        if (!IsOwner) return;
        victoriaActiva?.gameObject.SetActive(false);
        MostrarValoracion();
    }

    public void MostrarValoracion()
    {
        if (victoriaActiva != null) victoriaActiva.gameObject.SetActive(false);
        if (popupValoracion != null) popupValoracion.SetActive(true);
    }

    public void ContinuarDespuesDeRating()
    {
        if (!IsOwner) return;
        if (popupValoracion != null) popupValoracion.SetActive(false);
        Salir();
    }

    public void MostrarDerrota()
    {
        if (!IsOwner) return;

        finDeRondaActivo = true;
        if (hud != null) hud.SetActive(false);
        BloquearJugador();

        derrotaActiva = ElegirPantallaDerrota();
        if (derrotaActiva != null)
        {
            derrotaActiva.gameObject.SetActive(true);
            derrotaActiva.Mostrar(playerScore);
            botonVolverAlMenuDerrota = derrotaActiva.BotonVolver;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void OnClickBotonVolverAlMenuDerrota()
    {
        if (EsModoSupervivencia())
            AbrirScoreSupervivencia();
        else
            Salir();
    }

    private void AbrirScoreSupervivencia()
    {
        if (botonVolverAlMenuDerrota != null)
        {
            Color colorNaranja = new Color(1.0f, 0.45f, 0.0f, 1.0f);
            var img = botonVolverAlMenuDerrota.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = colorNaranja;

            var colors = botonVolverAlMenuDerrota.colors;
            colors.disabledColor = colorNaranja;
            botonVolverAlMenuDerrota.colors = colors;
            botonVolverAlMenuDerrota.interactable = false;
        }

        GuardarScoreCartelUI cartelEnEscena = GetComponentInChildren<GuardarScoreCartelUI>(true);
        if (cartelEnEscena == null)
            cartelEnEscena = transform.root.GetComponentInChildren<GuardarScoreCartelUI>(true);

        if (cartelEnEscena == null)
        {
            Salir();
            return;
        }

        cartelGuardarScore = cartelEnEscena;
        Transform t = cartelGuardarScore.transform;
        while (t != null && t != transform.root)
        {
            t.gameObject.SetActive(true);
            t = t.parent;
        }

        cartelGuardarScore.gameObject.SetActive(true);
        cartelGuardarScore.transform.localScale = Vector3.one;
        cartelGuardarScore.transform.SetAsLastSibling();

        string nombreFinal = PlayerPrefs.GetString("PLAYER_CUSTOM_USERNAME", "");
        if (string.IsNullOrWhiteSpace(nombreFinal))
            nombreFinal = PlayerPrefs.GetString("NOMBRE_PERSONAJE_OSCAR", "Oscar");

        int puntajeFinal = playerScore != null ? playerScore.ScoreNetwork.Value : 0;
        string modoJuego = EsSinglePlayer() ? "Singleplayer" : "Multiplayer";

        cartelGuardarScore.AbrirCartel(nombreFinal, puntajeFinal, modoJuego, Salir, this);
    }

    public void MostrarPanelRonda(int ronda, bool esRondaFinal)
    {
        if (!IsOwner || panelRonda == null) return;

        if (textoRonda != null)
            textoRonda.text = esRondaFinal ? "Ronda Final" : "Ronda " + ronda;

        panelRonda.SetActive(true);
        if (ocultarPanelRondaCoroutine != null) StopCoroutine(ocultarPanelRondaCoroutine);
        ocultarPanelRondaCoroutine = StartCoroutine(OcultarPanelRondaDespuesDeTiempo(duracionPanelRonda));
    }

    public void OcultarPanelRonda()
    {
        if (panelRonda != null) panelRonda.SetActive(false);
    }

    private IEnumerator OcultarPanelRondaDespuesDeTiempo(float segundos)
    {
        yield return new WaitForSeconds(segundos);
        OcultarPanelRonda();
    }

    public void MostrarPanelFaseCompras()
    {
        if (!IsOwner || panelFaseCompras == null) return;

        if (textoFaseCompras != null) textoFaseCompras.text = mensajeFaseCompras;
        panelFaseCompras.SetActive(true);

        if (ocultarPanelFaseComprasCoroutine != null) StopCoroutine(ocultarPanelFaseComprasCoroutine);
        ocultarPanelFaseComprasCoroutine = StartCoroutine(OcultarPanelFaseComprasDespuesDeTiempo(duracionPanelFaseCompras));
    }

    public void OcultarPanelFaseCompras()
    {
        if (panelFaseCompras != null) panelFaseCompras.SetActive(false);
    }

    private IEnumerator OcultarPanelFaseComprasDespuesDeTiempo(float segundos)
    {
        yield return new WaitForSeconds(segundos);
        OcultarPanelFaseCompras();
    }

    /*private void MostrarAvisoCompaneroAbandono()
    {
        if (panelCompaneroAbandono == null) return;

        panelCompaneroAbandono.SetActive(true);
        if (ocultarPanelCompaneroCoroutine != null) StopCoroutine(ocultarPanelCompaneroCoroutine);
        ocultarPanelCompaneroCoroutine = StartCoroutine(OcultarAvisoCompaneroDespuesDeTiempo(duracionPanelCompaneroAbandono));
    }

    private IEnumerator OcultarAvisoCompaneroDespuesDeTiempo(float segundos)
    {
        yield return new WaitForSeconds(segundos);
        if (panelCompaneroAbandono != null) panelCompaneroAbandono.SetActive(false);
    }*/

    private void MostrarAvisoCompaneroAbandono(string nombre)
{
    if (panelCompaneroAbandono == null) return;

    string linea = string.Format(formatoCompaneroAbandono, nombre);
    avisosAbandono.Add((linea, Time.unscaledTime + duracionPanelCompaneroAbandono));
    ActualizarAvisosAbandono();

    if (ocultarPanelCompaneroCoroutine != null) StopCoroutine(ocultarPanelCompaneroCoroutine);
    ocultarPanelCompaneroCoroutine = StartCoroutine(LimpiarAvisosAbandono());
}

private IEnumerator LimpiarAvisosAbandono()
{
    while (avisosAbandono.Count > 0)
    {
        yield return new WaitForSecondsRealtime(0.25f);
        avisosAbandono.RemoveAll(a => a.vence <= Time.unscaledTime);
        ActualizarAvisosAbandono();
    }
}

private void ActualizarAvisosAbandono()
{
    if (panelCompaneroAbandono == null) return;

    if (avisosAbandono.Count == 0)
    {
        panelCompaneroAbandono.SetActive(false);
        return;
    }

    panelCompaneroAbandono.SetActive(true);

    if (textoCompaneroAbandono != null)
        textoCompaneroAbandono.text = string.Join("\n", avisosAbandono.Select(a => a.linea));
}

}
