using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TextMeshProUGUI))]
public class LinkOpener : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
{
    [Header("Color Hover (Código Hexadecimal)")]
    [Tooltip("Color del texto y subrayado al pasar el mouse por encima")]
    [SerializeField] private string hexHoverColor = "#58A6FF"; // Azul claro / celeste

    private TextMeshProUGUI textMeshPro;
    private Camera uiCamera;
    private string originalText;
    private int currentLinkIndex = -1;
    private bool isHovering = false;

    private void Awake()
    {
        textMeshPro = GetComponent<TextMeshProUGUI>();
        originalText = textMeshPro.text; // Guarda el formato original configurado en el Inspector

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(textMeshPro, eventData.position, uiCamera);

        if (linkIndex != currentLinkIndex)
        {
            currentLinkIndex = linkIndex;

            if (currentLinkIndex != -1)
            {
                OnLinkEnter(currentLinkIndex);
            }
            else
            {
                OnLinkExit();
            }
        }
    }

    private void OnLinkEnter(int linkIndex)
    {
        if (isHovering) return;
        isHovering = true;

        TMP_LinkInfo linkInfo = textMeshPro.textInfo.linkInfo[linkIndex];
        string linkText = linkInfo.GetLinkText();

        // Envuelve únicamente el nombre con el color y el tag de subrayado <u>
        string styledText = $"<color={hexHoverColor}><u>{linkText}</u></color>";

        // Aplica el reemplazo visual
        textMeshPro.text = originalText.Replace(linkText, styledText);
    }

    private void OnLinkExit()
    {
        if (!isHovering) return;
        isHovering = false;

        // Restaura el texto sin subrayado ni cambio de color
        textMeshPro.text = originalText;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        currentLinkIndex = -1;
        OnLinkExit();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(textMeshPro, eventData.position, uiCamera);

        if (linkIndex != -1)
        {
            TMP_LinkInfo linkInfo = textMeshPro.textInfo.linkInfo[linkIndex];
            string url = linkInfo.GetLinkID();

            if (!string.IsNullOrEmpty(url))
            {
                Application.OpenURL(url);
            }
        }
    }

    private void OnDisable()
    {
        OnLinkExit();
    }
}