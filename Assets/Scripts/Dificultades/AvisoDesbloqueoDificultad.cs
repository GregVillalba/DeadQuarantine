using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Dificultad = ConfiguracionPartidaSeleccionada.Dificultad;
using Modo = ConfiguracionPartidaSeleccionada.ModoJuego;

/// <summary>
/// Aviso al jugador local de que desbloqueó una dificultad.
/// - Al ganar la partida lo muestra la pantalla de resultado (PantallaFinPartida lee DetalleDePartida
///   y lo escribe debajo de "Desempeño global de combate").
/// - A mitad de partida (Supervivencia) se muestra un cartel que se arma solo en runtime.
/// </summary>
public class AvisoDesbloqueoDificultad : MonoBehaviour
{
    private const float DuracionVisible = 6f;
    private const float DuracionFade = 0.4f;

    private static AvisoDesbloqueoDificultad actual;

    // =========================================================
    // DESBLOQUEOS DE LA PARTIDA EN CURSO
    // =========================================================

    /// <summary>
    /// Lo desbloqueado en esta partida, listo para mostrar. Ej: "RONDA 5  -  DIFICIL Y PESADILLA".
    /// Vacío si no se desbloqueó nada.
    /// </summary>
    public static string DetalleDePartida { get; private set; } = string.Empty;

    /// <summary>Se dispara cuando cambia DetalleDePartida.</summary>
    public static event Action AlRegistrar;

    // Dificultad máxima que tenía el jugador antes del primer desbloqueo de esta partida.
    private static Dificultad desdeDePartida = Dificultad.Ninguna;

    /// <summary>Lo llama RoundManager al empezar cada partida.</summary>
    public static void ReiniciarPartida()
    {
        DetalleDePartida = string.Empty;
        desdeDePartida = Dificultad.Ninguna;
        AlRegistrar?.Invoke();
    }

    /// <summary>
    /// Anota un desbloqueo de esta partida. Con mostrarCartel además muestra el cartel en pantalla
    /// (para cuando no hay pantalla de resultado donde escribirlo).
    /// </summary>
    public static void Registrar(Modo modo, Dificultad anterior, Dificultad nueva, bool mostrarCartel)
    {
        if (desdeDePartida == Dificultad.Ninguna)
            desdeDePartida = anterior;

        DetalleDePartida = Componer(modo, desdeDePartida, nueva);
        AlRegistrar?.Invoke();

        if (mostrarCartel)
            Mostrar(Componer(modo, anterior, nueva));
    }

    private static string Componer(Modo modo, Dificultad desde, Dificultad hasta)
    {
        // De Normal a Pesadilla de una vez: se desbloquearon las dos.
        string dificultades =
            desde <= Dificultad.Normal && hasta == Dificultad.Pesadilla
                ? "DIFICIL Y PESADILLA"
                : DesbloqueoDificultades.NombreDificultad(hasta);

        return DesbloqueoDificultades.NombreModo(modo) + "  -  " + dificultades;
    }

    // =========================================================
    // CARTEL EN PANTALLA
    // =========================================================

    private CanvasGroup grupo;
    private TMP_Text textoDetalle;
    private Coroutine rutina;

    /// <summary>Muestra "DIFICULTAD DESBLOQUEADA" con el detalle (modo y dificultades).</summary>
    public static void Mostrar(string detalle)
    {
        if (actual == null)
            actual = Crear();

        actual.MostrarInterno(detalle);
    }

    private void MostrarInterno(string detalle)
    {
        textoDetalle.text = detalle;

        if (rutina != null)
            StopCoroutine(rutina);

        rutina = StartCoroutine(Rutina());
    }

    private IEnumerator Rutina()
    {
        for (float t = 0f; t < DuracionFade; t += Time.unscaledDeltaTime)
        {
            grupo.alpha = Mathf.Clamp01(t / DuracionFade);
            yield return null;
        }

        grupo.alpha = 1f;

        yield return new WaitForSecondsRealtime(DuracionVisible);

        for (float t = 0f; t < DuracionFade; t += Time.unscaledDeltaTime)
        {
            grupo.alpha = 1f - Mathf.Clamp01(t / DuracionFade);
            yield return null;
        }

        grupo.alpha = 0f;
        rutina = null;
    }

    private void OnDestroy()
    {
        if (actual == this)
            actual = null;
    }

    private static AvisoDesbloqueoDificultad Crear()
    {
        GameObject raiz = new GameObject(
            "AvisoDesbloqueoDificultad",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup));

        Canvas canvas = raiz.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000; // por encima del HUD y de las pantallas de fin de partida

        CanvasScaler scaler = raiz.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        AvisoDesbloqueoDificultad aviso = raiz.AddComponent<AvisoDesbloqueoDificultad>();
        aviso.grupo = raiz.GetComponent<CanvasGroup>();
        aviso.grupo.alpha = 0f;
        aviso.grupo.interactable = false;
        aviso.grupo.blocksRaycasts = false;

        // Panel arriba al centro.
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(raiz.transform, false);

        RectTransform rectPanel = (RectTransform)panel.transform;
        rectPanel.anchorMin = rectPanel.anchorMax = new Vector2(0.5f, 1f);
        rectPanel.pivot = new Vector2(0.5f, 1f);
        rectPanel.anchoredPosition = new Vector2(0f, -120f);
        rectPanel.sizeDelta = new Vector2(820f, 120f);

        Image fondo = panel.GetComponent<Image>();
        fondo.color = new Color(0.04f, 0.07f, 0.11f, 0.94f);
        fondo.raycastTarget = false;

        GameObject linea = new GameObject("Linea", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        linea.transform.SetParent(panel.transform, false);

        RectTransform rectLinea = (RectTransform)linea.transform;
        rectLinea.anchorMin = new Vector2(0f, 1f);
        rectLinea.anchorMax = new Vector2(1f, 1f);
        rectLinea.pivot = new Vector2(0.5f, 1f);
        rectLinea.anchoredPosition = Vector2.zero;
        rectLinea.sizeDelta = new Vector2(0f, 4f);

        Image colorLinea = linea.GetComponent<Image>();
        colorLinea.color = new Color(0f, 1f, 0.4f, 1f);
        colorLinea.raycastTarget = false;

        CrearTexto(panel.transform, "Titulo", "DIFICULTAD DESBLOQUEADA", 34f,
            new Color(0f, 1f, 0.4f, 1f), new Vector2(0f, -16f), 44f);

        aviso.textoDetalle = CrearTexto(panel.transform, "Detalle", "", 28f,
            Color.white, new Vector2(0f, -64f), 40f);

        return aviso;
    }

    private static TMP_Text CrearTexto(
        Transform padre,
        string nombre,
        string contenido,
        float tamano,
        Color color,
        Vector2 posicion,
        float alto)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = posicion;
        rect.sizeDelta = new Vector2(-40f, alto);

        TextMeshProUGUI texto = go.AddComponent<TextMeshProUGUI>();
        texto.text = contenido;
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = color;
        texto.fontStyle = FontStyles.Bold;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 14f;
        texto.fontSizeMax = tamano;
        texto.raycastTarget = false;

        return texto;
    }
}
