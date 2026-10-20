using NUnit.Framework;
using UnityEngine;

public class PlayerRecursosTests
{
    private GameObject playerGO;
    private PlayerRecursos playerRecursos;

    [SetUp]
    public void SetUp()
    {
        playerGO = new GameObject("Player");
        playerRecursos = playerGO.AddComponent<PlayerRecursos>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(playerGO);
    }

    // ---------------------------------------------------------------
    // Valor inicial
    // ---------------------------------------------------------------

    [TestCase(TipoRecurso.PilaDeLatas)]
    [TestCase(TipoRecurso.Cantimplora)]
    [TestCase(TipoRecurso.Bidon)]
    public void Cantidad_AlCrearse_EmpiezaEnCero(TipoRecurso tipo)
    {
        Assert.AreEqual(0, playerRecursos.Cantidad(tipo));
    }

    // ---------------------------------------------------------------
    // RecursoNetwork
    // ---------------------------------------------------------------

    [Test]
    public void RecursoNetwork_DevuelveLaVariableDeCadaTipo()
    {
        Assert.AreSame(playerRecursos.PilaDeLatasNetwork, playerRecursos.RecursoNetwork(TipoRecurso.PilaDeLatas));
        Assert.AreSame(playerRecursos.CantimploraNetwork, playerRecursos.RecursoNetwork(TipoRecurso.Cantimplora));
        Assert.AreSame(playerRecursos.BidonNetwork, playerRecursos.RecursoNetwork(TipoRecurso.Bidon));
    }

    // ---------------------------------------------------------------
    // Sumar / Gastar (comportamiento real, sin spawnear)
    // ---------------------------------------------------------------

    [Test]
    public void SumarRecurso_SinSpawnearComoServidor_NoModificaLaCantidad()
    {
        // Sin NetworkObject spawneado por un NetworkManager, IsServer da
        // false, así que SumarRecurso no debería tener ningún efecto.
        playerRecursos.SumarRecurso(TipoRecurso.Cantimplora, 2);

        Assert.AreEqual(0, playerRecursos.Cantidad(TipoRecurso.Cantimplora));
    }

    [Test]
    public void GastarRecursos_SinSpawnearComoServidor_DevuelveFalseYNoModificaLaCantidad()
    {
        playerRecursos.BidonNetwork.Value = 2;

        bool gastado = playerRecursos.GastarRecursos(new PrecioRecursos(0, 0, 1));

        Assert.IsFalse(gastado);
        Assert.AreEqual(2, playerRecursos.Cantidad(TipoRecurso.Bidon));
    }

    // ---------------------------------------------------------------
    // Alcanza
    // ---------------------------------------------------------------

    [Test]
    public void Alcanza_PrecioGratis_EsTrueSinRecursos()
    {
        Assert.IsTrue(playerRecursos.Alcanza(new PrecioRecursos(0, 0, 0)));
    }

    [Test]
    public void Alcanza_SinRecursosSuficientes_EsFalse()
    {
        playerRecursos.PilaDeLatasNetwork.Value = 2;

        Assert.IsFalse(playerRecursos.Alcanza(new PrecioRecursos(3, 0, 0)));
    }

    [Test]
    public void Alcanza_ConTodosLosRecursosDelPrecio_EsTrue()
    {
        playerRecursos.PilaDeLatasNetwork.Value = 3;
        playerRecursos.BidonNetwork.Value = 1;

        Assert.IsTrue(playerRecursos.Alcanza(new PrecioRecursos(3, 0, 1)));
    }

    [Test]
    public void Alcanza_SiFaltaUnSoloRecurso_EsFalse()
    {
        playerRecursos.PilaDeLatasNetwork.Value = 10;

        Assert.IsFalse(playerRecursos.Alcanza(new PrecioRecursos(3, 1, 0)));
    }

    [Test]
    public void Alcanza_ConPrecioNegativo_LoTomaComoCero()
    {
        Assert.IsTrue(playerRecursos.Alcanza(new PrecioRecursos(-5, 0, 0)));
    }
}
