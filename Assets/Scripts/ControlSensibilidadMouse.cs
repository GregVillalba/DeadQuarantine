using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ControlSensibilidadMouse : MonoBehaviour
{
    [SerializeField] private Slider sliderSensibilidad;
    [SerializeField] private TextMeshProUGUI textoSensibilidad;

    private void OnEnable()
    {
        float sensibilidadGuardada = ConfiguracionesJuego.ObtenerSensibilidad();

        if (sliderSensibilidad != null)
            sliderSensibilidad.SetValueWithoutNotify(sensibilidadGuardada);

        // Actualiza el texto al valor guardado cuando se abre el panel
        ActualizarTexto(sensibilidadGuardada);
    }

    // Enganchar al OnValueChanged (float) del Slider
    public void CambiarSensibilidad(float valor)
    {
        ConfiguracionesJuego.GuardarSensibilidad(valor);
        ActualizarTexto(valor);
    }

    private void ActualizarTexto(float valor)
    {
        if (textoSensibilidad != null)
        {
            // Muestra el número con 1 decimal (ejemplo: 1.5, 2.0). Si querés entero cambiá "F1" por "F0"
            textoSensibilidad.text = valor.ToString("F1");
        }
    }
}