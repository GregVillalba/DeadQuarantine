using UnityEngine;
using UnityEngine.SceneManagement;

// Diagnóstico: avisa cuando un frame tarda demasiado. Un congelamiento largo del hilo principal
// puede hacer que Relay dé por inactiva la conexión (AllocationInvalid) y se caiga la red.
// Se crea solo al iniciar el juego: no hace falta ponerlo en ninguna escena.
public class FrameHitchLogger : MonoBehaviour
{
    [SerializeField] private float umbralSegundos = 1f;

    private static FrameHitchLogger instancia;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("FrameHitchLogger");
        instancia = go.AddComponent<FrameHitchLogger>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (dt >= umbralSegundos)
        {
            Debug.LogWarning(
                "[Hitch] Frame de " + dt.ToString("F2") + " s en la escena '" +
                SceneManager.GetActiveScene().name + "' (t=" +
                Time.realtimeSinceStartup.ToString("F1") + ")."
            );
        }
    }
}

