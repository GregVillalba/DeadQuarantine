using UnityEngine;

/// <summary>
/// Valores de escalado que dependen de la dificultad en el modo de rondas infinitas.
/// </summary>
[System.Serializable]
public struct DifficultyScaling
{
    public DifficultyLevel level;

    [Tooltip("Puntos de vida que se suman al zombie en cada ronda.")]
    [Min(0)] public int zombieHealthPerRound;

    [Tooltip("Tope de la probabilidad de zombies corredores (0.5 = 50%).")]
    [Range(0f, 1f)] public float runChanceCap;
}

/// <summary>
/// Base de un modo de juego (rondas, zombies, boss, intervalos).
/// La dificultad se aplica ENCIMA de estos valores.
/// Create > Game > Game Mode Config
///
/// Todo lo nuevo de "Rondas infinitas" solo se usa si infiniteRounds esta activado.
/// Con infiniteRounds apagado (Ronda 5) el comportamiento es exactamente el de antes.
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

    // =========================================================
    // RONDAS INFINITAS (SUPERVIVENCIA)
    // =========================================================

    [Header("Rondas infinitas (Supervivencia)")]
    [Tooltip("Activado = sin ultima ronda ni victoria. Usa los valores de esta seccion en lugar de las formulas de Ronda 5.")]
    public bool infiniteRounds = false;

    [Header("Infinito: jefes")]
    [Tooltip("Aparece jefe cada N rondas.")]
    [Min(1)] public int bossEveryNRounds = 10;
    [Tooltip("Activado: cantidad de jefes = ronda / N (ronda 10 = 1, 20 = 2, 30 = 3...).")]
    public bool bossesAccumulate = true;

    [Header("Infinito: cantidad de zombies")]
    [Tooltip("Tope de zombies por ronda. 0 = sin tope.")]
    [Min(0)] public int maxZombiesPerRound = 80;
    [Tooltip("Maximo de zombies vivos al mismo tiempo. 0 = sin limite.")]
    [Min(0)] public int maxAliveAtOnce = 24;

    [Header("Infinito: intervalo de spawn")]
    [Tooltip("En esta ronda el intervalo llega al minimo (los valores 'finalRound...' de arriba) y despues se mantiene.")]
    [Min(2)] public int roundsToMinSpawnInterval = 20;

    [Header("Infinito: vida y corredores")]
    [Tooltip("Vida del zombie normal en la ronda 1.")]
    [Min(1)] public int baseZombieHealth = 50;

    [Tooltip("Cuanto sube la probabilidad de correr por ronda (0.05 = 5%).")]
    [Range(0f, 1f)] public float runChanceStepPerRound = 0.05f;

    public DifficultyScaling[] difficultyScaling =
    {
        new DifficultyScaling { level = DifficultyLevel.Normal,    zombieHealthPerRound = 10, runChanceCap = 0.5f },
        new DifficultyScaling { level = DifficultyLevel.Dificil,   zombieHealthPerRound = 25, runChanceCap = 0.7f },
        new DifficultyScaling { level = DifficultyLevel.Pesadilla, zombieHealthPerRound = 50, runChanceCap = 0.9f }
    };

    [Header("Infinito: velocidad de los zombies")]
    [Tooltip("Cuanto sube la velocidad por ronda (0.02 = +2%).")]
    [Min(0f)] public float zombieSpeedIncreasePerRound = 0.02f;
    [Tooltip("Multiplicador maximo de velocidad (1.5 = +50%).")]
    [Min(1f)] public float maxZombieSpeedMultiplier = 1.5f;

    [Header("Infinito: fase de compras")]
    [Tooltip("Primera ronda tras la cual hay tienda.")]
    [Min(1)] public int shopFirstRound = 3;
    [Tooltip("Despues de la primera, hay tienda cada N rondas (2 = rondas impares: 3, 5, 7...).")]
    [Min(1)] public int shopEveryNRounds = 2;

    // =========================================================
    // CALCULOS (solo se usan con infiniteRounds activado)
    // =========================================================

    public bool IsBossRound(int round)
    {
        if (round <= 0)
            return false;

        return infiniteRounds
            ? round % bossEveryNRounds == 0
            : round == bossRound;
    }

    public int GetBossCount(int round)
    {
        if (!IsBossRound(round))
            return 0;

        if (infiniteRounds && bossesAccumulate)
            return round / bossEveryNRounds;

        return 1;
    }

    // Cantidad base de zombies (antes del multiplicador de dificultad).
    public int GetBaseZombieCount(int round)
    {
        int count = startingZombies + (Mathf.Max(round, 1) - 1) * zombiesPerRound;

        if (infiniteRounds && maxZombiesPerRound > 0)
            count = Mathf.Min(count, maxZombiesPerRound);

        return count;
    }

    public DifficultyScaling GetScaling(DifficultyLevel level)
    {
        if (difficultyScaling != null)
        {
            foreach (DifficultyScaling s in difficultyScaling)
            {
                if (s.level == level)
                    return s;
            }
        }

        // Si falta la entrada de esa dificultad: valores de Normal.
        return new DifficultyScaling
        {
            level = level,
            zombieHealthPerRound = 10,
            runChanceCap = 0.5f
        };
    }

    public int GetZombieHealth(int round, DifficultyLevel level)
    {
        return baseZombieHealth +
               GetScaling(level).zombieHealthPerRound * (Mathf.Max(round, 1) - 1);
    }

    public float GetRunChance(int round, DifficultyLevel level)
    {
        float chance = runChanceStepPerRound * (Mathf.Max(round, 1) - 1);
        return Mathf.Min(chance, GetScaling(level).runChanceCap);
    }

    public void GetSpawnIntervalRange(int round, out float min, out float max)
    {
        float progress = Mathf.Clamp01(
            (Mathf.Max(round, 1) - 1f) / (roundsToMinSpawnInterval - 1f)
        );

        min = Mathf.Lerp(round1MinSpawnInterval, finalRoundMinSpawnInterval, progress);
        max = Mathf.Lerp(round1MaxSpawnInterval, finalRoundMaxSpawnInterval, progress);
    }

    public float GetZombieSpeedMultiplier(int round)
    {
        float multiplier = 1f + zombieSpeedIncreasePerRound * (Mathf.Max(round, 1) - 1);
        return Mathf.Min(multiplier, maxZombieSpeedMultiplier);
    }

    public bool HasShopAfterRound(int round)
    {
        return round >= shopFirstRound &&
               (round - shopFirstRound) % shopEveryNRounds == 0;
    }
}
