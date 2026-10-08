using System;
using UnityEngine;

[Serializable]
public class AchievementTierDefinition
{
    [Header("Rango")]
    public AchievementRank rango;

    [Header("Objetivo numérico")]
    public int objetivo;

    [Header("Descripción del objetivo")]
    [TextArea(1, 3)]
    public string descripcion;

    public AchievementTierDefinition()
    {
        rango = AchievementRank.Ninguno;
        objetivo = 0;
        descripcion = "";
    }
}