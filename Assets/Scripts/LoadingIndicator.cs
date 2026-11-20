using DeadQuarantine.PvPvE;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

internal sealed class LoadingIndicator : MonoBehaviour
{
    private const float Travel = 118f;
    private RectTransform indicator;
    private float start;
    private Button exitButton;
    private TMP_Text exitLabel;

    public static LoadingIndicator Show(string message, Transform parent = null, UnityEngine.Events.UnityAction onExit = null)
    {
        var owner = new GameObject("Loading indicator");
        owner.transform.SetParent(parent, false);
        var view = owner.AddComponent<LoadingIndicator>();
        view.Build(message, onExit);
        if (onExit != null)
            MaintainInput(owner.transform);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        return view;
    }

    internal static (Button, TMP_Text) CreateExitButton(Transform parent, UnityEngine.Events.UnityAction onExit)
    {
        RectTransform rect = PvPvEUIFactory.Panel(parent, "Salir de la partida", 0, 0, 1, 1, 1f);
        PvPvEUIFactory.Place(rect, new Vector2(.76f, .89f), new Vector2(.97f, .96f));
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.targetGraphic.raycastTarget = true;
        PvPvEUIFactory.StyleButton(button);
        var label = PvPvEUIFactory.Text(rect, "Etiqueta", "Salir de la partida", 0, 0, 1, 1, null, 26);
        PvPvEUIFactory.Place(label.rectTransform, Vector2.zero, Vector2.one);
        label.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(onExit);
        return (button, label);
    }

    internal static void MaintainInput(Transform owner)
    {
        if (EventSystem.current == null)
        {
            var input = new GameObject("Entrada de pantalla de carga",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            input.transform.SetParent(owner, false);
            input.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ShowReturningToMenu()
    {
        exitButton.interactable = false;
        exitLabel.text = "Volviendo al menú…";
    }

    public void Dismiss()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void Update()
    {
        float position = Mathf.PingPong((Time.realtimeSinceStartup - start) * 0.85f, 1f);
        indicator.anchoredPosition = new Vector2(Mathf.Lerp(-Travel, Travel, position), 0f);
    }

    private void Build(string message, UnityEngine.Events.UnityAction onExit)
    {
        Canvas canvas = PvPvEUIFactory.Canvas(gameObject, "Loading canvas", short.MaxValue);
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        Image inputBlocker = canvas.gameObject.AddComponent<Image>();
        inputBlocker.color = Color.clear;

        RectTransform track = PvPvEUIFactory.Panel(canvas.transform, "Track", 0, 0, 300, 5);
        track.anchorMin = track.anchorMax = new Vector2(0.5f, 0f);
        track.pivot = new Vector2(0.5f, 0.5f);
        track.anchoredPosition = new Vector2(0f, 38f);

        indicator = PvPvEUIFactory.Panel(track, "Indicator", 0, 0, 64, 5);
        indicator.anchorMin = indicator.anchorMax = indicator.pivot = new Vector2(0.5f, 0.5f);
        indicator.anchoredPosition = new Vector2(-Travel, 0f);
        indicator.GetComponent<Image>().color = PvPvEUIFactory.Accent;
        start = Time.realtimeSinceStartup;

        if (!string.IsNullOrEmpty(message))
        {
            var label = PvPvEUIFactory.Text(track, "Status", message, 0, 0, 300, 35, null);
            if (PvPvEUIFactory.HeadingFont)
                label.font = PvPvEUIFactory.HeadingFont;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchoredPosition = new Vector2(0f, 41f);
        }

        if (onExit != null)
            (exitButton, exitLabel) = CreateExitButton(canvas.transform, onExit);
    }
}
