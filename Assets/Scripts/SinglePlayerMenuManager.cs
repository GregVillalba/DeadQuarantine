using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SingleplayerMenuManager : MonoBehaviour
{
    [Header("Paneles Principales")]
    [SerializeField] private GameObject modalidadPanel;
    [SerializeField] private GameObject aparienciaPanel;
    [SerializeField] private GameObject cartelAvisoInhabilitado;

    [Header("Texto Encabezado de Resumen")]
    [SerializeField] private TMP_Text textoModalidadDificultad;

    [Header("1. Modos - Cuadros Secundarios")]
    [SerializeField] private GameObject cuadroSubmodoHistoria;
    [SerializeField] private GameObject cuadroSubmodoRonda5;
    [SerializeField] private GameObject cuadroSubmodoSupervivencia;

    [Header("1. Modos - Objetos 'Seleccionado'")]
    [SerializeField] private GameObject selectorModoHistoria;
    [SerializeField] private GameObject selectorModoRonda5;
    [SerializeField] private GameObject selectorModoSupervivencia;

    [Header("2. Dificultades - Selectores por Modo")]
    [SerializeField] private GameObject[] selectoresDificultadHistoria;
    [SerializeField] private GameObject[] selectoresDificultadRonda5;
    [SerializeField] private GameObject[] selectoresDificultadSupervivencia;

    [Header("3. Personajes")]
    [SerializeField] private GameObject panelPersonaje;
    [SerializeField] private GameObject jugador1_select;
    [SerializeField] private GameObject jugador2_select;

    [Header("4. Escenarios")]
    [SerializeField] private GameObject panelEscenario;
    [SerializeField] private GameObject selectorEscenario1;
    [SerializeField] private GameObject selectorEscenario2;
    [SerializeField] private GameObject selectorEscenario3;

    [Header("4. Escenarios - Vistas (Info e Imagen)")]
    [SerializeField] private GameObject vistaLaboratorio;
    [SerializeField] private GameObject vistaCiudad;
    [SerializeField] private GameObject vistaCabana;

    [Header("Navegación y Carga")]
    [SerializeField] private GameObject botonComenzarJuego;
    [SerializeField] private GameObject botonHome;
    [SerializeField] private GameObject cartelCargandoEscenario;
    [SerializeField] private string escenaJuegoSingleplayer = "MainSceneSinglePlayer";

    private enum ModoSeleccionado
    {
        Ninguno,
        Historia,
        Ronda5,
        Infinitas
    }

    private ModoSeleccionado modoActual = ModoSeleccionado.Ninguno;

    private string textoNombreModo = "";
    private string textoNombreDificultad = "";

    private bool jugador1Listo = false;

    private bool escenarioSeleccionado = false;

    private bool cargandoPartida = false;


    private void Start()
    {
        BotonHome();
    }


    // =========================================================
    // 1. MODOS Y BOTÓN ALEATORIO
    // =========================================================

    public void SeleccionarModoHistoria()
    {
        modoActual = ModoSeleccionado.Historia;
        textoNombreModo = "MODALIDAD MODO HISTORIA";

        ApagarSelectoresModos();

        if (selectorModoHistoria != null)
            selectorModoHistoria.SetActive(true);

        MostrarCuadroSubmodo(cuadroSubmodoHistoria);
        ApagarTodosLosSelectoresDificultad();
        OcultarAvisoInhabilitado();
    }


    public void SeleccionarModoRonda5()
    {
        modoActual = ModoSeleccionado.Ronda5;
        textoNombreModo = "MODALIDAD RONDA 5";

        ApagarSelectoresModos();

        if (selectorModoRonda5 != null)
            selectorModoRonda5.SetActive(true);

        MostrarCuadroSubmodo(cuadroSubmodoRonda5);
        ApagarTodosLosSelectoresDificultad();
        OcultarAvisoInhabilitado();
    }


    public void SeleccionarModoSupervivencia()
    {
        modoActual = ModoSeleccionado.Infinitas;
        textoNombreModo = "MODALIDAD RONDAS INFINITAS";

        ApagarSelectoresModos();

        if (selectorModoSupervivencia != null)
            selectorModoSupervivencia.SetActive(true);

        MostrarCuadroSubmodo(cuadroSubmodoSupervivencia);
        ApagarTodosLosSelectoresDificultad();
        OcultarAvisoInhabilitado();
    }


    public void SeleccionarModalidadAleatoria()
    {
        MostrarAvisoInhabilitado();
    }


    // =========================================================
    // 2. DIFICULTADES
    // =========================================================

    public void SeleccionarDificultadNormal(GameObject marcoSeleccionado)
    {
        MarcarDificultadExclusiva(marcoSeleccionado);

        if (modoActual == ModoSeleccionado.Ronda5)
        {
            textoNombreDificultad = "DIFICULTAD NORMAL";
            OcultarAvisoInhabilitado();

            if (textoModalidadDificultad != null)
            {
                textoModalidadDificultad.text =
                    $"{textoNombreModo} - {textoNombreDificultad}";
            }

            if (modalidadPanel != null)
                modalidadPanel.SetActive(false);

            if (aparienciaPanel != null)
                aparienciaPanel.SetActive(true);

            ResetearApariencia();
        }
        else
        {
            MostrarAvisoInhabilitado();
        }
    }


    public void SeleccionarDificultadDificil(GameObject marcoSeleccionado)
    {
        MarcarDificultadExclusiva(marcoSeleccionado);
        MostrarAvisoInhabilitado();
    }


    public void SeleccionarDificultadPesadilla(GameObject marcoSeleccionado)
    {
        MarcarDificultadExclusiva(marcoSeleccionado);
        MostrarAvisoInhabilitado();
    }


    private void MarcarDificultadExclusiva(GameObject marcoSeleccionado)
    {
        GameObject[] grupoActual = null;

        if (modoActual == ModoSeleccionado.Historia)
            grupoActual = selectoresDificultadHistoria;

        else if (modoActual == ModoSeleccionado.Ronda5)
            grupoActual = selectoresDificultadRonda5;

        else if (modoActual == ModoSeleccionado.Infinitas)
            grupoActual = selectoresDificultadSupervivencia;


        if (grupoActual != null)
        {
            foreach (var s in grupoActual)
            {
                if (s != null)
                {
                    s.SetActive(s == marcoSeleccionado);
                }
            }
        }
    }


    // =========================================================
    // 3. APARIENCIA - PERSONAJES
    // =========================================================

    public void SeleccionarJugador1()
    {
        jugador1Listo = true;

        if (jugador1_select != null)
            jugador1_select.SetActive(true);

        if (jugador2_select != null)
            jugador2_select.SetActive(false);

        OcultarAvisoInhabilitado();

        if (panelEscenario != null)
            panelEscenario.SetActive(true);

        ValidarComenzar();
    }


    public void SeleccionarJugador2()
    {
        jugador1Listo = false;

        if (jugador2_select != null)
            jugador2_select.SetActive(true);

        if (jugador1_select != null)
            jugador1_select.SetActive(false);

        if (panelEscenario != null)
            panelEscenario.SetActive(false);

        escenarioSeleccionado = false;

        OcultarVistasEscenarios();

        MostrarAvisoInhabilitado();
        ValidarComenzar();
    }


    // =========================================================
    // 4. ESCENARIOS
    // =========================================================

    public void SeleccionarEscenario1()
    {
        escenarioSeleccionado = true;

        OcultarVistasEscenarios();
        ApagarSelectoresEscenarios();

        if (selectorEscenario1 != null)
            selectorEscenario1.SetActive(true);

        if (vistaLaboratorio != null)
            vistaLaboratorio.SetActive(true);

        OcultarAvisoInhabilitado();
        ValidarComenzar();
    }


    public void SeleccionarEscenario2()
    {
        escenarioSeleccionado = true;

        OcultarVistasEscenarios();
        ApagarSelectoresEscenarios();

        if (selectorEscenario2 != null)
            selectorEscenario2.SetActive(true);

        if (vistaCiudad != null)
            vistaCiudad.SetActive(true);

        OcultarAvisoInhabilitado();
        ValidarComenzar();
    }


    public void SeleccionarEscenario3()
    {
        escenarioSeleccionado = true;

        OcultarVistasEscenarios();
        ApagarSelectoresEscenarios();

        if (selectorEscenario3 != null)
            selectorEscenario3.SetActive(true);

        if (vistaCabana != null)
            vistaCabana.SetActive(true);

        OcultarAvisoInhabilitado();
        ValidarComenzar();
    }


    // =========================================================
    // 5. NAVEGACIÓN
    // =========================================================

    public void BotonAtrasDesdeApariencia()
    {
        ResetearApariencia();

        if (aparienciaPanel != null)
            aparienciaPanel.SetActive(false);

        if (modalidadPanel != null)
            modalidadPanel.SetActive(true);

        OcultarAvisoInhabilitado();
    }


    public void BotonHome()
    {
        cargandoPartida = false;

        if (cartelCargandoEscenario != null)
            cartelCargandoEscenario.SetActive(false);

        ResetearApariencia();
        ResetearModalidad();

        if (aparienciaPanel != null)
            aparienciaPanel.SetActive(false);

        if (modalidadPanel != null)
            modalidadPanel.SetActive(true);

        OcultarAvisoInhabilitado();
    }


    // =========================================================
    // 6. COMENZAR PARTIDA
    // =========================================================
public void OnClick_ComenzarAJugar()
{
    if (jugador1Listo && escenarioSeleccionado && !cargandoPartida)
    {
        Debug.Log("========================================");
        Debug.Log("INICIANDO PARTIDA");
        Debug.Log("Escenario seleccionado en el menú: " + ObtenerEscenarioSeleccionado());
        Debug.Log("========================================");

        GuardarConfiguracionPartida();

        if (ConfiguracionPartidaSeleccionada.Instancia != null)
        {
            Debug.Log(
                "Configuración guardada correctamente. Escenario: " +
                ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado
            );
        }
        else
        {
            Debug.LogError(
                "NO EXISTE ConfiguracionPartidaSeleccionada."
            );
        }

        cargandoPartida = true;

        StartCoroutine(CargarEscenarioRutina());
    }
}


    private IEnumerator CargarEscenarioRutina()
    {
        if (cartelCargandoEscenario != null)
            cartelCargandoEscenario.SetActive(true);

        if (botonComenzarJuego != null)
            botonComenzarJuego.SetActive(false);

        AsyncOperation operacion =
            SceneManager.LoadSceneAsync(escenaJuegoSingleplayer);

        while (!operacion.isDone)
        {
            yield return null;
        }
    }


    // =========================================================
    // 7. AUXILIARES
    // =========================================================

    private void MostrarCuadroSubmodo(GameObject cuadroActivo)
    {
        if (cuadroSubmodoHistoria != null)
            cuadroSubmodoHistoria.SetActive(
                cuadroSubmodoHistoria == cuadroActivo
            );

        if (cuadroSubmodoRonda5 != null)
            cuadroSubmodoRonda5.SetActive(
                cuadroSubmodoRonda5 == cuadroActivo
            );

        if (cuadroSubmodoSupervivencia != null)
            cuadroSubmodoSupervivencia.SetActive(
                cuadroSubmodoSupervivencia == cuadroActivo
            );
    }


    private void ResetearModalidad()
    {
        modoActual = ModoSeleccionado.Ninguno;

        textoNombreModo = "";
        textoNombreDificultad = "";

        if (textoModalidadDificultad != null)
            textoModalidadDificultad.text = "";

        MostrarCuadroSubmodo(null);
        ApagarSelectoresModos();
        ApagarTodosLosSelectoresDificultad();
    }


    private void ResetearApariencia()
    {
        jugador1Listo = false;
        escenarioSeleccionado = false;

        if (panelPersonaje != null)
            panelPersonaje.SetActive(true);

        if (panelEscenario != null)
            panelEscenario.SetActive(false);

        if (botonComenzarJuego != null)
            botonComenzarJuego.SetActive(false);

        SetBotonHomeHabilitado(true);

        if (cartelCargandoEscenario != null)
            cartelCargandoEscenario.SetActive(false);

        if (jugador1_select != null)
            jugador1_select.SetActive(false);

        if (jugador2_select != null)
            jugador2_select.SetActive(false);

        ApagarSelectoresEscenarios();
        OcultarVistasEscenarios();
    }


    private void ApagarSelectoresModos()
    {
        if (selectorModoHistoria != null)
            selectorModoHistoria.SetActive(false);

        if (selectorModoRonda5 != null)
            selectorModoRonda5.SetActive(false);

        if (selectorModoSupervivencia != null)
            selectorModoSupervivencia.SetActive(false);
    }


    private void ApagarTodosLosSelectoresDificultad()
    {
        ApagarArray(selectoresDificultadHistoria);
        ApagarArray(selectoresDificultadRonda5);
        ApagarArray(selectoresDificultadSupervivencia);
    }


    private void ApagarArray(GameObject[] arr)
    {
        if (arr == null)
            return;

        foreach (var go in arr)
        {
            if (go != null)
                go.SetActive(false);
        }
    }


    private void ApagarSelectoresEscenarios()
    {
        if (selectorEscenario1 != null)
            selectorEscenario1.SetActive(false);

        if (selectorEscenario2 != null)
            selectorEscenario2.SetActive(false);

        if (selectorEscenario3 != null)
            selectorEscenario3.SetActive(false);
    }


    private void OcultarVistasEscenarios()
    {
        if (vistaLaboratorio != null)
            vistaLaboratorio.SetActive(false);

        if (vistaCiudad != null)
            vistaCiudad.SetActive(false);

        if (vistaCabana != null)
            vistaCabana.SetActive(false);
    }


    private void ValidarComenzar()
    {
        bool todoSeleccionado =
            jugador1Listo &&
            escenarioSeleccionado;

        if (botonComenzarJuego != null && !cargandoPartida)
        {
            botonComenzarJuego.SetActive(todoSeleccionado);
        }

        SetBotonHomeHabilitado(!todoSeleccionado);
    }


    private void SetBotonHomeHabilitado(bool habilitado)
    {
        if (botonHome != null)
        {
            Button btn = botonHome.GetComponent<Button>();

            if (btn != null)
            {
                btn.interactable = habilitado;
            }
            else
            {
                botonHome.SetActive(habilitado);
            }
        }
    }


    private void MostrarAvisoInhabilitado()
    {
        if (cartelAvisoInhabilitado != null)
            cartelAvisoInhabilitado.SetActive(true);
    }


    private void OcultarAvisoInhabilitado()
    {
        if (cartelAvisoInhabilitado != null)
            cartelAvisoInhabilitado.SetActive(false);
    }

    private void GuardarConfiguracionPartida()
{
    if (ConfiguracionPartidaSeleccionada.Instancia == null)
    {
        Debug.LogError(
            "SingleplayerMenuManager: No existe una ConfiguracionPartidaSeleccionada en la escena del menú."
        );

        return;
    }


    // =========================================================
    // ESCENARIO
    // =========================================================

    if (selectorEscenario1 != null && selectorEscenario1.activeSelf)
    {
        ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado =
            ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;
    }
    else if (selectorEscenario2 != null && selectorEscenario2.activeSelf)
    {
        ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado =
            ConfiguracionPartidaSeleccionada.Escenario.Ciudad;
    }
    else if (selectorEscenario3 != null && selectorEscenario3.activeSelf)
    {
        ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado =
            ConfiguracionPartidaSeleccionada.Escenario.Cabana;
    }
    else
    {
        Debug.LogError(
            "SingleplayerMenuManager: No se pudo determinar el escenario seleccionado."
        );

        return;
    }


    // =========================================================
    // MODO
    // =========================================================

    if (modoActual == ModoSeleccionado.Historia)
    {
        ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado =
            ConfiguracionPartidaSeleccionada.ModoJuego.Historia;
    }
    else if (modoActual == ModoSeleccionado.Ronda5)
    {
        ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado =
            ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5;
    }
    else if (modoActual == ModoSeleccionado.Infinitas)
    {
        ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado =
            ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;
    }


    // =========================================================
    // DIFICULTAD
    // =========================================================

    if (textoNombreDificultad == "DIFICULTAD NORMAL")
    {
        ConfiguracionPartidaSeleccionada.Instancia.dificultadSeleccionada =
            ConfiguracionPartidaSeleccionada.Dificultad.Normal;
    }


    // =========================================================
    // PERSONAJE
    // =========================================================

    if (jugador1Listo)
    {
        ConfiguracionPartidaSeleccionada.Instancia.personajeSeleccionado =
            ConfiguracionPartidaSeleccionada.Personaje.Jugador1;
    }


    // =========================================================
    // DEBUG
    // =========================================================

    Debug.Log(
        "Configuración de partida guardada: " +
        "Escenario = " +
        ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado +
        " | Modo = " +
        ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado +
        " | Dificultad = " +
        ConfiguracionPartidaSeleccionada.Instancia.dificultadSeleccionada +
        " | Personaje = " +
        ConfiguracionPartidaSeleccionada.Instancia.personajeSeleccionado
    );
}

private string ObtenerEscenarioSeleccionado()
{
    if (selectorEscenario1 != null && selectorEscenario1.activeSelf)
    {
        return "LABORATORIO";
    }

    if (selectorEscenario2 != null && selectorEscenario2.activeSelf)
    {
        return "CIUDAD";
    }

    if (selectorEscenario3 != null && selectorEscenario3.activeSelf)
    {
        return "MANSION DEL BOSQUE";
    }

    return "NINGUNO";
}
}