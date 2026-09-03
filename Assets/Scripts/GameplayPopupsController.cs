using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections;

public class GameplayPopupsController : MonoBehaviour
{
    [Header("Paneles de resultado")]
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

    }

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