using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ButtonHoverMenuPrincipal : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Referencias")]
    [SerializeField] private TextMeshProUGUI textoTMP;
    [SerializeField] private RectTransform targetRect;

    [Header("Colores")]
    public Color colorNormal = new Color32(120, 145, 165, 255);
    public Color colorHover = new Color32(0, 255, 102, 255);
    public Color colorPressed = new Color32(0, 200, 80, 255);

    [Header("Profundidad / Animaci�n")]
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float clickScale = 0.94f;
    [SerializeField] private float pressYOffset = -4f; // Sensaci�n f�sica de pulsar hacia abajo
    [SerializeField] private float transitionSpeed = 14f;

    private Vector3 initialScale;
    private Vector2 initialAnchoredPosition;
    private Coroutine currentAnimation;

    private void Awake()
    {
        if (textoTMP == null)
            textoTMP = GetComponentInChildren<TextMeshProUGUI>();

        if (targetRect == null)
            targetRect = GetComponent<RectTransform>();

        initialScale = targetRect.localScale;
        initialAnchoredPosition = targetRect.anchoredPosition;
    }

    private void OnEnable()
    {
        RestaurarEstadoInmediato();
    }

    private void OnDisable()
    {
        RestaurarEstadoInmediato();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (textoTMP != null)
            textoTMP.color = colorHover;

        // Eleva y agranda ligeramente al pasar el mouse
        AnimateButton(initialScale * hoverScale, initialAnchoredPosition);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        RestaurarAnimado();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (textoTMP != null)
            textoTMP.color = colorPressed;

        // Se comprime y baja unos p�xeles dando efecto de click profundo
        Vector2 pressedPos = initialAnchoredPosition + new Vector2(0f, pressYOffset);
        AnimateButton(initialScale * clickScale, pressedPos);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Si el puntero sigue arriba al soltar, vuelve al estado Hover
        if (eventData.hovered.Contains(gameObject))
        {
            if (textoTMP != null)
                textoTMP.color = colorHover;

            AnimateButton(initialScale * hoverScale, initialAnchoredPosition);
        }
        else
        {
            RestaurarAnimado();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void RestaurarAnimado()
    {
        if (textoTMP != null)
            textoTMP.color = colorNormal;

        AnimateButton(initialScale, initialAnchoredPosition);
    }

    private void RestaurarEstadoInmediato()
    {
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        if (targetRect != null)
        {
            targetRect.localScale = initialScale;
            targetRect.anchoredPosition = initialAnchoredPosition;
        }

        if (textoTMP != null)
            textoTMP.color = colorNormal;
    }

    private void AnimateButton(Vector3 targetScale, Vector2 targetPos)
    {
        if (!gameObject.activeInHierarchy) return;

        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        currentAnimation = StartCoroutine(AnimateRoutine(targetScale, targetPos));
    }

    private IEnumerator AnimateRoutine(Vector3 targetScale, Vector2 targetPos)
    {
        while (Vector3.Distance(targetRect.localScale, targetScale) > 0.002f ||
               Vector2.Distance(targetRect.anchoredPosition, targetPos) > 0.05f)
        {
            targetRect.localScale = Vector3.Lerp(targetRect.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
            targetRect.anchoredPosition = Vector2.Lerp(targetRect.anchoredPosition, targetPos, Time.unscaledDeltaTime * transitionSpeed);
            yield return null;
        }

        targetRect.localScale = targetScale;
        targetRect.anchoredPosition = targetPos;
    }
}