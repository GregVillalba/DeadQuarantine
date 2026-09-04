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


     [Header("Fin de Ronda")]
    [SerializeField] private GameObject popupVictoria;
    [SerializeField] private GameObject popupDerrota;
    [SerializeField] private TMPro.TextMeshProUGUI textoPuntajeVictoria;
    [SerializeField] private TMPro.TextMeshProUGUI textoPuntajeDerrota;
    [SerializeField] private PlayerScore playerScore;

    [Header("Valoración")]
    [SerializeField] private GameObject popupValoracion; //referencia al panel de valoración




    private bool estaPausado;
    private bool historiaActiva;

    // Guardamos el estado anterior a la pausa.
    private bool movementWasEnabled;
    private bool lookWasEnabled;
    private bool weaponWasEnabled;

   
    private bool finDeRondaActivo;
    private bool pendienteAccionEsVictoria; //"banderita" que guarda si el jugador ganó o perdió



    private void Awake()
    {
        playerScore = GetComponent<PlayerScore>();
        
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

        if (popupVictoria != null)
            popupVictoria.SetActive(false);

        if (popupDerrota != null)
            popupDerrota.SetActive(false);

        //Al iniciar el juego, el popup de valoración debe estar oculto
        if (popupValoracion != null)
            popupValoracion.SetActive(false);
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

    // Mientras la historia o el fin de ronda están abiertos,
    // ESC no abre el menú de pausa.
        if (historiaActiva || finDeRondaActivo)
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
        if (!IsOwner ||estaPausado ||historiaActiva)
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
        HabilitarJugador();

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
            textoPuntajeVictoria.text = "Puntaje final: " + playerScore.ScoreNetwork.Value;



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
            textoPuntajeDerrota.text = "Puntaje obtenido: " + playerScore.ScoreNetwork.Value;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    //Te faltaba este metodo para mostrar el popup de valoración después de la victoria o derrota
    public void MostrarValoracion()
{
    if (popupValoracion != null)
        popupValoracion.SetActive(true);
}

// Llamar desde el botón "Siguiente ronda" del popup de victoria
    public void OnSiguienteRondaPresionado()
    {
        if (!IsOwner)
            return;

        finDeRondaActivo = false;

        if (popupVictoria != null)
            popupVictoria.SetActive(false);

        // Guardamos que la acción pendiente es salir del juego, ya que el jugador ganó

        if (hud != null)
            hud.SetActive(true);

        HabilitarJugador();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        pendienteAccionEsVictoria = true;
        MostrarValoracion();

       // Salir();

}

// Llamar desde el botón "Reintentar" del popup de derrota
    public void OnReintentarPresionado()
    {
        if (!IsOwner)
            return;

        finDeRondaActivo = false;

        if (popupDerrota != null)
            popupDerrota.SetActive(false);
        
        // Guardamos que la acción pendiente es reiniciar el juego, ya que el jugador perdió
        pendienteAccionEsVictoria = false;
        MostrarValoracion();

    //    ReiniciarJuego();
    }
    // Llamar desde el botón "Continuar" del popup de valoración
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

}

