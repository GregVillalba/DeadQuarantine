using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PauseController : NetworkBehaviour
{
    [Header("Pausa")]
    [SerializeField] private GameObject popupMenuHome;

    [Header("Historia Singleplayer")]
    [SerializeField] private GameObject popupHistoria;

    [Header("HUD del jugador")]
    [SerializeField] private GameObject hud;

    [Header("Controles del jugador")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private Weapon weapon;

    [Header("Botón Reiniciar")]
    [SerializeField] private GameObject botonReiniciar;

    private bool estaPausado;
    private bool historiaActiva;

    private void Awake()
    {
        if (popupMenuHome != null)
            popupMenuHome.SetActive(false);

        if (popupHistoria != null)
            popupHistoria.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        ActualizarBotonReiniciar();

        // SINGLEPLAYER
        if (EsSinglePlayer())
        {
            MostrarHistoriaInicial();
            return;
        }

        // MULTIPLAYER
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        // Mientras la historia está abierta,
        // ESC no abre el menú de pausa.
        if (historiaActiva)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (estaPausado)
                ReanudarJuego();
            else
                PausarJuego();
        }

        ActualizarCursor();
    }

    // =========================================================
    // HISTORIA
    // =========================================================

    private void MostrarHistoriaInicial()
    {
        historiaActiva = true;

        // Ocultar HUD.
        if (hud != null)
            hud.SetActive(false);

        // Bloquear jugador.
        BloquearJugador();

        // Mostrar historia.
        if (popupHistoria != null)
            popupHistoria.SetActive(true);

        // Liberar cursor.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ContinuarHistoria()
    {
        if (!IsOwner)
            return;

        if (!historiaActiva)
            return;

        historiaActiva = false;

        // Ocultar historia.
        if (popupHistoria != null)
            popupHistoria.SetActive(false);

        // Mostrar HUD.
        if (hud != null)
            hud.SetActive(true);

        // Habilitar jugador.
        HabilitarJugador();

        // Bloquear cursor nuevamente.
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // Singleplayer = Host local.
        // El servidor inicia la ronda.
        if (IsServer &&
            RoundManager.Instance != null)
        {
            RoundManager.Instance.StartRound(1);
        }
    }

    // =========================================================
    // PAUSA
    // =========================================================

    public void PausarJuego()
    {
        if (!IsOwner || estaPausado || historiaActiva)
            return;

        estaPausado = true;

        if (popupMenuHome != null)
            popupMenuHome.SetActive(true);

        BloquearJugador();

        // SOLO SINGLEPLAYER
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

        if (popupMenuHome != null)
            popupMenuHome.SetActive(false);

        HabilitarJugador();

        // SOLO SINGLEPLAYER
        if (EsSinglePlayer())
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // =========================================================
    // BLOQUEAR PLAYER
    // =========================================================

    private void BloquearJugador()
    {
        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerLook != null)
            playerLook.enabled = false;

        if (weapon != null)
            weapon.enabled = false;
    }

    private void HabilitarJugador()
    {
        if (playerMovement != null)
            playerMovement.enabled = true;

        if (playerLook != null)
            playerLook.enabled = true;

        if (weapon != null)
            weapon.enabled = true;
    }

    // =========================================================
    // MODO
    // =========================================================

    private bool EsSinglePlayer()
    {
        return SceneManager.GetActiveScene().name ==
               "MainSceneSinglePlayer";
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

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

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

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene("PantallasUI");
    }

    // =========================================================
    // CURSOR
    // =========================================================

    private void ActualizarCursor()
    {
        if (!IsOwner)
            return;

        if (estaPausado || historiaActiva)
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
        if (EsSinglePlayer())
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}