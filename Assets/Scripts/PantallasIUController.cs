using UnityEngine;

public class PantallasUIController : MonoBehaviour
{
    public static bool VolverAElegirModo = false;

    [Header("PANTALLAS")]
    [SerializeField] private GameObject menuPrincipalPanel;
    [SerializeField] private GameObject comoJugarPanel;
    [SerializeField] private GameObject elegirModoPanel;

    private void Start()
    {
        if (VolverAElegirModo)
        {
            VolverAElegirModo = false;
            MostrarElegirModo();
        }
        else
        {
            MostrarMenuPrincipal();
        }
    }

    public void MostrarMenuPrincipal()
    {
        if (menuPrincipalPanel != null)
            menuPrincipalPanel.SetActive(true);

        if (comoJugarPanel != null)
            comoJugarPanel.SetActive(false);

        if (elegirModoPanel != null)
            elegirModoPanel.SetActive(false);
    }

    public void MostrarElegirModo()
    {
        if (menuPrincipalPanel != null)
            menuPrincipalPanel.SetActive(false);

        if (comoJugarPanel != null)
            comoJugarPanel.SetActive(false);

        if (elegirModoPanel != null)
            elegirModoPanel.SetActive(true);
    }
}