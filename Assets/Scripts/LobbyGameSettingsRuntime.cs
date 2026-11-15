using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Convierte el ConfigurarPartidaPopup que ya existe en los lobbies en un selector real.
/// No necesita referencias nuevas en el Inspector: reutiliza Seccion_Modo / Seccion_Mapa /
/// Seccion_Dificultad y sus objetos Contenido/Valor.
///
/// SINGLEPLAYER: el jugador puede editar todo.
/// MULTIPLAYER: solamente el Host puede editar. Los clientes ven la configuración sincronizada.
///
/// DIFICULTADES BLOQUEADAS (ver DesbloqueoDificultades): el que edita puede dejar seleccionada una
/// dificultad bloqueada. Se muestra en rojo con su requisito debajo y, mientras siga elegida,
/// el lobby no deja comenzar (ver DificultadSeleccionadaBloqueada).
/// Los invitados no se validan: juegan lo que eligió el host.
/// </summary>
public class LobbyGameSettingsRuntime : MonoBehaviour
{
    // Alto reservado, dentro del recuadro de dificultad, para el texto del requisito.
    private const float AltoRequisito = 26f;

    // Color de los valores elegidos (el mismo verde de los títulos "MODO DE JUEGO", "MAPA"...).
    private static readonly Color ColorValor = new Color(0f, 1f, 0.4f, 1f);

    // Color de una dificultad bloqueada, en el popup y en el cartel "SELECCIÓN ACTUAL".
    private static readonly Color ColorBloqueada = new Color(1f, 0f, 0.038f, 1f);
    private const string ColorBloqueadaHex = "#FF000A";

    private GameObject popup;
    private TMP_Text textoModo;
    private TMP_Text textoMapa;
    private TMP_Text textoDificultad;
    private TMP_Text textoRequisito;
    private TMP_Text subtitulo;

    private Button botonModo;
    private Button botonMapa;
    private Button botonDificultad;

    private bool editable;
    private Action onChanged;
    private bool inicializado;

    public bool Inicializado => inicializado;

    public void Inicializar(
        TMP_Text referenciaModo,
        TMP_Text referenciaMapa,
        TMP_Text referenciaDificultad,
        bool puedeEditar,
        Action alCambiar)
    {
        onChanged = alCambiar;

        // NetworkLobby no contiene obligatoriamente este objeto. Si venimos de una ruta
        // donde no sobrevivió desde la escena anterior, los botones del popup no tendrían
        // ningún dato que modificar. Garantizamos una única instancia persistente acá.
        EnsureConfigurationInstance();

        popup = BuscarPopupDesdeReferencia(referenciaModo);

        if (popup == null)
            popup = BuscarPopupConfiguracionEnEscena();

        if (popup == null)
        {
            Debug.LogWarning("[LobbyGameSettings] No encontré ConfigurarPartidaPopup en la escena.");
            inicializado = false;
            return;
        }

        textoModo = referenciaModo != null
            ? referenciaModo
            : BuscarValorDeSeccion(popup.transform, "Seccion_Modo");

        textoMapa = referenciaMapa != null
            ? referenciaMapa
            : BuscarValorDeSeccion(popup.transform, "Seccion_Mapa");

        textoDificultad = referenciaDificultad != null
            ? referenciaDificultad
            : BuscarValorDeSeccion(popup.transform, "Seccion_Dificultad");

        subtitulo = BuscarTexto(popup.transform, "Subtitulo");

        botonModo = PrepararBoton("Seccion_Modo", SiguienteModo);
        botonMapa = PrepararBoton("Seccion_Mapa", SiguienteMapa);
        botonDificultad = PrepararBoton("Seccion_Dificultad", SiguienteDificultad);
        textoRequisito = PrepararTextoRequisito();

        inicializado = true;
        SetEditable(puedeEditar);
        ActualizarVisual();
    }

    public void SetEditable(bool puedeEditar)
    {
        editable = puedeEditar;

        if (!editable)
            Cerrar();

        if (!inicializado)
            return;

        bool mapaEditable = editable && !EsHistoria();

        if (botonModo != null)
            botonModo.interactable = editable;

        if (botonMapa != null)
            botonMapa.interactable = mapaEditable;

        if (botonDificultad != null)
            botonDificultad.interactable = editable;

        if (subtitulo != null)
        {
            subtitulo.text = editable
                ? "HACE CLICK EN CADA OPCION PARA CAMBIARLA"
                : "SOLO EL ANFITRION PUEDE MODIFICAR LA CONFIGURACION";
        }

        ActualizarVisual();
    }

