using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuPrincipalAcciones : MonoBehaviour
{
    [Header("Paneles Principales")]
    [SerializeField] private GameObject menuPrincipalPanel;
    [SerializeField] private GameObject comoJugarPanel;
    [SerializeField] private GameObject elegirModoPanel;
    [SerializeField] private GameObject elegirModalidadJuegoPanel;
    [SerializeField] private GameObject configuracionesPanel;
    [SerializeField] private GameObject logrosPanel;
    [SerializeField] private GameObject creditosInfoPanel;
    [SerializeField] private GameObject RANKINGPanel;

    [Header("Subpaneles de Configuración")]
    [SerializeField] private GameObject sonidoPanel;
    [SerializeField] private GameObject sensibilidadMousePanel;

    [Header("Transferir Datos (Copiar ID)")]
    [SerializeField] private TMP_Text txtFeedbackTransferir;

    private Coroutine corrutinaFeedbackTransferir;
    private const string EscenaSinglePlayer = "mainSceneSinglePlayer";
    private const string EscenaTutorial = "TutorialScene";

    public void MostrarModoJuego()
    {
        OcultarTodosLosPaneles();
        if (elegirModoPanel != null) elegirModoPanel.SetActive(true);
    }

    public void MostrarElegirModalidadJuego()
    {
        OcultarTodosLosPaneles();
        if (elegirModalidadJuegoPanel != null) elegirModalidadJuegoPanel.SetActive(true);
    }

    public void MostrarComoJugar()
    {
        OcultarTodosLosPaneles();
        if (comoJugarPanel != null) comoJugarPanel.SetActive(true);
    }

    public void MostrarRANKING()
    {
        OcultarTodosLosPaneles();
        if (RANKINGPanel != null) RANKINGPanel.SetActive(true);
    }

    public void MostrarConfiguraciones()
    {
        OcultarTodosLosPaneles();
        if (configuracionesPanel != null) configuracionesPanel.SetActive(true);
    }

    public void MostrarLogros()
    {
        OcultarTodosLosPaneles();
        if (logrosPanel != null) logrosPanel.SetActive(true);
    }

    public void MostrarCreditos()
    {
        OcultarTodosLosPaneles();
        if (creditosInfoPanel != null) creditosInfoPanel.SetActive(true);
    }

    public void MostrarSonido()
    {
        if (sonidoPanel != null) sonidoPanel.SetActive(true);
        if (sensibilidadMousePanel != null) sensibilidadMousePanel.SetActive(false);
    }

    public void MostrarSensibilidadMouse()
    {
        if (sensibilidadMousePanel != null) sensibilidadMousePanel.SetActive(true);
        if (sonidoPanel != null) sonidoPanel.SetActive(false);
    }

    public void VolverAMenuPrincipal()
    {
        OcultarTodosLosPaneles();
        if (menuPrincipalPanel != null) menuPrincipalPanel.SetActive(true);
    }

    public void OcultarTodosLosPaneles()
    {
        if (menuPrincipalPanel != null) menuPrincipalPanel.SetActive(false);
        if (RANKINGPanel != null) RANKINGPanel.SetActive(false);
        if (comoJugarPanel != null) comoJugarPanel.SetActive(false);
        if (elegirModoPanel != null) elegirModoPanel.SetActive(false);
        if (elegirModalidadJuegoPanel != null) elegirModalidadJuegoPanel.SetActive(false);
        if (configuracionesPanel != null) configuracionesPanel.SetActive(false);
        if (logrosPanel != null) logrosPanel.SetActive(false);
        if (creditosInfoPanel != null) creditosInfoPanel.SetActive(false);
        if (sonidoPanel != null) sonidoPanel.SetActive(false);
        if (sensibilidadMousePanel != null) sensibilidadMousePanel.SetActive(false);
    }

    public void TransferirDatosCopiarID()
    {
        string idJugador = PlayerIdentity.GetUserId();
        GUIUtility.systemCopyBuffer = idJugador;
        Debug.Log($"<color=cyan>[TRANSFERIR DATOS]</color> ID copiado: {idJugador}");

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
        yield return new WaitForSeconds(2f);
        txtFeedbackTransferir.text = string.Empty;
        corrutinaFeedbackTransferir = null;
    }

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

    public void IniciarJuego(string nombreEscena)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(nombreEscena);
    }

    public void SalirDelJuego()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}