using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider))]
public class ArcadeMinijuegoTrigger : MonoBehaviour
{
    [Header("Escena a Cargar")]
    [SerializeField] private string nombreEscenaMinijuego = "Demo";

    [Header("Configuración del Jugador")]
    [SerializeField] private string playerTag = "Player";

    // Se asigna automáticamente al detectar al jugador
    private GameObject promptUI;
    private bool jugadorCerca = false;

    void Awake()
    {
        // Asegurar que el collider sea de tipo disparador
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    void Update()
    {
        if (!jugadorCerca) return;

        bool ePressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            ePressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.E))
        {
            ePressed = true;
        }
#endif

        if (ePressed)
        {
            IniciarMinijuego();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Detectar si el objeto que entró es el Player
        if (other.CompareTag(playerTag) || other.name.ToLower().Contains("player"))
        {
            jugadorCerca = true;

            // Busca el cartel Interface_T-REX dentro de la jerarquía del Player si aún no lo tiene
            if (promptUI == null)
            {
                Transform found = BuscarTransformRecursivo(other.transform.root, "Interface_T-REX");
                if (found != null)
                {
                    promptUI = found.gameObject;
                }
                else
                {
                    // Alternativa: buscarlo en toda la escena activa
                    promptUI = GameObject.Find("Interface_T-REX");
                }
            }

            if (promptUI != null)
            {
                promptUI.SetActive(true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag) || other.name.ToLower().Contains("player"))
        {
            jugadorCerca = false;

            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }
        }
    }

    private void IniciarMinijuego()
    {
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (Application.CanStreamedLevelBeLoaded(nombreEscenaMinijuego))
        {
            SceneManager.LoadScene(nombreEscenaMinijuego);
        }
        else
        {
            Debug.LogError($"[ArcadeTrigger] La escena '{nombreEscenaMinijuego}' no está en Build Profiles.");
        }
    }

    private Transform BuscarTransformRecursivo(Transform parent, string targetName)
    {
        if (parent.name == targetName) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = BuscarTransformRecursivo(parent.GetChild(i), targetName);
            if (result != null) return result;
        }

        return null;
    }

    private void OnDisable()
    {
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
        jugadorCerca = false;
    }
}