using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections;

public class GameplayPopupsController : MonoBehaviour
{
    [Header("Paneles de resultado")]
  //  [SerializeField] private GameObject panelGanador;
//    [SerializeField] private GameObject panelPerdedor;
     [SerializeField] private GameObject panelRonda;
    

    [Header("Configuración")]
    [SerializeField] private string nombreEscenaMenu = "PantallasUI";

    public static GameplayPopupsController Instance { get; private set; }

    private void Awake()
    {
        Instance = this;

        if (EventSystem.current == null)
        {
            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
        }

     /*   if (panelGanador != null)
            panelGanador.SetActive(false);

        if (panelPerdedor != null)
            panelPerdedor.SetActive(false);*/
    }

 /*   private void Update()
    {
        // Siguiente ronda desde el panel de ganador
        if (panelGanador != null && panelGanador.activeSelf)
        {
            if (
                Keyboard.current != null &&
                Keyboard.current.sKey.wasPressedThisFrame
            )
            {
                OnSiguienteRondaPresionado();
            }

            return;
        }

        // Reintentar después de perder
        if (
            Keyboard.current != null &&
            Keyboard.current.qKey.wasPressedThisFrame
        )
        {
            ReintentarDespuesDePerder();
        }
    }*/

    // ============================================================
    // GANADOR
    // ============================================================

   /* public void MostrarPanelGanador()
    {
        if (panelGanador != null)
        {
            panelGanador.SetActive(true);
        }
        else
        {
            Debug.LogError(
                "El panelGanador no está asignado en el Inspector."
            );
        }
    }

    public void OcultarPanelGanador()
    {
        if (panelGanador != null)
            panelGanador.SetActive(false);
    }

    public void OnSiguienteRondaPresionado()
    {
        OcultarPanelGanador();

        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.ConfirmarSiguienteRonda();
        }
    }

    // ============================================================
    // PERDEDOR
    // ============================================================

    public void MostrarPanelPerdedor()
    {
        if (panelPerdedor != null)
        {
            panelPerdedor.SetActive(true);
        }
        else
        {
            Debug.LogError(
                "El panelPerdedor no está asignado en el Inspector."
            );
        }
    }

    public void OcultarPanelPerdedor()
    {
        if (panelPerdedor != null)
            panelPerdedor.SetActive(false);
    }

    public void ReintentarDespuesDePerder()
    {
        OcultarPanelPerdedor();

        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().name
        );
    }*/

    // ============================================================
    // VOLVER AL MENÚ
    // ============================================================

    public void VolverAlMenuPrincipal()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(nombreEscenaMenu);
    }

    public void MostrarPanelRonda()
    {
        if (panelRonda != null)
        {
            panelRonda.SetActive(true);
          //  StartCoroutine(OcultarPanelRondaDespuesDeTiempo(3f));
        }
        else
        {
            Debug.LogError(
                "El panelRonda no está asignado en el Inspector."
            );
        }
    }

    public void OcultarPanelRonda()
    {
        if (panelRonda != null)
            panelRonda.SetActive(false);
    }
}