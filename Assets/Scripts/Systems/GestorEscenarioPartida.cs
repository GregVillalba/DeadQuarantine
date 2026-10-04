using UnityEngine;

public class GestorEscenariosPartida : MonoBehaviour
{
    [Header("Escenarios disponibles")]
    [SerializeField] private ConfiguracionEscenarioJugable escenarioLaboratorio;
    [SerializeField] private ConfiguracionEscenarioJugable escenarioCiudad;
    [SerializeField] private ConfiguracionEscenarioJugable escenarioCabana;

    private ConfiguracionEscenarioJugable escenarioActivo;


    private void Awake()
    {
        ConfigurarEscenarioActivo();
    }


    private void ConfigurarEscenarioActivo()
    {
        if (ConfiguracionPartidaSeleccionada.Instancia == null)
        {
            Debug.LogError(
                "GestorEscenariosPartida: No existe una ConfiguracionPartidaSeleccionada."
            );

            return;
        }

        ConfiguracionPartidaSeleccionada.Escenario escenarioSeleccionado =
            ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado;


        // Primero desactivamos todos los escenarios.
        DesactivarTodosLosEscenarios();


        // Seleccionamos el escenario correspondiente.
        switch (escenarioSeleccionado)
        {
            case ConfiguracionPartidaSeleccionada.Escenario.Laboratorio:

                escenarioActivo = escenarioLaboratorio;
                break;


            case ConfiguracionPartidaSeleccionada.Escenario.Ciudad:

                escenarioActivo = escenarioCiudad;
                break;


            case ConfiguracionPartidaSeleccionada.Escenario.Cabana:

                escenarioActivo = escenarioCabana;
                break;


            default:

                Debug.LogError(
                    "GestorEscenariosPartida: No se seleccionó un escenario válido."
                );

                return;
        }


        // Activamos únicamente el escenario seleccionado.
        if (escenarioActivo != null)
        {
            escenarioActivo.gameObject.SetActive(true);

            Debug.Log(
                "Escenario activo: " +
                escenarioActivo.TipoEscenario
            );
        }
    }


    private void DesactivarTodosLosEscenarios()
    {
        if (escenarioLaboratorio != null)
            escenarioLaboratorio.gameObject.SetActive(false);

        if (escenarioCiudad != null)
            escenarioCiudad.gameObject.SetActive(false);

        if (escenarioCabana != null)
            escenarioCabana.gameObject.SetActive(false);
    }


    public ConfiguracionEscenarioJugable ObtenerEscenarioActivo()
    {
        return escenarioActivo;
    }


    public Transform ObtenerSpawnJugador()
    {
        if (escenarioActivo == null)
        {
            Debug.LogError(
                "GestorEscenariosPartida: No hay un escenario activo."
            );

            return null;
        }

        return escenarioActivo.SpawnJugador;
    }


    public Transform[] ObtenerSpawnsZombies()
    {
        if (escenarioActivo == null)
        {
            Debug.LogError(
                "GestorEscenariosPartida: No hay un escenario activo."
            );

            return null;
        }

        return escenarioActivo.SpawnsZombies;
    }
}