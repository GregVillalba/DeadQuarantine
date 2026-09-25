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

    [Header("Icono")]
    [SerializeField]
    private Image achievementIcon;

    [Header("Tiempo visible")]
    [SerializeField]
    private float maxVisibleTime = 5f;

    private readonly Queue<AchievementId> colaLogros =
        new Queue<AchievementId>();

    private Coroutine mostrarColasCoroutine;


    private void Awake()
    {
        gameObject.SetActive(false);

        if (database != null)
        {
            database.Inicializar();
        }
    }


    public void Show(AchievementId id)
    {
        colaLogros.Enqueue(id);

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (mostrarColasCoroutine == null)
        {
            mostrarColasCoroutine =
                StartCoroutine(MostrarColaDeLogros());
        }
    }


    private IEnumerator MostrarColaDeLogros()
    {
        while (colaLogros.Count > 0)
        {
            AchievementId id = colaLogros.Dequeue();

            MostrarInformacion(id);

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


    private void MostrarInformacion(AchievementId id)
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
        {
            return;
        }

        if (achievementTitleText != null)
        {
            achievementTitleText.text =
                logro.titulo;
        }

        if (achievementDescriptionText != null)
        {
            achievementDescriptionText.text =
                logro.descripcion;
        }

        if (achievementIcon != null)
        {
            achievementIcon.sprite =
                logro.icono;

            achievementIcon.enabled =
                logro.icono != null;
        }
    }


    public void Hide()
    {
        if (mostrarColasCoroutine != null)
        {
            StopCoroutine(mostrarColasCoroutine);

            mostrarColasCoroutine = null;
        }

        colaLogros.Clear();

        gameObject.SetActive(false);
    }
}