    public void Abrir()
    {
        if (editable && popup != null)
        {
            popup.SetActive(true);
            popup.transform.SetAsLastSibling();
            ActualizarVisual();
        }
    }

    public void Cerrar()
    {
        if (popup != null)
            popup.SetActive(false);
    }

    public void ActualizarVisual()
    {
        ConfiguracionPartidaSeleccionada cfg = EnsureConfigurationInstance();

        if (cfg == null)
            return;

        // Historia siempre comienza en Laboratorio.
        if (cfg.modoSeleccionado == ConfiguracionPartidaSeleccionada.ModoJuego.Historia &&
            cfg.escenarioSeleccionado != ConfiguracionPartidaSeleccionada.Escenario.Laboratorio)
        {
            cfg.escenarioSeleccionado = ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;
        }

        string modo = NombreModo(cfg.modoSeleccionado);
        string mapa = NombreEscenario(cfg.escenarioSeleccionado);
        string dificultad = NombreDificultad(cfg.dificultadSeleccionada);

        if (textoModo != null)
        {
            textoModo.text = DecorarValor(modo, editable);
            PonerColor(textoModo, ColorValor);
        }

        if (textoMapa != null)
        {
            if (EsHistoria())
                textoMapa.text = "LABORATORIO  [ FIJO EN HISTORIA ]";
            else
                textoMapa.text = DecorarValor(mapa, editable);

            PonerColor(textoMapa, ColorValor);
        }

        ActualizarDificultadVisual(cfg, dificultad);

        // También actualiza el resumen superior del lobby si existe.
        ActualizarTodosLosTextosPorNombre("ModoValor", modo);
        ActualizarTodosLosTextosPorNombre("MapaValor", mapa);
        ActualizarTodosLosTextosPorNombre("DificultadValor", dificultad);

        if (botonMapa != null)
            botonMapa.interactable = editable && !EsHistoria();
    }

    // =========================================================
    // LECTURA DE LA SELECCIÓN ACTUAL (para el lobby cinematográfico)
    // Fuente real: ConfiguracionPartidaSeleccionada.Instancia. El host la edita
    // y el controlador del lobby la sincroniza a los clientes. No se leen textos de la UI.
    // =========================================================

    /// <summary>
    /// "MODO  -  DIFICULTAD" con los valores reales. Solo incluye lo que ya está elegido.
    /// Con marcarBloqueada, si la dificultad está bloqueada para el jugador de esta máquina
    /// se escribe en rojo (rich text de TMP). Pasarlo en true solo para el que decide la
    /// configuración (singleplayer o anfitrión): a los invitados no se les bloquea nada.
    /// </summary>
    public static string ObtenerResumenModoDificultad(bool marcarBloqueada = false)
    {
        ConfiguracionPartidaSeleccionada cfg = EnsureConfigurationInstance();

        if (cfg == null)
            return "SIN CONFIGURAR";

        string modo = NombreModo(cfg.modoSeleccionado);
        string dificultad = NombreDificultad(cfg.dificultadSeleccionada);

        bool hayModo = modo != "-";
        bool hayDificultad = dificultad != "-";

        if (marcarBloqueada && hayDificultad && DificultadSeleccionadaBloqueada())
            dificultad = "<color=" + ColorBloqueadaHex + ">" + dificultad + "</color>";

        if (hayModo && hayDificultad)
            return modo + "  -  " + dificultad;

        if (hayModo)
            return modo;

        if (hayDificultad)
            return dificultad;

        return "SIN CONFIGURAR";
    }

    /// <summary>Nombre real del mapa elegido. En Historia el mapa es siempre el Laboratorio.</summary>
    public static string ObtenerNombreMapaActual()
    {
        ConfiguracionPartidaSeleccionada cfg = EnsureConfigurationInstance();

        if (cfg == null)
            return "-";

        if (cfg.modoSeleccionado == ConfiguracionPartidaSeleccionada.ModoJuego.Historia)
            return NombreEscenario(ConfiguracionPartidaSeleccionada.Escenario.Laboratorio);

        return NombreEscenario(cfg.escenarioSeleccionado);
    }

