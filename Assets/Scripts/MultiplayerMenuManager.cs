using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MultiplayerMenuManager : MonoBehaviour
{
    [Header("Paneles Principales")]
    [SerializeField] private GameObject modalidadPanel;
    [SerializeField] private GameObject aparienciaPanel;
    [SerializeField] private GameObject cartelAvisoInhabilitado;

    [Header("Pantalla Redirigiendo")]
    [SerializeField] private GameObject panelRedirigiendo;

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

    [Header("4. Escenarios - Selectores Estáticos")]
    [SerializeField] private GameObject panelEscenario;
    [SerializeField] private GameObject selectorEscenario1;
    [SerializeField] private GameObject selectorEscenario2;
    [SerializeField] private GameObject selectorEscenario3;

    [Header("4. Escenarios - Vistas")]
    [SerializeField] private GameObject vistaLaboratorio;
    [SerializeField] private GameObject vistaCiudad;
    [SerializeField] private GameObject vistaCabana;

    [Header("Navegación")]
    [SerializeField] private GameObject botonComenzarJuego;
    [SerializeField] private GameObject botonHome;

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

    // Dificultad elegida (se guarda al comenzar la partida).
    private ConfiguracionPartidaSeleccionada.Dificultad dificultadActual =
        ConfiguracionPartidaSeleccionada.Dificultad.Ninguna;

    // Modos que ya se pueden jugar (Historia todavia no).
    private bool ModoHabilitado()
    {
        return modoActual == ModoSeleccionado.Ronda5 ||
               modoActual == ModoSeleccionado.Infinitas;
    }

    private bool jugador1Listo = false;

    // Laboratorio y Cabaña están disponibles.
    // Ciudad continúa bloqueada.
    private bool escenarioLista = false;

    private void Start()
    {
        BotonHome();
    }

    // =========================================================
    // MODOS DE JUEGO
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
    // DIFICULTADES
    // =========================================================

   /* public void SeleccionarDificultadNormal(GameObject marcoSeleccionado)
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
    }*/

        private void ElegirDificultad(
        GameObject marcoSeleccionado,
        string nombreDificultad,
        ConfiguracionPartidaSeleccionada.Dificultad dificultad
    )
    {
        MarcarDificultadExclusiva(marcoSeleccionado);

        if (!ModoHabilitado())
        {
            MostrarAvisoInhabilitado();
            return;
        }

        dificultadActual = dificultad;
        textoNombreDificultad = nombreDificultad;
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

    public void SeleccionarDificultadNormal(GameObject marcoSeleccionado)
    {
        ElegirDificultad(
            marcoSeleccionado,
            "DIFICULTAD NORMAL",
            ConfiguracionPartidaSeleccionada.Dificultad.Normal
        );
    }

    public void SeleccionarDificultadDificil(GameObject marcoSeleccionado)
    {
        ElegirDificultad(
            marcoSeleccionado,
            "DIFICULTAD DIFICIL",
            ConfiguracionPartidaSeleccionada.Dificultad.Dificil
        );
    }

    public void SeleccionarDificultadPesadilla(GameObject marcoSeleccionado)
    {
        ElegirDificultad(
            marcoSeleccionado,
            "DIFICULTAD PESADILLA",
            ConfiguracionPartidaSeleccionada.Dificultad.Pesadilla
        );
    }

    private void MarcarDificultadExclusiva(GameObject marcoSeleccionado)
    {
        GameObject[] grupoActual = null;

        if (modoActual == ModoSeleccionado.Historia)
        {
            grupoActual = selectoresDificultadHistoria;
        }
        else if (modoActual == ModoSeleccionado.Ronda5)
        {
            grupoActual = selectoresDificultadRonda5;
        }
        else if (modoActual == ModoSeleccionado.Infinitas)
        {
            grupoActual = selectoresDificultadSupervivencia;
        }

        if (grupoActual != null)
        {
            foreach (var s in grupoActual)
            {
                if (s != null)
                    s.SetActive(s == marcoSeleccionado);
            }
        }
    }

    // =========================================================
    // PERSONAJES
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

        escenarioLista = false;

        OcultarVistasEscenarios();

        MostrarAvisoInhabilitado();
        ValidarComenzar();
    }

    // =========================================================
    // ESCENARIOS
    // =========================================================

    // LABORATORIO
    public void SeleccionarEscenario1()
    {
        escenarioLista = true;

        OcultarVistasEscenarios();
        ApagarSelectoresEscenarios();

        if (selectorEscenario1 != null)
            selectorEscenario1.SetActive(true);

        if (vistaLaboratorio != null)
            vistaLaboratorio.SetActive(true);

        OcultarAvisoInhabilitado();
        ValidarComenzar();
    }

    // CIUDAD - TODAVÍA NO DISPONIBLE
    public void SeleccionarEscenario2()
    {
        escenarioLista = false;

        OcultarVistasEscenarios();
        ApagarSelectoresEscenarios();

        if (selectorEscenario2 != null)
            selectorEscenario2.SetActive(true);

        if (vistaCiudad != null)
            vistaCiudad.SetActive(true);

        MostrarAvisoInhabilitado();
        ValidarComenzar();
    }

    // CABAÑA
    public void SeleccionarEscenario3()
    {
        escenarioLista = true;

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
    // COMENZAR PARTIDA
    // =========================================================

    public void OnClick_ComenzarAJugar()
    {
        if (jugador1Listo && escenarioLista)
        {
            Debug.Log("========================================");
            Debug.Log("INICIANDO PARTIDA MULTIPLAYER");
            Debug.Log("========================================");

            GuardarConfiguracionPartida();

            if (ConfiguracionPartidaSeleccionada.Instancia != null)
            {
                Debug.Log(
                    "Configuración MULTIPLAYER guardada correctamente: " +
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
            else
            {
                Debug.LogError(
                    "MultiplayerMenuManager: NO EXISTE ConfiguracionPartidaSeleccionada."
                );

                return;
            }

            if (panelRedirigiendo != null)
            {
                panelRedirigiendo.SetActive(true);
                panelRedirigiendo.transform.SetAsLastSibling();

                foreach (Transform t in panelRedirigiendo.transform)
                {
                    t.gameObject.SetActive(true);
                }
            }

            if (botonComenzarJuego != null)
                botonComenzarJuego.SetActive(false);
        }
    }

    // =========================================================
    // GUARDAR CONFIGURACIÓN
    // =========================================================

    private void GuardarConfiguracionPartida()
    {
        if (ConfiguracionPartidaSeleccionada.Instancia == null)
        {
            Debug.LogError(
                "MultiplayerMenuManager: No existe una ConfiguracionPartidaSeleccionada."
            );

            return;
        }

        // -----------------------------------------------------
        // ESCENARIO
        // -----------------------------------------------------

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
                "MultiplayerMenuManager: No se pudo determinar el escenario seleccionado."
            );

            return;
        }

        // -----------------------------------------------------
        // MODO
        // -----------------------------------------------------

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
        else
        {
            Debug.LogError(
                "MultiplayerMenuManager: No se pudo determinar el modo de juego."
            );

            return;
        }

        // -----------------------------------------------------
        // DIFICULTAD
        // -----------------------------------------------------

       /* if (textoNombreDificultad == "DIFICULTAD NORMAL")
        {
            ConfiguracionPartidaSeleccionada.Instancia.dificultadSeleccionada =
                ConfiguracionPartidaSeleccionada.Dificultad.Normal;
        }
        else
        {
            Debug.LogError(
                "MultiplayerMenuManager: No se pudo determinar la dificultad."
            );

            return;
        }*/

        if (dificultadActual != ConfiguracionPartidaSeleccionada.Dificultad.Ninguna)
        {
            ConfiguracionPartidaSeleccionada.Instancia.dificultadSeleccionada =
                dificultadActual;
        }
        else
        {
            Debug.LogError(
                "MultiplayerMenuManager: No se pudo determinar la dificultad."
            );

            return;
        }

        // -----------------------------------------------------
        // PERSONAJE
        // -----------------------------------------------------

        if (jugador1Listo)
        {
            ConfiguracionPartidaSeleccionada.Instancia.personajeSeleccionado =
                ConfiguracionPartidaSeleccionada.Personaje.Jugador1;
        }
        else
        {
            Debug.LogError(
                "MultiplayerMenuManager: No se pudo determinar el personaje."
            );

            return;
        }

        // -----------------------------------------------------
        // LOG FINAL
        // -----------------------------------------------------

        Debug.Log(
            "Configuración de partida MULTIPLAYER guardada: " +
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

    // =========================================================
    // NAVEGACIÓN
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
        ResetearApariencia();
        ResetearModalidad();

        if (aparienciaPanel != null)
            aparienciaPanel.SetActive(false);

        if (modalidadPanel != null)
            modalidadPanel.SetActive(true);

        OcultarAvisoInhabilitado();
    }

    // =========================================================
    // UTILIDADES
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

        dificultadActual = ConfiguracionPartidaSeleccionada.Dificultad.Ninguna;

        if (textoModalidadDificultad != null)
            textoModalidadDificultad.text = "";

        MostrarCuadroSubmodo(null);
        ApagarSelectoresModos();
        ApagarTodosLosSelectoresDificultad();
    }

    private void ResetearApariencia()
    {
        jugador1Listo = false;
        escenarioLista = false;

        if (panelPersonaje != null)
            panelPersonaje.SetActive(true);

        if (panelEscenario != null)
            panelEscenario.SetActive(false);

        if (botonComenzarJuego != null)
            botonComenzarJuego.SetActive(false);

        SetBotonHomeHabilitado(true);

        if (panelRedirigiendo != null)
            panelRedirigiendo.SetActive(false);

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
            escenarioLista;

        if (botonComenzarJuego != null)
            botonComenzarJuego.SetActive(todoSeleccionado);

        SetBotonHomeHabilitado(!todoSeleccionado);
    }

    private void SetBotonHomeHabilitado(bool habilitado)
    {
        if (botonHome != null)
        {
            Button btn = botonHome.GetComponent<Button>();

            if (btn != null)
                btn.interactable = habilitado;
            else
                botonHome.SetActive(habilitado);
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
}