using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controlador del lobby de UN JUGADOR (escena SinglePlayerLobby).
/// - Muestra la configuración que se eligió antes en el menú (modo, escenario, dificultad), leída de ConfiguracionPartidaSeleccionada.
/// - Maneja la única columna (LobbyPlayerSlot): el personaje se elige con el botón PERSONAJE, igual que en el lobby multijugador.
/// - COMENZAR carga la escena de la partida. VOLVER cierra el lobby y vuelve al menú (misma escena, ver SingleplayerMenuManager).
/// - El menú lo abre con Abrir() y lo cierra con Cerrar(). Si la escena no tiene SingleplayerMenuManager (por ejemplo, probando
///   la escena del lobby sola), se abre solo al arrancar y VOLVER carga la escena del menú.
/// - La ventana CONFIGURAR PARTIDA es el mismo apartado del lobby multijugador: por ahora solo muestra lo elegido
///   (más adelante se podrá cambiar desde ahí).
/// Lo arma Game > Construir UI del Lobby Singleplayer (3D) (ver SinglePlayerLobbyUIBuilder).
/// </summary>
public class SinglePlayerLobbyController : MonoBehaviour
{
    // Solo para cuando el lobby está en una escena aparte del menú: la escena a la que vuelve VOLVER.
    public static string escenaOrigen = "";

    [Header("Jugador")]
    [SerializeField] private LobbyPlayerSlot slot;
    [SerializeField] private CharacterCatalog catalogoPersonajes;
    [SerializeField] private CharacterPreviewStage escenarioPersonajes;
    [Tooltip("Texto del rol de la tarjeta (se reemplaza por UN JUGADOR / TU).")]
    [SerializeField] private TMP_Text rolTexto;
    [SerializeField] private Sprite iconoVerde;
    [SerializeField] private Sprite iconoRojo;
    [SerializeField] private Color colorRojo = new Color(1f, 0f, 0.038f, 1f);

    [Header("Botones")]
    [SerializeField] private Button btnComenzar;
    [SerializeField] private Button btnPersonaje;
    [SerializeField] private Button btnVolver;

    [Header("Resumen de la partida (arriba a la derecha)")]
    [SerializeField] private TMP_Text resumenModo;
    [SerializeField] private TMP_Text resumenMapa;
    [SerializeField] private TMP_Text resumenDificultad;

    [Header("Ventana CONFIGURAR PARTIDA (por ahora solo muestra lo elegido)")]
    [SerializeField] private TMP_Text popupModo;
    [SerializeField] private TMP_Text popupMapa;
    [SerializeField] private TMP_Text popupDificultad;

    [Header("Ventana de mensajes")]
    [SerializeField] private GameObject popupMensaje;
    [SerializeField] private TMP_Text popupMensajeTitulo;
    [SerializeField] private TMP_Text popupMensajeTexto;

    [Header("Ventana PERSONAJE")]
    [Tooltip("Opcional. Si queda vacío se busca por nombre en la escena (incluso desactivada).")]
    [SerializeField] private GameObject ventanaSeleccionPersonaje;
    [SerializeField] private string nombreVentanaPersonaje = "CharacterSelectCanvas";

    [Header("Escenas")]
    [Tooltip("Escena de la partida de un jugador. Tiene que estar en Build Settings.")]
    [SerializeField] private string escenaJuegoSingleplayer = "MainSceneSinglePlayer";
    [Tooltip("Solo se usa si el lobby está solo en su escena (sin SingleplayerMenuManager).")]
    [SerializeField] private string escenaMenuPorDefecto = "PantallasUI";

    private const string TextoRol = "[ UN JUGADOR / TU ]";

    private bool cargando;

    // True mientras el lobby está abierto. Mientras está cerrado, Update no hace nada.
    private bool abierto;

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    private void Start()
    {
        // Si hay un menú de un jugador en la escena, es él quien abre el lobby (Abrir).
        // Si no lo hay (escena del lobby sola), se abre acá mismo.
        if (FindObjectOfType<SingleplayerMenuManager>(true) == null)
            Abrir();
    }

