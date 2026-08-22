using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class GameplayPopupsController : MonoBehaviour
{
    [Header("Paneles Emergentes")]
    [SerializeField] private GameObject popupHistoria;
    [SerializeField] private GameObject popupMenuHome;

    [Header("Configuración Escenas")]
    [SerializeField] private string nombreEscenaMenu = "PantallasUI";

    [Header("Objetos / Componentes a desactivar al pausar")]
    [SerializeField] private GameObject[] objetosParaDesactivar;
    [SerializeField] private Behaviour[] componentesParaDesactivar;

    private void Awake()
    {
        Debug.Log(
            "AWAKE GameplayPopupsController | " +
            "Objeto: " + gameObject.name +
            " | ID: " + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this)
        );

        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (popupHistoria != null)
            popupHistoria.SetActive(true);

        if (popupMenuHome != null)
            popupMenuHome.SetActive(false);

        SetEstadoPausa(true);
    }

    private void Update()
    {
        if (!Cursor.visible || Cursor.lockState != CursorLockMode.None)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    // --- POPUP HISTORIA ---
    public void CerrarHistoria()
    {
        if (popupHistoria != null)
            popupHistoria.SetActive(false);

        SetEstadoPausa(false);
    }

    // --- BOTON HOME ---
    public void AbrirConfirmarSalir()
    {
        if (popupHistoria != null)
            popupHistoria.SetActive(false);

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
        Time.timeScale = pausar ? 0f : 1f;
        AudioListener.pause = pausar;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (objetosParaDesactivar != null)
        {
            foreach (var go in objetosParaDesactivar)
                if (go != null) go.SetActive(!pausar);
        }

        if (componentesParaDesactivar != null)
        {
            foreach (var comp in componentesParaDesactivar)
                if (comp != null) comp.enabled = !pausar;
        }
    }
}