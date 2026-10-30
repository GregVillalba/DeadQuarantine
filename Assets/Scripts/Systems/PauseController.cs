using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections;
using TMPro;

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
    [SerializeField] private GameObject popupVictoria;
    [SerializeField] private GameObject popupDerrota;
    [SerializeField] private TextMeshProUGUI textoPuntajeVictoria;
    [SerializeField] private TextMeshProUGUI textoPuntajeDerrota;
    [SerializeField] private TextMeshProUGUI textoRondaDerrota;
    [SerializeField] private TextMeshProUGUI textoZombiesDerrota;
    [SerializeField] private TextMeshProUGUI textoZombiesVictoria;
    [SerializeField] private TextMeshProUGUI textoPrecisionDerrota;
    [SerializeField] private TextMeshProUGUI textoPrecisionVictoria;
    [SerializeField] private TextMeshProUGUI textoCaidasDerrota;
    [SerializeField] private TextMeshProUGUI textoCaidasVictoria;
    [SerializeField] private TextMeshProUGUI textoReaparicionesVictoria;

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

    [Header("Guardar Score (Modo Supervivencia)")]
    [SerializeField] private GuardarScoreCartelUI cartelGuardarScore;
    [SerializeField] private UnityEngine.UI.Button botonVolverAlMenuDerrota;

    private bool estaPausado;
    public bool EstaPausado => estaPausado;

    public static bool LocalPlayerPaused { get; private set; }
    private static int resumeFrame = -100;

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

    private void Awake()
    {
        playerScore = transform.root.GetComponentInChildren<PlayerScore>();

        if (popupMenuHome != null)
            popupMenuHome.SetActive(false);

        if (menuConfiguracion != null)
            menuConfiguracion.SetActive(false);

        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerLook == null)
            playerLook = GetComponentInChildren<PlayerLook>(true);

        if (weaponSwitcher == null)
            weaponSwitcher = GetComponentInChildren<WeaponSwitcher>(true);

        if (shopUI == null)
            shopUI = GetComponentInChildren<NPCShopUI>(true);

        if (shopUI == null)
            shopUI = transform.root.GetComponentInChildren<NPCShopUI>(true);

        if (storyIntro == null)
            storyIntro = GetComponent<StoryIntroController>();

        if (popupVictoria != null)
            popupVictoria.SetActive(false);

        if (popupDerrota != null)
            popupDerrota.SetActive(false);

        if (popupValoracion != null)
            popupValoracion.SetActive(false);

        if (panelRonda != null)
            panelRonda.SetActive(false);

        if (panelFaseCompras != null)
            panelFaseCompras.SetActive(false);

        if (hud != null)
            hud.SetActive(false);

        if (panelCompaneroAbandono != null)
            panelCompaneroAbandono.SetActive(false);

        // Buscar y mantener deshabilitado el cartel de score al iniciar.
        if (cartelGuardarScore == null)
        {
            cartelGuardarScore =
                transform.root.GetComponentInChildren<GuardarScoreCartelUI>(true);
        }

        if (cartelGuardarScore != null)
        {
            cartelGuardarScore.gameObject.SetActive(false);

            Debug.Log(
                "<color=cyan>[PauseController] Panel de score localizado y DESHABILITADO al iniciar.</color>"
            );
        }

        ConectarBotonVolverAlMenuDerrota();

        // NUEVO:
        // Busca automáticamente el menú nuevo de configuración
        // aunque la referencia del Inspector esté vacía.
        ResolverMenuConfiguracion();

        // NUEVO:
        // Conecta automáticamente el botón CONFIGURACIÓN.
        ConectarBotonConfiguracionRuntime();
    }

    private void ConectarBotonVolverAlMenuDerrota()
    {
        if (botonVolverAlMenuDerrota == null && popupDerrota != null)
        {
            botonVolverAlMenuDerrota =
                popupDerrota.GetComponentInChildren<UnityEngine.UI.Button>(true);
        }

        if (botonVolverAlMenuDerrota != null)
        {
            botonVolverAlMenuDerrota.onClick.RemoveAllListeners();
            botonVolverAlMenuDerrota.onClick.AddListener(
                OnClickBotonVolverAlMenuDerrota
            );

            Debug.Log(
                "<color=cyan>[PauseController] Botón 'Volver al Menú' conectado exitosamente a OnClickBotonVolverAlMenuDerrota.</color>"
            );
        }
    }

    public void MostrarHUD()
    {
        if (hud != null)
            hud.SetActive(true);
    }

    // =========================================================
    // CONFIGURACIÓN DESDE PAUSA
    // =========================================================

    /// <summary>
    /// Busca el panel nuevo de configuración dentro del Player.
    /// Esto permite que funcione aunque el campo Menu Configuracion
    /// haya quedado vacío en el Inspector.
    /// </summary>
    private void ResolverMenuConfiguracion()
    {
        if (menuConfiguracion != null)
            return;

        Transform[] objetos =
            transform.root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < objetos.Length; i++)
        {
            Transform objeto = objetos[i];

            if (objeto == null)
                continue;

            if (objeto.name == "ConfiguracionesPanelPausa")
            {
                menuConfiguracion = objeto.gameObject;

                Debug.Log(
                    "<color=green>[PauseController] ConfiguracionesPanelPausa encontrado automáticamente.</color>"
                );

                return;
            }
        }

        Debug.LogWarning(
            "[PauseController] No se encontró ConfiguracionesPanelPausa. " +
            "Ejecutá Game > Construir UI > Configuración en menú de pausa."
        );
    }

    /// <summary>
    /// Busca automáticamente el botón CONFIGURACIÓN dentro
    /// del popup de pausa y conecta AbrirConfiguracion().
    /// </summary>
    private void ConectarBotonConfiguracionRuntime()
    {
        if (popupMenuHome == null)
            return;

        UnityEngine.UI.Button[] botones =
            popupMenuHome.GetComponentsInChildren<UnityEngine.UI.Button>(true);

        for (int i = 0; i < botones.Length; i++)
        {
            UnityEngine.UI.Button boton = botones[i];

            if (boton == null)
                continue;

            bool esBotonConfiguracion =
                boton.name == "BotonConfiguracion";

            TMP_Text textoBoton =
                boton.GetComponentInChildren<TMP_Text>(true);

            if (!esBotonConfiguracion && textoBoton != null)
            {
                string texto =
                    textoBoton.text != null
                        ? textoBoton.text.Trim().ToUpperInvariant()
                        : string.Empty;

                esBotonConfiguracion =
                    texto == "CONFIGURACIÓN" ||
                    texto == "CONFIGURACION";
            }

            if (!esBotonConfiguracion)
                continue;

            boton.onClick.RemoveListener(AbrirConfiguracion);
            boton.onClick.AddListener(AbrirConfiguracion);

            Debug.Log(
                "<color=green>[PauseController] Botón CONFIGURACIÓN conectado a AbrirConfiguracion().</color>"
            );

            return;
        }

        Debug.LogWarning(
            "[PauseController] No se encontró el botón CONFIGURACIÓN dentro del menú de pausa."
        );
    }

    /// <summary>
    /// Abre el menú de configuración desde el menú de pausa.
    /// La partida continúa pausada y el escenario queda visible detrás.
    /// </summary>
    public void AbrirConfiguracion()
    {
        Debug.Log(
            $"<color=cyan>[PauseController] AbrirConfiguracion() | " +
            $"IsOwner={IsOwner} | estaPausado={estaPausado}</color>"
        );

        ResolverMenuConfiguracion();

        if (!IsOwner)
        {
            Debug.LogWarning(
                "[PauseController] No se abrió Configuración porque este Player no es el Owner."
            );

            return;
        }

        if (!estaPausado)
        {
            Debug.LogWarning(
                "[PauseController] No se abrió Configuración porque el juego no figura como pausado."
            );

            return;
        }

        if (menuConfiguracion == null)
        {
            Debug.LogError(
                "[PauseController] No se puede abrir Configuración: " +
                "menuConfiguracion es NULL y no existe un objeto llamado ConfiguracionesPanelPausa."
            );

            return;
        }

        // Cierra cualquier panel viejo de Configuración
        // para evitar superposiciones.
        if (configuraciones != null)
            configuraciones.CerrarTodo();

        // Ocultamos únicamente el menú de pausa.
        if (popupMenuHome != null)
            popupMenuHome.SetActive(false);

        // Activamos el nuevo menú.
        menuConfiguracion.SetActive(true);

        // Lo ponemos por encima del resto del Canvas.
        menuConfiguracion.transform.SetAsLastSibling();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Debug.Log(
            $"<color=green>[PauseController] Configuración ABIERTA. " +
            $"activeSelf={menuConfiguracion.activeSelf}, " +
            $"activeInHierarchy={menuConfiguracion.activeInHierarchy}</color>"
        );
    }

    /// <summary>
    /// Cierra Configuración y vuelve al menú de pausa
    /// sin reanudar la partida.
    /// </summary>
    public void VolverDesdeConfiguracion()
    {
        if (!IsOwner || !estaPausado)
            return;

        ResolverMenuConfiguracion();

        if (menuConfiguracion != null)
            menuConfiguracion.SetActive(false);

        if (configuraciones != null)
            configuraciones.CerrarTodo();

        if (popupMenuHome != null)
            popupMenuHome.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>
    /// Evita que ESC cierre Configuración mientras
    /// se está reasignando una tecla.
    /// </summary>
    private bool EstaReasignandoControlEnMenuConfiguracion()
    {
        if (
            menuConfiguracion == null ||
            !menuConfiguracion.activeInHierarchy
        )
        {
            return false;
        }

        MonoBehaviour[] behaviours =
            menuConfiguracion.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null)
                continue;

            var property =
                behaviour.GetType().GetProperty("IsRebinding");

            if (
                property == null ||
                property.PropertyType != typeof(bool) ||
                !property.CanRead
            )
            {
                continue;
            }

            try
            {
                if ((bool)property.GetValue(behaviour))
                    return true;
            }
            catch
            {
                // Si algún componente no permite leer la propiedad
                // simplemente lo ignoramos.
            }
        }

        return false;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        ActualizarBotonReiniciar();

        if (EsSinglePlayer())
            return;

        if (NetworkBootstrap.Instance != null)
        {
            NetworkBootstrap.Instance.OnGuestLeft +=
                MostrarAvisoCompaneroAbandono;
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private bool CartelGuardarScoreAbierto =>
        cartelGuardarScore != null &&
        cartelGuardarScore.gameObject.activeSelf;

    private void Update()
    {
        if (!IsOwner)
            return;

        bool historiaActiva =
            storyIntro != null &&
            storyIntro.HistoriaActiva;

        bool tiendaActiva =
            shopUI != null &&
            shopUI.EstaAbierta;

        if (historiaActiva || finDeRondaActivo || tiendaActiva)
        {
            if (finDeRondaActivo && Keyboard.current != null)
            {
                if (
                    popupVictoria != null &&
                    popupVictoria.activeSelf &&
                    Keyboard.current.sKey.wasPressedThisFrame
                )
                {
                    OnSiguienteRondaVictoriaPresionado();
                }
            }

            return;
        }

        bool reasignandoTecla =
            (
                opcionesControles != null &&
                opcionesControles.ReasignandoTecla
            )
            ||
            EstaReasignandoControlEnMenuConfiguracion();

        if (
            !reasignandoTecla &&
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame
        )
        {
            // Si estamos en Configuración, ESC vuelve a Pausa.
            if (
                menuConfiguracion != null &&
                menuConfiguracion.activeSelf
            )
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
    // PAUSA
    // =========================================================

    public void PausarJuego()
    {
        bool historiaActiva =
            storyIntro != null &&
            storyIntro.HistoriaActiva;

        if (
            !IsOwner ||
            estaPausado ||
            historiaActiva
        )
        {
            return;
        }

        estaPausado = true;
        LocalPlayerPaused = true;

        stateAtPause =
            playerHealth != null
                ? playerHealth.State.Value
                : PlayerHealth.PlayerState.Alive;

        movementWasLocked =
            playerMovement != null &&
            playerMovement.MovementLocked;

        lookWasEnabled =
            playerLook != null &&
            playerLook.enabled;

        weaponWasLocked =
            weaponSwitcher != null &&
            weaponSwitcher.CurrentWeapon != null &&
            weaponSwitcher.CurrentWeapon.InputLocked;

        ResolverMenuConfiguracion();

        if (menuConfiguracion != null)
            menuConfiguracion.SetActive(false);

        if (popupMenuHome != null)
            popupMenuHome.SetActive(true);

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
        if (!IsOwner || !estaPausado)
            return;

        estaPausado = false;
        LocalPlayerPaused = false;
        resumeFrame = Time.frameCount;

        if (opcionesControles != null)
            opcionesControles.Cerrar();

        if (configuraciones != null)
            configuraciones.CerrarTodo();

        if (menuConfiguracion != null)
            menuConfiguracion.SetActive(false);

        if (popupMenuHome != null)
            popupMenuHome.SetActive(false);

        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current
                .SetSelectedGameObject(null);
        }

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
        if (playerMovement != null)
            playerMovement.MovementLocked = true;

        if (playerLook != null)
            playerLook.enabled = false;

        if (
            weaponSwitcher != null &&
            weaponSwitcher.CurrentWeapon != null
        )
        {
            weaponSwitcher.CurrentWeapon.InputLocked = true;
        }
    }

    private void HabilitarJugador()
    {
        if (
            playerHealth != null &&
            !playerHealth.IsAlive
        )
        {
            return;
        }

        if (playerMovement != null)
            playerMovement.MovementLocked = false;

        if (playerLook != null)
            playerLook.enabled = true;

        if (
            weaponSwitcher != null &&
            weaponSwitcher.CurrentWeapon != null
        )
        {
            weaponSwitcher.CurrentWeapon.InputLocked = false;
        }
    }

    private void RestaurarEstadoJugador()
    {
        if (
            !EsSinglePlayer() &&
            playerHealth != null &&
            playerHealth.State.Value != stateAtPause
        )
        {
            return;
        }

        if (playerMovement != null)
            playerMovement.MovementLocked = movementWasLocked;

        if (playerLook != null)
            playerLook.enabled = lookWasEnabled;

        if (
            weaponSwitcher != null &&
            weaponSwitcher.CurrentWeapon != null
        )
        {
            weaponSwitcher.CurrentWeapon.InputLocked =
                weaponWasLocked;
        }
    }

    private bool EsSinglePlayer()
    {
        return
            SceneManager.GetActiveScene().name ==
            "MainSceneSinglePlayer";
    }

    private bool EsModoSupervivencia()
    {
        return
            (
                RoundManager.Instance != null &&
                RoundManager.Instance.IsInfiniteMode
            )
            ||
            (
                ConfiguracionPartidaSeleccionada.Instancia != null &&
                ConfiguracionPartidaSeleccionada
                    .Instancia
                    .modoSeleccionado ==
                ConfiguracionPartidaSeleccionada
                    .ModoJuego
                    .Supervivencia
            );
    }

    private void ActualizarBotonReiniciar()
    {
        if (botonReiniciar != null)
            botonReiniciar.SetActive(EsSinglePlayer());
    }

    public void ReiniciarJuego()
    {
        if (!IsOwner || !EsSinglePlayer())
            return;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening
        )
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene("MainSceneSinglePlayer");
    }

    public void Salir()
    {
        if (!IsOwner)
            return;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (NetworkBootstrap.Instance != null)
        {
            NetworkBootstrap.Instance.NotifyIntentionalLeave();
        }

        if (
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening
        )
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene("PantallasUI");
    }

    private void ActualizarCursor()
    {
        if (!IsOwner)
            return;

        if (estaPausado || finDeRondaActivo)
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

    private void OnDestroy()
    {
        if (NetworkBootstrap.Instance != null)
        {
            NetworkBootstrap.Instance.OnGuestLeft -=
                MostrarAvisoCompaneroAbandono;
        }

        if (estaPausado)
            LocalPlayerPaused = false;

        if (EsSinglePlayer())
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }

    // =========================================================
    // VICTORIA
    // =========================================================

    public void MostrarVictoria()
    {
        if (!IsOwner)
            return;

        finDeRondaActivo = true;

        if (hud != null)
            hud.SetActive(false);

        BloquearJugador();

        if (popupVictoria != null)
            popupVictoria.SetActive(true);

        if (
            textoPuntajeVictoria != null &&
            playerScore != null
        )
        {
            textoPuntajeVictoria.text =
                playerScore.ScoreNetwork.Value.ToString();
        }

        if (
            textoZombiesVictoria != null &&
            playerScore != null
        )
        {
            textoZombiesVictoria.text =
                playerScore.ZombiesEliminadosNetwork.Value.ToString();
        }

        if (
            textoPrecisionVictoria != null &&
            playerScore != null
        )
        {
            textoPrecisionVictoria.text =
                playerScore.PrecisionPorcentaje.ToString() + "%";
        }

        if (
            textoReaparicionesVictoria != null &&
            playerScore != null
        )
        {
            textoReaparicionesVictoria.text =
                playerScore.ReaparicionesNetwork.Value.ToString();
        }

        if (
            textoCaidasVictoria != null &&
            playerScore != null
        )
        {
            textoCaidasVictoria.text =
                playerScore.CaidasNetwork.Value.ToString();
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>
    /// Al continuar desde victoria se abre el panel de valoración.
    /// </summary>
    public void OnSiguienteRondaVictoriaPresionado()
    {
        if (!IsOwner)
            return;

        if (popupVictoria != null)
            popupVictoria.SetActive(false);

        Debug.Log(
            "<color=green>[PauseController] Victoria en modo con rondas: abriendo panel de valoración.</color>"
        );

        MostrarValoracion();
    }

    public void MostrarValoracion()
    {
        if (popupValoracion != null)
            popupValoracion.SetActive(true);
    }

    /// <summary>
    /// Luego de completar o cerrar la valoración
    /// en victoria, salimos al menú principal.
    /// </summary>
    public void ContinuarDespuesDeRating()
    {
        if (!IsOwner)
            return;

        if (popupValoracion != null)
            popupValoracion.SetActive(false);

        Salir();
    }

    // =========================================================
    // DERROTA
    // =========================================================

    public void MostrarDerrota()
    {
        if (!IsOwner)
            return;

        finDeRondaActivo = true;

        if (hud != null)
            hud.SetActive(false);

        BloquearJugador();

        if (popupDerrota != null)
            popupDerrota.SetActive(true);

        if (
            textoPuntajeDerrota != null &&
            playerScore != null
        )
        {
            textoPuntajeDerrota.text =
                playerScore.ScoreNetwork.Value.ToString();
        }

        if (
            textoZombiesDerrota != null &&
            playerScore != null
        )
        {
            textoZombiesDerrota.text =
                playerScore.ZombiesEliminadosNetwork.Value.ToString();
        }

        if (
            textoPrecisionDerrota != null &&
            playerScore != null
        )
        {
            textoPrecisionDerrota.text =
                playerScore.PrecisionPorcentaje.ToString() + "%";
        }

        if (
            textoRondaDerrota != null &&
            playerScore != null
        )
        {
            textoRondaDerrota.text =
                (
                    RoundManager.Instance != null &&
                    RoundManager.Instance.IsInfiniteMode
                )
                    ? RoundManager.Instance.CurrentRound.ToString()
                    : (
                        RoundManager.Instance != null
                            ? RoundManager.Instance.CurrentRound +
                              " / " +
                              RoundManager.Instance.MaxRounds
                            : "1"
                    );
        }

        if (
            textoCaidasDerrota != null &&
            playerScore != null
        )
        {
            textoCaidasDerrota.text =
                playerScore.CaidasNetwork.Value.ToString();
        }

        // En caso de que el botón se hubiera reinstanciado,
        // lo volvemos a vincular.
        ConectarBotonVolverAlMenuDerrota();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>
    /// Ejecutado al presionar VOLVER AL MENÚ
    /// en la pantalla de derrota.
    /// </summary>
    public void OnClickBotonVolverAlMenuDerrota()
    {
        Debug.Log(
            "<color=yellow>[PauseController] >>> CLIC EN BOTÓN VOLVER AL MENÚ DE DERROTA <<<</color>"
        );

        if (EsModoSupervivencia())
        {
            Debug.Log(
                "<color=orange>[PauseController] Modo Supervivencia detectado: Bloqueando botón, pintando naranja y abriendo score...</color>"
            );

            AbrirScoreSupervivencia();
        }
        else
        {
            Debug.Log(
                "<color=green>[PauseController] Derrota en modo normal: Regresando directo al menú principal...</color>"
            );

            Salir();
        }
    }

    private void AbrirScoreSupervivencia()
    {
        // 1. Pintar de naranja y deshabilitar interacción.
        if (botonVolverAlMenuDerrota != null)
        {
            Color colorNaranja =
                new Color(
                    1.0f,
                    0.45f,
                    0.0f,
                    1.0f
                );

            var img =
                botonVolverAlMenuDerrota
                    .GetComponent<UnityEngine.UI.Image>();

            if (img != null)
                img.color = colorNaranja;

            var colors =
                botonVolverAlMenuDerrota.colors;

            colors.disabledColor = colorNaranja;

            botonVolverAlMenuDerrota.colors =
                colors;

            botonVolverAlMenuDerrota.interactable =
                false;

            Debug.Log(
                "<color=orange>[PauseController] Botón pintado de NARANJA y DESHABILITADO.</color>"
            );
        }

        // 2. Buscar el cartel dentro de este clon exacto.
        GuardarScoreCartelUI cartelEnEscena =
            GetComponentInChildren<GuardarScoreCartelUI>(true);

        if (cartelEnEscena == null)
        {
            cartelEnEscena =
                transform.root
                    .GetComponentInChildren<GuardarScoreCartelUI>(true);
        }

        if (cartelEnEscena == null)
        {
            Debug.LogError(
                "<color=red>[PauseController] CRÍTICO: No se encontró GuardarScoreCartelUI dentro de Player(Clone) en la escena activa.</color>"
            );

            Salir();
            return;
        }

        cartelGuardarScore =
            cartelEnEscena;

        // 3. Activar explícitamente todos los padres hasta la raíz.
        Transform t =
            cartelGuardarScore.transform;

        while (
            t != null &&
            t != transform.root
        )
        {
            t.gameObject.SetActive(true);
            t = t.parent;
        }

        cartelGuardarScore.gameObject.SetActive(true);
        cartelGuardarScore.transform.localScale = Vector3.one;
        cartelGuardarScore.transform.SetAsLastSibling();

        Debug.Log(
            $"<color=green>[PauseController] Cartel '{cartelGuardarScore.gameObject.name}' ACTIVADO FORZOSAMENTE. activeInHierarchy={cartelGuardarScore.gameObject.activeInHierarchy}</color>"
        );

        string nombreFinal =
            PlayerPrefs.GetString(
                "PLAYER_CUSTOM_USERNAME",
                ""
            );

        if (string.IsNullOrWhiteSpace(nombreFinal))
        {
            nombreFinal =
                PlayerPrefs.GetString(
                    "NOMBRE_PERSONAJE_OSCAR",
                    "Oscar"
                );
        }

        int puntajeFinal =
            playerScore != null
                ? playerScore.ScoreNetwork.Value
                : 0;

        string modoJuego =
            EsSinglePlayer()
                ? "Singleplayer"
                : "Multiplayer";

        cartelGuardarScore.AbrirCartel(
            nombreFinal,
            puntajeFinal,
            modoJuego,
            () =>
            {
                Debug.Log(
                    "<color=green>[PauseController] Cartel finalizado. Volviendo al menú principal...</color>"
                );

                Salir();
            },
            this
        );
    }

    // =========================================================
    // PANELES DE RONDA Y COMPRAS
    // =========================================================

    public void MostrarPanelRonda(
        int ronda,
        bool esRondaFinal
    )
    {
        if (!IsOwner)
            return;

        if (panelRonda == null)
            return;

        if (textoRonda != null)
        {
            textoRonda.text =
                esRondaFinal
                    ? "Ronda Final"
                    : "Ronda " + ronda;
        }

        panelRonda.SetActive(true);

        if (ocultarPanelRondaCoroutine != null)
        {
            StopCoroutine(
                ocultarPanelRondaCoroutine
            );
        }

        ocultarPanelRondaCoroutine =
            StartCoroutine(
                OcultarPanelRondaDespuesDeTiempo(
                    duracionPanelRonda
                )
            );
    }

    public void OcultarPanelRonda()
    {
        if (panelRonda != null)
            panelRonda.SetActive(false);
    }

    private IEnumerator OcultarPanelRondaDespuesDeTiempo(
        float segundos
    )
    {
        yield return new WaitForSeconds(segundos);

        OcultarPanelRonda();
    }

    public void MostrarPanelFaseCompras()
    {
        if (!IsOwner)
            return;

        if (panelFaseCompras == null)
            return;

        if (textoFaseCompras != null)
        {
            textoFaseCompras.text =
                mensajeFaseCompras;
        }

        panelFaseCompras.SetActive(true);

        if (ocultarPanelFaseComprasCoroutine != null)
        {
            StopCoroutine(
                ocultarPanelFaseComprasCoroutine
            );
        }

        ocultarPanelFaseComprasCoroutine =
            StartCoroutine(
                OcultarPanelFaseComprasDespuesDeTiempo(
                    duracionPanelFaseCompras
                )
            );
    }

    public void OcultarPanelFaseCompras()
    {
        if (panelFaseCompras != null)
            panelFaseCompras.SetActive(false);
    }

    private IEnumerator OcultarPanelFaseComprasDespuesDeTiempo(
        float segundos
    )
    {
        yield return new WaitForSeconds(segundos);

        OcultarPanelFaseCompras();
    }

    private void MostrarAvisoCompaneroAbandono()
    {
        if (panelCompaneroAbandono == null)
            return;

        panelCompaneroAbandono.SetActive(true);

        if (ocultarPanelCompaneroCoroutine != null)
        {
            StopCoroutine(
                ocultarPanelCompaneroCoroutine
            );
        }

        ocultarPanelCompaneroCoroutine =
            StartCoroutine(
                OcultarAvisoCompaneroDespuesDeTiempo(
                    duracionPanelCompaneroAbandono
                )
            );
    }

    private IEnumerator OcultarAvisoCompaneroDespuesDeTiempo(
        float segundos
    )
    {
        yield return new WaitForSeconds(segundos);

        if (panelCompaneroAbandono != null)
            panelCompaneroAbandono.SetActive(false);
    }
}