    /// <summary>
    /// ¿La dificultad seleccionada está bloqueada para el jugador de ESTA máquina en el modo elegido?
    /// Mientras lo esté, el que decide la configuración (singleplayer o anfitrión) no puede comenzar.
    /// Los invitados no deben consultarlo: juegan lo que eligió el anfitrión.
    /// </summary>
    public static bool DificultadSeleccionadaBloqueada()
    {
        ConfiguracionPartidaSeleccionada cfg = EnsureConfigurationInstance();

        return cfg != null &&
               !DesbloqueoDificultades.EstaDesbloqueada(
                   cfg.modoSeleccionado,
                   cfg.dificultadSeleccionada);
    }

    public static bool ConfiguracionValida()
    {
        ConfiguracionPartidaSeleccionada cfg = EnsureConfigurationInstance();

        return cfg != null &&
               cfg.modoSeleccionado != ConfiguracionPartidaSeleccionada.ModoJuego.Ninguno &&
               cfg.escenarioSeleccionado != ConfiguracionPartidaSeleccionada.Escenario.Ninguno &&
               cfg.dificultadSeleccionada != ConfiguracionPartidaSeleccionada.Dificultad.Ninguna;
    }

    private void SiguienteModo()
    {
        if (!editable)
            return;

        ConfiguracionPartidaSeleccionada cfg = EnsureConfigurationInstance();
        if (cfg == null)
            return;

        switch (cfg.modoSeleccionado)
        {
            case ConfiguracionPartidaSeleccionada.ModoJuego.Ninguno:
                cfg.modoSeleccionado = ConfiguracionPartidaSeleccionada.ModoJuego.Historia;
                break;
            case ConfiguracionPartidaSeleccionada.ModoJuego.Historia:
                cfg.modoSeleccionado = ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5;
                break;
            case ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5:
                cfg.modoSeleccionado = ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;
                break;
            default:
                cfg.modoSeleccionado = ConfiguracionPartidaSeleccionada.ModoJuego.Historia;
                break;
        }

        if (cfg.modoSeleccionado == ConfiguracionPartidaSeleccionada.ModoJuego.Historia)
            cfg.escenarioSeleccionado = ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;

        CambioRealizado();
    }

    private void SiguienteMapa()
    {
        if (!editable || EsHistoria())
            return;

        ConfiguracionPartidaSeleccionada cfg = EnsureConfigurationInstance();
        if (cfg == null)
            return;

        switch (cfg.escenarioSeleccionado)
        {
            case ConfiguracionPartidaSeleccionada.Escenario.Ninguno:
                cfg.escenarioSeleccionado = ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;
                break;
            case ConfiguracionPartidaSeleccionada.Escenario.Laboratorio:
                cfg.escenarioSeleccionado = ConfiguracionPartidaSeleccionada.Escenario.Ciudad;
                break;
            case ConfiguracionPartidaSeleccionada.Escenario.Ciudad:
                cfg.escenarioSeleccionado = ConfiguracionPartidaSeleccionada.Escenario.Cabana;
                break;
            default:
                cfg.escenarioSeleccionado = ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;
                break;
        }

        CambioRealizado();
    }

    private void SiguienteDificultad()
    {
        if (!editable)
            return;

        ConfiguracionPartidaSeleccionada cfg = EnsureConfigurationInstance();
        if (cfg == null)
            return;

        switch (cfg.dificultadSeleccionada)
        {
            case ConfiguracionPartidaSeleccionada.Dificultad.Ninguna:
                cfg.dificultadSeleccionada = ConfiguracionPartidaSeleccionada.Dificultad.Normal;
                break;
            case ConfiguracionPartidaSeleccionada.Dificultad.Normal:
                cfg.dificultadSeleccionada = ConfiguracionPartidaSeleccionada.Dificultad.Dificil;
                break;
            case ConfiguracionPartidaSeleccionada.Dificultad.Dificil:
                cfg.dificultadSeleccionada = ConfiguracionPartidaSeleccionada.Dificultad.Pesadilla;
                break;
            default:
                cfg.dificultadSeleccionada = ConfiguracionPartidaSeleccionada.Dificultad.Normal;
                break;
        }

        // Una dificultad bloqueada también queda seleccionada: se muestra en rojo con su
        // requisito y el lobby no deja comenzar mientras siga elegida.
        CambioRealizado();
    }

