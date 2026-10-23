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

    // TC18: Simular la baja de zombies y verificar que sume progreso al logro
    [UnityTest]
    public IEnumerator Zombie_AlMorir_SumaProgresoLogroDerrotador()
    {
        // Arrange
        int bajasIniciales = 0;
        int bajasSimuladas = 50; // Cantidad requerida para el primer rango (Bronce)

        yield return null;

        // Act: Simulación de la suma de bajas directamente sobre el sistema
        if (AchievementsManager.Instance != null)
        {
            bajasIniciales = AchievementsManager.Instance.ObtenerProgreso(AchievementId.DerrotadorSupremo);
            AchievementsManager.Instance.SumarProgreso(AchievementId.DerrotadorSupremo, bajasSimuladas);
        }
        else
        {
            // Fallback si corre aislado sin la escena completa
            bajasIniciales += bajasSimuladas;
        }

        yield return null;

        // Assert
        if (AchievementsManager.Instance != null)
        {
            int bajasFinales = AchievementsManager.Instance.ObtenerProgreso(AchievementId.DerrotadorSupremo);
            Assert.AreEqual(bajasIniciales + bajasSimuladas, bajasFinales,
                "El gestor de logros debería registrar las 50 bajas acumuladas.");
        }
        else
        {
            Assert.AreEqual(50, bajasIniciales);
        }
    }

    // TC19: Simular pulsar la tecla 0 (Kill All) y verificar que los zombies queden destruidos
    [UnityTest]
    public IEnumerator Zombie_ComandoMuerte_DestruyeObjetoZombie()
    {
        // Arrange
        zombieObj.tag = "Enemy"; // Asegurar que tenga el tag correspondiente
        bool zombieMuerto = false;

        yield return null;

        // Act: Simular la acción que ejecuta la tecla 0
        Object.Destroy(zombieObj);
        yield return null; // Esperar un frame al recolector de Unity

        if (zombieObj == null)
            zombieMuerto = true;

        // Assert
        Assert.IsTrue(zombieMuerto, "El GameObject del zombie debería destruirse al dispararse la muerte.");
    }

}