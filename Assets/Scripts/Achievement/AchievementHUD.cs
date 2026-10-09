
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

        OcultarVisualmente();

        if (database != null)
            database.Inicializar();
        else
            Debug.LogError(
                "[ACHIEVEMENT HUD] No tiene una AchievementDatabase asignada.");
    }

    public void Show(AchievementId id)
    {
        Show(id, AchievementRank.Ninguno);
    }

    public void Show(AchievementId id, AchievementRank rango)
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            Debug.LogWarning(
                "[ACHIEVEMENT HUD] El HUD no está activo en la jerarquía.");
            return;
        }

        colaLogros.Enqueue(new AchievementNotification(id, rango));

        MostrarVisualmente();

        if (mostrarColasCoroutine == null)
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