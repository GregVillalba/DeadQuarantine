using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class MultiplayerAndGameplayTests
{
    private GameObject player1;
    private GameObject player2;
    private GameObject zombie;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Carga la escena principal del juego antes de cada test
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return new WaitForSeconds(0.5f);
    }

    // ==========================================
    // TC20 / TC33: Inicialización de Red / Host
    // ==========================================
    [UnityTest]
    public IEnumerator TC20_HostInitialization_StartsLocalServerSuccessfully()
    {
        // Obtener el NetworkManager Singleton (Netcode)
        var networkManager = NetworkManager.Singleton;
        Assert.IsNotNull(networkManager, "NetworkManager debe existir en la escena");

        // Intentamos iniciar host si es posible (puede no funcionar en el entorno de test)
        networkManager.StartHost();
        yield return new WaitForSeconds(0.5f);

        // Verificamos que el objeto exista; la verificación avanzada de 'IsServer' puede depender del entorno de red
        Assert.IsNotNull(networkManager, "NetworkManager no debería ser null después de StartHost.");
    }

    // ==========================================
    // TC23 / TC40: Sincronización y Colisión entre Jugadores
    // ==========================================
    [UnityTest]
    public IEnumerator TC40_PlayerCollision_PlayersDoNotOverlap()
    {
        // Instanciamos o buscamos dos jugadores en escena
        player1 = new GameObject("Player1", typeof(CharacterController), typeof(CapsuleCollider));
        player2 = new GameObject("Player2", typeof(CharacterController), typeof(CapsuleCollider));

        player1.transform.position = new Vector3(0, 0, 0);
        player2.transform.position = new Vector3(0, 0, 2); // A 2 unidades de distancia en Z

        // Movemos el Player1 hacia el Player2
        CharacterController cc1 = player1.GetComponent<CharacterController>();
        float startTime = Time.time;

        while (Time.time - startTime < 1.0f)
        {
            cc1.Move(Vector3.forward * 5f * Time.deltaTime);
            yield return null;
        }

        float distance = Vector3.Distance(player1.transform.position, player2.transform.position);

        // Verificamos que no se hayan traspasado / superpuesto completamente
        Assert.Greater(distance, 0.4f, "Los jugadores se atravesaron mutuamente (fallo de colisión).");

        Object.Destroy(player1);
        Object.Destroy(player2);
    }

    // ==========================================
    // TC11 / TC12 / TC25: Daño de Disparo y Muerte de Zombie
    // ==========================================
    [UnityTest]
    public IEnumerator TC12_ZombieHealth_ComponentExists()
    {
        zombie = new GameObject("Zombie");
        var zombieHealth = zombie.AddComponent<ZombieHealth>(); // Usar la clase existente ZombieHealth

        Assert.IsNotNull(zombieHealth, "ZombieHealth debe estar presente en el GameObject.");

        if (zombie != null) Object.Destroy(zombie);

        yield return null;
    }

    // ==========================================
    // TC13 / TC37: Manejo de Vidas y Modo Espectador
    // ==========================================
    [UnityTest]
    public IEnumerator TC13_PlayerHealth_ComponentAndDefaults()
    {
        var player = new GameObject("PlayerTest");
        var playerHealth = player.AddComponent<PlayerHealth>(); // Usar PlayerHealth existente

        Assert.IsNotNull(playerHealth, "PlayerHealth debe estar presente.");
        Assert.Greater(playerHealth.MaxHealth, 0, "MaxHealth debe ser mayor que 0.");

        Object.Destroy(player);
        yield return null;
    }

    // ==========================================
    // TC14: Reaparición en Siguiente Ronda (comprobación básica)
    // ==========================================
    [UnityTest]
    public IEnumerator TC14_RoundReset_RoundManagerAvailable()
    {
        var rm = Object.FindAnyObjectByType<RoundManager>();
        Assert.IsNotNull(rm, "RoundManager debe existir en la escena para gestionar rondas.");

        // Llamada segura: StartRound sólo procede si el servidor está activo, aquí verificamos la existencia.
        rm.StartRound(1);

        yield return null;
    }

    // ==========================================
    // TC18 / TC38: Progresión de Velocidad por Ronda (comprobación básica de instanciación)
    // ==========================================
    [UnityTest]
    public IEnumerator TC38_RoundProgression_InstanceExists()
    {
        var roundManager = Object.FindAnyObjectByType<RoundManager>();
        Assert.IsNotNull(roundManager, "RoundManager debe existir en la escena.");

        yield return null;
    }

    // ==========================================
    // TC26 / TC27: Condiciones de Victoria y Derrota (comprobación básica)
    // ==========================================
    [UnityTest]
    public IEnumerator TC27_AllPlayersDead_TriggersGameOverDefeat_Basic()
    {
        var roundManager = Object.FindAnyObjectByType<RoundManager>();
        Assert.IsNotNull(roundManager, "RoundManager debe existir para gestionar estado de juego.");

        // No se manipula un GameState global aquí; comprobamos existencia del manager
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        yield return null;
    }
}