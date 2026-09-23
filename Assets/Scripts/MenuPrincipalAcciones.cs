using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuPrincipalAcciones : MonoBehaviour
{
    [Header("Paneles Principales")]
    [SerializeField] private GameObject menuPrincipalPanel;
    [SerializeField] private GameObject comoJugarPanel;
    [SerializeField] private GameObject elegirModoPanel;
    [SerializeField] private GameObject configuracionesPanel;
    [SerializeField] private GameObject logrosPanel;
    [SerializeField] private GameObject creditosInfoPanel;

    [Header("Subpaneles de Configuración (Opcional)")]
    [SerializeField] private GameObject sonidoPanel;
    [SerializeField] private GameObject sensibilidadMousePanel;

    // Nombre exacto de tu escena de un jugador
    private const string EscenaSinglePlayer = "mainSceneSinglePlayer";

    // ==========================================
    // CARGA DE ESCENAS
    // ==========================================

    // Opción 1: Carga directa de la escena single player (no pide escribir nada en el Inspector)
    public void IniciarJuegoSinglePlayer()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(EscenaSinglePlayer);
    }

    // Opción 2: Carga cualquier escena escribiendo su nombre en el recuadro del botón (como lo tenías antes)
    public void IniciarJuego(string nombreEscena)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(nombreEscena);
    }

    // ==========================================
    // CONTROL DE PANELES PRINCIPALES
    // ==========================================

    // Botón "INICIAR PARTIDA"
    public void MostrarModoJuego()
    {
        OcultarTodosLosPaneles();
        if (elegirModoPanel != null) elegirModoPanel.SetActive(true);
    }

    // Botón "COMO JUGAR"
    public void MostrarComoJugar()
    {
        OcultarTodosLosPaneles();
        if (comoJugarPanel != null) comoJugarPanel.SetActive(true);
    }

    // Botón "CONFIGURACIONES"
    public void MostrarConfiguraciones()
    {
        OcultarTodosLosPaneles();
        if (configuracionesPanel != null) configuracionesPanel.SetActive(true);
    }

    // Botón "LOGROS"
    public void MostrarLogros()
    {
        OcultarTodosLosPaneles();
        if (logrosPanel != null) logrosPanel.SetActive(true);
    }

    // Botón "CREDITOS"
    public void MostrarCreditos()
    {
        OcultarTodosLosPaneles();
        if (creditosInfoPanel != null) creditosInfoPanel.SetActive(true);
    }

    // ==========================================
    // SUBPANELES
    // ==========================================

    // Subpanel: Sonido
    public void MostrarSonido()
    {
        if (sonidoPanel != null) sonidoPanel.SetActive(true);
        if (sensibilidadMousePanel != null) sensibilidadMousePanel.SetActive(false);
    }

    // Subpanel: Sensibilidad Mouse
    public void MostrarSensibilidadMouse()
    {
        if (sensibilidadMousePanel != null) sensibilidadMousePanel.SetActive(true);
        if (sonidoPanel != null) sonidoPanel.SetActive(false);
    }

    // Botón "VOLVER" (vuelve al menú principal desde cualquier panel)
    public void VolverAMenuPrincipal()
    {
        OcultarTodosLosPaneles();
        if (menuPrincipalPanel != null) menuPrincipalPanel.SetActive(true);
    }

    // Botón "SALIR"
    public void SalirDelJuego()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // Cierra todos los paneles activos de la pantalla
    private void OcultarTodosLosPaneles()
    {
        if (menuPrincipalPanel != null) menuPrincipalPanel.SetActive(false);
        if (comoJugarPanel != null) comoJugarPanel.SetActive(false);
        if (elegirModoPanel != null) elegirModoPanel.SetActive(false);
        if (configuracionesPanel != null) configuracionesPanel.SetActive(false);
        if (logrosPanel != null) logrosPanel.SetActive(false);
        if (creditosInfoPanel != null) creditosInfoPanel.SetActive(false);
        if (sonidoPanel != null) sonidoPanel.SetActive(false);
        if (sensibilidadMousePanel != null) sensibilidadMousePanel.SetActive(false);
    }
}