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
public class RecursosTiendaUITests
{
    private static readonly Color ColorSinRecursos = new Color(0.45f, 0.45f, 0.45f, 1f);

    private readonly List<GameObject> creados = new List<GameObject>();

    private PlayerRecursos playerRecursos;
    private RecursosTiendaUI recursosUI;

    private readonly Dictionary<TipoRecurso, Image> iconos = new Dictionary<TipoRecurso, Image>();
    private readonly Dictionary<TipoRecurso, TextMeshProUGUI> cantidades = new Dictionary<TipoRecurso, TextMeshProUGUI>();

    [SetUp]
    public void SetUp()
    {
        // Misma jerarquía que en Player.prefab: el inventario en la raíz del
        // jugador y la UI de la tienda colgando de él.
        GameObject playerGO = CrearGameObject("Player");
        playerRecursos = playerGO.AddComponent<PlayerRecursos>();

        recursosUI = CrearRecursosUI(playerGO.transform);
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
    // Valor inicial
    // ---------------------------------------------------------------

    [Test]
    public void AlHabilitarse_SinRecursos_MuestraCeroEnLosTres()
    {
        foreach (TipoRecurso tipo in Enum.GetValues(typeof(TipoRecurso)))
            Assert.AreEqual("0", cantidades[tipo].text, tipo.ToString());
    }

    [Test]
    public void AlHabilitarse_SinRecursos_MuestraLosIconosEnGris()
    {
        foreach (TipoRecurso tipo in Enum.GetValues(typeof(TipoRecurso)))
            Assert.AreEqual(ColorSinRecursos, iconos[tipo].color, tipo.ToString());
    }

    [Test]
    public void AlHabilitarse_SinPlayerRecursos_MuestraCero()
    {
        // UI sin jugador (ej: escena de prueba): no debe romper y queda en 0.
        GameObject sueltoGO = CrearGameObject("SinJugador");
        cantidades.Clear();
        iconos.Clear();

        CrearRecursosUI(sueltoGO.transform);

        Assert.AreEqual("0", cantidades[TipoRecurso.PilaDeLatas].text);
    }

    // ---------------------------------------------------------------
    // Actualización al cambiar una cantidad
    // ---------------------------------------------------------------

    [Test]
    public void AlCambiarUnRecurso_ActualizaSuCantidadYSuIcono()
    {
        playerRecursos.CantimploraNetwork.Value = 3;

        Assert.AreEqual("3", cantidades[TipoRecurso.Cantimplora].text);
        Assert.AreEqual(Color.white, iconos[TipoRecurso.Cantimplora].color);
    }

    [Test]
    public void AlCambiarUnRecurso_NoCambiaLosDemas()
    {
        playerRecursos.BidonNetwork.Value = 2;

        Assert.AreEqual("0", cantidades[TipoRecurso.PilaDeLatas].text);
        Assert.AreEqual("0", cantidades[TipoRecurso.Cantimplora].text);
        Assert.AreEqual(ColorSinRecursos, iconos[TipoRecurso.PilaDeLatas].color);
    }

    [Test]
    public void AlGastarRecursos_BajaLaCantidadYVuelveAGrisEnCero()
    {
        playerRecursos.PilaDeLatasNetwork.Value = 4;
        playerRecursos.PilaDeLatasNetwork.Value = 0;

        Assert.AreEqual("0", cantidades[TipoRecurso.PilaDeLatas].text);
        Assert.AreEqual(ColorSinRecursos, iconos[TipoRecurso.PilaDeLatas].color);
    }

    [Test]
    public void AlDeshabilitarse_DejaDeEscucharLosCambios()
    {
        InvokePrivateMethod("OnDisable");

        playerRecursos.PilaDeLatasNetwork.Value = 5;

        Assert.AreEqual("0", cantidades[TipoRecurso.PilaDeLatas].text);
    }

    [Test]
    public void AlVolverAHabilitarse_MuestraLaCantidadActual()
    {
        InvokePrivateMethod("OnDisable");
        playerRecursos.PilaDeLatasNetwork.Value = 5;

        InvokePrivateMethod("OnEnable");

        Assert.AreEqual("5", cantidades[TipoRecurso.PilaDeLatas].text);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private RecursosTiendaUI CrearRecursosUI(Transform padre)
    {
        GameObject uiGO = CrearGameObject("Recursos");
        uiGO.transform.SetParent(padre);

        RecursosTiendaUI ui = uiGO.AddComponent<RecursosTiendaUI>();

        Type tipoFila = typeof(RecursosTiendaUI).GetNestedType("RecursoTienda", BindingFlags.NonPublic);
        IList filas = (IList)typeof(RecursosTiendaUI)
            .GetField("recursos", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(ui);

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

        // Re-invocamos OnEnable ahora que las filas ya están cableadas,
        // simulando que en el Inspector estaban asignadas desde el principio.
        typeof(RecursosTiendaUI)
            .GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(ui, null);

        return ui;
    }

    private GameObject CrearGameObject(string nombre, params Type[] componentes)
    {
        GameObject go = componentes.Length > 0 ? new GameObject(nombre, componentes) : new GameObject(nombre);
        creados.Add(go);
        return go;
    }

    private void InvokePrivateMethod(string methodName)
    {
        typeof(RecursosTiendaUI)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(recursosUI, null);
    }
}
