using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class ArcadeDinoMachine : MonoBehaviour
{
    public static ArcadeDinoMachine InstanciaActiva { get; private set; }

    [Header("Configuración")]
    [SerializeField] private string nombreEscenaMinijuego = "DEMO";

    [Header("Drop de Nafta (Gasolina)")]
    [SerializeField] private RecursoDrop prefabRecurso;
    [SerializeField] private TipoRecurso tipoGasolina;
    [SerializeField] private Transform puntoSpawnDrop;
    [SerializeField] private float alturaSobrePiso = 0.35f;
    [SerializeField] private int cantidad = 1;

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

    /// <summary>
    /// Dropea un bidón cada vez que se superan 100 puntos en el minijuego.
    /// </summary>
    public void DropearNafta()
    {
        if (prefabRecurso == null)
        {
            Debug.LogError("[ARCADE] Falta asignar 'Prefab Recurso' en el inspector de ArcadeDinoMachine.", this);
            return;
        }

        Transform origen = puntoSpawnDrop != null ? puntoSpawnDrop : transform;
        Vector3 posFinal = origen.position + (Vector3.up * alturaSobrePiso);

        posFinal += new Vector3(Random.Range(-0.15f, 0.15f), 0f, Random.Range(-0.15f, 0.15f));

        NetworkManager red = NetworkManager.Singleton;
        bool enRed = red != null && red.IsListening;

        if (enRed)
        {
            if (red.IsServer)
            {
                RecursoDrop drop = Instantiate(prefabRecurso, posFinal, Quaternion.identity);
                drop.Configurar(tipoGasolina, cantidad);

                NetworkObject netObj = drop.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                }

                Debug.Log($"<color=green>[ARCADE] Nafta spawneada en RED en {posFinal}.</color>");
            }
        }
        else
        {
            RecursoDrop drop = Instantiate(prefabRecurso, posFinal, Quaternion.identity);
            drop.Configurar(tipoGasolina, cantidad);

            Debug.Log($"<color=green>[ARCADE] Nafta spawneada LOCAL en {posFinal}.</color>");
        }
    }
}