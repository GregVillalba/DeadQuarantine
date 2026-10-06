using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NuevoAchievement",
    menuName = "Achievements/Achievement"
)]
public class AchievementDefinition : ScriptableObject
{
    [Header("Identificación")]
    public AchievementId id;

    [Header("Información")]
    public string titulo;

    [TextArea(2, 4)]
    public string descripcion;

    [Header("Clasificación")]
    public AchievementCategory categoria;

    public AchievementMode modalidad;

    [Header("Rangos")]
    public bool tieneRangos = true;

    public List<AchievementTierDefinition> rangos =
        new List<AchievementTierDefinition>();

    [Header("Visual")]
    public Sprite icono;


    // ============================================================
    // OBTENER DEFINICIÓN DE RANGO
    // ============================================================

    public AchievementTierDefinition ObtenerRango(
        AchievementRank rango
    )
    {
        if (rangos == null)
            return null;

        foreach (AchievementTierDefinition tier in rangos)
        {
            if (tier == null)
                continue;

            if (tier.rango == rango)
                return tier;
        }

        return null;
    }


    // ============================================================
    // OBTENER OBJETIVO
    // ============================================================

    public int ObtenerObjetivo(
        AchievementRank rango
    )
    {
        AchievementTierDefinition tier =
            ObtenerRango(rango);

        if (tier == null)
            return 0;

        return tier.objetivo;
    }


    // ============================================================
    // OBTENER DESCRIPCIÓN
    // ============================================================

    public string ObtenerDescripcion(
        AchievementRank rango
    )
    {
        AchievementTierDefinition tier =
            ObtenerRango(rango);

        if (tier == null)
            return descripcion;

        if (string.IsNullOrEmpty(tier.descripcion))
            return descripcion;

        return tier.descripcion;
    }
}