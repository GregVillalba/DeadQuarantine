using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class ArcadeDinoMachine : MonoBehaviour
{
    public static ArcadeDinoMachine InstanciaActiva { get; private set; }

    [Header("Configuración de Escena")]
    [SerializeField] private string nombreEscenaMinijuego = "DEMO";

    [Header("Suministros (Estilo Tragamonedas)")]
    [Tooltip("Prefab del recurso que va a arrojar (por ejemplo RecursoDrop).")]
    [SerializeField] private GameObject prefabRecurso;
    [Tooltip("De dónde sale el drop. Si queda vacío se usa la máquina.")]
    [SerializeField] private Transform puntoSpawnDrop;
    [SerializeField, Min(1)] private int cantidadADropear = 1;

    [Header("Lanzamiento e Impulso")]
    [SerializeField, Min(0f)] private float fuerzaImpulso = 3.5f;
    [SerializeField, Min(0f)] private float dispersion = 0.35f;

    [HideInInspector] public bool estaJugando = false;

    private void Awake()
    {
        InstanciaActiva = this;
    }

    private void OnEnable()
    {
        InstanciaActiva = this;
    }

    private void OnDestroy()
    {
        if (InstanciaActiva == this)
            InstanciaActiva = null;
    }

    /// <summary>
    /// Dropea el recurso usando la misma física y lanzamiento que la tragamonedas.
    /// </summary>
    public void DropearNafta()
    {
        if (prefabRecurso == null)
        {
            Debug.LogWarning($"[ArcadeDinoMachine] No hay prefabRecurso asignado en {name}.", this);
            return;
        }

        StartCoroutine(RutinaArrojar());
    }

    private IEnumerator RutinaArrojar()
    {
        Transform origen = puntoSpawnDrop != null ? puntoSpawnDrop : transform;

        for (int i = 0; i < cantidadADropear; i++)
        {
            Vector2 azar = Random.insideUnitCircle * dispersion;
            Vector3 posicion = origen.position + new Vector3(azar.x, 0.2f, azar.y);

            GameObject item = Instantiate(prefabRecurso, posicion, Quaternion.identity);

            // Si el objeto tiene Rigidbody (o sus hijos), le damos el salto hacia afuera
            if (item.TryGetComponent(out Rigidbody rb))
            {
                Vector3 direccion = (origen.forward + Vector3.up * 1.2f).normalized;
                rb.AddForce(direccion * fuerzaImpulso, ForceMode.Impulse);
            }

            // Si es un prefab de RecursoDrop, nos aseguramos de que su tipo sea Gasolina/Bidón si tiene el método
            var recursoDrop = item.GetComponent<RecursoDrop>();
            if (recursoDrop != null)
            {
                recursoDrop.Configurar(TipoRecurso.Bidon, 1);
            }

            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log("<color=green>[ARCADE] ¡Premio arrojado exitosamente estilo tragamonedas!</color>");
    }

    // Métodos para cargar y salir de la escena
    public void IniciarMinijuego(GameObject jugador)
    {
        if (estaJugando) return;
        StartCoroutine(RutinaCargar(jugador));
    }

    private IEnumerator RutinaCargar(GameObject jugador)
    {
        estaJugando = true;
        AlternarControlJugador(jugador, false);

        AsyncOperation op = SceneManager.LoadSceneAsync(nombreEscenaMinijuego, LoadSceneMode.Additive);
        while (op != null && !op.isDone) yield return null;

        Scene escena = SceneManager.GetSceneByName(nombreEscenaMinijuego);
        if (escena.isLoaded) SceneManager.SetActiveScene(escena);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void SalirMinijuego(GameObject jugador)
    {
        if (!estaJugando) return;
        StartCoroutine(RutinaSalir(jugador));
    }

    private IEnumerator RutinaSalir(GameObject jugador)
    {
        AsyncOperation op = SceneManager.UnloadSceneAsync(nombreEscenaMinijuego);
        while (op != null && !op.isDone) yield return null;

        AlternarControlJugador(jugador, true);
        estaJugando = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void AlternarControlJugador(GameObject jugador, bool activar)
    {
        if (jugador == null) return;
        MonoBehaviour[] scripts = jugador.GetComponentsInChildren<MonoBehaviour>();
        foreach (var s in scripts)
        {
            if (s == null) continue;
            string n = s.GetType().Name;
            if (n.Contains("Movement") || n.Contains("Controller") || n.Contains("Look"))
            {
                s.enabled = activar;
            }
        }
    }
}