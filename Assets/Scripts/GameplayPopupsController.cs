
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Unity.Netcode;

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
                typeof(InputSystemUIInputModule));
        }

        BuscarAchievementHUD();
    }

    public void VolverAlMenuPrincipal()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(nombreEscenaMenu);
    }

    public void MostrarPanelRonda()
    {
        if (panelRonda != null)
            panelRonda.SetActive(true);
        else
            Debug.LogError(
                "[POPUPS] El panelRonda no está asignado en el Inspector.");
    }

    public void OcultarPanelRonda()
    {
        if (panelRonda != null)
            panelRonda.SetActive(false);
    }

    public void MostrarPanelLogro()
    {
        BuscarAchievementHUD();

        if (achievementHUD == null)
        {
            Debug.LogError(
                "[POPUPS] No se encontró el AchievementHUD del jugador local.");
            return;
        }

        // No desactivar ni activar el GameObject del HUD:
        // Show controla su visibilidad mediante CanvasGroup.
    }

    public void MostrarPanelLogro(AchievementId id)
    {
        MostrarPanelLogro(id, AchievementRank.Ninguno);
    }

    public void MostrarPanelLogro(
        AchievementId id,
        AchievementRank rango)
    {
        BuscarAchievementHUD();

        if (achievementHUD == null)
        {
            Debug.LogWarning(
                "[POPUPS] No se encontró el AchievementHUD del jugador local. " +
                "Logro: " + id);
            return;
        }

        achievementHUD.Show(id, rango);
    }

    public void MostrarLogro(AchievementId id)
    {
        MostrarPanelLogro(id);
    }

    public void MostrarLogro(
        AchievementId id,
        AchievementRank rango)
    {
        MostrarPanelLogro(id, rango);
    }

    public void OcultarPanelLogro()
    {
        if (achievementHUD != null)
            achievementHUD.Hide();
    }

    private void BuscarAchievementHUD()
    {
        // En multijugador, buscar primero el HUD del jugador propietario
        // de este cliente, nunca el de otro jugador.
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            var clienteLocal = NetworkManager.Singleton.LocalClient;

            if (clienteLocal == null ||
                clienteLocal.PlayerObject == null)
            {
                achievementHUD = null;
                return;
            }

            achievementHUD =
                clienteLocal.PlayerObject.GetComponentInChildren<AchievementHUD>(
                    true);

            return;
        }

        // En modo individual, utilizar la referencia del Inspector
        // o buscar el HUD de la escena, incluso si está inactivo.
        if (achievementHUD == null)
        {
            achievementHUD =
                FindAnyObjectByType<AchievementHUD>(
                    FindObjectsInactive.Include);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}