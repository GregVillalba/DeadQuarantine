using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

[TestFixture]
public class TutorialSeccionesTests
{
    private readonly List<Object> creados = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object objeto in creados)
        {
            if (objeto != null)
                Object.DestroyImmediate(objeto);
        }

        creados.Clear();
        ZonaTutorial.ColocacionActiva = null;
    }

    // ---------------------------------------------------------------
    // ZonaTutorial
    // ---------------------------------------------------------------

    [Test]
    public void ContieneLocal_PuntoAdentroYEnElBorde_EsTrue()
    {
        Vector3 tamano = new Vector3(2f, 1f, 4f);

        Assert.IsTrue(ZonaTutorial.ContieneLocal(Vector3.zero, tamano));
        Assert.IsTrue(ZonaTutorial.ContieneLocal(new Vector3(1f, 0.5f, -2f), tamano), "El borde cuenta como adentro.");
    }

    [Test]
    public void ContieneLocal_PuntoAfuera_EsFalse()
    {
        Vector3 tamano = new Vector3(2f, 1f, 4f);

        Assert.IsFalse(ZonaTutorial.ContieneLocal(new Vector3(1.01f, 0f, 0f), tamano));
        Assert.IsFalse(ZonaTutorial.ContieneLocal(new Vector3(0f, 0.6f, 0f), tamano));
        Assert.IsFalse(ZonaTutorial.ContieneLocal(new Vector3(0f, 0f, -2.5f), tamano));
    }

    [Test]
    public void Contiene_UsaLaPosicionYRotacionDelObjeto()
    {
        GameObject go = Crear("Zona");
        go.transform.position = new Vector3(10f, 0f, 5f);
        go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        ZonaTutorial zona = go.AddComponent<ZonaTutorial>();
        zona.Configurar(new Vector3(4f, 2f, 1f));

        // Girada 90°: el largo de 4 m queda sobre el eje Z del mundo.
        Assert.IsTrue(zona.Contiene(new Vector3(10f, 0f, 6.9f)));
        Assert.IsFalse(zona.Contiene(new Vector3(11.9f, 0f, 5f)));
    }

    // ---------------------------------------------------------------
    // ObstaculoTerreno: caja ajustada a la malla
    // ---------------------------------------------------------------

    [Test]
    public void CajaDelModelo_CoberturaCompleta_EsLaCajaDeLaMallaConAltoMinimo()
    {
        Bounds malla = new Bounds(new Vector3(0.5f, 0.1f, 0f), new Vector3(3f, 0.2f, 1f));

        ObstaculoTerreno.CajaDelModelo(malla, 1f, 1f, out Vector3 centro, out Vector3 tamano);

        Assert.AreEqual(new Vector3(3f, 1f, 1f), tamano);
        Assert.AreEqual(0.5f, centro.x, 0.0001f);
        Assert.AreEqual(0.5f, centro.y, 0.0001f, "El alto crece desde la base de la malla (0), no desde el centro.");
        Assert.AreEqual(0f, centro.z, 0.0001f);
    }

    [Test]
    public void CajaDelModelo_ConCobertura_AchicaSoloAnchoYLargo()
    {
        Bounds malla = new Bounds(Vector3.zero, new Vector3(5f, 2f, 4f));

        ObstaculoTerreno.CajaDelModelo(malla, 0.8f, 1f, out Vector3 centro, out Vector3 tamano);

        Assert.AreEqual(4f, tamano.x, 0.0001f);
        Assert.AreEqual(2f, tamano.y, 0.0001f, "Más alto que el mínimo: queda igual.");
        Assert.AreEqual(3.2f, tamano.z, 0.0001f);
        Assert.AreEqual(Vector3.zero, centro);
    }

    [Test]
    public void CalcularCajaDelModelo_SinMalla_EsFalse()
    {
        ObstaculoTerreno obstaculo = Crear("Obstaculo").AddComponent<ObstaculoTerreno>();

        Assert.IsFalse(obstaculo.CalcularCajaDelModelo(out _, out _));
    }

    // ---------------------------------------------------------------
    // TutorialRecursosMapa
    // ---------------------------------------------------------------

    [Test]
    public void TutorialRecursosMapa_ActivoAlIniciarDesmarcado_NoEstaActivoHastaActivar()
    {
        GameObject go = new GameObject("Recursos");
        go.SetActive(false);
        creados.Add(go);
        TutorialRecursosMapa mapa = go.AddComponent<TutorialRecursosMapa>();
        typeof(TutorialRecursosMapa)
            .GetField("activoAlIniciar", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(mapa, false);
        go.SetActive(true);

        Assert.IsFalse(mapa.Activo);

        mapa.Activar();

        Assert.IsTrue(mapa.Activo);
        Assert.AreEqual(0, mapa.PuntosJuntados);
    }

    [Test]
    public void TutorialRecursosMapa_CantidadPuntos_CuentaSoloLosActivos()
    {
        TutorialRecursosMapa mapa = Crear("Recursos").AddComponent<TutorialRecursosMapa>();

        for (int i = 0; i < 3; i++)
        {
            GameObject punto = new GameObject("Punto " + i);
            punto.transform.SetParent(mapa.transform);
            punto.AddComponent<PuntoRecursoEscenario>();
            punto.SetActive(i != 2);
        }

        Assert.AreEqual(2, mapa.CantidadPuntos);
    }

    private GameObject Crear(string nombre)
    {
        GameObject go = new GameObject(nombre);
        creados.Add(go);
        return go;
    }
}
