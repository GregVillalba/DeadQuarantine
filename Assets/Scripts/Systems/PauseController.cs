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

    private bool estaPausado;

    public bool EstaPausado => estaPausado;

    // ---------------------------------------------------------
    // Estado de pausa del jugador LOCAL, legible desde cualquier script de movimiento / arma / cámara.
    // Solo lo escribe el PauseController del dueño (los de los otros jugadores están deshabilitados),
    // así que en multiplayer refleja únicamente TU pausa.
    // ---------------------------------------------------------

    /// <summary>true mientras el menú de pausa del jugador local está abierto.</summary>
    public static bool LocalPlayerPaused { get; private set; }

    private static int resumeFrame = -100;

    /// <summary>
    /// true en pausa y durante los 2 frames siguientes a reanudar. Hay que ignorar el delta del mouse
    /// en ese lapso: al volver a bloquear el cursor el primer delta puede ser enorme y haría saltar
    /// la cámara y el arma.
    /// </summary>
    public static bool MouseInputBlocked =>
        LocalPlayerPaused || Time.frameCount - resumeFrame <= 2;

    // Con "Enter Play Mode Options" (sin recargar dominio) los estáticos sobreviven entre partidas.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        LocalPlayerPaused = false;
        resumeFrame = -100;
    }

    private bool movementWasLocked;
    private bool lookWasEnabled;
    private bool weaponWasLocked;
    private PlayerHealth.PlayerState stateAtPause; // solo se usa en multiplayer

    private bool finDeRondaActivo;
    private bool pendienteAccionEsVictoria;
    private Coroutine ocultarPanelRondaCoroutine;
    private Coroutine ocultarPanelFaseComprasCoroutine;

    private Coroutine ocultarPanelCompaneroCoroutine;

    private void Awake()
    {
        playerScore = transform.root.GetComponentInChildren<PlayerScore>();

        if (popupMenuHome != null)
            popupMenuHome.SetActive(false);

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
    }

    public void MostrarHUD()
    {
        if (hud != null)
            hud.SetActive(true);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        ActualizarBotonReiniciar();

        // El cursor y el bloqueo inicial de la historia los maneja StoryIntroController.
        if (EsSinglePlayer())
            return;

        if (NetworkBootstrap.Instance != null)
            NetworkBootstrap.Instance.OnGuestLeft += MostrarAvisoCompaneroAbandono;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        bool historiaActiva = storyIntro != null && storyIntro.HistoriaActiva;
        bool tiendaActiva = shopUI != null && shopUI.EstaAbierta;

        // Mientras la historia, el fin de ronda o la tienda están abiertos, ESC no abre el menú de pausa.
        if (historiaActiva || finDeRondaActivo || tiendaActiva)
        {
            if (finDeRondaActivo && Keyboard.current != null)
            {
                if (popupVictoria != null && popupVictoria.activeSelf &&
                    Keyboard.current.sKey.wasPressedThisFrame)
                {
                    OnSiguienteRondaPresionado();
                }
                else if (popupDerrota != null && popupDerrota.activeSelf &&
                    Keyboard.current.qKey.wasPressedThisFrame)
                {
                    OnReintentarPresionado();
                }
            }

            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
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
        bool historiaActiva = storyIntro != null && storyIntro.HistoriaActiva;

        if (!IsOwner || estaPausado || historiaActiva)
            return;

        estaPausado = true;
        LocalPlayerPaused = true;

        stateAtPause = playerHealth != null
            ? playerHealth.State.Value
            : PlayerHealth.PlayerState.Alive;

        movementWasLocked = playerMovement != null && playerMovement.MovementLocked;
        lookWasEnabled = playerLook != null && playerLook.enabled;

        weaponWasLocked =
            weaponSwitcher != null &&
            weaponSwitcher.CurrentWeapon != null &&
            weaponSwitcher.CurrentWeapon.InputLocked;

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

        if (popupMenuHome != null)
            popupMenuHome.SetActive(false);

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

    // =========================================================
    // BLOQUEAR / HABILITAR (solo la pausa; historia tiene su propia versión)
    // =========================================================

    private void BloquearJugador()
    {
        if (playerMovement != null)
            playerMovement.MovementLocked = true;

        if (playerLook != null)
            playerLook.enabled = false;

        if (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null)
            weaponSwitcher.CurrentWeapon.InputLocked = true;
    }

    private void HabilitarJugador()
    {
        if (playerHealth != null && !playerHealth.IsAlive)
            return;

        if (playerMovement != null)
            playerMovement.MovementLocked = false;

        if (playerLook != null)
            playerLook.enabled = true;

        if (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null)
            weaponSwitcher.CurrentWeapon.InputLocked = false;
    }

    private void RestaurarEstadoJugador()
    {
        // En multiplayer la partida sigue corriendo con el menú abierto: si te
        // derribaron, eliminaron o revivieron durante la pausa, la foto guardada
        // ya no vale y PlayerHealth/PlayerSpectator ya configuraron todo para el
        // estado nuevo. Restaurarla reactivaría PlayerLook en pleno modo espectador.
        if (!EsSinglePlayer() &&
            playerHealth != null &&
            playerHealth.State.Value != stateAtPause)
        {
            return;
        }

        if (playerMovement != null)
            playerMovement.MovementLocked = movementWasLocked;

        if (playerLook != null)
            playerLook.enabled = lookWasEnabled;

        if (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null)
            weaponSwitcher.CurrentWeapon.InputLocked = weaponWasLocked;
    }

    // =========================================================
    // MODO
    // =========================================================

    private bool EsSinglePlayer()
    {
        return SceneManager.GetActiveScene().name == "MainSceneSinglePlayer";
    }

    // =========================================================
    // REINICIAR
    // =========================================================

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

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("MainSceneSinglePlayer");
    }

    // =========================================================
    // SALIR
    // =========================================================

    public void Salir()
    {
        if (!IsOwner)
            return;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (NetworkBootstrap.Instance != null)
            NetworkBootstrap.Instance.NotifyIntentionalLeave(); // <- nuevo

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("PantallasUI");
    }

    // =========================================================
    // CURSOR
    // =========================================================

    private void ActualizarCursor()
    {
        if (!IsOwner)
            return;

        if (estaPausado)
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
            NetworkBootstrap.Instance.OnGuestLeft -= MostrarAvisoCompaneroAbandono;

        // Solo si ESTA instancia era la que estaba en pausa: el PauseController de otro jugador
        // que se va de la partida no debe cancelar tu pausa.
        if (estaPausado)
            LocalPlayerPaused = false;

        if (EsSinglePlayer())
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }

    // =========================================================
    // VICTORIA / DERROTA
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

        if (textoPuntajeVictoria != null && playerScore != null)
            textoPuntajeVictoria.text = playerScore.ScoreNetwork.Value.ToString();

        if (textoZombiesVictoria != null && playerScore != null)
            textoZombiesVictoria.text = playerScore.ZombiesEliminadosNetwork.Value.ToString();

        if (textoPrecisionVictoria != null && playerScore != null)
            textoPrecisionVictoria.text = playerScore.PrecisionPorcentaje.ToString() + "%";

        if (textoReaparicionesVictoria != null && playerScore != null)
            textoReaparicionesVictoria.text = playerScore.ReaparicionesNetwork.Value.ToString();

        if (textoCaidasVictoria != null && playerScore != null)
            textoCaidasVictoria.text = playerScore.CaidasNetwork.Value.ToString();



        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

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

        if (textoPuntajeDerrota != null && playerScore != null)
            textoPuntajeDerrota.text = playerScore.ScoreNetwork.Value.ToString();

        if (textoZombiesDerrota != null && playerScore != null)
            textoZombiesDerrota.text = playerScore.ZombiesEliminadosNetwork.Value.ToString();

        if (textoPrecisionDerrota != null && playerScore != null)
            textoPrecisionDerrota.text = playerScore.PrecisionPorcentaje.ToString() + "%";

       /* if (textoRondaDerrota != null && playerScore != null)
            textoRondaDerrota.text = RoundManager.Instance.CurrentRound + " / " + RoundManager.Instance.MaxRounds;*/

        if (textoRondaDerrota != null && playerScore != null)
            textoRondaDerrota.text =
                RoundManager.Instance.IsInfiniteMode
                    ? RoundManager.Instance.CurrentRound.ToString()
                    : RoundManager.Instance.CurrentRound + " / " + RoundManager.Instance.MaxRounds;

        if (textoCaidasDerrota != null && playerScore != null)
        textoCaidasDerrota.text = playerScore.CaidasNetwork.Value.ToString();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void MostrarValoracion()
    {
        if (popupValoracion != null)
            popupValoracion.SetActive(true);
    }

    public void OnSiguienteRondaPresionado()
    {
        if (!IsOwner)
            return;

        finDeRondaActivo = false;

        if (popupVictoria != null)
            popupVictoria.SetActive(false);

        if (hud != null)
            hud.SetActive(true);

        HabilitarJugador();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        pendienteAccionEsVictoria = true;
        MostrarValoracion();
    }

    public void OnReintentarPresionado()
    {
        if (!IsOwner)
            return;

        finDeRondaActivo = false;

        if (popupDerrota != null)
            popupDerrota.SetActive(false);

        pendienteAccionEsVictoria = false;
        MostrarValoracion();
    }

    public void ContinuarDespuesDeRating()
    {
        if (!IsOwner)
            return;

        if (popupValoracion != null)
            popupValoracion.SetActive(false);

        if (pendienteAccionEsVictoria)
            Salir();
        else
            ReiniciarJuego();
    }

    public void MostrarPanelRonda(int ronda, bool esRondaFinal)
    {
        if (!IsOwner)
            return;

        if (panelRonda == null)
        {
            Debug.LogError("El panelRonda no está asignado en el Inspector.");
            return;
        }

        if (textoRonda != null)
            textoRonda.text = esRondaFinal ? "Ronda Final" : "Ronda " + ronda;

        panelRonda.SetActive(true);

        if (ocultarPanelRondaCoroutine != null)
            StopCoroutine(ocultarPanelRondaCoroutine);

        ocultarPanelRondaCoroutine = StartCoroutine(OcultarPanelRondaDespuesDeTiempo(duracionPanelRonda));
    }

    public void OcultarPanelRonda()
    {
        if (panelRonda != null)
            panelRonda.SetActive(false);
    }

    private IEnumerator OcultarPanelRondaDespuesDeTiempo(float segundos)
    {
        yield return new WaitForSeconds(segundos);
        OcultarPanelRonda();
    }

    // =========================================================
    // FASE DE COMPRAS
    // =========================================================

    public void MostrarPanelFaseCompras()
    {
        if (!IsOwner)
            return;

        if (panelFaseCompras == null)
        {
            Debug.LogError("El panelFaseCompras no está asignado en el Inspector.");
            return;
        }

        if (textoFaseCompras != null)
            textoFaseCompras.text = mensajeFaseCompras;

        panelFaseCompras.SetActive(true);

        if (ocultarPanelFaseComprasCoroutine != null)
            StopCoroutine(ocultarPanelFaseComprasCoroutine);

        ocultarPanelFaseComprasCoroutine = StartCoroutine(OcultarPanelFaseComprasDespuesDeTiempo(duracionPanelFaseCompras));
    }

    public void OcultarPanelFaseCompras()
    {
        if (panelFaseCompras != null)
            panelFaseCompras.SetActive(false);
    }

    private IEnumerator OcultarPanelFaseComprasDespuesDeTiempo(float segundos)
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
        StopCoroutine(ocultarPanelCompaneroCoroutine);

    ocultarPanelCompaneroCoroutine =
        StartCoroutine(OcultarAvisoCompaneroDespuesDeTiempo(duracionPanelCompaneroAbandono));
    }

    private IEnumerator OcultarAvisoCompaneroDespuesDeTiempo(float segundos)
    {
        yield return new WaitForSeconds(segundos);

        if (panelCompaneroAbandono != null)
            panelCompaneroAbandono.SetActive(false);
    }
}