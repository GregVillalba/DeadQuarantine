using UnityEngine;

public class ConfiguracionesPanelController : MonoBehaviour
{
    [Header("Pantallas")]
    [SerializeField] private GameObject menuPrincipalPanel;
    [SerializeField] private GameObject configuracionesPanel;
    [SerializeField] private GameObject sonidoPanel;
    [SerializeField] private GameObject sensibilidadMousePanel;

    // Botón "Configuraciones" del menú principal
    public void MostrarConfiguraciones()
    {
        if (menuPrincipalPanel != null) menuPrincipalPanel.SetActive(false);
        if (configuracionesPanel != null) configuracionesPanel.SetActive(true);
        if (sonidoPanel != null) sonidoPanel.SetActive(false);
        if (sensibilidadMousePanel != null) sensibilidadMousePanel.SetActive(false);
    }

    // Botón "Audio" dentro de configuracionesPanel
    public void MostrarSonido()
    {
        if (configuracionesPanel != null) configuracionesPanel.SetActive(false);
        if (sonidoPanel != null) sonidoPanel.SetActive(true);
    }

    // Botón "Sensibilidad Mouse" dentro de configuracionesPanel
    public void MostrarSensibilidadMouse()
    {
        if (configuracionesPanel != null) configuracionesPanel.SetActive(false);
        if (sensibilidadMousePanel != null) sensibilidadMousePanel.SetActive(true);
    }

    // Botón "Volver" dentro de sonidoPanel o sensibilidadMousePanel
    public void VolverAConfiguraciones()    
    {
        if (sonidoPanel != null) sonidoPanel.SetActive(false);
        if (sensibilidadMousePanel != null) sensibilidadMousePanel.SetActive(false);
        if (configuracionesPanel != null) configuracionesPanel.SetActive(true);
    }

    // Botón "Volver" dentro de configuracionesPanel
    public void VolverAMenuPrincipalDesdeConfiguraciones()
    {
        if (configuracionesPanel != null) configuracionesPanel.SetActive(false);
        if (menuPrincipalPanel != null) menuPrincipalPanel.SetActive(true);
    }

    // Cierra cualquier pantalla de configuración abierta y deja visible el
    // menú (lo usa el menú de pausa al reanudar, para que la próxima pausa
    // arranque desde sus botones y no desde un subpanel).
    public void CerrarTodo()
    {
        if (sonidoPanel != null) sonidoPanel.SetActive(false);
        if (sensibilidadMousePanel != null) sensibilidadMousePanel.SetActive(false);
        if (configuracionesPanel != null) configuracionesPanel.SetActive(false);
        if (menuPrincipalPanel != null) menuPrincipalPanel.SetActive(true);
    }
}