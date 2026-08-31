using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

public class GameFlowAndCombatTests
{
    private GameObject roundManagerObject;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Asegurarse de que haya un RoundManager en la escena para las pruebas
        var existing = Object.FindAnyObjectByType<RoundManager>();
        if (existing == null)
        {
            roundManagerObject = new GameObject("RoundManager");
            roundManagerObject.AddComponent<RoundManager>();
        }
        else
        {
            roundManagerObject = existing.gameObject;
        }

        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (roundManagerObject != null)
        {
            Object.Destroy(roundManagerObject);
        }

        // Limpiar cualquier GO residual creado por pruebas
        var remaining = Object.FindObjectsByType<GameObject>();
        foreach (var go in remaining)
        {
            if (go.name.StartsWith("Test_") || go.name.StartsWith("Zombie") || go.name.StartsWith("Player"))
            {
                Object.Destroy(go);
            }
        }

        yield return null;
    }

    // ---------------------------------------------------------------------------------
    // CASO 1: Comprobación básica de existencia del RoundManager (evita GameManager inexistente)
    // ---------------------------------------------------------------------------------
    [UnityTest]
    public IEnumerator TC01_RoundManager_InstanceExists()
    {
        Assert.IsNotNull(RoundManager.Instance, "RoundManager.Instance debe existir después de Awake.");
        yield return null;
    }

    // ---------------------------------------------------------------------------------
    // CASO 2: Llamada segura a StartRound (no debe lanzar excepciones en entorno de test)
    // ---------------------------------------------------------------------------------
    [UnityTest]
    public IEnumerator TC02_StartRound_DoesNotThrow()
    {
        var rm = RoundManager.Instance;
        Assert.IsNotNull(rm, "RoundManager no debe ser null.");

        // Llamamos a StartRound de forma segura; en tests sin servidor puede no iniciar lógica de servidor,
        // pero no debe lanzar excepciones.
        try
        {
            rm.StartRound(1);
        }
        catch (System.Exception ex)
        {
            Assert.Fail("StartRound lanzó una excepción: " + ex.Message);
        }

        yield return null;
    }

    // ---------------------------------------------------------------------------------
    // CASO 3: Se puede añadir componente ZombieHealth (verifica existencia del tipo)
    // ---------------------------------------------------------------------------------
    [UnityTest]
    public IEnumerator TC03_ZombieHealth_ComponentCanBeAdded()
    {
        var go = new GameObject("Test_Zombie");
        var zh = go.AddComponent<ZombieHealth>();

        Assert.IsNotNull(zh, "Se debe poder añadir ZombieHealth al GameObject.");
        Assert.IsFalse(zh.IsDead, "Por defecto IsDead debería ser falso.");

        Object.Destroy(go);
        yield return null;
    }

    // ---------------------------------------------------------------------------------
    // CASO 4: Existencia básica de la IA de zombies
    // ---------------------------------------------------------------------------------
    [UnityTest]
    public IEnumerator TC04_ZombieAI_ComponentCanBeAdded()
    {
        var go = new GameObject("Test_ZombieAI");
        var zai = go.AddComponent<ZombieAI>();

        Assert.IsNotNull(zai, "Se debe poder añadir ZombieAI al GameObject.");

        Object.Destroy(go);
        yield return null;
    }
}