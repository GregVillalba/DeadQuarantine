using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Modo = ConfiguracionPartidaSeleccionada.ModoJuego;

/// <summary>
/// Cartel "DESBLOQUEO ESPECIAL" del panel de tutorial (pestaña DIFICULTADES), debajo del cartel de NORMAL.
/// Explica cómo desbloquear Difícil y Pesadilla juntas en cada modo con la condición especial.
///
/// - Se arma solo al cargar la escena del menú: no hay que agregar nada en la escena.
///   Por eso se ve en Play, no en la vista de Escena del Editor.
/// - Los números salen de DesbloqueoDificultades: si se cambia un requisito especial, el texto se actualiza solo.
/// - Copia fuentes y colores del cartel de NORMAL para verse igual.
/// - Va debajo del cartel de NORMAL porque la condición especial se cumple jugando en Normal.
/// </summary>
public static class TutorialDesbloqueoEspecial
{
    // Dónde está el cartel de NORMAL dentro de la escena del menú.
    private const string EscenaMenu = "PantallasUI";
    private const string NombrePanelTutorial = "TutorialPanel";
    private const string NombrePanelDificultades = "Image_DIFICULTADES";
    private const string NombreCartelNormal = "normal";
    private const string NombreContenidoNormal = "CONT1";

    private const string NombreCartel = "DesbloqueoEspecial";

    // Medidas del cartel nuevo (en unidades del Canvas, 1920 x 1080).
    private const float Separacion = 10f;   // espacio entre el cartel de NORMAL y este
    private const float Alto = 185f;
    private const float Margen = 18f;
    private const float AltoTitulo = 36f;
    private const float TamanoTitulo = 26f;
    private const float TamanoTextoMaximo = 21f;
    private const float TamanoTextoMinimo = 13f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AlIniciar()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;

