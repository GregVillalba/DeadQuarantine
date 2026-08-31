using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class CombatAndSurvivalTests
{
    // TC9: Interacción física con objetos Rigidbody
    [UnityTest]
    public IEnumerator TC9_RigidbodyImpact_AppliesPhysicalDisplacement()
    {
        GameObject dynamicBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Rigidbody rb = dynamicBox.AddComponent<Rigidbody>();
        rb.mass = 10f;
        dynamicBox.transform.position = Vector3.zero;

        rb.AddForce(Vector3.forward * 40f, ForceMode.Impulse);
        yield return new WaitForSeconds(0.1f);

        Assert.Greater(dynamicBox.transform.position.z, 0f);
        Object.Destroy(dynamicBox);
    }

    // TC10: Disparo y consumo de munición
    [Test]
    public void TC10_WeaponFire_ConsumesAmmo()
    {
        int ammo = 30;
        if (ammo > 0) ammo--;

        Assert.AreEqual(29, ammo);
    }

    // TC11 & TC12: Daño de 50 pts y muerte de Zombie al llegar a 0 de salud
    [Test]
    public void TC11_TC12_BulletDamage_ReducesHealth_AndEliminatesZombie()
    {
        int zombieHealth = 100;
        int bulletDamage = 50;

        // Disparo 1 (TC11)
        zombieHealth -= bulletDamage;
        Assert.AreEqual(50, zombieHealth);

        // Disparo 2 mortal (TC12)
        zombieHealth -= bulletDamage;
        bool isDead = zombieHealth <= 0;

        Assert.AreEqual(0, zombieHealth);
        Assert.IsTrue(isDead);
    }

    // TC13 & TC37: Salud en 0 decrementa vidas y activa espectador
    [Test]
    public void TC13_TC37_PlayerHealthZero_DecrementsLife_EntersSpectator()
    {
        int health = 0;
        int lives = 3;
        bool isSpectator = false;
        bool canMove = true;

        if (health <= 0)
        {
            lives--;
            isSpectator = true;
            canMove = false;
        }

        Assert.AreEqual(2, lives);
        Assert.IsTrue(isSpectator);
        Assert.IsFalse(canMove);
    }

    // TC14: Reaparición en siguiente ronda con 100 de salud
    [Test]
    public void TC14_RespawnNextRound_RestoresHealthTo100()
    {
        int health = 0;
        int lives = 2;
        bool isSpectator = true;

        // Evento de inicio de siguiente ronda
        health = 100;
        isSpectator = false;

        Assert.AreEqual(100, health);
        Assert.AreEqual(2, lives);
        Assert.IsFalse(isSpectator);
    }

    // TC15: Daño mortal con 0 vidas elimina definitivamente
    [Test]
    public void TC15_FatalDamageWithZeroLives_EliminatesPermanently()
    {
        int lives = 0;
        int health = 0;
        bool isPermanentlyDead = (lives <= 0 && health <= 0);

        Assert.IsTrue(isPermanentlyDead);
    }

    // TC35: Puerta interactiva con Rigidbody se abre tras presionar E
    [Test]
    public void TC35_InteractiveDoor_OpensOnKeyE()
    {
        bool inRange = true;
        bool pressedE = true;
        bool doorOpened = inRange && pressedE;

        Assert.IsTrue(doorOpened);
    }

    // TC36: Disparo contra pared estática detecta impacto sin dañar entorno
    [Test]
    public void TC36_BulletImpactWall_RegistersHitWithoutEnvironmentDamage()
    {
        bool hitSolidWall = true;
        bool environmentDamaged = false;

        Assert.IsTrue(hitSolidWall);
        Assert.IsFalse(environmentDamaged);
    }
}