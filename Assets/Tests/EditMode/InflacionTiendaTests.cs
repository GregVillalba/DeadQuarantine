using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Inflación del mercader en Supervivencia: cada fase de compras sube los precios un porcentaje sobre el
/// precio base, hasta un tope. Es igual en las tres dificultades.
/// </summary>
[TestFixture]
public class InflacionTiendaTests
{
    private GameModeConfig modo;

    [SetUp]
    public void SetUp()
    {
        // Supervivencia: tienda tras las rondas 3, 5, 7...; +25% por tienda, tope x3.
        modo = ScriptableObject.CreateInstance<GameModeConfig>();
        modo.infiniteRounds = true;
        modo.shopFirstRound = 3;
        modo.shopEveryNRounds = 2;
        modo.shopPriceIncreasePerShop = 0.25f;
        modo.maxShopPriceMultiplier = 3f;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(modo);
    }

    // ---------------------------------------------------------------
    // GameModeConfig: número de tienda
    // ---------------------------------------------------------------

    [TestCase(1, 0)]
    [TestCase(2, 0)]
    [TestCase(3, 1)]
    [TestCase(5, 2)]
    [TestCase(9, 4)]
    [TestCase(19, 9)]
    public void GetShopNumber_CuentaLasFasesDeCompras(int ronda, int esperado)
    {
        Assert.AreEqual(esperado, modo.GetShopNumber(ronda));
    }

    // ---------------------------------------------------------------
    // GameModeConfig: multiplicador de precios
    // ---------------------------------------------------------------

    [Test]
    public void Multiplicador_PrimeraTienda_CobraElPrecioBase()
    {
        Assert.AreEqual(1f, modo.GetShopPriceMultiplier(3), 0.0001f);
    }

    [TestCase(5, 1.25f)]
    [TestCase(7, 1.5f)]
    [TestCase(9, 1.75f)]
    [TestCase(19, 3f)]
    public void Multiplicador_SubeUnPorcentajePorTienda(int ronda, float esperado)
    {
        Assert.AreEqual(esperado, modo.GetShopPriceMultiplier(ronda), 0.0001f);
    }

    [TestCase(21)]
    [TestCase(51)]
    [TestCase(201)]
    public void Multiplicador_NoPasaDelTope(int ronda)
    {
        Assert.AreEqual(3f, modo.GetShopPriceMultiplier(ronda), 0.0001f);
    }

    [Test]
    public void Multiplicador_AntesDeLaPrimeraTienda_EsUno()
    {
        Assert.AreEqual(1f, modo.GetShopPriceMultiplier(1), 0.0001f);
    }

    [Test]
    public void Multiplicador_FueraDeSupervivencia_SiempreEsUno()
    {
        modo.infiniteRounds = false;

        Assert.AreEqual(1f, modo.GetShopPriceMultiplier(3), 0.0001f);
        Assert.AreEqual(1f, modo.GetShopPriceMultiplier(19), 0.0001f);
    }

    [Test]
    public void Multiplicador_ConSubaCero_DejaLosPreciosFijos()
    {
        modo.shopPriceIncreasePerShop = 0f;

        Assert.AreEqual(1f, modo.GetShopPriceMultiplier(19), 0.0001f);
    }

    // ---------------------------------------------------------------
    // PrecioRecursos: precio con inflación
    // ---------------------------------------------------------------

    [Test]
    public void Multiplicado_PorUno_NoCambiaElPrecio()
    {
        PrecioRecursos precio = new PrecioRecursos(3, 2, 1).Multiplicado(1f);

        AssertPrecio(precio, 3, 2, 1);
    }

    [Test]
    public void Multiplicado_RedondeaAlEnteroMasCercano()
    {
        // Rifle (3 / 2 / 1) en la tienda de la ronda 9: 5,25 / 3,5 / 1,75.
        PrecioRecursos precio = new PrecioRecursos(3, 2, 1).Multiplicado(1.75f);

        AssertPrecio(precio, 5, 4, 2);
    }

    [Test]
    public void Multiplicado_EnElTope_TriplicaElPrecio()
    {
        // Torreta (6 / 4 / 2) con el tope x3.
        PrecioRecursos precio = new PrecioRecursos(6, 4, 2).Multiplicado(3f);

        AssertPrecio(precio, 18, 12, 6);
    }

    [TestCase(1.25f, 1)]
    [TestCase(1.5f, 2)]
    [TestCase(2.25f, 2)]
    [TestCase(2.5f, 3)]
    public void Multiplicado_PrecioDeUnaUnidad_SubeDeAUnEscalon(float multiplicador, int esperado)
    {
        PrecioRecursos precio = new PrecioRecursos(1, 0, 0).Multiplicado(multiplicador);

        Assert.AreEqual(esperado, precio.Cantidad(TipoRecurso.PilaDeLatas));
    }

    [Test]
    public void Multiplicado_LoQueNoSePide_SigueEnCero()
    {
        PrecioRecursos precio = new PrecioRecursos(2, 0, 0).Multiplicado(3f);

        AssertPrecio(precio, 6, 0, 0);
    }

    [Test]
    public void Multiplicado_MenorAUno_NoAbarata()
    {
        PrecioRecursos precio = new PrecioRecursos(4, 2, 1).Multiplicado(0.5f);

        AssertPrecio(precio, 4, 2, 1);
    }

    // ---------------------------------------------------------------
    // WeaponOffer
    // ---------------------------------------------------------------

    [Test]
    public void Precio_SinPartidaEnCurso_EsElPrecioBase()
    {
        WeaponOffer oferta = new WeaponOffer { precioPilaDeLatas = 3, precioCantimplora = 2, precioBidon = 1 };

        Assert.AreEqual(1f, RoundManager.MultiplicadorPreciosTienda, 0.0001f);
        AssertPrecio(oferta.Precio, 3, 2, 1);
        AssertPrecio(oferta.PrecioBase, 3, 2, 1);
    }

    private static void AssertPrecio(PrecioRecursos precio, int latas, int cantimploras, int bidones)
    {
        Assert.AreEqual(latas, precio.Cantidad(TipoRecurso.PilaDeLatas), "Pila de latas");
        Assert.AreEqual(cantimploras, precio.Cantidad(TipoRecurso.Cantimplora), "Cantimplora");
        Assert.AreEqual(bidones, precio.Cantidad(TipoRecurso.Bidon), "Bidón");
    }
}