        for (int i = 0; i < SceneManager.sceneCount; i++)
            Preparar(SceneManager.GetSceneAt(i));
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        Preparar(escena);
    }

    private static void Preparar(Scene escena)
    {
        if (!escena.IsValid() || !escena.isLoaded || escena.name != EscenaMenu)
            return;

        Transform panelDificultades = BuscarPanelDificultades(escena);
        if (panelDificultades == null)
            return;

        Transform cartelNormal = panelDificultades.Find(NombreCartelNormal);
        if (cartelNormal == null || cartelNormal.Find(NombreCartel) != null)
            return;

        Transform contenido = cartelNormal.Find(NombreContenidoNormal);
        if (contenido == null && cartelNormal.childCount > 0)
            contenido = cartelNormal.GetChild(0);

        RectTransform rectContenido = contenido as RectTransform;
        if (rectContenido == null)
            return;

        Crear(cartelNormal, rectContenido);
    }

    private static Transform BuscarPanelDificultades(Scene escena)
    {
        GameObject[] raices = escena.GetRootGameObjects();

        for (int i = 0; i < raices.Length; i++)
        {
            Transform[] todos = raices[i].GetComponentsInChildren<Transform>(true);

            for (int j = 0; j < todos.Length; j++)
            {
                Transform t = todos[j];

                if (t.name == NombrePanelDificultades &&
                    t.parent != null &&
                    t.parent.name == NombrePanelTutorial)
                {
                    return t;
                }
            }
        }

        return null;
    }

    // =========================================================
    // ARMADO DEL CARTEL
    // =========================================================

    private static void Crear(Transform cartelNormal, RectTransform rectContenido)
    {
        // Estilo del cartel de NORMAL: el primer texto es el título ("NORMAL"); el cuerpo usa otra fuente.
        TMP_Text[] textos = rectContenido.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text plantillaTitulo = textos.Length > 0 ? textos[0] : null;
        TMP_Text plantillaCuerpo = plantillaTitulo;

        for (int i = 0; i < textos.Length; i++)
        {
            if (plantillaTitulo != null && textos[i].font != plantillaTitulo.font)
            {
                plantillaCuerpo = textos[i];
                break;
            }
        }

        Image fondoNormal = rectContenido.GetComponent<Image>();
        Color colorFondo = fondoNormal != null ? fondoNormal.color : new Color(0.046f, 0.147f, 0.046f, 0.94f);
        Color colorTitulo = plantillaTitulo != null ? plantillaTitulo.color : new Color(0.77f, 0.83f, 0.77f, 1f);
        Color colorLinea = new Color(0.188f, 1f, 0.237f, 0.867f);

        Image[] imagenes = rectContenido.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < imagenes.Length; i++)
        {
            if (imagenes[i] != fondoNormal && imagenes[i].name.StartsWith("Linea"))
            {
                colorLinea = imagenes[i].color;
                break;
            }
        }

        int capa = rectContenido.gameObject.layer;

        // ---------- Recuadro: mismo ancho que el de NORMAL, pegado debajo ----------
        GameObject cartel = new GameObject(NombreCartel, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cartel.layer = capa;
        cartel.transform.SetParent(cartelNormal, false);

        Vector2 tamanoContenido = rectContenido.rect.size;
        float centroX = rectContenido.anchoredPosition.x + (0.5f - rectContenido.pivot.x) * tamanoContenido.x;
        float bordeInferior = rectContenido.anchoredPosition.y - rectContenido.pivot.y * tamanoContenido.y;

        RectTransform rect = (RectTransform)cartel.transform;
        rect.anchorMin = rectContenido.anchorMin;
        rect.anchorMax = rectContenido.anchorMax;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(centroX, bordeInferior - Separacion);
        rect.sizeDelta = new Vector2(tamanoContenido.x, Alto);

        Image fondo = cartel.GetComponent<Image>();
        fondo.color = colorFondo;
        fondo.raycastTarget = false;

        // ---------- Título ----------
        TMP_Text titulo = CrearTexto("Titulo", rect, plantillaTitulo, capa);
        titulo.text = "DESBLOQUEO ESPECIAL";
        titulo.color = colorTitulo;
        titulo.fontSize = TamanoTitulo;
        titulo.alignment = TextAlignmentOptions.Left;
        titulo.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform rectTitulo = titulo.rectTransform;
        rectTitulo.anchorMin = new Vector2(0f, 1f);
        rectTitulo.anchorMax = new Vector2(1f, 1f);
        rectTitulo.pivot = new Vector2(0.5f, 1f);
        rectTitulo.anchoredPosition = new Vector2(0f, -8f);
        rectTitulo.sizeDelta = new Vector2(-2f * Margen, AltoTitulo);

        // ---------- Línea debajo del título ----------
        GameObject linea = new GameObject("Linea", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        linea.layer = capa;
        linea.transform.SetParent(rect, false);

        RectTransform rectLinea = (RectTransform)linea.transform;
        rectLinea.anchorMin = new Vector2(0f, 1f);
        rectLinea.anchorMax = new Vector2(1f, 1f);
        rectLinea.pivot = new Vector2(0.5f, 1f);
        rectLinea.anchoredPosition = new Vector2(0f, -(8f + AltoTitulo));
        rectLinea.sizeDelta = new Vector2(-2f * Margen, 2f);

        Image imagenLinea = linea.GetComponent<Image>();
        imagenLinea.color = colorLinea;
        imagenLinea.raycastTarget = false;

        // ---------- Texto ----------
        TMP_Text cuerpo = CrearTexto("Texto", rect, plantillaCuerpo, capa);
        cuerpo.richText = true;
        cuerpo.color = Color.white;
        cuerpo.alignment = TextAlignmentOptions.TopLeft;
        cuerpo.textWrappingMode = TextWrappingModes.Normal;
        cuerpo.enableAutoSizing = true;
        cuerpo.fontSizeMin = TamanoTextoMinimo;
        cuerpo.fontSizeMax = TamanoTextoMaximo;
        cuerpo.text = ComponerTexto(colorTitulo);

        RectTransform rectCuerpo = cuerpo.rectTransform;
        rectCuerpo.anchorMin = Vector2.zero;
        rectCuerpo.anchorMax = Vector2.one;
        rectCuerpo.pivot = new Vector2(0.5f, 0.5f);
        rectCuerpo.offsetMin = new Vector2(Margen, 8f);
        rectCuerpo.offsetMax = new Vector2(-Margen, -(8f + AltoTitulo + 8f));
    }

    private static TMP_Text CrearTexto(string nombre, Transform padre, TMP_Text plantilla, int capa)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = capa;
        go.transform.SetParent(padre, false);

        TextMeshProUGUI texto = go.AddComponent<TextMeshProUGUI>();

        if (plantilla != null && plantilla.font != null)
        {
            texto.font = plantilla.font;
            texto.fontSharedMaterial = plantilla.fontSharedMaterial;
        }

        texto.raycastTarget = false;
        return texto;
    }

    // =========================================================
    // TEXTO (con los valores actuales de DesbloqueoDificultades)
    // =========================================================

    private static string ComponerTexto(Color colorModo)
    {
        string color = "#" + ColorUtility.ToHtmlStringRGB(colorModo);
        StringBuilder texto = new StringBuilder();

        texto.Append("Si todavía no tenés ninguna dificultad desbloqueada de un modo, ");
        texto.Append("podés desbloquear Difícil y Pesadilla juntas en una sola partida en Normal:");

        AgregarModo(texto, color, "HISTORIA", Modo.Historia);
        AgregarModo(texto, color, "RONDA 5", Modo.Ronda5);
        AgregarModo(texto, color, "SUPERVIVENCIA", Modo.Supervivencia);

        return texto.ToString();
    }

    private static void AgregarModo(StringBuilder texto, string color, string nombre, Modo modo)
    {
        texto.Append("\n<b><color=").Append(color).Append(">").Append(nombre).Append(":</color></b> ");
        texto.Append(FraseCondicionEspecial(modo));
    }

    // Ej: "completar las 5 rondas con 100.000 puntos o más y sin caídas."
    private static string FraseCondicionEspecial(Modo modo)
    {
        DesbloqueoDificultades.Requisito requisito = DesbloqueoDificultades.ObtenerRequisitoEspecial(modo);
        string frase;

        switch (modo)
        {
            case Modo.Historia:
                frase = "completar las misiones";
                break;

            case Modo.Ronda5:
                frase = "completar las 5 rondas";
                break;

            default:
                frase = "superar la ronda " + requisito.rondaASuperar;
                break;
        }

        bool pidePuntaje = requisito.puntajeMinimo > 0;
        bool pideCaidas = requisito.caidasMenorQue >= 0;

        if (pidePuntaje)
            frase += " con " + DesbloqueoDificultades.FormatearPuntaje(requisito.puntajeMinimo) + " puntos o más";

        if (pideCaidas)
        {
            if (requisito.caidasMenorQue <= 1)
                frase += pidePuntaje ? " y sin caídas" : " sin caídas";
            else
                frase += (pidePuntaje ? " y menos de " : " con menos de ") + requisito.caidasMenorQue + " caídas";
        }

        return frase + ".";
    }
}
