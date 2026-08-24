using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuPrincipalAcciones : MonoBehaviour
{
    [SerializeField] private GameObject menuPrincipalPanel;
    [SerializeField] private GameObject comoJugarPanel;
    [SerializeField] private GameObject elegirModoPanel;

    // Cargar la escena del juego por su nombre exacto 
    public void IniciarJuego(string nombreEscena)
    {
        SceneManager.LoadScene(nombreEscena);
    }

    // Llamar desde el OnClick del botón "INICIAR JUEGO"
    public void MostrarModoJuego()
    {
        if (menuPrincipalPanel != null)
            menuPrincipalPanel.SetActive(false);

        if (elegirModoPanel != null)
            elegirModoPanel.SetActive(true);
    }

    // Llamar desde el OnClick del botón "Como_jugar"
    public void MostrarComoJugar()
    {
        if (menuPrincipalPanel != null)
            menuPrincipalPanel.SetActive(false);

        if (comoJugarPanel != null)
            comoJugarPanel.SetActive(true);

        if (elegirModoPanel != null)
            elegirModoPanel.SetActive(false);
    }

    // Útil para el botón "Volver" en el panel ComoJugar
    public void VolverAMenuPrincipal()
    {
        if (menuPrincipalPanel != null)
            menuPrincipalPanel.SetActive(true);

        if (comoJugarPanel != null)
            comoJugarPanel.SetActive(false);

        if (elegirModoPanel != null)
            elegirModoPanel.SetActive(false);
    }

    public void SalirDelJuego()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();

        // Detiene el modo Play si estás dentro del editor de Unity
        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
