using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public class GameplayPopupsController : MonoBehaviour
{
    [Header("Paneles de resultado")]
    [SerializeField]
    private GameObject panelRonda;

    [Header("HUD de logros")]
    [SerializeField]
    private AchievementHUD achievementHUD;

    [Header("Configuración")]
    [SerializeField]
    private string nombreEscenaMenu = "PantallasUI";


    public static GameplayPopupsController Instance { get; private set; }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (EventSystem.current == null)
        {
            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
        }

        if (achievementHUD == null)
        {
            achievementHUD =
                FindAnyObjectByType<AchievementHUD>(
                    FindObjectsInactive.Include
                );
        }
    }


    public void VolverAlMenuPrincipal()
    {
        Time.timeScale = 1f;

        AudioListener.pause = false;

        SceneManager.LoadScene(
            nombreEscenaMenu
        );
    }


    public void MostrarPanelRonda()
    {
        if (panelRonda != null)
        {
            panelRonda.SetActive(true);
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
        {
            panelRonda.SetActive(false);
        }
    }


    public void MostrarPanelLogro()
    {
        if (achievementHUD == null)
        {
            achievementHUD =
                FindAnyObjectByType<AchievementHUD>(
                    FindObjectsInactive.Include
                );
        }

        if (achievementHUD == null)
        {
            Debug.LogError(
                "No se encontró un AchievementHUD en la escena."
            );

            return;
        }

        achievementHUD.gameObject.SetActive(true);
    }


    public void MostrarPanelLogro(AchievementId id)
    {
        if (achievementHUD == null)
        {
            achievementHUD =
                FindAnyObjectByType<AchievementHUD>(
                    FindObjectsInactive.Include
                );
        }

        if (achievementHUD == null)
        {
            Debug.LogError(
                "No se encontró un AchievementHUD en la escena."
            );

            return;
        }

        achievementHUD.Show(id);
    }


    public void MostrarLogro(AchievementId id)
    {
        MostrarPanelLogro(id);
    }


    public void OcultarPanelLogro()
    {
        if (achievementHUD != null)
        {
            achievementHUD.Hide();
        }
    }
}