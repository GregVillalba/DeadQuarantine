using UnityEngine;
using UnityEngine.UI;
using TMPro; // Si usas el Text clásico de UnityEngine.UI, cambia TMP_Text por Text

public class MultiplayerMenuManager : MonoBehaviour
{
    [Header("Paneles Principales")]
    [SerializeField] private GameObject modalidadPanel;
    [SerializeField] private GameObject aparienciaPanel;
    [SerializeField] private GameObject cartelAvisoInhabilitado;

    [Header("Pantalla Redirigiendo")]
    [SerializeField] private GameObject panelRedirigiendo; // Cartel/Panel "CARGANDO_ESCENARIO (1)"

    [Header("Texto Encabezado de Resumen")]
    [SerializeField] private TMP_Text textoModalidadDificultad; // "Titulo_elecciones"

    [Header("1. Modos - Cuadros Secundarios")]
    [SerializeField] private GameObject cuadroSubmodoHistoria;      // Objeto hijo modoHistoria
    [SerializeField] private GameObject cuadroSubmodoRonda5;         // Objeto hijo modoRonda5
    [SerializeField] private GameObject cuadroSubmodoSupervivencia;  // Objeto hijo modoSupervivencia

    [Header("1. Modos - Objetos 'Seleccionado'")]
    [SerializeField] private GameObject selectorModoHistoria;
    [SerializeField] private GameObject selectorModoRonda5;
    [SerializeField] private GameObject selectorModoSupervivencia;

    [Header("2. Dificultades - Selectores por Modo")]
    [SerializeField] private GameObject[] selectoresDificultadHistoria;      // Normal, Dificil, Pesadilla
    [SerializeField] private GameObject[] selectoresDificultadRonda5;        // Normal, Dificil, Pesadilla
    [SerializeField] private GameObject[] selectoresDificultadSupervivencia; // Normal, Dificil, Pesadilla

    [Header("3. Personajes")]
    [SerializeField] private GameObject panelPersonaje;
    [SerializeField] private GameObject jugador1_select;
    [SerializeField] private GameObject jugador2_select;

    [Header("4. Escenarios - Selectores Estéticos")]
    [SerializeField] private GameObject panelEscenario;
    [SerializeField] private GameObject selectorEscenario1; // Hijo 'Seleccionado' de Laboratorio
    [SerializeField] private GameObject selectorEscenario2; // Hijo 'Seleccionado' de Ciudad
    [SerializeField] private GameObject selectorEscenario3; // Hijo 'Seleccionado' de Cabaña

    [Header("4. Escenarios - Vistas (Info e Imagen)")]
    [SerializeField] private GameObject vistaLaboratorio;
    [SerializeField] private GameObject vistaCiudad;
    [SerializeField] private GameObject vistaCabana;

    [Header("Navegación")]
    [SerializeField] private GameObject botonComenzarJuego; // Botón Siguiente / Comenzar
    [SerializeField] private GameObject botonHome;           // Botón Home a deshabilitar

    private enum ModoSeleccionado { Ninguno, Historia, Ronda5, Infinitas }
    private ModoSeleccionado modoActual = ModoSeleccionado.Ninguno;

    private string textoNombreModo = "";
    private string textoNombreDificultad = "";
    private bool jugador1Listo = false;
    private bool cabanaLista = false;

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
        if (selectorModoHistoria != null) selectorModoHistoria.SetActive(true);

