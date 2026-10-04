using UnityEngine;

public class ConfiguracionPartidaSeleccionada : MonoBehaviour
{
    public static ConfiguracionPartidaSeleccionada Instancia { get; private set; }

    public enum Escenario
    {
        Ninguno,
        Laboratorio,
        Ciudad,
        Cabana
    }

    public enum ModoJuego
    {
        Ninguno,
        Historia,
        Ronda5,
        Supervivencia
    }

    public enum Dificultad
    {
        Ninguna,
        Normal,
        Dificil,
        Pesadilla
    }

    public enum Personaje
    {
        Ninguno,
        Jugador1,
        Jugador2
    }

    [Header("Configuración seleccionada")]

    public Escenario escenarioSeleccionado = Escenario.Ninguno;

    public ModoJuego modoSeleccionado = ModoJuego.Ninguno;

    public Dificultad dificultadSeleccionada = Dificultad.Ninguna;

    public Personaje personajeSeleccionado = Personaje.Ninguno;


    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;

        DontDestroyOnLoad(gameObject);
    }
}