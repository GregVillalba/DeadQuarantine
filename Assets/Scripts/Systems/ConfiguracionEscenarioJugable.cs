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