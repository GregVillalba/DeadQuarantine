using UnityEngine;

public class ConfiguracionEscenarioJugable : MonoBehaviour
{
    [Header("Identificación del escenario")]
    [SerializeField] private ConfiguracionPartidaSeleccionada.Escenario tipoEscenario;

    [Header("Spawn del jugador")]
    [Tooltip("Punto del jugador 1 (host).")]
    [SerializeField] private Transform spawnJugador;

    [Tooltip("Puntos de los demas jugadores, en orden: el elemento 0 es para el jugador 2, el 1 para el jugador 3, etc.")]
    [SerializeField] private Transform[] spawnsJugadoresAdicionales;

    [Header("Spawns de zombies")]
    [SerializeField] private Transform[] spawnsZombies;

    [Header("Recursos del escenario")]
    [Tooltip("Las 3 distribuciones de recursos del escenario. Al empezar la partida se usa una al azar.")]
    [SerializeField] private OpcionRecursosEscenario[] opcionesRecursos;

    [Header("Postprocesado multiplayer")]
    [SerializeField] private UnityEngine.Rendering.VolumeProfile perfilPostProcesoLaboratorio;


    private void Start()
    {
        if (gameObject.scene.name != "MP_Laboratorio" ||
            perfilPostProcesoLaboratorio == null)
            return;

        GameObject volumenObjeto = new GameObject("Post_Process_Laboratorio");
        volumenObjeto.transform.SetParent(transform, false);
        volumenObjeto.transform.localPosition = new Vector3(-30f, 0f, -8.7f);

        UnityEngine.Rendering.Volume volumen =
            volumenObjeto.AddComponent<UnityEngine.Rendering.Volume>();
        volumen.isGlobal = false;
        volumen.priority = 0f;
        volumen.blendDistance = 2f;
        volumen.weight = 1f;
        volumen.sharedProfile = perfilPostProcesoLaboratorio;

        BoxCollider zona = volumenObjeto.AddComponent<BoxCollider>();
        zona.isTrigger = true;
        zona.size = new Vector3(100f, 50f, 100f);
    }
    public ConfiguracionPartidaSeleccionada.Escenario TipoEscenario
    {
        get { return tipoEscenario; }
    }

    public Transform SpawnJugador
    {
        get { return spawnJugador; }
    }

    public Transform[] SpawnsZombies
    {
        get { return spawnsZombies; }
    }

    public OpcionRecursosEscenario[] OpcionesRecursos
    {
        get { return opcionesRecursos; }
    }

    // Devuelve el punto de aparicion segun el indice del jugador
    // (0 = host / jugador 1, 1 = jugador 2, ...).
    public Transform ObtenerSpawnJugador(int indice)
    {
        if (indice <= 0)
            return spawnJugador;

        int posicion = indice - 1;

        if (spawnsJugadoresAdicionales != null &&
            posicion < spawnsJugadoresAdicionales.Length &&
            spawnsJugadoresAdicionales[posicion] != null)
        {
            return spawnsJugadoresAdicionales[posicion];
        }

        Debug.LogWarning(
            "ConfiguracionEscenarioJugable (" + tipoEscenario + "): " +
            "no hay un punto para el jugador de indice " + indice +
            ". Se usa el punto del jugador 1."
        );

        return spawnJugador;
    }
}