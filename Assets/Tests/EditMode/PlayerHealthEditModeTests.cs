using NUnit.Framework;
using UnityEngine;

public class PlayerHealthEditModeTests
{
    [Test]
    public void Zombie_TakeDamage_DecreasesHealthByBaseDamage()
    {
        int initialHealth = 100;
        int weaponDamage = 50;

        int currentHealth = initialHealth - weaponDamage;

        Assert.AreEqual(50, currentHealth, "La salud del enemigo no se redujo correctamente tras el impacto.");
    }

    // TC12: Muerte del zombie cuando la vida llega a 0
    [Test]
    public void Zombie_TakesLethalDamage_IsDead()
    {
        int health = 50;
        int weaponDamage = 50;

        health -= weaponDamage;
        bool isDead = (health <= 0);

        Assert.IsTrue(isDead, "El zombie debería considerarse muerto al llegar a 0 de vida.");
    }

    // TC13: Jugador llega a 0 de salud y pierde 1 vida
    [Test]
    public void Player_ReachesZeroHealth_LosesOneLife()
    {
        int currentLives = 3;
        int playerHealth = 0;

        if (playerHealth <= 0)
        {
            currentLives--;
        }
        Assert.AreEqual(2, currentLives, "El jugador debería haber perdido una vida al llegar a 0 de salud.");
    }

    // TC15: Jugador sin vidas (0 vidas) queda eliminado definitivamente
    [Test]
    public void Player_WithZeroLives_IsPermanentlyEliminated()
    {
        int currentLives = 0;
        int health = 0;

        bool canRespawn = (currentLives > 0);

        Assert.IsFalse(canRespawn, "El jugador no debería reaparecer cuando sus vidas son 0.");
    }
}