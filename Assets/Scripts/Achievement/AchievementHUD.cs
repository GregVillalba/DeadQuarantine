
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AchievementHUD : MonoBehaviour
{
    [Header("Base de datos")]
    [SerializeField]
    private AchievementDatabase database;

    [Header("Textos")]
    [SerializeField]
    private TextMeshProUGUI achievementTitleText;

    [SerializeField]
    private TextMeshProUGUI achievementDescriptionText;

    [SerializeField]
    private TextMeshProUGUI achievementRankText;

    [Header("Icono")]
    [SerializeField]
    private Image achievementIcon;

    [Header("Tiempo visible")]
    [SerializeField]
    private float maxVisibleTime = 5f;

    private CanvasGroup canvasGroup;
    private Canvas notificationCanvas;
    private bool notificationCanvasOriginalOverride;
    private int notificationCanvasOriginalOrder;
    private int notificationCanvasOriginalLayer;
    private bool notificationCanvasWasRaised;
    private RectTransform popupRect;
    private Image iconView;
    private readonly Dictionary<AchievementId, Sprite> iconCache =
        new Dictionary<AchievementId, Sprite>();

    private struct AchievementNotification
    {
        public AchievementId id;
        public AchievementRank rango;

        public AchievementNotification(
            AchievementId id,
            AchievementRank rango)
        {
            this.id = id;
            this.rango = rango;
        }
    }

    private readonly Queue<AchievementNotification> colaLogros =
        new Queue<AchievementNotification>();

    private Coroutine mostrarColasCoroutine;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        ConfigurarEscalaResponsive();
        ConstruirPanelVerde();

        OcultarVisualmente();

        if (database != null)
            database.Inicializar();
        else
            Debug.LogError(
                "[ACHIEVEMENT HUD] No tiene una AchievementDatabase asignada.");
    }

    private void ConfigurarEscalaResponsive()
    {
        Canvas canvas = transform.parent != null
            ? transform.parent.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
            return;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rect = transform as RectTransform;
        if (rect == null)
            return;

        // Debajo del puntaje (esquina superior derecha), aun si el puntaje
        // está oculto en un minijuego o en la pantalla de victoria.
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-26f, -320f);
        popupRect = rect;
        AjustarDentroDePantalla();
    }

    private void AjustarDentroDePantalla()
    {
        if (popupRect == null) return;
        Canvas canvas = transform.parent != null
            ? transform.parent.GetComponentInParent<Canvas>() : null;
        RectTransform canvasRect = canvas != null
            ? canvas.transform as RectTransform : null;
        if (canvasRect == null) return;

        float width = canvasRect.rect.width;
        float height = canvasRect.rect.height;
        if (width <= 0f || height <= 0f) return;
        popupRect.sizeDelta = new Vector2(Mathf.Min(500f, width - 32f), 150f);
        popupRect.anchoredPosition = new Vector2(
            -16f, -Mathf.Min(320f, Mathf.Max(16f, height - 166f)));
    }

    private void ConstruirPanelVerde()
    {
        // Los textos viejos permanecen en el prefab para conservar referencias;
        // solo se oculta su presentación y se construye el aviso unificado.
        foreach (Transform child in transform)
            child.gameObject.SetActive(false);

        Image background = GetComponent<Image>();
        if (background != null)
        {
            background.color = new Color(0.005f, 0.13f, 0.055f, 0.91f);
            background.raycastTarget = false;
        }

        Color green = new Color(0.22f, 0.84f, 0.40f, 1f);
        Color white = new Color(0.97f, 0.98f, 0.96f, 1f);
        MakeBar("BordeSuperior", new Vector2(0f, 1f),
            new Vector2(1f, 1f), new Vector2(0f, -2f), Vector2.zero, green);
        MakeBar("BordeInferior", new Vector2(0f, 0f),
            new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f), green);
        MakeBar("BordeIzquierdo", new Vector2(0f, 0f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(2f, 0f), green);
        MakeBar("BordeDerecho", new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(-2f, 0f), Vector2.zero, green);
        MakeBar("Separador", new Vector2(0f, 0.16f),
            new Vector2(0f, 0.84f), new Vector2(108f, 0f),
            new Vector2(111f, 0f), green);

        GameObject iconObject = new GameObject("IconoLogro", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(transform, false);
        iconView = iconObject.GetComponent<Image>();
        iconView.color = white;
        iconView.preserveAspect = true;
        iconView.raycastTarget = false;
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = iconRect.anchorMin;
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(55f, 0f);
        iconRect.sizeDelta = new Vector2(80f, 80f);

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("PvPvE/HeadingFont");
        MakeLabel("Etiqueta", "LOGRO DESBLOQUEADO", font,
            18f, green, new Vector2(120f, -43f), new Vector2(-12f, -15f));
        achievementTitleText = MakeLabel("TituloLogro", "", font,
            34f, white, new Vector2(120f, -97f), new Vector2(-12f, -43f));
        achievementTitleText.enableAutoSizing = true;
        achievementTitleText.fontSizeMin = 24f;
        achievementTitleText.fontSizeMax = 34f;
        achievementDescriptionText = MakeLabel("DescripcionLogro", "", font,
            20f, white, new Vector2(120f, -142f), new Vector2(-12f, -102f));
        achievementDescriptionText.enableAutoSizing = true;
        achievementDescriptionText.fontSizeMin = 16f;
        achievementDescriptionText.fontSizeMax = 20f;
        achievementRankText = MakeLabel("RangoLogro", "", font,
            17f, green, new Vector2(320f, -43f), new Vector2(-12f, -15f));
        achievementRankText.alignment = TextAlignmentOptions.MidlineRight;
    }

    private void MakeBar(string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private TextMeshProUGUI MakeLabel(string name, string value, TMP_FontAsset font,
        float size, Color color, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.text = value;
        if (font != null) label.font = font;
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold;
        label.color = color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    public void Show(AchievementId id)
    {
        Show(id, AchievementRank.Ninguno);
    }

    public void Show(AchievementId id, AchievementRank rango)
    {
        colaLogros.Enqueue(new AchievementNotification(id, rango));
        IniciarColaSiVisible();
    }

    private void OnEnable()
    {
        IniciarColaSiVisible();
    }

    private void Update()
    {
        if (mostrarColasCoroutine != null)
        {
            AjustarDentroDePantalla();
            ElevarAvisoSobreResultados();
        }
    }

    private void OnDisable()
    {
        mostrarColasCoroutine = null;
        RestaurarOrdenCanvas();
    }

    private void IniciarColaSiVisible()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy ||
            colaLogros.Count == 0 || mostrarColasCoroutine != null)
            return;

        ElevarAvisoSobreResultados();
        MostrarVisualmente();
        mostrarColasCoroutine = StartCoroutine(MostrarColaDeLogros());
    }

    private IEnumerator MostrarColaDeLogros()
    {
        while (colaLogros.Count > 0)
        {
            AchievementNotification notificacion = colaLogros.Dequeue();

            MostrarInformacion(notificacion.id, notificacion.rango);

            yield return new WaitForSeconds(
                Mathf.Max(0.1f, maxVisibleTime));

            if (colaLogros.Count == 0)
                OcultarVisualmente();
        }

        mostrarColasCoroutine = null;
        RestaurarOrdenCanvas();
    }

    private void ElevarAvisoSobreResultados()
    {
        if (notificationCanvas == null)
        {
            notificationCanvas = GetComponent<Canvas>();
            if (notificationCanvas == null)
                notificationCanvas = gameObject.AddComponent<Canvas>();
            notificationCanvasOriginalOverride = notificationCanvas.overrideSorting;
            notificationCanvasOriginalOrder = notificationCanvas.sortingOrder;
            notificationCanvasOriginalLayer = notificationCanvas.sortingLayerID;
        }
        if (notificationCanvas == null)
            return;

        int highestLayer = int.MinValue;
        int highestLayerId = notificationCanvas.sortingLayerID;
        int highestOrder = 0;
        Canvas[] activeCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas candidate in activeCanvases)
        {
            if (candidate == null || candidate == notificationCanvas ||
                !candidate.isActiveAndEnabled ||
                candidate.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            int layerValue = SortingLayer.GetLayerValueFromID(candidate.sortingLayerID);
            if (layerValue > highestLayer)
            {
                highestLayer = layerValue;
                highestLayerId = candidate.sortingLayerID;
                highestOrder = candidate.sortingOrder;
            }
            else if (layerValue == highestLayer)
            {
                highestOrder = Mathf.Max(highestOrder, candidate.sortingOrder);
            }
        }

        notificationCanvas.overrideSorting = true;
        notificationCanvas.sortingLayerID = highestLayerId;
        notificationCanvas.sortingOrder = Mathf.Min(32767, highestOrder + 1);
        transform.SetAsLastSibling();
        notificationCanvasWasRaised = true;
    }

    private void RestaurarOrdenCanvas()
    {
        if (!notificationCanvasWasRaised || notificationCanvas == null)
            return;

        notificationCanvas.overrideSorting = notificationCanvasOriginalOverride;
        notificationCanvas.sortingOrder = notificationCanvasOriginalOrder;
        notificationCanvas.sortingLayerID = notificationCanvasOriginalLayer;
        notificationCanvasWasRaised = false;
        notificationCanvas = null;
    }

    private void MostrarInformacion(
        AchievementId id,
        AchievementRank rango)
    {
        if (database == null)
        {
            Debug.LogError(
                "[ACHIEVEMENT HUD] No tiene una AchievementDatabase asignada.");
            return;
        }

        AchievementDefinition logro = database.Obtener(id);

        if (logro == null)
        {
            Debug.LogWarning(
                "[ACHIEVEMENT HUD] No se encontró la definición de: " + id);
            return;
        }

        if (achievementTitleText != null)
            achievementTitleText.text = logro.titulo;

        if (achievementDescriptionText != null)
        {
            achievementDescriptionText.text =
                rango != AchievementRank.Ninguno
                    ? logro.ObtenerDescripcion(rango)
                    : logro.descripcion;
        }

        if (achievementRankText != null)
            achievementRankText.text = ObtenerTextoRango(rango);

        if (iconView != null)
        {
            iconView.sprite = GetIcon(id) ?? logro.icono;
            iconView.enabled = iconView.sprite != null;
        }
    }

    private Sprite GetIcon(AchievementId id)
    {
        if (iconCache.TryGetValue(id, out Sprite cached)) return cached;
        string name;
        switch (id)
        {
            case AchievementId.DerrotadorSupremo: name = "skull_list"; break;
            case AchievementId.DondeEstaWally: name = "not_listed_location"; break;
            case AchievementId.MeDueleLaCabeza: name = "cognition_2"; break;
            case AchievementId.CompletadorDeRondas: name = "military_tech"; break;
            case AchievementId.CampeonDesconectado: name = "wifi_off"; break;
            case AchievementId.Historiador: name = "book_ribbon"; break;
            case AchievementId.SupervivienteDelInfierno: name = "swords"; break;
            case AchievementId.NadieSeQuedaAtras: name = "sports_kabaddi"; break;
            case AchievementId.VictoriaEnEquipo: name = "groups"; break;
            case AchievementId.CampeonDelPoligono: name = "target"; break;
            default: return null;
        }
        Texture2D texture = Resources.Load<Texture2D>("AchievementIcons/" + name);
        if (texture == null) return null;
        Sprite sprite = Sprite.Create(texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 100f);
        iconCache[id] = sprite;
        return sprite;
    }

    private string ObtenerTextoRango(AchievementRank rango)
    {
        switch (rango)
        {
            case AchievementRank.Bronce:
                return "BRONCE";

            case AchievementRank.Plata:
                return "PLATA";

            case AchievementRank.Oro:
                return "ORO";

            default:
                return "";
        }
    }

    private void MostrarVisualmente()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void OcultarVisualmente()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void Hide()
    {
        if (mostrarColasCoroutine != null)
        {
            StopCoroutine(mostrarColasCoroutine);
            mostrarColasCoroutine = null;
        }

        colaLogros.Clear();
        OcultarVisualmente();
    }
}
