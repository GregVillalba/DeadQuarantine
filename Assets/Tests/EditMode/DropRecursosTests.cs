using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

[TestFixture]
public class DropRecursosTests
{
    private readonly List<Object> creados = new List<Object>();

    [SetUp]
    public void SetUp()
    {
        InvocarEstatico(typeof(DropRecursosZombie), "ResetStaticState");
        InvocarEstatico(typeof(PlayerRecursos), "ResetStaticState");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object objeto in creados)
        {
            if (objeto != null)
                Object.DestroyImmediate(objeto);
        }

        creados.Clear();
        InvocarEstatico(typeof(DropRecursosZombie), "ResetStaticState");
        InvocarEstatico(typeof(PlayerRecursos), "ResetStaticState");
    }

    // ---------------------------------------------------------------
    // TablaDropRecursos
    // ---------------------------------------------------------------

    [Test]
    public void Elegir_TiradaDentroDelPrimerRango_DevuelveElPrimero()
    {
        TablaDropRecursos tabla = CrearTabla(0, (TipoRecurso.PilaDeLatas, 0.2f), (TipoRecurso.Bidon, 0.05f));

        Assert.AreEqual(TipoRecurso.PilaDeLatas, tabla.Elegir(0.1f).tipo);
    }

    [Test]
    public void Elegir_TiradaEnElSegundoRango_DevuelveElSegundo()
    {
        TablaDropRecursos tabla = CrearTabla(0, (TipoRecurso.PilaDeLatas, 0.2f), (TipoRecurso.Bidon, 0.05f));

        Assert.AreEqual(TipoRecurso.Bidon, tabla.Elegir(0.22f).tipo);
    }

    [Test]
    public void Elegir_TiradaFueraDeLaSuma_NoSueltaNada()
    {
        TablaDropRecursos tabla = CrearTabla(0, (TipoRecurso.PilaDeLatas, 0.2f), (TipoRecurso.Bidon, 0.05f));

        Assert.IsNull(tabla.Elegir(0.25f));
        Assert.IsNull(tabla.Elegir(0.99f));
    }

    [Test]
    public void Elegir_ConProbabilidadCero_NuncaLoElige()
    {
        TablaDropRecursos tabla = CrearTabla(0, (TipoRecurso.Cantimplora, 0f), (TipoRecurso.Bidon, 0.5f));

        Assert.AreEqual(TipoRecurso.Bidon, tabla.Elegir(0f).tipo);
    }

    [Test]
    public void Elegir_TablaVacia_NoSueltaNada()
    {
        Assert.IsNull(CrearTabla(0).Elegir(0f));
    }

    [Test]
    public void ProbabilidadDeSoltarAlgo_EsLaSumaYNoPasaDeUno()
    {
        Assert.AreEqual(0.25f, CrearTabla(0, (TipoRecurso.PilaDeLatas, 0.2f), (TipoRecurso.Bidon, 0.05f)).ProbabilidadDeSoltarAlgo, 0.0001f);
        Assert.AreEqual(1f, CrearTabla(0, (TipoRecurso.PilaDeLatas, 0.8f), (TipoRecurso.Bidon, 0.8f)).ProbabilidadDeSoltarAlgo, 0.0001f);
    }

    // ---------------------------------------------------------------
    // DropRecursosZombie
    // ---------------------------------------------------------------

    [Test]
    public void PuedeSoltar_ZombieNormalConTablaYPrefab_EsTrue()
    {
        DropRecursosZombie drop = CrearDropZombie(CrearTabla(0, (TipoRecurso.PilaDeLatas, 1f)));

        Assert.IsTrue(PuedeSoltar(drop));
    }

    [Test]
    public void PuedeSoltar_ZombieDelTutorial_EsFalse()
    {
        DropRecursosZombie drop = CrearDropZombie(CrearTabla(0, (TipoRecurso.PilaDeLatas, 1f)));
        drop.gameObject.AddComponent<TutorialZombieRespawn>();

        Assert.IsFalse(PuedeSoltar(drop));
    }

    [Test]
    public void PuedeSoltar_SinTabla_EsFalse()
    {
        Assert.IsFalse(PuedeSoltar(CrearDropZombie(null)));
    }

    [Test]
    public void PuedeSoltar_AlLlegarAlTopeDeLaRonda_EsFalse()
    {
        DropRecursosZombie drop = CrearDropZombie(CrearTabla(2, (TipoRecurso.PilaDeLatas, 1f)));

        Invocar(drop, "ContarSoltado");
        Assert.IsTrue(PuedeSoltar(drop), "Con 1 de 2 todavía puede soltar.");

        Invocar(drop, "ContarSoltado");
        Assert.IsFalse(PuedeSoltar(drop), "Con 2 de 2 llegó al tope de la ronda.");
    }

    [Test]
    public void PuedeSoltar_SinTope_SiempreEsTrue()
    {
        DropRecursosZombie drop = CrearDropZombie(CrearTabla(0, (TipoRecurso.PilaDeLatas, 1f)));

        for (int i = 0; i < 50; i++)
            Invocar(drop, "ContarSoltado");

        Assert.IsTrue(PuedeSoltar(drop));
    }

    [Test]
    public void Soltar_SinNetworkManager_NoSueltaNada()
    {
        DropRecursosZombie drop = CrearDropZombie(CrearTabla(0, (TipoRecurso.PilaDeLatas, 1f)));

        Assert.IsNull(drop.Soltar());
    }

    // ---------------------------------------------------------------
    // RecursoDrop: atracción
    // ---------------------------------------------------------------

    [Test]
    public void JugadorQueLoAtrae_SinJugadoresCerca_EsNull()
    {
        RecursoDrop recurso = CrearRecurso(Vector3.zero);
        RegistrarJugador(new Vector3(10f, 0f, 0f));

        Assert.IsNull(recurso.JugadorQueLoAtrae(out _));
    }

    [Test]
    public void JugadorQueLoAtrae_ConJugadorDentroDelRadio_LoDevuelveConElPuntoDeSuCuerpo()
    {
        RecursoDrop recurso = CrearRecurso(Vector3.zero);
        PlayerRecursos jugador = RegistrarJugador(new Vector3(1f, 0f, 0f));

        Assert.AreSame(jugador, recurso.JugadorQueLoAtrae(out Vector3 objetivo));
        Assert.AreEqual(new Vector3(1f, 0.9f, 0f), objetivo);
    }

    [Test]
    public void JugadorQueLoAtrae_ConDosJugadores_EligeElMasCercano()
    {
        RecursoDrop recurso = CrearRecurso(Vector3.zero);
        RegistrarJugador(new Vector3(2f, -0.9f, 0f));
        PlayerRecursos cercano = RegistrarJugador(new Vector3(0.5f, -0.9f, 0f));

        Assert.AreSame(cercano, recurso.JugadorQueLoAtrae(out _));
    }

    [Test]
    public void JugadorQueLoAtrae_JugadorAbatido_NoLoAtrae()
    {
        RecursoDrop recurso = CrearRecurso(Vector3.zero);
        PlayerRecursos jugador = RegistrarJugador(new Vector3(0.5f, -0.9f, 0f));

        PlayerHealth salud = jugador.gameObject.AddComponent<PlayerHealth>();
        salud.State.Value = PlayerHealth.PlayerState.Downed;

        Assert.IsNull(recurso.JugadorQueLoAtrae(out _));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private TablaDropRecursos CrearTabla(int maximoPorRonda, params (TipoRecurso tipo, float probabilidad)[] drops)
    {
        TablaDropRecursos tabla = ScriptableObject.CreateInstance<TablaDropRecursos>();
        creados.Add(tabla);

        var lista = (List<TablaDropRecursos.Drop>)typeof(TablaDropRecursos)
            .GetField("drops", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(tabla);

        foreach (var (tipo, probabilidad) in drops)
            lista.Add(new TablaDropRecursos.Drop { tipo = tipo, probabilidad = probabilidad, cantidad = 1 });

        typeof(TablaDropRecursos)
            .GetField("maximoPorRonda", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(tabla, maximoPorRonda);

        return tabla;
    }

    private DropRecursosZombie CrearDropZombie(TablaDropRecursos tabla)
    {
        GameObject zombie = CrearGameObject("Zombie");
        DropRecursosZombie drop = zombie.AddComponent<DropRecursosZombie>();

        SetCampo(drop, "tabla", tabla);
        SetCampo(drop, "prefabRecurso", CrearRecurso(Vector3.zero));

        return drop;
    }

    private RecursoDrop CrearRecurso(Vector3 posicion)
    {
        GameObject go = CrearGameObject("RecursoDrop");
        go.transform.position = posicion;
        return go.AddComponent<RecursoDrop>();
    }

    // En juego se registran solos al spawnear; acá no hay NetworkManager.
    private PlayerRecursos RegistrarJugador(Vector3 posicion)
    {
        GameObject go = CrearGameObject("Player");
        go.transform.position = posicion;
        PlayerRecursos jugador = go.AddComponent<PlayerRecursos>();

        var jugadores = (List<PlayerRecursos>)typeof(PlayerRecursos)
            .GetField("jugadores", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);
        jugadores.Add(jugador);

        return jugador;
    }

    private GameObject CrearGameObject(string nombre)
    {
        GameObject go = new GameObject(nombre);
        creados.Add(go);
        return go;
    }

    private static bool PuedeSoltar(DropRecursosZombie drop)
    {
        return (bool)Invocar(drop, "PuedeSoltar");
    }

    private static object Invocar(object objeto, string metodo)
    {
        return objeto.GetType()
            .GetMethod(metodo, BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(objeto, null);
    }

    private static void InvocarEstatico(Type tipo, string metodo)
    {
        tipo.GetMethod(metodo, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
    }

    private static void SetCampo(object objeto, string campo, object valor)
    {
        objeto.GetType()
            .GetField(campo, BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(objeto, valor);
    }
}