    // Lo llama SingleplayerMenuManager cuando el jugador terminó de elegir modalidad, mapa y dificultad.
    public void Abrir()
    {
        abierto = true;
        cargando = false;

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetBotonesInteractables(true);

        if (slot != null)
            slot.Configure(catalogoPersonajes, escenarioPersonajes, colorRojo, iconoVerde, iconoRojo);

        ActualizarResumen();

        if (!ConfiguracionValida())
        {
            MostrarMensaje(
                "SIN CONFIGURACION",
                "No hay una partida configurada. Volve al menu, elegi modalidad, dificultad y escenario, y entra de nuevo.");
        }
    }

    // Lo llama SingleplayerMenuManager al volver al menú: deja cerradas las ventanas que hayan quedado abiertas.
    public void Cerrar()
    {
        abierto = false;

        if (ventanaSeleccionPersonaje != null)
            ventanaSeleccionPersonaje.SetActive(false);

        if (popupMensaje != null)
            popupMensaje.SetActive(false);
    }

    private void Update()
    {
        if (!abierto || slot == null)
            return;

        // El slot no reescribe textos si nada cambió, así que se puede llamar todos los frames.
        // "Listo" = hay una configuración válida para empezar.
        slot.SetPlayer(true, true, ConfiguracionValida(), CharacterSelection.SelectedIndex);

        // El slot escribe "ANFITRION / TU": acá es un solo jugador.
        if (rolTexto != null && rolTexto.text != TextoRol)
            rolTexto.text = TextoRol;
    }

    // =========================================================
    // BOTONES
    // =========================================================

    public void OnClick_Comenzar()
    {
        if (cargando)
            return;

        if (!ConfiguracionValida())
        {
            MostrarMensaje(
                "SIN CONFIGURACION",
                "Falta elegir modalidad, dificultad o escenario. Volve al menu y elegilos de nuevo.");
            return;
        }

        StartCoroutine(CargarPartida());
    }

    public void OnClick_Personaje()
    {
        if (cargando)
            return;

        if (ventanaSeleccionPersonaje == null)
            ventanaSeleccionPersonaje = BuscarVentanaPersonaje();

        if (ventanaSeleccionPersonaje != null)
        {
            ventanaSeleccionPersonaje.SetActive(true);
        }
        else
        {
            MostrarMensaje(
                "SIN SELECCION",
                "No encuentro la ventana '" + nombreVentanaPersonaje + "' en esta escena. Agregala (Game > Construir UI de Seleccion de Personaje).");
        }
    }

    public void OnClick_Volver()
    {
        if (cargando)
            return;

        // Lo normal: el lobby está en la misma escena que el menú, así que solo se cierra y se vuelve al menú.
        SingleplayerMenuManager menu = FindObjectOfType<SingleplayerMenuManager>(true);

        if (menu != null)
        {
            menu.VolverDesdeLobby();
            return;
        }

        // Sin menú en la escena (probando el lobby solo): se carga la escena del menú.
        string escena = string.IsNullOrEmpty(escenaOrigen) ? escenaMenuPorDefecto : escenaOrigen;

        Time.timeScale = 1f;
        SceneManager.LoadScene(escena);
    }

    // =========================================================
    // CARGA DE LA PARTIDA
    // =========================================================

    private IEnumerator CargarPartida()
    {
        cargando = true;
        SetBotonesInteractables(false);

        TMP_Text label = btnComenzar != null ? btnComenzar.GetComponentInChildren<TMP_Text>(true) : null;
        string textoOriginal = label != null ? label.text : "";

        if (label != null)
            label.text = "CARGANDO...";

        Time.timeScale = 1f;
        AsyncOperation operacion = SceneManager.LoadSceneAsync(escenaJuegoSingleplayer);

        if (operacion == null)
        {
            Debug.LogError("[SinglePlayerLobby] No se pudo cargar la escena '" + escenaJuegoSingleplayer + "'. Revisa el nombre y que este en Build Settings.");

            if (label != null)
                label.text = textoOriginal;

            SetBotonesInteractables(true);
            cargando = false;

            MostrarMensaje("NO SE PUDO CARGAR", "No encuentro la escena '" + escenaJuegoSingleplayer + "'. Revisa que este en Build Settings.");
            yield break;
        }

        while (!operacion.isDone)
            yield return null;
    }

