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


    private struct AchievementNotification
    {
        public AchievementId id;
        public AchievementRank rango;

        public AchievementNotification(
            AchievementId id,
            AchievementRank rango
        )
        {
            this.id = id;
            this.rango = rango;
        }
    }


    private readonly Queue<AchievementNotification> colaLogros =
        new Queue<AchievementNotification>();

    private Coroutine mostrarColasCoroutine;


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        gameObject.SetActive(false);

        if (database != null)
        {
            database.Inicializar();
        }
    }


    // ============================================================
    // SHOW
    // ============================================================

    public void Show(
        AchievementId id
    )
    {
        Show(
            id,
            AchievementRank.Ninguno
        );
    }


    public void Show(
        AchievementId id,
        AchievementRank rango
    )
    {
        colaLogros.Enqueue(
            new AchievementNotification(
                id,
                rango
            )
        );

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (mostrarColasCoroutine == null)
        {
            mostrarColasCoroutine =
                StartCoroutine(
                    MostrarColaDeLogros()
                );
        }
    }


    // ============================================================
    // COLA
    // ============================================================

    private IEnumerator MostrarColaDeLogros()
    {
        while (colaLogros.Count > 0)
        {
            AchievementNotification notificacion =
                colaLogros.Dequeue();

            MostrarInformacion(
                notificacion.id,
                notificacion.rango
            );

            yield return new WaitForSeconds(
                maxVisibleTime
            );

            if (colaLogros.Count == 0)
            {
                gameObject.SetActive(false);
            }
        }

        mostrarColasCoroutine = null;
    }


    // ============================================================
    // MOSTRAR INFORMACIÓN
    // ============================================================

    private void MostrarInformacion(
        AchievementId id,
        AchievementRank rango
    )
    {
        if (database == null)
        {
            Debug.LogError(
                "AchievementHUD no tiene una AchievementDatabase asignada."
            );

            return;
        }

        AchievementDefinition logro =
            database.Obtener(id);

        if (logro == null)
            return;


        // --------------------------------------------------------
        // TITULO
        // --------------------------------------------------------

        if (achievementTitleText != null)
        {
            achievementTitleText.text =
                logro.titulo;
        }


        // --------------------------------------------------------
        // DESCRIPCIÓN
        // --------------------------------------------------------

        if (achievementDescriptionText != null)
        {
            if (rango != AchievementRank.Ninguno)
            {
                achievementDescriptionText.text =
                    logro.ObtenerDescripcion(rango);
            }
            else
            {
                achievementDescriptionText.text =
                    logro.descripcion;
            }
        }


        // --------------------------------------------------------
        // RANGO
        // --------------------------------------------------------

        if (achievementRankText != null)
        {
            if (rango == AchievementRank.Ninguno)
            {
                achievementRankText.text = "";
            }
            else
            {
                achievementRankText.text =
                    ObtenerTextoRango(rango);
            }
        }


        // --------------------------------------------------------
        // ICONO
        // --------------------------------------------------------

        if (achievementIcon != null)
        {
            achievementIcon.sprite =
                logro.icono;

            achievementIcon.enabled =
                logro.icono != null;
        }
    }


    // ============================================================
    // TEXTO RANGO
    // ============================================================

    private string ObtenerTextoRango(
        AchievementRank rango
    )
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


    // ============================================================
    // HIDE
    // ============================================================

    public void Hide()
    {
        if (mostrarColasCoroutine != null)
        {
            StopCoroutine(
                mostrarColasCoroutine
            );

            mostrarColasCoroutine = null;
        }

        colaLogros.Clear();

        gameObject.SetActive(false);
    }
}