using UnityEngine;
using UnityEngine.UI;

public class ControlVolumen : MonoBehaviour
{
    [SerializeField] private Slider sliderVolumen;

    private void OnEnable()
    {
        if (sliderVolumen != null)
            sliderVolumen.SetValueWithoutNotify(ConfiguracionesJuego.ObtenerVolumen());
    }

    // Enganchar al OnValueChanged (float) del Slider
    public void CambiarVolumen(float valor)
    {
        ConfiguracionesJuego.GuardarVolumen(valor);
    }
}