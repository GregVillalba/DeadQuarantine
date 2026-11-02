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

    public void Mostrar(PlayerScore score)
    {
        if (score != null)
        {
            if (textoPuntaje != null)
                textoPuntaje.text = score.ScoreNetwork.Value.ToString();

            if (textoZombies != null)
                textoZombies.text =
                    score.ZombiesEliminadosNetwork.Value.ToString();

            if (textoPrecision != null)
                textoPrecision.text =
                    score.PrecisionPorcentaje.ToString() + "%";

            if (textoCaidas != null)
                textoCaidas.text = score.CaidasNetwork.Value.ToString();

            if (textoReapariciones != null)
                textoReapariciones.text =
                    score.ReaparicionesNetwork.Value.ToString();
        }

        ActualizarComponentes();
        ActualizarRonda();
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