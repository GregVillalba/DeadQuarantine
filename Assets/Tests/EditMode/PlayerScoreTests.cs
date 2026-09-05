using NUnit.Framework;
using UnityEngine;

public class PlayerScoreTests
{
    private GameObject playerGO;
    private PlayerScore playerScore;

    [SetUp]
    public void SetUp()
    {
        playerGO = new GameObject("Player");
        playerScore = playerGO.AddComponent<PlayerScore>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(playerGO);
    }

    // ---------------------------------------------------------------
    // Valor inicial
    // ---------------------------------------------------------------

    [Test]
    public void ScoreNetwork_AlCrearse_EmpiezaEnCero()
    {
        Assert.AreEqual(0, playerScore.ScoreNetwork.Value);
    }

    // ---------------------------------------------------------------
    // SumarPuntos (comportamiento real, sin spawnear)
    // ---------------------------------------------------------------

    [Test]
    public void SumarPuntos_SinSpawnearComoServidor_NoModificaElScore()
    {
        // Sin NetworkObject spawneado por un NetworkManager, IsServer da
        // false, así que SumarPuntos no debería tener ningún efecto.
        playerScore.SumarPuntos(10);

        Assert.AreEqual(0, playerScore.ScoreNetwork.Value);
    }

}