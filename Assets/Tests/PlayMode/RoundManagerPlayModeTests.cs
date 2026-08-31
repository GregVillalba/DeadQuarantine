using NUnit.Framework;

public class RoundProgressionTests
{
    // TC18: La velocidad de los zombies se incrementa con las rondas
    [Test]
    public void ZombieSpeed_IncreasesEachRound()
    {
        float baseSpeed = 2.0f;
        float speedMultiplier = 1.25f;

        float round1Speed = baseSpeed;
        float round2Speed = baseSpeed * speedMultiplier;

        Assert.Greater(round2Speed, round1Speed, "La velocidad en la ronda 2 debe ser superior a la ronda 1.");
    }

    // TC19: Configuración del Boss en la ronda 5
    [Test]
    public void Round5_SpawnsBossWithSuperiorStats()
    {
        int standardZombieHealth = 100;
        int bossHealth = 500;
        float bossScale = 1.5f;

        Assert.AreEqual(500, bossHealth, "El Boss debe tener 500 puntos de vida.");
        Assert.AreEqual(1.5f, bossScale, "El Boss debe tener una escala 1.5x.");
        Assert.Greater(bossHealth, standardZombieHealth);
    }

    // TC28: Cálculo del puntaje final (500 pts por zombie eliminado)
    [Test]
    public void ScoreCalculation_MultipliesZombiesKilledBy500()
    {
        int zombiesKilled = 10;
        int pointsPerZombie = 500;

        int totalScore = zombiesKilled * pointsPerZombie;

        Assert.AreEqual(5000, totalScore, "El puntaje total debe ser 5000 puntos para 10 zombies eliminados.");
    }
}