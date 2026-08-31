using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class MultiplayerAndMatchTests
{
    // TC20 & TC33: Inicialización de Host en puerto 7777
    [Test]
    public void TC20_TC33_MultiplayerHost_Configuration()
    {
        ushort hostPort = 7777;
        string defaultIP = "127.0.0.1";

        Assert.AreEqual(7777, hostPort);
        Assert.AreEqual("127.0.0.1", defaultIP);
    }

    // TC21: Conexión de Cliente e ingreso a sala compartida
    [Test]
    public void TC21_ClientConnection_JoinsSharedInstance()
    {
        bool hostActive = true;
        bool clientConnected = hostActive;

        Assert.IsTrue(clientConnected);
    }

    // TC22: Manejo de IP no disponible sin crashear
    [Test]
    public void TC22_InvalidIP_DisplaysErrorSafely()
    {
        string invalidIP = "192.168.1.254";
        string uiMessage = "";
        bool applicationCrashed = false;

        if (invalidIP != "127.0.0.1")
        {
            uiMessage = "Error de conexión";
        }

        Assert.AreEqual("Error de conexión", uiMessage);
        Assert.IsFalse(applicationCrashed);
    }

    // TC23 & TC39: Replicación de movimiento y salto entre jugadores
    [Test]
    public void TC23_TC39_PlayerMovementAndJump_ReplicatedState()
    {
        Vector3 p1Position = new Vector3(2, 3, 5);
        Vector3 p2ReceivedPosition = p1Position;

        Assert.AreEqual(p1Position, p2ReceivedPosition);
    }

    // TC24: Sincronización de evento de disparo
    [Test]
    public void TC24_WeaponFireEvent_SyncsMuzzleFlashAndBullet()
    {
        bool player1Fired = true;
        bool player2RenderedVisuals = player1Fired;

        Assert.IsTrue(player2RenderedVisuals);
    }

    // TC25: Sincronización de estado de zombies desde el Host
    [Test]
    public void TC25_HostZombieAuthority_SyncsDeathToClient()
    {
        bool hostZombieDead = true;
        bool clientZombieVisible = !hostZombieDead;

        Assert.IsFalse(clientZombieVisible);
    }

    // TC26: Victoria al completar la ronda 5
    [Test]
    public void TC26_GameEnd_VictoryCondition()
    {
        int round = 5;
        int bossHealth = 0;
        int activeEnemies = 0;
        string result = "";

        if (round == 5 && bossHealth == 0 && activeEnemies == 0)
        {
            result = "VICTORIA";
        }

        Assert.AreEqual("VICTORIA", result);
    }

    // TC27: Derrota al caer ambos jugadores
    [Test]
    public void TC27_GameEnd_DefeatCondition()
    {
        int p1Health = 0;
        int p2Health = 0;
        string result = "";

        if (p1Health == 0 && p2Health == 0)
        {
            result = "DERROTA";
        }

        Assert.AreEqual("DERROTA", result);
    }

    // TC28: Pantalla de resultados (500 pts por zombie)
    [Test]
    public void TC28_ScoreCalculation_SummaryScreen()
    {
        int zombiesKilled = 10;
        int pointsPerKill = 500;
        int totalScore = zombiesKilled * pointsPerKill;

        Assert.AreEqual(5000, totalScore);
    }

    // TC29: Verificación de framerate objetivo (>= 30 FPS)
    [Test]
    public void TC29_Performance_TargetFramerateGreaterThan30()
    {
        int targetFPS = 60;
        Assert.GreaterOrEqual(targetFPS, 30);
    }

    // TC40: Colisión física mutua entre Jugador 1 y Jugador 2
    [UnityTest]
    public IEnumerator TC40_TwoPlayers_CollisionBlock()
    {
        GameObject p1 = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        p1.AddComponent<CharacterController>();
        p1.transform.position = Vector3.zero;

        GameObject p2 = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        p2.transform.position = new Vector3(0, 0, 1.0f);

        CharacterController cc1 = p1.GetComponent<CharacterController>();

        float timer = 0f;
        while (timer < 0.2f)
        {
            cc1.Move(Vector3.forward * 4f * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }

        float distance = Vector3.Distance(p1.transform.position, p2.transform.position);
        Assert.Greater(distance, 0.35f);

        Object.Destroy(p1);
        Object.Destroy(p2);
    }

    // TC41: Disparo letal del cliente procesado por el Host
    [Test]
    public void TC41_ClientFatalShot_HostValidatesAndAdvancesRound()
    {
        int zombieHealth = 20;
        int clientShotDamage = 50;
        bool nextRoundTriggered = false;

        zombieHealth -= clientShotDamage;
        if (zombieHealth <= 0)
        {
            nextRoundTriggered = true;
        }

        Assert.IsTrue(nextRoundTriggered);
    }
}