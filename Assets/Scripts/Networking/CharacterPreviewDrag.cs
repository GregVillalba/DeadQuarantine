using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Va en una Image transparente (Raycast Target activo) que cubre la zona del modelo de UNA columna.
/// Arrastrar gira el modelo de esa columna. LobbyPlayerSlot la activa solo para el jugador local.
/// </summary>
public class CharacterPreviewDrag : MonoBehaviour, IDragHandler
{
    [SerializeField] private CharacterPreviewStage stage;
    [SerializeField] private int slotIndex;
    [SerializeField] private float sensitivity = 0.4f;

    public void OnDrag(PointerEventData eventData)
    {
        if (stage != null)
            stage.Rotate(slotIndex, -eventData.delta.x * sensitivity);
    }
}