    private void ActualizarDificultadVisual(ConfiguracionPartidaSeleccionada cfg, string nombreSeleccionada)
    {
        // Solo se le bloquea al que decide la configuración (singleplayer o anfitrión).
        bool bloqueada = editable && DificultadSeleccionadaBloqueada();

        if (textoDificultad != null)
        {
            textoDificultad.text = bloqueada
                ? nombreSeleccionada + "  [ BLOQUEADA ]  >"
                : DecorarValor(nombreSeleccionada, editable);

            PonerColor(textoDificultad, bloqueada ? ColorBloqueada : ColorValor);

            // Deja lugar abajo para el requisito.
            Vector4 margen = new Vector4(0f, 0f, 0f, bloqueada ? AltoRequisito : 0f);

            if (textoDificultad.margin != margen)
                textoDificultad.margin = margen;
        }

        if (textoRequisito == null)
            return;

        if (textoRequisito.gameObject.activeSelf != bloqueada)
            textoRequisito.gameObject.SetActive(bloqueada);

        if (!bloqueada)
            return;

        string requisito =
            "DESBLOQUEAR: " +
            DesbloqueoDificultades.TextoRequisito(cfg.modoSeleccionado, cfg.dificultadSeleccionada);

        if (textoRequisito.text != requisito)
            textoRequisito.text = requisito;
    }

    private static void PonerColor(TMP_Text texto, Color color)
    {
        if (texto != null && texto.color != color)
            texto.color = color;
    }

    // Texto del requisito, debajo del valor de la dificultad (dentro del mismo recuadro).
    // Se crea en runtime para no tener que reconstruir la UI de los lobbies.
    private TMP_Text PrepararTextoRequisito()
    {
        Transform seccion = BuscarEnHijos(popup.transform, "Seccion_Dificultad");
        if (seccion == null)
            return null;

        Transform contenido = BuscarEnHijos(seccion, "Contenido");
        if (contenido == null)
            contenido = seccion;

        const string nombre = "RequisitoDesbloqueo";

        Transform existente = contenido.Find(nombre);
        if (existente != null)
            return existente.GetComponent<TMP_Text>();

        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = contenido.gameObject.layer;
        go.transform.SetParent(contenido, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 4f);
        rect.sizeDelta = new Vector2(-12f, AltoRequisito);

        TextMeshProUGUI texto = go.AddComponent<TextMeshProUGUI>();

        if (textoDificultad != null && textoDificultad.font != null)
        {
            texto.font = textoDificultad.font;
            texto.fontSharedMaterial = textoDificultad.fontSharedMaterial;
        }

        texto.alignment = TextAlignmentOptions.Center;
        texto.color = new Color(1f, 0.78f, 0.25f, 1f);
        // Siempre en un solo renglón: si el requisito es largo, se achica la letra en vez de cortarse.
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.overflowMode = TextOverflowModes.Overflow;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 9f;
        texto.fontSizeMax = 15f;
        texto.raycastTarget = false;
        texto.text = string.Empty;

        go.SetActive(false);
        return texto;
    }

    private void CambioRealizado()
    {
        ActualizarVisual();

        if (DifficultyManager.Instance != null)
            DifficultyManager.Instance.SincronizarConSeleccion();

        onChanged?.Invoke();
    }

    private bool EsHistoria()
    {
        ConfiguracionPartidaSeleccionada cfg = EnsureConfigurationInstance();
        return cfg != null &&
               cfg.modoSeleccionado == ConfiguracionPartidaSeleccionada.ModoJuego.Historia;
    }

    private Button PrepararBoton(string nombreSeccion, Action accion)
    {
        Transform seccion = BuscarEnHijos(popup.transform, nombreSeccion);
        if (seccion == null)
            return null;

        Transform contenido = BuscarEnHijos(seccion, "Contenido");
        if (contenido == null)
            contenido = seccion;

        Image image = contenido.GetComponent<Image>();
        if (image != null)
            image.raycastTarget = true;

        Button button = contenido.GetComponent<Button>();
        if (button == null)
            button = contenido.gameObject.AddComponent<Button>();

        if (image != null)
            button.targetGraphic = image;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => accion());

