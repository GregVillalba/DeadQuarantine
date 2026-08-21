using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ButtonHoverMenuPrincipal : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (textoTMP != null)
            textoTMP.color = colorHover;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        RestaurarColor();
    }

    private void RestaurarColor()
    {
        if (textoTMP != null)
            textoTMP.color = colorNormal;
    }
}