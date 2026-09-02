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

    [Header("Vida del jugador")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Botón Reiniciar")]
    [SerializeField] private GameObject botonReiniciar;

    private bool estaPausado;
    private bool historiaActiva;

    // Guardamos el estado anterior a la pausa.
    private bool movementWasEnabled;
    private bool lookWasEnabled;
    private bool weaponWasEnabled;

    private void Awake()
    {
        if (popupMenuHome != null)
            popupMenuHome.SetActive(false);

        if (popupHistoria != null)
            popupHistoria.SetActive(false);

        if (playerHealth == null)
            playerHealth =
                GetComponent<PlayerHealth>();

        if (playerMovement == null)
            playerMovement =
                GetComponent<PlayerMovement>();

        if (playerLook == null)
            playerLook =
                GetComponentInChildren<PlayerLook>(true);

        if (weapon == null)
            weapon =
                GetComponentInChildren<Weapon>(true);
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
        {
            MostrarHistoriaInicial();
            return;
        }

        Cursor.visible = false;
        Cursor.lockState =
            CursorLockMode.Locked;
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (historiaActiva)
            return;

        if (
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame
        )
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

        if (hud != null)
            hud.SetActive(false);

        BloquearJugador();

        if (popupHistoria != null)
            popupHistoria.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState =
            CursorLockMode.None;
    }

    public void ContinuarHistoria()
    {
        if (!IsOwner)
            return;

        if (!historiaActiva)
            return;

        historiaActiva = false;

        if (popupHistoria != null)
            popupHistoria.SetActive(false);

        if (hud != null)
            hud.SetActive(true);

        HabilitarJugador();

        Cursor.visible = false;
        Cursor.lockState =
            CursorLockMode.Locked;

        if (
            IsServer &&
            RoundManager.Instance != null
        )
        {
            RoundManager.Instance.StartRound(1);
        }
    }

    // =========================================================
    // PAUSA
    // =========================================================

    public void PausarJuego()
    {
        if (
            !IsOwner ||
            estaPausado ||
            historiaActiva
        )
        {
            return;
        }

        estaPausado = true;

        // Guardar exactamente cómo estaba el jugador.
        movementWasEnabled =
            playerMovement != null &&
            playerMovement.enabled;

        lookWasEnabled =
            playerLook != null &&
            playerLook.enabled;

        weaponWasEnabled =
            weapon != null &&
            weapon.enabled;

        if (popupMenuHome != null)
            popupMenuHome.SetActive(true);

        BloquearJugador();

        if (EsSinglePlayer())
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        Cursor.visible = true;
        Cursor.lockState =
            CursorLockMode.None;
    }

    public void ReanudarJuego()
    {
        if (
            !IsOwner ||
            !estaPausado
        )
        {
            return;
        }

        estaPausado = false;

        if (popupMenuHome != null)
            popupMenuHome.SetActive(false);

        // Restaurar EXACTAMENTE el estado anterior.
        RestaurarEstadoJugador();

        if (EsSinglePlayer())
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        Cursor.visible = false;
        Cursor.lockState =
            CursorLockMode.Locked;
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

    // =========================================================
    // HABILITAR PLAYER
    // =========================================================

    private void HabilitarJugador()
    {
        // La historia solamente puede habilitar controles
        // si el jugador está realmente vivo.
        if (
            playerHealth != null &&
            !playerHealth.IsAlive
        )
        {
            return;
        }

        if (playerMovement != null)
            playerMovement.enabled = true;

        if (playerLook != null)
            playerLook.enabled = true;

        if (weapon != null)
            weapon.enabled = true;
    }

    // =========================================================
    // RESTAURAR ESTADO PREVIO A LA PAUSA
    // =========================================================

    private void RestaurarEstadoJugador()
    {
        if (playerMovement != null)
        {
            playerMovement.enabled =
                movementWasEnabled;
        }

        if (playerLook != null)
        {
            playerLook.enabled =
                lookWasEnabled;
        }

        if (weapon != null)
        {
            weapon.enabled =
                weaponWasEnabled;
        }
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
        {
            botonReiniciar.SetActive(
                EsSinglePlayer()
            );
        }
    }

    public void ReiniciarJuego()
    {
        if (
            !IsOwner ||
            !EsSinglePlayer()
        )
        {
            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening
        )
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(
            "MainSceneSinglePlayer"
        );
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

        if (
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening
        )
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(
            "PantallasUI"
        );
    }

    // =========================================================
    // CURSOR
    // =========================================================

    private void ActualizarCursor()
    {
        if (!IsOwner)
            return;

        if (
            estaPausado ||
            historiaActiva
        )
        {
            Cursor.visible = true;
            Cursor.lockState =
                CursorLockMode.None;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState =
                CursorLockMode.Locked;
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