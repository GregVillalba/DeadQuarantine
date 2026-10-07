using UnityEngine;
using TMPro;
using UnityEngine.UI;

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

    [Header("Opacidad de rangos")]
    [SerializeField]
    [Range(0f, 1f)]
    private float opacidadBloqueado = 0.30f;

    [SerializeField]
    [Range(0f, 1f)]
    private float opacidadDesbloqueado = 1f;


    // ============================================================
    // COLORES ORIGINALES DE LAS COPITAS
    // ============================================================

    private Color colorOriginalBronce;
    private Color colorOriginalPlata;
    private Color colorOriginalOro;

    private bool coloresGuardados = false;


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        BuscarEstadosAutomaticamente();

        GuardarColoresOriginales();
    }


    // ============================================================
    // GUARDAR COLORES ORIGINALES
    // ============================================================

    private void GuardarColoresOriginales()
    {
        if (coloresGuardados)
            return;


        RawImage bronce =
            rangoBronce != null
                ? rangoBronce.GetComponentInChildren<RawImage>(true)
                : null;

        RawImage plata =
            rangoPlata != null
                ? rangoPlata.GetComponentInChildren<RawImage>(true)
                : null;

        RawImage oro =
            rangoOro != null
                ? rangoOro.GetComponentInChildren<RawImage>(true)
                : null;


        if (bronce != null)
        {
            colorOriginalBronce =
                bronce.color;
        }

        if (plata != null)
        {
            colorOriginalPlata =
                plata.color;
        }

        if (oro != null)
        {
            colorOriginalOro =
                oro.color;
        }


        coloresGuardados = true;
    }


    // ============================================================
    // BUSCAR ESTADOS AUTOMATICAMENTE
    // ============================================================

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


    // ============================================================
    // ACTUALIZAR ESTADO
    // ============================================================

    public void ActualizarEstado()
    {
        if (AchievementsManager.Instance == null)
            return;


        // Aseguramos que los colores originales estén guardados.
        GuardarColoresOriginales();


        AchievementRank rango =
            AchievementsManager.Instance
                .ObtenerRango(
                    achievementId
                );


        // ========================================================
        // ESTADO GENERAL
        // ========================================================

        // El logro solamente está completamente desbloqueado
        // cuando llegó a ORO.

        bool desbloqueadoCompletamente =
            rango == AchievementRank.Oro;


        if (estadoLogrado != null)
        {
            estadoLogrado.SetActive(
                desbloqueadoCompletamente
            );
        }


        if (estadoNoLogrado != null)
        {
            estadoNoLogrado.SetActive(
                !desbloqueadoCompletamente
            );
        }


        // ========================================================
        // RANGOS
        // ========================================================

        bool bronceDesbloqueado =
            rango >= AchievementRank.Bronce;

        bool plataDesbloqueada =
            rango >= AchievementRank.Plata;

        bool oroDesbloqueado =
            rango >= AchievementRank.Oro;


        // Las tres copitas permanecen visibles.
        // Lo que cambia es su color y opacidad.

        if (rangoBronce != null)
        {
            rangoBronce.SetActive(true);

            AplicarOpacidad(
                rangoBronce,
                bronceDesbloqueado
            );
        }


        if (rangoPlata != null)
        {
            rangoPlata.SetActive(true);

            AplicarOpacidad(
                rangoPlata,
                plataDesbloqueada
            );
        }


        if (rangoOro != null)
        {
            rangoOro.SetActive(true);

            AplicarOpacidad(
                rangoOro,
                oroDesbloqueado
            );
        }


        // ========================================================
        // PROGRESO
        // ========================================================

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
                    desbloqueadoCompletamente
                        ? "COMPLETADO"
                        : "PENDIENTE";
            }
        }


        // ========================================================
        // TEXTO DEL RANGO
        // ========================================================

        if (rangoActualText != null)
        {
            rangoActualText.text =
                ObtenerTextoRango(rango);
        }
    }


    // ============================================================
    // APLICAR COLOR Y OPACIDAD
    // ============================================================

    private void AplicarOpacidad(
        GameObject objeto,
        bool desbloqueado
    )
    {
        if (objeto == null)
            return;


        RawImage rawImage =
            objeto.GetComponentInChildren<RawImage>(true);


        if (rawImage == null)
            return;


        // ========================================================
        // OBTENER COLOR ORIGINAL
        // ========================================================

        Color colorOriginal;


        if (objeto == rangoBronce)
        {
            colorOriginal =
                colorOriginalBronce;
        }
        else if (objeto == rangoPlata)
        {
            colorOriginal =
                colorOriginalPlata;
        }
        else if (objeto == rangoOro)
        {
            colorOriginal =
                colorOriginalOro;
        }
        else
        {
            colorOriginal =
                Color.white;
        }


        // ========================================================
        // DESBLOQUEADO
        // ========================================================

        if (desbloqueado)
        {
            rawImage.color =
                new Color(
                    colorOriginal.r,
                    colorOriginal.g,
                    colorOriginal.b,
                    opacidadDesbloqueado
                );

            return;
        }


        // ========================================================
        // BLOQUEADO
        // ========================================================

        rawImage.color =
            new Color(
                colorOriginal.r * 0.5f,
                colorOriginal.g * 0.5f,
                colorOriginal.b * 0.5f,
                opacidadBloqueado
            );
    }


    // ============================================================
    // ¿ESTÁ COMPLETAMENTE DESBLOQUEADO?
    // ============================================================

    public bool EstaDesbloqueado()
    {
        if (AchievementsManager.Instance == null)
            return false;


        return AchievementsManager.Instance
            .EstaDesbloqueado(
                achievementId
            );
    }


    // ============================================================
    // TEXTO DEL RANGO
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
}