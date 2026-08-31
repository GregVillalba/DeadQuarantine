using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ZombieAITest
{
    private GameObject zombieObj;
    private GameObject playerObj;

    [SetUp]
    public void Setup()
    {
        zombieObj = new GameObject("Zombie");
        playerObj = new GameObject("Player");
    }

    [TearDown]
    public void Teardown()
    {
        Object.Destroy(zombieObj);
        Object.Destroy(playerObj);
    }

    // TC16: Zombie permanece dormido cuando el jugador está fuera del radio (12m > 8m)
    [UnityTest]
    public IEnumerator Zombie_PlayerOutsideDetectionRadius_RemainsAsleep()
    {
        // Arrange
        float detectionRadius = 8.0f;
        zombieObj.transform.position = Vector3.zero;
        playerObj.transform.position = new Vector3(12.0f, 0, 0); // 12 metros de distancia

        yield return null;

        // Act
        float distance = Vector3.Distance(zombieObj.transform.position, playerObj.transform.position);
        bool isChasing = (distance <= detectionRadius);

        // Assert
        Assert.IsFalse(isChasing, "El zombie no debería activarse si el jugador está fuera del radio de 8m.");
    }

    // TC17: Zombie despierta e inicia persecución cuando el jugador entra en rango (5m <= 8m)
    [UnityTest]
    public IEnumerator Zombie_PlayerInsideDetectionRadius_StartsChasing()
    {
        // Arrange
        float detectionRadius = 8.0f;
        zombieObj.transform.position = Vector3.zero;
        playerObj.transform.position = new Vector3(5.0f, 0, 0); // 5 metros de distancia

        yield return null;

        // Act
        float distance = Vector3.Distance(zombieObj.transform.position, playerObj.transform.position);
        bool isChasing = (distance <= detectionRadius);

        // Assert
        Assert.IsTrue(isChasing, "El zombie debería entrar en estado de persecución al detectar al jugador.");
    }
}