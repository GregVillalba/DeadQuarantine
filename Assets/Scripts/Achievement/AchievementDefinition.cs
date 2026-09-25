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

    [Header("Objetivo")]
    public int objetivo;

    [Header("Visual")]
    public Sprite icono;
}