using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ButtonHoverEffect : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textoTMP;

    // Colores configurables desde el Inspector
    public Color colorNormal = new Color32(120, 145, 165, 255); // Gris azulado
    public Color colorHover = new Color32(0, 255, 102, 255);     // Verde neón

    private void Awake()
    {
        if (textoTMP == null)
            textoTMP = GetComponentInChildren<TextMeshProUGUI>();

        RestaurarColor();
    }

    public void OnMouseEnterButton()
    {
        if (textoTMP != null)
            textoTMP.color = colorHover;
    }

    public void OnMouseExitButton()
    {
        RestaurarColor();
    }

    private void RestaurarColor()
    {
        if (textoTMP != null)
            textoTMP.color = colorNormal;
    }
}