using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MenuPrincipalAcciones : MonoBehaviour
{
    [Header("Paneles Principales")]
    [SerializeField] private GameObject menuPrincipalPanel;
    [SerializeField] private GameObject comoJugarPanel;
    [SerializeField] private GameObject RANKINGPanel;
    [SerializeField] private GameObject elegirModoPanel;
    [SerializeField] private GameObject configuracionesPanel;
    [SerializeField] private GameObject logrosPanel;
    [SerializeField] private GameObject creditosInfoPanel;

    [Header("Subpaneles de Configuraci�n (Opcional)")]
    [SerializeField] private GameObject sonidoPanel;
    [SerializeField] private GameObject sensibilidadMousePanel;

    // Nombre exacto de tu escena de un jugador
    private const string EscenaSinglePlayer = "mainSceneSinglePlayer";
    private const string EscenaTutorial = "TutorialScene";

    [Header("Transferir Datos (Copiar ID)")]
    [SerializeField] private TMP_Text txtFeedbackTransferir;
    private Coroutine corrutinaFeedbackTransferir;

    // ==========================================
    // COPIAR ID / TRANSFERENCIA DE DATOS
    // ==========================================

    public void TransferirDatosCopiarID()
    {
        // 1. Obtiene el ID completo registrado en PlayerIdentity
        string idJugador = PlayerIdentity.GetUserId();

        // 2. Copia el texto al portapapeles del sistema
        GUIUtility.systemCopyBuffer = idJugador;
        Debug.Log($"<color=cyan>[TRANSFERIR DATOS]</color> ID copiado: {idJugador}");

        // 3. Muestra el feedback visual durante 2 segundos
        if (txtFeedbackTransferir != null)
        {
            if (corrutinaFeedbackTransferir != null)
                StopCoroutine(corrutinaFeedbackTransferir);

            corrutinaFeedbackTransferir = StartCoroutine(RutinaFeedbackTransferir("¡Copiado exitosamente!"));
        }
    }

    private IEnumerator RutinaFeedbackTransferir(string mensaje)
    {
        txtFeedbackTransferir.text = mensaje;
        txtFeedbackTransferir.gameObject.SetActive(true);

        yield return new WaitForSeconds(2.0f);

        txtFeedbackTransferir.text = string.Empty;
        corrutinaFeedbackTransferir = null;
    }



    // ==========================================
    // CARGA DE ESCENAS
    // ==========================================

    // Opci�n 1: Carga directa de la escena single player (no pide escribir nada en el Inspector)
    public void IniciarJuegoSinglePlayer()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(EscenaSinglePlayer);
    }

    public void IniciarTutorial()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(EscenaTutorial);
    }

    // Opci�n 2: Carga cualquier escena escribiendo su nombre en el recuadro del bot�n (como lo ten�as antes)
    public void IniciarJuego(string nombreEscena)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(nombreEscena);
    }

    // ==========================================
    // CONTROL DE PANELES PRINCIPALES
    // ==========================================

    // Bot�n "INICIAR PARTIDA"
    public void MostrarModoJuego()
    {
        OcultarTodosLosPaneles();
        if (elegirModoPanel != null) elegirModoPanel.SetActive(true);
    }

    // Bot�n "COMO JUGAR"
    public void MostrarComoJugar()
    {
        OcultarTodosLosPaneles();
        if (comoJugarPanel != null) comoJugarPanel.SetActive(true);
    }

    // Bot�n "ranking"
    public void MostrarRANKING()
    {
        OcultarTodosLosPaneles();
        if (RANKINGPanel != null) RANKINGPanel.SetActive(true);
    }

    // Bot�n "CONFIGURACIONES"
    public void MostrarConfiguraciones()
    {
        OcultarTodosLosPaneles();
        if (configuracionesPanel != null) configuracionesPanel.SetActive(true);
    }

    // Bot�n "LOGROS"
    public void MostrarLogros()
    {
        OcultarTodosLosPaneles();
        if (logrosPanel != null) logrosPanel.SetActive(true);
    }

    // Bot�n "CREDITOS"
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

    // Bot�n "VOLVER" (vuelve al men� principal desde cualquier panel)
    public void VolverAMenuPrincipal()
    {
        OcultarTodosLosPaneles();
        if (menuPrincipalPanel != null) menuPrincipalPanel.SetActive(true);
    }

    // Bot�n "SALIR"
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

        if (RANKINGPanel != null) RANKINGPanel.SetActive(false);
        if (comoJugarPanel != null) comoJugarPanel.SetActive(false);
        if (elegirModoPanel != null) elegirModoPanel.SetActive(false);
        if (configuracionesPanel != null) configuracionesPanel.SetActive(false);
        if (logrosPanel != null) logrosPanel.SetActive(false);
        if (creditosInfoPanel != null) creditosInfoPanel.SetActive(false);
        if (sonidoPanel != null) sonidoPanel.SetActive(false);
        if (sensibilidadMousePanel != null) sensibilidadMousePanel.SetActive(false);
    }

    


}