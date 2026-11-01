using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Menú de un jugador: modalidad -> dificultad -> mapa -> LOBBY SINGLEPLAYER (todo en la MISMA escena).
/// Al elegir el mapa (o la dificultad, en Historia, que siempre es el Laboratorio) guarda la configuración,
/// oculta los otros menús y activa el panel del lobby (el que arma SinglePlayerLobbyUIBuilder).
/// El personaje se elige dentro del lobby (botón PERSONAJE, ventana que arma CharacterSelectBuilder).
/// El botón VOLVER del lobby llama a VolverDesdeLobby(): cierra el lobby y vuelve al inicio del menú.
/// </summary>
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

    [Header("3. Escenarios (mapa)")]
    [SerializeField] private GameObject panelEscenario;
    [SerializeField] private GameObject selectorEscenario1;
    [SerializeField] private GameObject selectorEscenario2;
    [SerializeField] private GameObject selectorEscenario3;

    [Header("3. Escenarios - Vistas (Info e Imagen)")]
    [SerializeField] private GameObject vistaLaboratorio;
    [SerializeField] private GameObject vistaCiudad;
    [SerializeField] private GameObject vistaCabana;

    [Header("Navegación")]
    [SerializeField] private GameObject botonHome;
    [SerializeField] private GameObject cartelCargandoEscenario;

    [Header("Lobby (misma escena)")]
    [Tooltip("Panel del lobby (el que arma SinglePlayerLobbyUIBuilder). Si queda vacío se busca por nombre en la escena, incluso desactivado. NO puede ser hijo de ningún panel del menú que se oculte.")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private string nombrePanelLobby = "salaEsperaSinglePlayerPanel";
    [Tooltip("Opcional. Si queda vacío se busca el SinglePlayerLobbyController en la escena.")]
    [SerializeField] private SinglePlayerLobbyController lobbyController;
    [Tooltip("Otros objetos del menú que se ocultan mientras está el lobby (título, fondo del menú, etc.). Se vuelven a mostrar al volver.")]
    [SerializeField] private GameObject[] objetosOcultosEnLobby;

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

    private ConfiguracionPartidaSeleccionada.Dificultad dificultadActual =
        ConfiguracionPartidaSeleccionada.Dificultad.Ninguna;

    // True mientras el lobby está abierto (evita que se vuelva a elegir algo del menú por detrás).
    private bool enLobby = false;

    // Objetos que estaban activos y se ocultaron al abrir el lobby (para devolverlos al volver).
    private readonly List<GameObject> ocultosPorElLobby = new List<GameObject>();


    private bool ModoHabilitado()
    {
        return modoActual == ModoSeleccionado.Historia ||
               modoActual == ModoSeleccionado.Ronda5 ||
               modoActual == ModoSeleccionado.Infinitas;
    }


    private void Start()
    {
        BotonHome();
    }


    // =========================================================
    // 1. MODOS
    // =========================================================

    public void SeleccionarModoHistoria()
    {
        modoActual = ModoSeleccionado.Historia;
        textoNombreModo = "MODALIDAD MODO HISTORIA";

        ApagarSelectoresModos();

        if (selectorModoHistoria != null)
            selectorModoHistoria.SetActive(true);

        MostrarCuadroSubmodo(cuadroSubmodoHistoria);
       // ApagarTodosLosSelectoresDificultad();
        ReiniciarDificultad();
        OcultarAvisoInhabilitado();
        ActualizarPanelEscenarioSegunModo();
    }


    public void SeleccionarModoRonda5()
    {
        modoActual = ModoSeleccionado.Ronda5;
        textoNombreModo = "MODALIDAD RONDA 5";

        ApagarSelectoresModos();

        if (selectorModoRonda5 != null)
            selectorModoRonda5.SetActive(true);

        MostrarCuadroSubmodo(cuadroSubmodoRonda5);
        //ApagarTodosLosSelectoresDificultad();
        ReiniciarDificultad();
        OcultarAvisoInhabilitado();
        ActualizarPanelEscenarioSegunModo();
    }


    public void SeleccionarModoSupervivencia()
    {
        modoActual = ModoSeleccionado.Infinitas;
        textoNombreModo = "MODALIDAD RONDAS INFINITAS";

        ApagarSelectoresModos();

        if (selectorModoSupervivencia != null)
            selectorModoSupervivencia.SetActive(true);

        MostrarCuadroSubmodo(cuadroSubmodoSupervivencia);
        //ApagarTodosLosSelectoresDificultad();

        ReiniciarDificultad();
        OcultarAvisoInhabilitado();
        ActualizarPanelEscenarioSegunModo();
    }


    public void SeleccionarModalidadAleatoria()
    {
        MostrarAvisoInhabilitado();
    }


    // =========================================================
    // 2. DIFICULTADES
    // =========================================================

   /* private void ElegirDificultad(
        GameObject marcoSeleccionado,
        string nombreDificultad,
        ConfiguracionPartidaSeleccionada.Dificultad dificultad
    )
    {
        if (enLobby)
            return;

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

        // Historia siempre es en el Laboratorio: no hay mapa que elegir, va directo al lobby.
        if (modoActual == ModoSeleccionado.Historia)
        {
            if (!IrAlLobby() && modalidadPanel != null)
                modalidadPanel.SetActive(true);

            return;
        }

        // Ronda 5 / Infinitas: falta elegir el mapa.
        if (aparienciaPanel != null)
            aparienciaPanel.SetActive(true);

        ResetearApariencia();

        if (panelEscenario != null)
            panelEscenario.SetActive(true);
    }*/

    private void ElegirDificultad(
    GameObject marcoSeleccionado,
    string nombreDificultad,
    ConfiguracionPartidaSeleccionada.Dificultad dificultad
)
{
    if (enLobby)
        return;

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

    AvanzarSiEstaCompleto(false);
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
    // 3. ESCENARIOS (al elegir uno, va directo al lobby)
    // =========================================================

    public void SeleccionarEscenario1()
    {
        ElegirEscenario(selectorEscenario1, vistaLaboratorio);
    }


    public void SeleccionarEscenario2()
    {
        ElegirEscenario(selectorEscenario2, vistaCiudad);
    }


    public void SeleccionarEscenario3()
    {
        ElegirEscenario(selectorEscenario3, vistaCabana);
    }


    private void ElegirEscenario(GameObject selector, GameObject vista)
    {
        if (enLobby)
            return;

        OcultarVistasEscenarios();
        ApagarSelectoresEscenarios();

        if (selector != null)
            selector.SetActive(true);

        if (vista != null)
            vista.SetActive(true);

        OcultarAvisoInhabilitado();

       // IrAlLobby();
       AvanzarSiEstaCompleto(true);
    }


    // =========================================================
    // 4. NAVEGACIÓN
    // =========================================================

    public void BotonAtrasDesdeApariencia()
    {
        if (enLobby)
            return;

        ResetearApariencia();

        if (aparienciaPanel != null)
            aparienciaPanel.SetActive(false);

        if (modalidadPanel != null)
            modalidadPanel.SetActive(true);

        OcultarAvisoInhabilitado();
    }


    public void BotonHome()
    {
        // Si el lobby estaba abierto (o recién arranca la escena), se cierra y se devuelve lo que se había ocultado.
        CerrarLobby();

        if (cartelCargandoEscenario != null)
            cartelCargandoEscenario.SetActive(false);

        ResetearApariencia();
        ResetearModalidad();

        if (aparienciaPanel != null)
            aparienciaPanel.SetActive(false);

        if (modalidadPanel != null)
            modalidadPanel.SetActive(false);

        OcultarAvisoInhabilitado();
    }

    public void MostrarConfiguracionSingleplayer()
{
    if (enLobby)
        return;

    if (modalidadPanel != null)
        modalidadPanel.SetActive(true);
}

    public void BotonContinuar()
{
    if (enLobby)
        return;

    bool mapaOk = modoActual == ModoSeleccionado.Historia ||
                  (selectorEscenario1 != null && selectorEscenario1.activeSelf) ||
                  (selectorEscenario2 != null && selectorEscenario2.activeSelf) ||
                  (selectorEscenario3 != null && selectorEscenario3.activeSelf);

    if (!ModoHabilitado() ||
        dificultadActual == ConfiguracionPartidaSeleccionada.Dificultad.Ninguna ||
        !mapaOk)
    {
        MostrarAvisoInhabilitado(); // falta elegir algo
        return;
    }

    IrAlLobby();
}


    // =========================================================
    // 5. LOBBY (misma escena)
    // =========================================================

    // Guarda la configuración, oculta los menús y activa el lobby. Devuelve false si no se pudo.
    private bool IrAlLobby()
    {
        if (enLobby)
            return false;

        if (!BuscarReferenciasDelLobby())
            return false;

        if (!GuardarConfiguracionPartida())
            return false;

        enLobby = true;

        OcultarMenusParaLobby();
        MostrarLobby();

        return true;
    }


    // Lo llama el botón VOLVER del lobby (SinglePlayerLobbyController.OnClick_Volver).
    // Cierra el lobby y deja el menú otra vez en la primera pantalla (modalidad).
    public void VolverDesdeLobby()
    {
        BotonHome();
    }


    // Busca el panel y el controlador del lobby si no se asignaron en el Inspector.
    private bool BuscarReferenciasDelLobby()
    {
        if (lobbyPanel == null)
            lobbyPanel = BuscarObjetoEnEscena(nombrePanelLobby);

        if (lobbyPanel == null)
        {
            Debug.LogError(
                "SingleplayerMenuManager: no encuentro el panel del lobby '" + nombrePanelLobby +
                "' en la escena. Armalo con Game > Construir UI del Lobby Singleplayer (3D) o asignalo en el Inspector."
            );

            return false;
        }

        if (lobbyController == null)
            lobbyController = FindObjectOfType<SinglePlayerLobbyController>(true);

        if (lobbyController == null)
        {
            Debug.LogError(
                "SingleplayerMenuManager: no encuentro el SinglePlayerLobbyController en la escena. " +
                "Lo crea Game > Construir UI del Lobby Singleplayer (3D)."
            );

            return false;
        }

        return true;
    }


    // Oculta todo lo del menú (paneles, carteles, botón home, encabezado y los objetos extra).
    private void OcultarMenusParaLobby()
    {
        ocultosPorElLobby.Clear();

        // Paneles del flujo (se vuelven a armar solos con BotonHome al volver).
        ApagarSinTocarAlLobby(modalidadPanel);
        ApagarSinTocarAlLobby(aparienciaPanel);
        ApagarSinTocarAlLobby(panelEscenario);
        ApagarSinTocarAlLobby(cartelAvisoInhabilitado);
        ApagarSinTocarAlLobby(cartelCargandoEscenario);

        // Estos se guardan para devolverlos tal cual estaban.
        OcultarGuardando(botonHome);

        if (textoModalidadDificultad != null)
            OcultarGuardando(textoModalidadDificultad.gameObject);

        if (objetosOcultosEnLobby != null)
        {
            for (int i = 0; i < objetosOcultosEnLobby.Length; i++)
                OcultarGuardando(objetosOcultosEnLobby[i]);
        }
    }


    private void MostrarLobby()
    {
        // Se activa el panel y también sus padres (por si el Canvas del lobby estaba apagado).
        Transform t = lobbyPanel.transform;

        while (t != null)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);

            t = t.parent;
        }

        if (lobbyController != null)
            lobbyController.Abrir();
    }


    // Cierra el lobby y devuelve lo que se había ocultado. Se puede llamar aunque el lobby no esté abierto.
    private void CerrarLobby()
    {
        enLobby = false;

        if (lobbyController == null)
            lobbyController = FindObjectOfType<SinglePlayerLobbyController>(true);

        if (lobbyController != null)
            lobbyController.Cerrar();

        if (lobbyPanel == null)
            lobbyPanel = BuscarObjetoEnEscena(nombrePanelLobby);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);

        for (int i = 0; i < ocultosPorElLobby.Count; i++)
        {
            if (ocultosPorElLobby[i] != null)
                ocultosPorElLobby[i].SetActive(true);
        }

        ocultosPorElLobby.Clear();
    }


    // Apaga un objeto, salvo que sea el lobby o lo contenga (para no apagar el lobby por error).
    private void ApagarSinTocarAlLobby(GameObject go)
    {
        if (go == null)
            return;

        if (ContieneAlLobby(go))
        {
            Debug.LogWarning(
                "SingleplayerMenuManager: '" + go.name + "' contiene al panel del lobby, no se oculta. " +
                "Sacá el lobby de adentro de ese objeto."
            );

            return;
        }

        go.SetActive(false);
    }


    // Igual que el anterior, pero recuerda si estaba activo para volver a activarlo al salir del lobby.
    private void OcultarGuardando(GameObject go)
    {
        if (go == null || !go.activeSelf)
            return;

        if (ContieneAlLobby(go))
        {
            Debug.LogWarning(
                "SingleplayerMenuManager: '" + go.name + "' contiene al panel del lobby, no se oculta. " +
                "Sacá el lobby de adentro de ese objeto."
            );

            return;
        }

        go.SetActive(false);
        ocultosPorElLobby.Add(go);
    }


    private bool ContieneAlLobby(GameObject go)
    {
        return lobbyPanel != null && lobbyPanel.transform.IsChildOf(go.transform);
    }


    // Busca un objeto por nombre en toda la escena (incluso desactivado).
    private static GameObject BuscarObjetoEnEscena(string nombre)
    {
        if (string.IsNullOrEmpty(nombre))
            return null;

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = BuscarEnHijos(roots[i].transform, nombre);

            if (found != null)
                return found.gameObject;
        }

        return null;
    }


    private static Transform BuscarEnHijos(Transform t, string nombre)
    {
        if (t.name == nombre)
            return t;

        foreach (Transform child in t)
        {
            Transform found = BuscarEnHijos(child, nombre);

            if (found != null)
                return found;
        }

        return null;
    }


    // =========================================================
    // 6. AUXILIARES
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
        if (panelEscenario != null)
            panelEscenario.SetActive(false);

        SetBotonHomeHabilitado(true);

        if (cartelCargandoEscenario != null)
            cartelCargandoEscenario.SetActive(false);

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


    // Guarda modo, escenario y dificultad en ConfiguracionPartidaSeleccionada (la lee el lobby y después la partida).
    // Devuelve false si algo impide guardarla.
    private bool GuardarConfiguracionPartida()
    {
        if (ConfiguracionPartidaSeleccionada.Instancia == null)
        {
            Debug.LogError(
                "SingleplayerMenuManager: No existe una ConfiguracionPartidaSeleccionada en la escena del menú."
            );

            return false;
        }


        // ESCENARIO
        if (modoActual == ModoSeleccionado.Historia)
        {
            ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado =
                ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;
        }
        else if (selectorEscenario1 != null && selectorEscenario1.activeSelf)
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

            return false;
        }


        // MODO
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


        // DIFICULTAD
        if (dificultadActual != ConfiguracionPartidaSeleccionada.Dificultad.Ninguna)
        {
            ConfiguracionPartidaSeleccionada.Instancia.dificultadSeleccionada =
                dificultadActual;
        }


        // PERSONAJE: ya no se guarda acá. Se elige en el lobby y queda en CharacterSelection.SelectedIndex.


        Debug.Log(
            "Configuración de partida guardada: " +
            "Escenario = " +
            ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado +
            " | Modo = " +
            ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado +
            " | Dificultad = " +
            ConfiguracionPartidaSeleccionada.Instancia.dificultadSeleccionada
        );

        return true;
    }

    private void ReiniciarDificultad()
{
    dificultadActual = ConfiguracionPartidaSeleccionada.Dificultad.Ninguna;
    textoNombreDificultad = "";
    ApagarTodosLosSelectoresDificultad();

    if (textoModalidadDificultad != null)
        textoModalidadDificultad.text = textoNombreModo;
}

