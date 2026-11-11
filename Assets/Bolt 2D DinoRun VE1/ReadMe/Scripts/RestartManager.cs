using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Va en el botón de reinicio (RawImage_again_no presed, que tiene el componente Button).
/// Al presionarlo muestra la imagen "presionado", espera unos segundos y reinicia el minijuego.
/// </summary>
public class RestartUIRawImage : MonoBehaviour
{
    [Header("Referencias de UI")]
    [SerializeField] private GameObject rawImagePressed;   // RawImage_again_presed
    [SerializeField] private GameObject rawImageGameOver;  // RawImage_gameover

    [Header("Configuración")]
    [Tooltip("Segundos que se ve la imagen 'presionado' antes de reiniciar")]
    [SerializeField, Min(0f)] private float delaySeconds = 0.25f;

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
        //    (se desactiva el componente, NO el objeto, para que esta corrutina siga corriendo)
        if (myRawImage != null)
        {
            myRawImage.enabled = false;
        }

        // 3. Esperar (ignora pausas de tiempo de juego)
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

        // 5. Reiniciar el minijuego
        if (MinijuegoArcadeTrigger.InstanciaActiva != null)
        {
            // Abierto desde la máquina arcade: se recarga solo la escena del minijuego (aditiva),
            // sin tocar el juego principal.
            MinijuegoArcadeTrigger.InstanciaActiva.ReiniciarMinijuego();
        }
        else
        {
            // Probando la escena sola, sin pasar por el arcade
            SceneManager.LoadScene(gameObject.scene.name);
        }
    }
}
