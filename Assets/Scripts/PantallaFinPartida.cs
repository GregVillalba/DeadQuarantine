using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Se coloca en la raíz de cada pantalla de victoria o derrota.
/// Completa sus propios textos con las estadísticas del jugador.
/// Todos los campos son opcionales: si un texto no está asignado, se ignora.
/// </summary>
public class PantallaFinPartida : MonoBehaviour
{
    [Header("Estadísticas")]
    [SerializeField] private TextMeshProUGUI textoPuntaje;
    [SerializeField] private TextMeshProUGUI textoPuntajeFinal;
    [SerializeField] private TextMeshProUGUI textoZombies;
    [SerializeField] private TextMeshProUGUI textoPrecision;
    [SerializeField] private TextMeshProUGUI textoCaidas;
    [SerializeField] private TextMeshProUGUI textoReapariciones;

    [Header("Componentes según el modo (solo pantallas de derrota)")]
    [Tooltip("Se muestra en modo Historia.")]
    [SerializeField] private GameObject componenteHistoria;

    [Tooltip("Se muestra en modo 5 Rondas y Supervivencia.")]
    [SerializeField] private GameObject componenteRondas;

    [Header("Ronda alcanzada (dentro del componente de rondas)")]
    [Tooltip("Muestra 'ronda / máximo' (modo 5 Rondas). Ej: 1 / 5")]
    [SerializeField] private TextMeshProUGUI textoRondaRondas;

    [Tooltip("Muestra solo la ronda (modo Supervivencia). Ej: 1")]
    [SerializeField] private TextMeshProUGUI textoRondaSupervivencia;

    [Header("Botón")]
    [SerializeField] private Button botonVolver;

    // Aviso "DIFICULTAD DESBLOQUEADA: ...". Se crea en runtime debajo del texto
    // "DESEMPEÑO GLOBAL DE COMBATE" (solo lo tienen las pantallas de victoria).
    private const string InicioTextoDesempeno = "DESEMPE";
    private const float SeparacionAvisoDesbloqueo = 8f;
    private TMP_Text textoDesbloqueo;
    private bool textoDesbloqueoBuscado;

    /// <summary>
    /// Botón "Volver al menú" de esta pantalla.
    /// Si no se asignó en el Inspector, usa el primer botón hijo.
    /// </summary>
    public Button BotonVolver
    {
        get
        {
            if (botonVolver == null)
                botonVolver = GetComponentInChildren<Button>(true);

            return botonVolver;
        }
    }

    private void OnEnable()
    {
        AvisoDesbloqueoDificultad.AlRegistrar += ActualizarAvisoDesbloqueo;
    }

    private void OnDisable()
    {
        AvisoDesbloqueoDificultad.AlRegistrar -= ActualizarAvisoDesbloqueo;
    }

    public void Mostrar(PlayerScore score)
    {
        PrepararTextosDeEstadisticas();

        if (score != null)
        {
            // Puntaje con las caídas ya descontadas (500 por cada una).
          /*  if (textoPuntaje != null)
                textoPuntaje.text = score.PuntajeFinal.ToString();*/
            // Puntaje ganado, sin descuento de caídas.
            if (textoPuntaje != null)
                textoPuntaje.text = score.ScoreNetwork.Value.ToString();

            // Puntaje con las caídas ya descontadas (500 por cada una).
            if (textoPuntajeFinal != null)
                textoPuntajeFinal.text = score.PuntajeFinal.ToString();

            if (textoZombies != null)
                textoZombies.text =
                    score.ZombiesEliminadosNetwork.Value.ToString();

            if (textoPrecision != null)
                textoPrecision.text =
                    score.PrecisionPorcentaje.ToString() + "%";

            // Al lado de las caídas, lo que restaron del puntaje. Ej: "2  (-1000)".
            if (textoCaidas != null)
            {
                int caidas = score.CaidasNetwork.Value;

                textoCaidas.text = caidas > 0
                    ? caidas + "  (-" + score.PenalizacionCaidas + ")"
                    : "0";
            }

            if (textoReapariciones != null)
                textoReapariciones.text =
                    score.ReaparicionesNetwork.Value.ToString();
        }

        ActualizarComponentes();
        ActualizarRonda();
        ActualizarAvisoDesbloqueo();
    }

