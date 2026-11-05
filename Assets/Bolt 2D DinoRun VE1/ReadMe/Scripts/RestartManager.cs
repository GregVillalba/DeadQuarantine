using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class RestartUIRawImage : MonoBehaviour
{
    [Header("Referencias de UI")]
    [SerializeField] private GameObject rawImagePressed;   // RawImage_again_presed
    [SerializeField] private GameObject rawImageGameOver;  // RawImage_gameover

    [Header("Configuración")]
    [SerializeField] private float delaySeconds = 1f;

    private Button btn;
    private RawImage myRawImage;
    private bool isRestarting = false;

    private void Awake()
    {
        btn = GetComponent<Button>();
        myRawImage = GetComponent<RawImage>();

        if (btn != null)
        {
            btn.onClick.AddListener(OnButtonClicked);
        }
    }

    private void OnEnable()
    {
        // Restablecer estados cada vez que se active este objeto/pantalla
        isRestarting = false;

        if (myRawImage != null)
        {
            myRawImage.enabled = true; // Botón normal visible
        }

        if (btn != null)
        {
            btn.interactable = true; // Botón listo para clickear
        }

        if (rawImageGameOver != null)
        {
            rawImageGameOver.SetActive(true); // Cartel de Game Over visible
        }

        if (rawImagePressed != null)
        {
            rawImagePressed.SetActive(false); // Imagen presionada oculta al inicio
        }
    }

    private void OnDestroy()
    {
        if (btn != null)
        {
            btn.onClick.RemoveListener(OnButtonClicked);
        }
    }

    public void OnButtonClicked()
    {
        if (isRestarting) return;
        isRestarting = true;

        if (btn != null)
        {
            btn.interactable = false;
        }

        StartCoroutine(RestartSequence());
    }

    private IEnumerator RestartSequence()
    {
        // 1. Mostrar la imagen de botón presionado
        if (rawImagePressed != null)
        {
            rawImagePressed.SetActive(true);
        }

        // 2. Ocultar la imagen del botón normal
        if (myRawImage != null)
        {
            myRawImage.enabled = false;
        }

        // 3. Esperar 1 segundo (ignora pausas de tiempo de juego si se pausó la física)
        yield return new WaitForSecondsRealtime(delaySeconds);

        // 4. Ocultar el cartel y la imagen presionada
        if (rawImageGameOver != null)
        {
            rawImageGameOver.SetActive(false);
        }

        if (rawImagePressed != null)
        {
            rawImagePressed.SetActive(false);
        }

        // 5. Reiniciar la escena
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }
}