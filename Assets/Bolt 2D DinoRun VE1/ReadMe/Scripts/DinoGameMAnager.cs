using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class DinoGameManager : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("El Canvas o panel inicial de Game Over y los textos")]
    [SerializeField] private GameObject gameOverCanvas;

    [Header("Nombre de la Escena Principal")]
    [Tooltip("La escena a la que vas a volver cuando presiones ESC")]
    [SerializeField] private string mainWorldSceneName = "MundoPrincipal";

    private DinoPlayer player;
    private bool esperandoInicio = false;

    void Start()
    {
        player = Object.FindFirstObjectByType<DinoPlayer>();

        // Al iniciar, si el player ya perdió o se acaba de cargar la escena
        if (player != null && player.isGameOver)
        {
            esperandoInicio = true;
            Time.timeScale = 0f; // Pausar todo el movimiento al inicio si se requiere
        }
    }

    void Update()
    {
        // --- 1. DETECTAR tecla ESPACIO para iniciar o reiniciar ---
        bool spacePressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            spacePressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.Space))
        {
            spacePressed = true;
        }
#endif

        if (spacePressed)
        {
            // Si el juego está en pantalla de Game Over y presionamos Espacio, se reinicia la partida
            if (player != null && player.isGameOver)
            {
                Time.timeScale = 1f; // Restaurar el tiempo
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }

        // --- 2. DETECTAR tecla ESC para salir ---
        bool escPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            escPressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            escPressed = true;
        }
#endif

        if (escPressed)
        {
            SalirDelMinijuego();
        }
    }

    public void SalirDelMinijuego()
    {
        Debug.Log("Saliendo del minijuego...");

        // Restauramos el flujo del tiempo por las dudas
        Time.timeScale = 1f;

        // Comportamiento A: Volver al escenario principal cargando su escena
        if (Application.CanStreamedLevelBeLoaded(mainWorldSceneName))
        {
            SceneManager.LoadScene(mainWorldSceneName);
        }
        else
        {
            // Comportamiento B: Si se cargó de forma aditiva, la descargamos
            try
            {
                SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene().buildIndex);
            }
            catch
            {
                // Comportamiento C: Si estamos en el editor sin otras escenas, cierra la app (o frena el Play)
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
    }
}