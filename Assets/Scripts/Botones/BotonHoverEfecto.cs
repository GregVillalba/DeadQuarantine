using UnityEngine;
using UnityEngine.EventSystems;

public class BotonHoverEfecto : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Objeto que se muestra al pasar el mouse por encima")]
    [SerializeField] private GameObject objetoHover;

    private void Start()
    {
        if (objetoHover != null)
            objetoHover.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (objetoHover != null)
            objetoHover.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (objetoHover != null)
            objetoHover.SetActive(false);
    }
}