        MostrarCuadroSubmodo(cuadroSubmodoHistoria);
        ApagarTodosLosSelectoresDificultad();
        OcultarAvisoInhabilitado();
    }

    public void SeleccionarModoRonda5()
    {
        modoActual = ModoSeleccionado.Ronda5;
        textoNombreModo = "MODALIDAD RONDA 5";

        ApagarSelectoresModos();
        if (selectorModoRonda5 != null) selectorModoRonda5.SetActive(true);

        MostrarCuadroSubmodo(cuadroSubmodoRonda5);
        ApagarTodosLosSelectoresDificultad();
        OcultarAvisoInhabilitado();
    }

    public void SeleccionarModoSupervivencia()
    {
        modoActual = ModoSeleccionado.Infinitas;
        textoNombreModo = "MODALIDAD RONDAS INFINITAS";

        ApagarSelectoresModos();
        if (selectorModoSupervivencia != null) selectorModoSupervivencia.SetActive(true);

        MostrarCuadroSubmodo(cuadroSubmodoSupervivencia);
        ApagarTodosLosSelectoresDificultad();
        OcultarAvisoInhabilitado();
    }

    public void SeleccionarModalidadAleatoria()
    {
        MostrarAvisoInhabilitado();
    }

    // =========================================================
    // 2. DIFICULTADES CON DESMARCADO MUTUO
    // =========================================================

    public void SeleccionarDificultadNormal(GameObject marcoSeleccionado)
    {
        MarcarDificultadExclusiva(marcoSeleccionado);

        if (modoActual == ModoSeleccionado.Ronda5)
        {
            textoNombreDificultad = "DIFICULTAD NORMAL";
            OcultarAvisoInhabilitado();

            if (textoModalidadDificultad != null)
                textoModalidadDificultad.text = $"{textoNombreModo} - {textoNombreDificultad}";

            // Pasa a la pantalla de Apariencia
            if (modalidadPanel != null) modalidadPanel.SetActive(false);
            if (aparienciaPanel != null) aparienciaPanel.SetActive(true);

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

        if (modoActual == ModoSeleccionado.Historia) grupoActual = selectoresDificultadHistoria;
        else if (modoActual == ModoSeleccionado.Ronda5) grupoActual = selectoresDificultadRonda5;
        else if (modoActual == ModoSeleccionado.Infinitas) grupoActual = selectoresDificultadSupervivencia;

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

        if (jugador1_select != null) jugador1_select.SetActive(true);
        if (jugador2_select != null) jugador2_select.SetActive(false);

        OcultarAvisoInhabilitado();

        if (panelEscenario != null) panelEscenario.SetActive(true);

        ValidarComenzar();
    }

    public void SeleccionarJugador2()
    {
        jugador1Listo = false;

        if (jugador2_select != null) jugador2_select.SetActive(true);
        if (jugador1_select != null) jugador1_select.SetActive(false);

        if (panelEscenario != null) panelEscenario.SetActive(false);
        cabanaLista = false;
        OcultarVistasEscenarios();

        MostrarAvisoInhabilitado();
        ValidarComenzar();
    }

    // =========================================================
    // 4. APARIENCIA - ESCENARIOS
    // =========================================================

    public void SeleccionarEscenario1() // Laboratorio
    {
        cabanaLista = false;
        OcultarVistasEscenarios();
        ApagarSelectoresEscenarios();

        if (selectorEscenario1 != null) selectorEscenario1.SetActive(true);
        if (vistaLaboratorio != null) vistaLaboratorio.SetActive(true);

        MostrarAvisoInhabilitado();
        ValidarComenzar();
    }

    public void SeleccionarEscenario2() // Ciudad
    {
        cabanaLista = false;
        OcultarVistasEscenarios();
        ApagarSelectoresEscenarios();

        if (selectorEscenario2 != null) selectorEscenario2.SetActive(true);
        if (vistaCiudad != null) vistaCiudad.SetActive(true);

        MostrarAvisoInhabilitado();
        ValidarComenzar();
    }

    public void SeleccionarEscenario3() // Cabaña
    {
        cabanaLista = true;
        OcultarVistasEscenarios();
        ApagarSelectoresEscenarios();

        if (selectorEscenario3 != null) selectorEscenario3.SetActive(true);
        if (vistaCabana != null) vistaCabana.SetActive(true);

        OcultarAvisoInhabilitado();
        ValidarComenzar();
    }

    // =========================================================
    // 5. NAVEGACIÓN Y ACTIVACIÓN DEL CARTEL
    // =========================================================

    // Asignar al botón Siguiente / Comenzar
    public void OnClick_ComenzarAJugar()
    {
        if (jugador1Listo && cabanaLista)
        {
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
            {
                botonComenzarJuego.SetActive(false);
            }
        }
    }

    // Asignar al botón Atrás en AparienciaPanel
    public void BotonAtrasDesdeApariencia()
    {
        ResetearApariencia();

        if (aparienciaPanel != null) aparienciaPanel.SetActive(false);
        if (modalidadPanel != null) modalidadPanel.SetActive(true);

        OcultarAvisoInhabilitado();
    }

    // Asignar a los botones Home
    public void BotonHome()
    {
        ResetearApariencia();
        ResetearModalidad();

        if (aparienciaPanel != null) aparienciaPanel.SetActive(false);
        if (modalidadPanel != null) modalidadPanel.SetActive(true);

        OcultarAvisoInhabilitado();
    }

    // =========================================================
    // MÉTODOS DE LIMPIEZA INTERNA
    // =========================================================

    private void MostrarCuadroSubmodo(GameObject cuadroActivo)
    {
        if (cuadroSubmodoHistoria != null)
            cuadroSubmodoHistoria.SetActive(cuadroSubmodoHistoria == cuadroActivo);

        if (cuadroSubmodoRonda5 != null)
            cuadroSubmodoRonda5.SetActive(cuadroSubmodoRonda5 == cuadroActivo);

        if (cuadroSubmodoSupervivencia != null)
            cuadroSubmodoSupervivencia.SetActive(cuadroSubmodoSupervivencia == cuadroActivo);
    }

    private void ResetearModalidad()
    {
        modoActual = ModoSeleccionado.Ninguno;
        textoNombreModo = "";
        textoNombreDificultad = "";

        if (textoModalidadDificultad != null) textoModalidadDificultad.text = "";

        MostrarCuadroSubmodo(null);
        ApagarSelectoresModos();
        ApagarTodosLosSelectoresDificultad();
    }

    private void ResetearApariencia()
    {
        jugador1Listo = false;
        cabanaLista = false;

        if (panelPersonaje != null) panelPersonaje.SetActive(true);
        if (panelEscenario != null) panelEscenario.SetActive(false);
        if (botonComenzarJuego != null) botonComenzarJuego.SetActive(false);

        // Al resetear la apariencia o volver atrás, se vuelve a habilitar Home
        SetBotonHomeHabilitado(true);

        if (panelRedirigiendo != null) panelRedirigiendo.SetActive(false);

        if (jugador1_select != null) jugador1_select.SetActive(false);
        if (jugador2_select != null) jugador2_select.SetActive(false);

        ApagarSelectoresEscenarios();
        OcultarVistasEscenarios();
    }

    private void ApagarSelectoresModos()
    {
        if (selectorModoHistoria != null) selectorModoHistoria.SetActive(false);
        if (selectorModoRonda5 != null) selectorModoRonda5.SetActive(false);
        if (selectorModoSupervivencia != null) selectorModoSupervivencia.SetActive(false);
    }

    private void ApagarTodosLosSelectoresDificultad()
    {
        ApagarArray(selectoresDificultadHistoria);
        ApagarArray(selectoresDificultadRonda5);
        ApagarArray(selectoresDificultadSupervivencia);
    }

    private void ApagarArray(GameObject[] arr)
    {
        if (arr == null) return;
        foreach (var go in arr)
        {
            if (go != null) go.SetActive(false);
        }
    }

    private void ApagarSelectoresEscenarios()
    {
        if (selectorEscenario1 != null) selectorEscenario1.SetActive(false);
        if (selectorEscenario2 != null) selectorEscenario2.SetActive(false);
        if (selectorEscenario3 != null) selectorEscenario3.SetActive(false);
    }

    private void OcultarVistasEscenarios()
    {
        if (vistaLaboratorio != null) vistaLaboratorio.SetActive(false);
        if (vistaCiudad != null) vistaCiudad.SetActive(false);
        if (vistaCabana != null) vistaCabana.SetActive(false);
    }

    private void ValidarComenzar()
    {
        bool todoSeleccionado = jugador1Listo && cabanaLista;

        if (botonComenzarJuego != null)
        {
            botonComenzarJuego.SetActive(todoSeleccionado);
        }

        // Si se eligió personaje y escenario válidos, deshabilita Home; de lo contrario queda activo
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
        if (cartelAvisoInhabilitado != null) cartelAvisoInhabilitado.SetActive(true);
    }

    private void OcultarAvisoInhabilitado()
    {
        if (cartelAvisoInhabilitado != null) cartelAvisoInhabilitado.SetActive(false);
    }
}