private void ActualizarPanelEscenarioSegunModo()
{
    bool necesitaMapa = modoActual == ModoSeleccionado.Ronda5 ||
                        modoActual == ModoSeleccionado.Infinitas;

    if (aparienciaPanel != null)
        aparienciaPanel.SetActive(necesitaMapa);

    if (panelEscenario != null)
        panelEscenario.SetActive(necesitaMapa);

    if (!necesitaMapa) // Historia siempre es en el Laboratorio
    {
        ApagarSelectoresEscenarios();
        OcultarVistasEscenarios();
    }
}

private bool ConfiguracionCompleta()
{
    bool mapaOk = modoActual == ModoSeleccionado.Historia ||
                  (selectorEscenario1 != null && selectorEscenario1.activeSelf) ||
                  (selectorEscenario2 != null && selectorEscenario2.activeSelf) ||
                  (selectorEscenario3 != null && selectorEscenario3.activeSelf);

    return ModoHabilitado() &&
           dificultadActual != ConfiguracionPartidaSeleccionada.Dificultad.Ninguna &&
           mapaOk;
}

private void AvanzarSiEstaCompleto(bool avisar)
{
    if (enLobby)
        return;

    if (ConfiguracionCompleta())
        IrAlLobby();
    else if (avisar)
        MostrarAvisoInhabilitado(); // falta elegir algo
}
}