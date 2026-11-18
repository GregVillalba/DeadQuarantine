using DeadQuarantine.PvPvE;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

internal sealed class LoadingIndicator : MonoBehaviour
{
    private const float Travel = 118f;
    private RectTransform indicator;
    private float start;

    public static LoadingIndicator Show(string message, Transform parent = null)
    {
        var owner = new GameObject("Loading indicator");
        owner.transform.SetParent(parent, false);
        var view = owner.AddComponent<LoadingIndicator>();
        view.Build(message);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        return view;
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

    private void Build(string message)
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
    }
}
