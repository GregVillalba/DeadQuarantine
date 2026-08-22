using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuPrincipalAcciones : MonoBehaviour
{
    [SerializeField] private GameObject menuPrincipalPanel;
    [SerializeField] private GameObject comoJugarPanel;

    // Cargar la escena del juego por su nombre exacto 
    public void IniciarJuego(string nombreEscena)
    {
        SceneManager.LoadScene(nombreEscena);
    }

    // Llamar desde el OnClick del botón "Como_jugar"
    public void MostrarComoJugar()
    {
        if (menuPrincipalPanel != null)
            menuPrincipalPanel.SetActive(false);

        if (comoJugarPanel != null)
            comoJugarPanel.SetActive(true);
    }

    // Útil para el botón "Volver" en el panel ComoJugar
    public void VolverAMenuPrincipal()
    {
        if (menuPrincipalPanel != null)
            menuPrincipalPanel.SetActive(true);

        if (comoJugarPanel != null)
            comoJugarPanel.SetActive(false);
    }

    public void SalirDelJuego()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();
    }
}