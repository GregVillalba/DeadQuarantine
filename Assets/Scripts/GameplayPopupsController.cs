using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public class GameplayPopupsController : MonoBehaviour
{
    [Header("Paneles Emergentes")]
    [SerializeField] private GameObject popupHistoria;
    [SerializeField] private GameObject popupMenuHome;
    [SerializeField] private GameObject panelGanador;
    [SerializeField] private GameObject panelPerdedor;
    [SerializeField] private IntroScreenAnimator introAnimator;

    [Header("Configuración Escenas")]
    [SerializeField] private string nombreEscenaMenu = "PantallasUI";

    [Header("Objetos a desactivar al pausar (HUD, etc.)")]
    [SerializeField] private GameObject[] objetosParaDesactivar;

    [Header("Control del jugador a desactivar al pausar")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private Weapon weapon;

    [Header("Panel Ganador - Ronda")]
   


    public static GameplayPopupsController Instance { get; private set; }


    private bool juegoIniciado;
    private bool estaPausado;

    private void Awake()
    {
        Instance = this;
        if(SceneManager.GetActiveScene().name == "MainScene"){
            if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); // ← antes: StandaloneInputModule

            if (introAnimator != null)
                introAnimator.PlayIn();

            if (popupMenuHome != null)
                popupMenuHome.SetActive(false);

            juegoIniciado = false;
            SetEstadoPausa(true);
        }
        else
        {
            if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            if (popupMenuHome != null)
            popupMenuHome.SetActive(false);

            // Arranca directo, sin popup de historia
            juegoIniciado = true;
            SetEstadoPausa(false);
        }

  /*      if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); // ← antes: StandaloneInputModule

        if (introAnimator != null)
            introAnimator.PlayIn();

        if (popupMenuHome != null)
            popupMenuHome.SetActive(false);

        juegoIniciado = false;
        SetEstadoPausa(true);*/
    }

    private void Update()
    {
        if (panelGanador != null && panelGanador.activeSelf)
        {
            if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
            {
               // SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
                OnSiguienteRondaPresionado();
            }
            return;
        }

        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            ReintentarDespuesDePerder();
        }

        if (!juegoIniciado) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePausa();
        }

        EnforceCursorState();
    }
    
    private void EnforceCursorState()
    {
        CursorLockMode expectedLock = estaPausado ? CursorLockMode.None : CursorLockMode.Locked;
        bool expectedVisible = estaPausado;

        if (Cursor.lockState != expectedLock)
            Cursor.lockState = expectedLock;

        if (Cursor.visible != expectedVisible)
            Cursor.visible = expectedVisible;
    }

    private void TogglePausa()
    {
        if (estaPausado)
            VolverALaPartida();
        else
            AbrirConfirmarSalir();
    }

    // --- POPUP HISTORIA ---
    public void CerrarHistoria()
    {
        if (introAnimator != null)
        {
            introAnimator.PlayOut(() =>
            {
                juegoIniciado = true;
                SetEstadoPausa(false);
            });
        }
        else
        {
            juegoIniciado = true;
            SetEstadoPausa(false);
        }
    }

    // --- MENÚ PAUSA ---
    public void AbrirConfirmarSalir()
    {
        if (popupMenuHome != null)
            popupMenuHome.SetActive(true);

        SetEstadoPausa(true);
    }

    public void VolverALaPartida()
    {
        if (popupMenuHome != null)
            popupMenuHome.SetActive(false);

        SetEstadoPausa(false);
    }

    public void ReiniciarJuego()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void VolverAlMenuPrincipal()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(nombreEscenaMenu);
    }

    private void SetEstadoPausa(bool pausar)
    {
        estaPausado = pausar;

        Time.timeScale = pausar ? 0f : 1f;
        AudioListener.pause = pausar;

        Cursor.visible = pausar;
        Cursor.lockState = pausar ? CursorLockMode.None : CursorLockMode.Locked;

        if (playerMovement != null) playerMovement.enabled = !pausar;
        if (playerLook != null) playerLook.enabled = !pausar;
        if (weapon != null) weapon.enabled = !pausar;

        if (objetosParaDesactivar != null)
        {
            foreach (var go in objetosParaDesactivar)
                if (go != null) go.SetActive(!pausar);
        }
    }
    //================ GANADOR ==

    public void MostrarPanelGanador()
    {
    if (panelGanador != null) 
    {
        panelGanador.SetActive(true);
        SetEstadoPausa(true);
    }
        else Debug.LogError("El panelGanador no está asignado en el inspector.");
    }
    public void OcultarPanelGanador()
    {
        if (panelGanador != null) panelGanador.SetActive(false);
    }

    private void OnSiguienteRondaPresionado()
    {
        OcultarPanelGanador();
        SetEstadoPausa(false);

        //if (RoundManager.Instance != null) RoundManager.Instance.ConfirmarSiguienteRonda();
        if (RoundManager.Instance != null) RoundManager.Instance.StartRound(RoundManager.Instance.CurrentRound + 1);
    }
 //================ PERDEDOR =====================
    public void MostrarPanelPerdedor()
    {
    if (panelPerdedor != null) 
    {
        panelPerdedor.SetActive(true);
        SetEstadoPausa(true);
    }
        else Debug.LogError("El panelPerdedor no está asignado en el inspector.");
    }
    public void OcultarPanelPerdedor()
    {
        if (panelPerdedor != null) panelPerdedor.SetActive(false);
    }

    public void ReintentarDespuesDePerder()
    {
        OcultarPanelPerdedor();
        SetEstadoPausa(false);

        ReiniciarJuego();
    }
}