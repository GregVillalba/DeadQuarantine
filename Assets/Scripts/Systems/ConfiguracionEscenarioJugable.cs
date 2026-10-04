using UnityEngine;

public class ConfiguracionEscenarioJugable : MonoBehaviour
{
    [Header("Identificación del escenario")]
    [SerializeField] private ConfiguracionPartidaSeleccionada.Escenario tipoEscenario;

    [Header("Spawn del jugador")]
    [SerializeField] private Transform spawnJugador;

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
}