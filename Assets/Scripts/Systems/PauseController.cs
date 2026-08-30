using UnityEngine;
using UnityEngine.InputSystem;

public class PauseController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject hud;
    [SerializeField] private GameObject pausePanel;

    public bool IsPaused { get; private set; }

    private void Start()
    {
        IsPaused = false;

        if (hud != null)
            hud.SetActive(true);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (IsPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        IsPaused = true;

        Debug.Log("PAUSA ACTIVADA");

        if (hud != null)
            hud.SetActive(false);

        if (pausePanel != null)
        {
            Debug.Log("Activando PausePanel: " + pausePanel.name);
            pausePanel.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        IsPaused = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (hud != null)
            hud.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}