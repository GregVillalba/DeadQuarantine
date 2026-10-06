using UnityEngine;
using TMPro;

public class AchievementUIItem : MonoBehaviour
{
    [Header("Base de datos")]
    [SerializeField]
    private AchievementDatabase database;

    [Header("Identificación")]
    [SerializeField]
    private AchievementId achievementId;

    [Header("Estados generales")]
    [SerializeField]
    private GameObject estadoLogrado;

    [SerializeField]
    private GameObject estadoNoLogrado;

    [Header("Rangos")]
    [SerializeField]
    private GameObject rangoBronce;

    [SerializeField]
    private GameObject rangoPlata;

    [SerializeField]
    private GameObject rangoOro;

    [Header("Progreso")]
    [SerializeField]
    private TextMeshProUGUI progresoText;

    [SerializeField]
    private TextMeshProUGUI rangoActualText;


    private void Awake()
    {
        BuscarEstadosAutomaticamente();

        if (database == null)
        {
            database =
                FindAnyObjectByType<AchievementsManager>() != null
                    ? null
                    : database;
        }
    }


    private void BuscarEstadosAutomaticamente()
    {
        Transform estado =
            transform.Find("estado");

        if (estado == null)
            return;

        Transform logrado =
            estado.Find("logrado");

        Transform noLogrado =
            estado.Find("no_logrado");

        if (logrado != null &&
            estadoLogrado == null)
        {
            estadoLogrado =
                logrado.gameObject;
        }

        if (noLogrado != null &&
            estadoNoLogrado == null)
        {
            estadoNoLogrado =
                noLogrado.gameObject;
        }
    }


    public void ActualizarEstado()
    {
        if (AchievementsManager.Instance == null)
            return;

        AchievementRank rango =
            AchievementsManager.Instance
                .ObtenerRango(
                    achievementId
                );

        bool desbloqueado =
            rango != AchievementRank.Ninguno;

        if (estadoLogrado != null)
            estadoLogrado.SetActive(desbloqueado);

        if (estadoNoLogrado != null)
            estadoNoLogrado.SetActive(!desbloqueado);

        if (rangoBronce != null)
            rangoBronce.SetActive(
                rango >= AchievementRank.Bronce
            );

        if (rangoPlata != null)
            rangoPlata.SetActive(
                rango >= AchievementRank.Plata
            );

        if (rangoOro != null)
            rangoOro.SetActive(
                rango >= AchievementRank.Oro
            );

        int progreso =
            AchievementsManager.Instance
                .ObtenerProgreso(
                    achievementId
                );

        AchievementDefinition definicion =
            database != null
                ? database.Obtener(achievementId)
                : null;

        if (progresoText != null)
        {
            if (definicion != null &&
                definicion.tieneRangos)
            {
                AchievementTierDefinition oro =
                    definicion.ObtenerRango(
                        AchievementRank.Oro
                    );

                if (oro != null)
                {
                    progresoText.text =
                        $"{progreso}/{oro.objetivo}";
                }
                else
                {
                    progresoText.text =
                        progreso.ToString();
                }
            }
            else
            {
                progresoText.text =
                    desbloqueado
                        ? "COMPLETADO"
                        : "PENDIENTE";
            }
        }

        if (rangoActualText != null)
        {
            rangoActualText.text =
                ObtenerTextoRango(rango);
        }
    }


    public bool EstaDesbloqueado()
    {
        if (AchievementsManager.Instance == null)
            return false;

        return AchievementsManager.Instance
            .EstaDesbloqueado(
                achievementId
            );
    }


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
}