using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WeaponStatisticsView : MonoBehaviour
{
    static readonly string[] Labels = { "DAÑO", "CADENCIA", "PRECISIÓN", "MOVILIDAD", "ALCANCE", "CONTROL" };
    static readonly Color Neutral = new(.86f, .92f, .9f);
    static readonly Color Better = new(.28f, .92f, .48f);
    static readonly Color Worse = new(1f, .39f, .32f);
    readonly TMP_Text[] labels = new TMP_Text[6];
    readonly Image[] fills = new Image[6];
    RectTransform bars;
    TMP_Text details, title, toggleLabel;
    Button toggle;
    bool showingDetails;

    public static void Draw(TMP_Text target, WeaponStatistics stats, WeaponStatistics? baseline, bool compact, string name,
        bool legibleEnTienda = false)
    {
        target.enabled = false;
        var view = target.GetComponent<WeaponStatisticsView>();
        if (!view) view = target.gameObject.AddComponent<WeaponStatisticsView>();
        view.Ensure(target.font, compact, legibleEnTienda);
        view.UpdateValues(stats, baseline, compact, name);
    }

    void Ensure(TMP_FontAsset font, bool compact, bool legibleEnTienda)
    {
        if (bars) return;
        bars = Rect("WeaponStatBars", transform, Vector2.zero, new Vector2(1, compact ? 1 : .88f));
        title = Text("WeaponStatsTitle", transform, font, new(0, .9f), new(.72f, 1), 18);
        var buttonRoot = Rect("ToggleStatDetails", transform, new(.73f, .9f), Vector2.one);
        var background = buttonRoot.gameObject.AddComponent<Image>();
        background.color = new(.07f, .14f, .12f, .98f);
        toggle = buttonRoot.gameObject.AddComponent<Button>();
        toggle.targetGraphic = background;
        toggleLabel = Text("Label", buttonRoot, font, Vector2.zero, Vector2.one, legibleEnTienda ? 20 : 12);
        toggleLabel.alignment = TextAlignmentOptions.Center;
        toggle.onClick.AddListener(() => {
            showingDetails = !showingDetails;
            UpdateVisibility();
        });
        details = Text("WeaponStatDetails", transform, font, Vector2.zero, new(1, .86f), 15);
        details.enableAutoSizing = true;
        details.fontSizeMin = legibleEnTienda ? 15 : 10;
        details.fontSizeMax = legibleEnTienda ? 23 : 15;
        details.richText = true;
        for (int i = 0; i < 6; i++)
        {
            float x = compact ? (i % 2) * .51f : 0;
            float top = 1 - (compact ? (i / 2) / 3f : i / 6f);
            var row = Rect("Stat" + i, bars, new(x, top - (compact ? .32f : .16f)),
                new(x + (compact ? .48f : 1), top));
            int maxLabelSize = legibleEnTienda ? 23 : compact ? 11 : 14;
            labels[i] = Text("Label", row, font, new(0, legibleEnTienda ? .24f : .29f), Vector2.one, maxLabelSize);
            labels[i].enableAutoSizing = true;
            labels[i].fontSizeMin = legibleEnTienda ? 16 : compact ? 8 : 11;
            labels[i].fontSizeMax = maxLabelSize;
            if (legibleEnTienda)
            {
                labels[i].alignment = TextAlignmentOptions.MidlineLeft;
                labels[i].textWrappingMode = TextWrappingModes.NoWrap;
            }
            var track = Rect("Track", row, new(0, .08f), new(1, .2f));
            track.gameObject.AddComponent<Image>().color = new(.16f, .21f, .19f);
            track.GetComponent<Image>().raycastTarget = false;
            fills[i] = Rect("Fill", track, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            fills[i].raycastTarget = false;
        }
    }

    void UpdateValues(WeaponStatistics stats, WeaponStatistics? baseline, bool compact, string name)
    {
        title.text = name;
        title.gameObject.SetActive(!compact);
        toggle.gameObject.SetActive(!compact);
        details.text = stats.Details;
        for (int i = 0; i < 6; i++)
        {
            float difference = baseline.HasValue ? stats.Rating(i) - baseline.Value.Rating(i) : 0f;
            var color = Mathf.Abs(difference) < .01f ? Neutral : difference > 0 ? Better : Worse;
            labels[i].text = Labels[i] + "  " + Value(stats, i);
            labels[i].color = color;
            fills[i].color = Mathf.Abs(difference) < .01f ? Better : color;
            fills[i].rectTransform.anchorMax = new Vector2(stats.Rating(i) / 100f, 1);
        }
        if (compact) showingDetails = false;
        UpdateVisibility();
    }

    void UpdateVisibility()
    {
        bars.gameObject.SetActive(!showingDetails);
        details.gameObject.SetActive(showingDetails);
        toggleLabel.text = showingDetails ? "BARRAS" : "DETALLES";
    }

    static string Value(WeaponStatistics s, int index) => index switch {
        0 => s.DamageAt(0).ToString("0.#"),
        1 => $"{s.roundsPerMinute:0}/min",
        2 => $"{s.hipSpread:0.##}°",
        3 => $"×{s.movement:0.##} · {s.adsSeconds * 1000:0} ms",
        4 => $"{s.fullDamageDistance:0.#}–{s.minimumDamageDistance:0.#} m",
        _ => $"{s.verticalRecoil:0.##}° / {s.horizontalRecoil:0.##}°"
    };

    static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Vector2 min, Vector2 max, int size)
    {
        var label = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.color = Neutral;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }
}
