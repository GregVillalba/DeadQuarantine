
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

        OcultarVisualmente();

        if (database != null)
            database.Inicializar();
        else
            Debug.LogError(
                "[ACHIEVEMENT HUD] No tiene una AchievementDatabase asignada.");
    }

    private void ConfigurarEscalaResponsive()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
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

        // Posición relativa a pantalla: centro del popup en 82% del ancho
        // y 25% de la altura desde abajo, como en el HUD de referencia.
        rect.anchorMin = new Vector2(0.82f, 0.25f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
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
            ElevarAvisoSobreResultados();
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
        if (notificationCanvasWasRaised)
            return;

        notificationCanvas = GetComponentInParent<Canvas>();
        if (notificationCanvas == null)
            return;

        int highestLayer = SortingLayer.GetLayerValueFromID(
            notificationCanvas.sortingLayerID);
        int highestLayerId = notificationCanvas.sortingLayerID;
        int highestOrder = notificationCanvas.sortingOrder;
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

        if (highestLayer == SortingLayer.GetLayerValueFromID(
                notificationCanvas.sortingLayerID) &&
            highestOrder <= notificationCanvas.sortingOrder)
        {
            notificationCanvas = null;
            return;
        }

        notificationCanvasOriginalOverride = notificationCanvas.overrideSorting;
        notificationCanvasOriginalOrder = notificationCanvas.sortingOrder;
        notificationCanvasOriginalLayer = notificationCanvas.sortingLayerID;
        notificationCanvas.overrideSorting = true;
        notificationCanvas.sortingLayerID = highestLayerId;
        notificationCanvas.sortingOrder = highestOrder + 1;
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

        if (achievementIcon != null)
        {
            achievementIcon.sprite = logro.icono;
            achievementIcon.enabled = logro.icono != null;
        }
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
