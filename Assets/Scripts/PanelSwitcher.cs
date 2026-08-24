using UnityEngine;

public class PanelSwitcher : MonoBehaviour
{
    [Header("Paneles a alternar")]
    [SerializeField] private GameObject panelCardControl1;
    [SerializeField] private GameObject panelCardControl2;

    /// <summary>
    /// Oculta CardControl (1) y activa CardControl
    /// </summary>
    public void MostrarCardControl()
    {
        if (panelCardControl1 != null)
        {
            panelCardControl1.SetActive(false);
        }

        if (panelCardControl2 != null)
        {
            panelCardControl2.SetActive(true);
        }
    }

    /// <summary>
    /// Oculta CardControl y activa CardControl (1)
    /// </summary>
    public void MostrarCardControl1()
    {
        if (panelCardControl2 != null)
        {
            panelCardControl2.SetActive(false);
        }

        if (panelCardControl1 != null)
        {
            panelCardControl1.SetActive(true);
        }
    }
}