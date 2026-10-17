using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[TestFixture]
public class PrecioRecursosTests
{
    private static readonly Color ColorNoPide = new Color(1f, 1f, 1f, 0.3f);

    private readonly List<GameObject> creados = new List<GameObject>();

    private PrecioRecursosUI precioUI;
    private readonly Dictionary<TipoRecurso, Image> iconos = new Dictionary<TipoRecurso, Image>();
    private readonly Dictionary<TipoRecurso, TextMeshProUGUI> cantidades = new Dictionary<TipoRecurso, TextMeshProUGUI>();

    [SetUp]
    public void SetUp()
    {
        precioUI = CrearGameObject("PrecioRecursos").AddComponent<PrecioRecursosUI>();

        Type tipoFila = typeof(PrecioRecursosUI).GetNestedType("PrecioRecurso", BindingFlags.NonPublic);
        IList filas = (IList)typeof(PrecioRecursosUI)
            .GetField("precios", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(precioUI);

        foreach (TipoRecurso tipo in Enum.GetValues(typeof(TipoRecurso)))
        {
            Image icono = CrearGameObject("Icono_" + tipo, typeof(Image)).GetComponent<Image>();
            TextMeshProUGUI cantidad = CrearGameObject("Cantidad_" + tipo, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();

            object fila = Activator.CreateInstance(tipoFila);
            tipoFila.GetField("tipo").SetValue(fila, tipo);
            tipoFila.GetField("icono").SetValue(fila, icono);
            tipoFila.GetField("cantidadText").SetValue(fila, cantidad);
            filas.Add(fila);

            iconos[tipo] = icono;
            cantidades[tipo] = cantidad;
        }
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in creados)
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }

        creados.Clear();
        iconos.Clear();
        cantidades.Clear();
    }

    // ---------------------------------------------------------------
    // WeaponOffer: precio en recursos
    // ---------------------------------------------------------------

    [TestCase(TipoRecurso.PilaDeLatas)]
    [TestCase(TipoRecurso.Cantimplora)]
    [TestCase(TipoRecurso.Bidon)]
    public void PrecioEnRecursos_OfertaNueva_EmpiezaEnCero(TipoRecurso tipo)
    {
        Assert.AreEqual(0, new WeaponOffer().PrecioEnRecursos(tipo));
    }

    [Test]
    public void PrecioEnRecursos_DevuelveElPrecioDeCadaRecurso()
    {
        WeaponOffer oferta = new WeaponOffer { precioPilaDeLatas = 3, precioCantimplora = 2, precioBidon = 1 };

        Assert.AreEqual(3, oferta.PrecioEnRecursos(TipoRecurso.PilaDeLatas));
        Assert.AreEqual(2, oferta.PrecioEnRecursos(TipoRecurso.Cantimplora));
        Assert.AreEqual(1, oferta.PrecioEnRecursos(TipoRecurso.Bidon));
    }

    [Test]
    public void Precio_OfertaNueva_EsGratis()
    {
        Assert.IsTrue(new WeaponOffer().Precio.EsGratis);
    }

    [Test]
    public void Precio_ConAlgunRecurso_NoEsGratis()
    {
        Assert.IsFalse(new WeaponOffer { precioCantimplora = 1 }.Precio.EsGratis);
    }

    // ---------------------------------------------------------------
    // PrecioRecursos
    // ---------------------------------------------------------------

    [Test]
    public void PrecioRecursos_CantidadNegativa_SeTomaComoCero()
    {
        PrecioRecursos precio = new PrecioRecursos(-2, 0, 0);

        Assert.AreEqual(0, precio.Cantidad(TipoRecurso.PilaDeLatas));
        Assert.IsTrue(precio.EsGratis);
    }

    // ---------------------------------------------------------------
    // NPCWeaponVendor: reparación en recursos (25% del precio, redondeado para arriba)
    // ---------------------------------------------------------------

    [Test]
    public void PrecioReparacion_RedondeaParaArriba()
    {
        NPCWeaponVendor vendor = CrearGameObject("Mercader").AddComponent<NPCWeaponVendor>();
        WeaponOffer chaleco = new WeaponOffer { precioPilaDeLatas = 6, precioBidon = 1 };

        PrecioRecursos reparacion = vendor.PrecioReparacion(chaleco);

        Assert.AreEqual(2, reparacion.Cantidad(TipoRecurso.PilaDeLatas));
        Assert.AreEqual(0, reparacion.Cantidad(TipoRecurso.Cantimplora));
        Assert.AreEqual(1, reparacion.Cantidad(TipoRecurso.Bidon));
    }

    [Test]
    public void PrecioReparacion_ConPrecioCero_EsGratis()
    {
        NPCWeaponVendor vendor = CrearGameObject("Mercader").AddComponent<NPCWeaponVendor>();

        Assert.IsTrue(vendor.PrecioReparacion(new WeaponOffer()).EsGratis);
    }

    // ---------------------------------------------------------------
    // PrecioRecursosUI
    // ---------------------------------------------------------------

    [Test]
    public void MostrarPrecio_ConPrecio_MuestraLaCantidadConElColorRecibido()
    {
        precioUI.MostrarPrecio(TipoRecurso.Cantimplora, 2, Color.green);

        Assert.AreEqual("2", cantidades[TipoRecurso.Cantimplora].text);
        Assert.AreEqual(Color.green, cantidades[TipoRecurso.Cantimplora].color);
        Assert.AreEqual(Color.white, iconos[TipoRecurso.Cantimplora].color);
    }

    [Test]
    public void MostrarPrecio_ConPrecioCero_MuestraCeroAtenuado()
    {
        precioUI.MostrarPrecio(TipoRecurso.Bidon, 0, Color.green);

        Assert.AreEqual("0", cantidades[TipoRecurso.Bidon].text);
        Assert.AreEqual(ColorNoPide, cantidades[TipoRecurso.Bidon].color);
        Assert.AreEqual(ColorNoPide, iconos[TipoRecurso.Bidon].color);
    }

    [Test]
    public void MostrarPrecio_SoloCambiaElRecursoIndicado()
    {
        cantidades[TipoRecurso.PilaDeLatas].text = "sin tocar";

        precioUI.MostrarPrecio(TipoRecurso.Bidon, 4, Color.red);

        Assert.AreEqual("sin tocar", cantidades[TipoRecurso.PilaDeLatas].text);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private GameObject CrearGameObject(string nombre, params Type[] componentes)
    {
        GameObject go = componentes.Length > 0 ? new GameObject(nombre, componentes) : new GameObject(nombre);
        creados.Add(go);
        return go;
    }
}
