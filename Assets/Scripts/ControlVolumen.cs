using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ControlVolumen : MonoBehaviour
{
    [SerializeField] private Slider sliderVolumen;
    [SerializeField] private TextMeshProUGUI textoVolumen;

    private void OnEnable()
    {
        float volumenGuardado = ConfiguracionesJuego.ObtenerVolumen();

        if (sliderVolumen != null)
            sliderVolumen.SetValueWithoutNotify(volumenGuardado);

        // Actualiza el texto al valor guardado cuando se abre el panel
        ActualizarTexto(volumenGuardado);
    }

    // Enganchar al OnValueChanged (float) del Slider
    public void CambiarVolumen(float valor)
    {
        ConfiguracionesJuego.GuardarVolumen(valor);
        ActualizarTexto(valor);
    }

    private void ActualizarTexto(float valor)
    {
        if (textoVolumen != null)
        {
            // Muestra de 0% a 100%
            textoVolumen.text = Mathf.RoundToInt(valor * 100f) + "%";
        }
    }
}