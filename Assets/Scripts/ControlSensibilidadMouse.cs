using UnityEngine;
using UnityEngine.UI;

public class ControlSensibilidadMouse : MonoBehaviour
{
    [SerializeField] private Slider sliderSensibilidad;

    private void OnEnable()
    {
        if (sliderSensibilidad != null)
            sliderSensibilidad.SetValueWithoutNotify(ConfiguracionesJuego.ObtenerSensibilidad());
    }

    // Enganchar al OnValueChanged (float) del Slider
    public void CambiarSensibilidad(float valor)
    {
        ConfiguracionesJuego.GuardarSensibilidad(valor);
    }
}