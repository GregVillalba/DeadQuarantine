using System.Collections;
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
    private TMP_Text statusLabel;

    public static LoadingIndicator Show(string message, Transform parent = null, UnityEngine.Events.UnityAction onExit = null)
        => Show(message, parent, onExit, blocksInput: true);

    internal static LoadingIndicator ShowAutosave()
        => Show("GUARDANDO PARTIDA…", null, null, blocksInput: false);

    private static LoadingIndicator Show(string message, Transform parent, UnityEngine.Events.UnityAction onExit, bool blocksInput)
    {
        var owner = new GameObject("Loading indicator");
        owner.transform.SetParent(parent, false);
        var view = owner.AddComponent<LoadingIndicator>();
        view.Build(message, onExit, blocksInput);
        if (onExit != null)
            MaintainInput(owner.transform);

        if (blocksInput && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        return view;
    }

    internal void FinishAutosave(bool saved)
    {
        if (!saved)
        {
            statusLabel.text = "NO SE PUDO GUARDAR LA PARTIDA";
            indicator.gameObject.SetActive(false);
        }

        StartCoroutine(DismissAfter(saved ? 1.5f : 3f));
    }

    private IEnumerator DismissAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Dismiss();
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

    private void Build(string message, UnityEngine.Events.UnityAction onExit, bool blocksInput)
    {
        Canvas canvas = PvPvEUIFactory.Canvas(gameObject, "Loading canvas", short.MaxValue);
        if (blocksInput)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            canvas.gameObject.AddComponent<Image>().color = Color.clear;
        }

        RectTransform track = PvPvEUIFactory.Panel(canvas.transform, "Track", 0, 0, 300, 5);
        track.anchorMin = track.anchorMax = new Vector2(0.5f, 0f);
        track.pivot = new Vector2(0.5f, 0.5f);
        track.anchoredPosition = new Vector2(0f, blocksInput ? 38f : 105f);

        indicator = PvPvEUIFactory.Panel(track, "Indicator", 0, 0, 64, 5);
        indicator.anchorMin = indicator.anchorMax = indicator.pivot = new Vector2(0.5f, 0.5f);
        indicator.anchoredPosition = new Vector2(-Travel, 0f);
        indicator.GetComponent<Image>().color = PvPvEUIFactory.Accent;
        start = Time.realtimeSinceStartup;

        if (!string.IsNullOrEmpty(message))
        {
            statusLabel = PvPvEUIFactory.Text(track, "Status", message, 0, 0, 300, 35, null);
            if (PvPvEUIFactory.HeadingFont)
                statusLabel.font = PvPvEUIFactory.HeadingFont;
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.rectTransform.anchoredPosition = new Vector2(0f, 41f);
        }

        if (onExit != null)
            (exitButton, exitLabel) = CreateExitButton(canvas.transform, onExit);
    }
}
