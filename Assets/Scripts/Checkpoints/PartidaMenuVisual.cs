using DeadQuarantine.PvPvE;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal static class PartidaMenuVisual
{
    internal static readonly Color Verde = new Color32(105, 233, 144, 255);
    private static readonly Color Neutro = new Color32(16, 28, 36, 255);
    private static readonly Color Activo = new Color32(19, 38, 30, 255);
    private static readonly Color Primario = new Color32(23, 56, 36, 255);

    internal static RectTransform Panel(Transform parent, string name, float height, float width = 760)
    {
        var panel = Caja(parent, name, 0, 0, width, height, new Color32(10, 17, 23, 250));
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        panel.anchoredPosition = Vector2.zero;
        Borde(panel, new Color32(59, 74, 82, 255));
        Caja(panel, "Acento", 0, 0, 100, 4, Verde);
        return panel;
    }

    internal static RectTransform Caja(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        var caja = PvPvEUIFactory.Panel(parent, name, x, y, width, height);
        caja.GetComponent<Image>().color = color;
        return caja;
    }

    internal static TMP_Text Texto(Transform parent, string name, string value, float x, float y, float width, float height,
        int size, bool titulo = false)
    {
        var text = PvPvEUIFactory.Text(parent, name, value, x, y, width, height, null, size);
        if (titulo && PvPvEUIFactory.HeadingFont)
            text.font = PvPvEUIFactory.HeadingFont;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * .65f;
        text.fontSizeMax = size;
        text.color = titulo ? Color.white : new Color32(166, 182, 191, 255);
        return text;
    }

    internal static Button Boton(Transform parent, string name, string value, float x, float y, bool principal = false)
    {
        var rect = Caja(parent, name, x, y, 326, 76, Neutro);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.targetGraphic.raycastTarget = true;
        PvPvEUIFactory.StyleButton(button);
        Caja(rect, "Acento", 0, 0, 5, 76, Verde);
        var label = Texto(rect, "Etiqueta", value, 10, 0, 306, 76, 35, true);
        label.alignment = TextAlignmentOptions.Center;
        Marcar(button, principal, principal);
        return button;
    }

    internal static void Marcar(Button button, bool marcado, bool principal = false)
    {
        button.targetGraphic.color = marcado ? principal ? Primario : Activo : Neutro;
        button.transform.Find("Acento").gameObject.SetActive(marcado);
        Borde((RectTransform)button.transform, marcado ? new Color32(64, 168, 99, 255) : new Color32(65, 80, 88, 255));
    }

    internal static void Borde(RectTransform rect, Color color)
    {
        var outline = rect.GetComponent<Outline>() ?? rect.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1, -1);
        outline.useGraphicAlpha = false;
    }

    internal static void Linea(Transform parent, float x, float y, float width) =>
        Caja(parent, "Línea", x, y, width, 1, new Color32(40, 56, 64, 255));
}