        return button;
    }

    private static string DecorarValor(string valor, bool puedeEditar)
    {
        if (!puedeEditar)
            return valor;

        if (valor == "-")
            return "-  [ CLICK PARA ELEGIR ]";

        return valor + "  >";
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

    /// <summary>
    /// Devuelve la configuración persistente. NetworkLobby no trae una instancia propia,
    /// así que si no llegó una desde la escena anterior se crea una en runtime.
    /// Esto evita que el popup parezca clickeable pero no cambie ningún valor.
    /// </summary>
    private static ConfiguracionPartidaSeleccionada EnsureConfigurationInstance()
    {
        if (ConfiguracionPartidaSeleccionada.Instancia != null)
            return ConfiguracionPartidaSeleccionada.Instancia;

        ConfiguracionPartidaSeleccionada[] existentes =
            FindObjectsByType<ConfiguracionPartidaSeleccionada>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (existentes != null && existentes.Length > 0)
        {
            GameObject existente = existentes[0].gameObject;

            // Awake no se ejecuta mientras un objeto de escena nace inactivo. Activarlo permite
            // que el propio componente establezca Instancia y aplique DontDestroyOnLoad.
            if (!existente.activeSelf)
                existente.SetActive(true);

            if (ConfiguracionPartidaSeleccionada.Instancia != null)
                return ConfiguracionPartidaSeleccionada.Instancia;
        }

        GameObject go = new GameObject("ConfiguracionPartidaSeleccionada_Runtime");
        ConfiguracionPartidaSeleccionada creada = go.AddComponent<ConfiguracionPartidaSeleccionada>();

        Debug.Log("[LobbyGameSettings] Se creó ConfiguracionPartidaSeleccionada en runtime para el lobby.");
        return creada;
    }

    private static GameObject BuscarPopupDesdeReferencia(TMP_Text referencia)
    {
        if (referencia == null)
            return null;

        Transform t = referencia.transform;
        while (t != null)
        {
            if (t.name == "ConfigurarPartidaPopup")
                return t.gameObject;
            t = t.parent;
        }

        return null;
    }

    private static GameObject BuscarPopupConfiguracionEnEscena()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < all.Length; j++)
            {
                if (all[j].name != "ConfigurarPartidaPopup")
                    continue;

                if (BuscarEnHijos(all[j], "Seccion_Modo") != null &&
                    BuscarEnHijos(all[j], "Seccion_Mapa") != null &&
                    BuscarEnHijos(all[j], "Seccion_Dificultad") != null)
                {
                    return all[j].gameObject;
                }
            }
        }

        return null;
    }

    private static TMP_Text BuscarValorDeSeccion(Transform popupTransform, string nombreSeccion)
    {
        Transform seccion = BuscarEnHijos(popupTransform, nombreSeccion);
        if (seccion == null)
            return null;

        Transform contenido = BuscarEnHijos(seccion, "Contenido");
        if (contenido == null)
            return null;

        Transform valor = BuscarEnHijos(contenido, "Valor");

        // El builder multiplayer antiguo creó este texto con nombre "Pendiente" y contenido
        // "PROXIMAMENTE". Reutilizarlo evita reconstruir la UI o tocar la escena completa.
        if (valor == null)
        {
            valor = BuscarEnHijos(contenido, "Pendiente");

            if (valor != null)
                valor.name = "Valor";
        }

        return valor != null ? valor.GetComponent<TMP_Text>() : null;
    }

    private static TMP_Text BuscarTexto(Transform root, string nombre)
    {
        Transform t = BuscarEnHijos(root, nombre);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private static Transform BuscarEnHijos(Transform root, string nombre)
    {
        if (root == null)
            return null;

        if (root.name == nombre)
            return root;

        foreach (Transform child in root)
        {
            Transform found = BuscarEnHijos(child, nombre);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void ActualizarTodosLosTextosPorNombre(string nombre, string valor)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            TMP_Text[] texts = roots[i].GetComponentsInChildren<TMP_Text>(true);
            for (int j = 0; j < texts.Length; j++)
            {
                if (texts[j] != null && texts[j].name == nombre)
                    texts[j].text = valor;
            }
        }
    }
}