    // Los valores vienen del diseño con un ancho fijo muy chico: con más de un par de caracteres
    // ("95%", "2  (-1000)") el texto bajaba al renglón siguiente. Se dejan en un solo renglón,
    // y la precisión se alinea con la columna de los demás valores.
    private void PrepararTextosDeEstadisticas()
    {
        EnUnRenglon(textoPuntaje);
        EnUnRenglon(textoPuntajeFinal);
        EnUnRenglon(textoZombies);
        EnUnRenglon(textoPrecision);
        EnUnRenglon(textoCaidas);
        EnUnRenglon(textoReapariciones);

        AlinearEnColumna(textoPrecision, textoZombies != null ? textoZombies : textoPuntaje);
    }

    private static void EnUnRenglon(TMP_Text texto)
    {
        if (texto == null)
            return;

        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.overflowMode = TextOverflowModes.Overflow;
    }

    // Pone el texto en la misma X que la referencia (cada valor está en su propia fila, con el mismo armado).
    private static void AlinearEnColumna(TMP_Text texto, TMP_Text referencia)
    {
        if (texto == null || referencia == null || texto == referencia)
            return;

        RectTransform rect = texto.rectTransform;
        RectTransform rectReferencia = referencia.rectTransform;

        // Solo si están armados igual; si no, comparar la X no significa nada.
        if (rect.anchorMin != rectReferencia.anchorMin ||
            rect.anchorMax != rectReferencia.anchorMax ||
            !Mathf.Approximately(rect.pivot.x, rectReferencia.pivot.x))
        {
            return;
        }

        Vector2 posicion = rect.anchoredPosition;
        posicion.x = rectReferencia.anchoredPosition.x;
        rect.anchoredPosition = posicion;
    }

    // =========================================================
    // AVISO DE DIFICULTAD DESBLOQUEADA
    // =========================================================

    private void ActualizarAvisoDesbloqueo()
    {
        string detalle = AvisoDesbloqueoDificultad.DetalleDePartida;

        if (string.IsNullOrEmpty(detalle))
        {
            if (textoDesbloqueo != null)
                textoDesbloqueo.gameObject.SetActive(false);

            return;
        }

        if (textoDesbloqueo == null && !textoDesbloqueoBuscado)
        {
            textoDesbloqueoBuscado = true;
            textoDesbloqueo = CrearTextoDesbloqueo();
        }

        if (textoDesbloqueo == null)
            return;

        textoDesbloqueo.text = "DIFICULTAD DESBLOQUEADA: " + detalle;
        textoDesbloqueo.gameObject.SetActive(true);
    }

    // Copia el texto "DESEMPEÑO GLOBAL DE COMBATE..." (misma fuente, tamaño y color) y la pone justo debajo.
    private TMP_Text CrearTextoDesbloqueo()
    {
        TMP_Text referencia = null;
        TMP_Text[] textos = GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < textos.Length; i++)
        {
            if (textos[i] != null &&
                textos[i].text != null &&
                textos[i].text.TrimStart().StartsWith(InicioTextoDesempeno, System.StringComparison.OrdinalIgnoreCase))
            {
                referencia = textos[i];
                break;
            }
        }

        if (referencia == null)
            return null;

        GameObject copia = Instantiate(referencia.gameObject, referencia.transform.parent);
        copia.name = "AvisoDesbloqueoDificultad";

        RectTransform rect = (RectTransform)copia.transform;
        RectTransform rectReferencia = referencia.rectTransform;

        rect.anchoredPosition =
            rectReferencia.anchoredPosition +
            new Vector2(0f, -(rectReferencia.rect.height + SeparacionAvisoDesbloqueo));

        TMP_Text texto = copia.GetComponent<TMP_Text>();
        EnUnRenglon(texto);
        texto.raycastTarget = false;

        return texto;
    }

    private void ActualizarComponentes()
    {
        bool historia = RoundManager.Instance != null &&
                        RoundManager.Instance.EsModoHistoria;

        if (componenteHistoria != null)
            componenteHistoria.SetActive(historia);

        if (componenteRondas != null)
            componenteRondas.SetActive(!historia);
    }

    private void ActualizarRonda()
    {
        RoundManager rm = RoundManager.Instance;

        bool infinito = rm != null && rm.IsInfiniteMode;
        int rondaActual = rm != null ? rm.CurrentRound : 1;

        // Modo 5 Rondas: "1 / 5".
        if (textoRondaRondas != null)
        {
            textoRondaRondas.gameObject.SetActive(!infinito);

            if (!infinito)
            {
                textoRondaRondas.text = rm != null
                    ? rondaActual + " / " + rm.MaxRounds
                    : "1";
            }
        }

        // Modo Supervivencia: solo la ronda.
        if (textoRondaSupervivencia != null)
        {
            textoRondaSupervivencia.gameObject.SetActive(infinito);

            if (infinito)
                textoRondaSupervivencia.text = rondaActual.ToString();
        }
    }
}