    private void SetBotonesInteractables(bool value)
    {
        if (btnComenzar != null) btnComenzar.interactable = value;
        if (btnPersonaje != null) btnPersonaje.interactable = value;
        if (btnVolver != null) btnVolver.interactable = value;
    }

    // =========================================================
    // CONFIGURACIÓN ELEGIDA EN EL MENÚ
    // =========================================================

    private static bool ConfiguracionValida()
    {
        ConfiguracionPartidaSeleccionada cfg = ConfiguracionPartidaSeleccionada.Instancia;

        return cfg != null &&
               cfg.modoSeleccionado != ConfiguracionPartidaSeleccionada.ModoJuego.Ninguno &&
               cfg.escenarioSeleccionado != ConfiguracionPartidaSeleccionada.Escenario.Ninguno &&
               cfg.dificultadSeleccionada != ConfiguracionPartidaSeleccionada.Dificultad.Ninguna;
    }

    private void ActualizarResumen()
    {
        string modo = "-";
        string mapa = "-";
        string dificultad = "-";

        ConfiguracionPartidaSeleccionada cfg = ConfiguracionPartidaSeleccionada.Instancia;

        if (cfg != null)
        {
            modo = NombreModo(cfg.modoSeleccionado);
            mapa = NombreEscenario(cfg.escenarioSeleccionado);
            dificultad = NombreDificultad(cfg.dificultadSeleccionada);
        }

        if (resumenModo != null) resumenModo.text = modo;
        if (resumenMapa != null) resumenMapa.text = mapa;
        if (resumenDificultad != null) resumenDificultad.text = dificultad;

        if (popupModo != null) popupModo.text = modo;
        if (popupMapa != null) popupMapa.text = mapa;
        if (popupDificultad != null) popupDificultad.text = dificultad;
    }

    private static string NombreModo(ConfiguracionPartidaSeleccionada.ModoJuego modo)
    {
        switch (modo)
        {
            case ConfiguracionPartidaSeleccionada.ModoJuego.Historia: return "HISTORIA";
            case ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5: return "RONDA 5";
            case ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia: return "RONDAS INFINITAS";
            default: return "-";
        }
    }

    private static string NombreEscenario(ConfiguracionPartidaSeleccionada.Escenario escenario)
    {
        switch (escenario)
        {
            case ConfiguracionPartidaSeleccionada.Escenario.Laboratorio: return "LABORATORIO";
            case ConfiguracionPartidaSeleccionada.Escenario.Ciudad: return "CIUDAD";
            case ConfiguracionPartidaSeleccionada.Escenario.Cabana: return "MANSION DEL BOSQUE";
            default: return "-";
        }
    }

    private static string NombreDificultad(ConfiguracionPartidaSeleccionada.Dificultad dificultad)
    {
        switch (dificultad)
        {
            case ConfiguracionPartidaSeleccionada.Dificultad.Normal: return "NORMAL";
            case ConfiguracionPartidaSeleccionada.Dificultad.Dificil: return "DIFICIL";
            case ConfiguracionPartidaSeleccionada.Dificultad.Pesadilla: return "PESADILLA";
            default: return "-";
        }
    }

    // =========================================================
    // AUXILIARES
    // =========================================================

    private void MostrarMensaje(string titulo, string texto)
    {
        if (popupMensaje == null)
        {
            Debug.LogWarning("[SinglePlayerLobby] " + titulo + ": " + texto);
            return;
        }

        if (popupMensajeTitulo != null) popupMensajeTitulo.text = titulo;
        if (popupMensajeTexto != null) popupMensajeTexto.text = texto;

        popupMensaje.SetActive(true);
    }

    private GameObject BuscarVentanaPersonaje()
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = BuscarEnHijos(roots[i].transform, nombreVentanaPersonaje);

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
}