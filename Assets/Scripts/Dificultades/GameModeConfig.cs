using UnityEngine;

/// <summary>
/// Base de un modo de juego (rondas, zombies, boss, intervalos).
/// La dificultad se aplica ENCIMA de estos valores.
/// Create > Game > Game Mode Config
/// </summary>
[CreateAssetMenu(fileName = "Mode_", menuName = "Game/Game Mode Config")]
public class GameModeConfig : ScriptableObject
{
    public string modeId = "clasico";
    public string displayName = "Clásico";

    [Header("Rondas")]
    [Min(1)] public int maxRounds = 5;
    [Min(1)] public int startingZombies = 6;
    [Min(0)] public int zombiesPerRound = 6;
    [Min(1)] public int shotsIncreasePerRound = 2;

    [Header("Boss")]
    public int bossRound = 5;
    public int bossHealthMultiplier = 10;

    [Header("Intervalos de spawn")]
    public float round1MinSpawnInterval = 2f;
    public float round1MaxSpawnInterval = 4f;
    public float finalRoundMinSpawnInterval = 0.8f;
    public float finalRoundMaxSpawnInterval = 1.6f;
}
