using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TextMeshProUGUI))]
public class LinkOpener : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
{
    [Header("Efecto Visual")]
    [SerializeField] private Color hoverColor = new Color(0.35f, 0.65f, 1f, 1f);

    [Header("Mensaje Hover")]
    [Tooltip("Arrastrá acá el GameObject del texto o cartel que dice 'Ir al perfil de LinkedIn'")]
    [SerializeField] private GameObject cartelHover;

    private TextMeshProUGUI textMeshPro;
    private Camera uiCamera;
    private int currentLinkIndex = -1;
    private bool isHovering = false;

    private void Awake()
    {
        textMeshPro = GetComponent<TextMeshProUGUI>();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }

        if (cartelHover != null)
        {
            cartelHover.SetActive(false);
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
        isHovering = true;
        SetLinkColor(linkIndex, hoverColor);

        if (cartelHover != null)
        {
            cartelHover.SetActive(true);
        }
    }

    private void OnLinkExit()
    {
        if (!isHovering) return;
        isHovering = false;

        if (cartelHover != null)
        {
            cartelHover.SetActive(false);
        }

        textMeshPro.ForceMeshUpdate();
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

    private void SetLinkColor(int linkIndex, Color32 color)
    {
        TMP_LinkInfo linkInfo = textMeshPro.textInfo.linkInfo[linkIndex];

        for (int i = 0; i < linkInfo.linkTextLength; i++)
        {
            int characterIndex = linkInfo.linkTextfirstCharacterIndex + i;
            TMP_CharacterInfo charInfo = textMeshPro.textInfo.characterInfo[characterIndex];

            if (!charInfo.isVisible) continue;

            int meshIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            Color32[] vertexColors = textMeshPro.textInfo.meshInfo[meshIndex].colors32;

            vertexColors[vertexIndex + 0] = color;
            vertexColors[vertexIndex + 1] = color;
            vertexColors[vertexIndex + 2] = color;
            vertexColors[vertexIndex + 3] = color;
        }

        textMeshPro.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    private void OnDisable()
    {
        OnLinkExit();
    }
}