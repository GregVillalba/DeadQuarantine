using NUnit.Framework;
using UnityEngine;

public class ZombieLogicEditModeTests
{
    // TC16: Comprueba matemáticamente que a 12 m (fuera de radio de 8 m) no se detecta al jugador
    [Test]
    public void Zombie_OutsideDetectionRadius_ShouldNotDetectPlayer()
    {
        Vector3 zombiePos = new Vector3(0, 0, 0);
        Vector3 playerPos = new Vector3(12, 0, 0);
        float detectionRadius = 8.0f;

        float currentDistance = Vector3.Distance(zombiePos, playerPos);
        bool isDetected = currentDistance <= detectionRadius;

        Assert.IsFalse(isDetected, "El jugador a 12m no debería ser detectado por el radio de 8m.");
    }

    // TC17: Comprueba que a 5 m (dentro de radio de 8 m) se activa la detección
    [Test]
    public void Zombie_InsideDetectionRadius_ShouldDetectPlayer()
    {
        Vector3 zombiePos = new Vector3(0, 0, 0);
        Vector3 playerPos = new Vector3(5, 0, 0);
        float detectionRadius = 8.0f;

        float currentDistance = Vector3.Distance(zombiePos, playerPos);
        bool isDetected = currentDistance <= detectionRadius;

        Assert.IsTrue(isDetected, "El jugador a 5m debe ser detectado por el radio de 8m.");
    }

    // TC17 (Ataque cuerpo a cuerpo): Comprueba si el zombie está en rango de ataque (<= 1.5 m)
    [Test]
    public void Zombie_WithinMeleeRange_CanAttackPlayer()
    {
        Vector3 zombiePos = new Vector3(0, 0, 0);
        Vector3 playerPos = new Vector3(1.2f, 0, 0);
        float attackRange = 1.5f;

        float distance = Vector3.Distance(zombiePos, playerPos);
        bool canAttack = distance <= attackRange;

        Assert.IsTrue(canAttack, "El zombie debe poder atacar si la distancia es menor o igual a 1.5m.");
    }

    // Comprueba el decremento de salud al recibir daño del ataque de un zombie
    [Test]
    public void Player_TakesZombieAttackDamage_HealthDecreasesCorrectly()
    {
        int playerInitialHealth = 100;
        int zombieAttackDamage = 20;

        int resultHealth = Mathf.Max(0, playerInitialHealth - zombieAttackDamage);

        Assert.AreEqual(80, resultHealth, "La salud del jugador debería ser 80 tras recibir un ataque de 20.");
    }
}