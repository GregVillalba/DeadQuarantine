using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ButtonHoverMenuPrincipal : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private TextMeshProUGUI textoTMP;

    // Colores configurables desde el Inspector
    public Color colorNormal = new Color32(120, 145, 165, 255); // Gris azulado
    public Color colorHover = new Color32(0, 255, 102, 255);     // Verde neón

    private void Awake()
    {
        if (textoTMP == null)
            textoTMP = GetComponentInChildren<TextMeshProUGUI>();
    }

    // Se ejecuta CADA VEZ que el menú o el botón vuelve a activarse
    private void OnEnable()
    {
        RestaurarColor();
    }

    // Se ejecuta en cuanto el menú se oculta al abrir otro panel
    private void OnDisable()
    {
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

    public void OnPointerClick(PointerEventData eventData)
    {
        // Quita la selección activa del EventSystem para que no quede trabado
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        RestaurarColor();
    }

    private void RestaurarColor()
    {
        if (textoTMP != null)
            textoTMP.color = colorNormal;
    }
}