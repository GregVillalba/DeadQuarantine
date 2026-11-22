using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class UIButtonAudio : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    Button button;
    void Awake() { button = GetComponent<Button>(); button.onClick.AddListener(PlayClick); }
    bool Available => button && button.isActiveAndEnabled && button.IsInteractable();
    public void OnPointerEnter(PointerEventData data) { if (Available) InterfaceAudio.Hover(); }
    public void OnSelect(BaseEventData data) { if (Available) InterfaceAudio.Hover(); }
    // Otro listener puede ocultar el panel antes de que llegue este aviso.
    void PlayClick() { InterfaceAudio.Click(); }
    void OnDestroy() { if (button) button.onClick.RemoveListener(PlayClick); }
}
