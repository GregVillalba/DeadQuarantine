using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

[TestFixture]
public class RecursosEscenarioTests
{
    private static readonly string[] EscenasPartida =
    {
        "Assets/Scenes/MainSceneSinglePlayer.unity",
        "Assets/Scenes/MainSceneMultiPlayer.unity"
    };

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
    }

    // ---------------------------------------------------------------
    // Nombres y cartel (US 19.2)
    // ---------------------------------------------------------------

    [Test]
    public void Nombre_DeCadaRecurso_EsElQueVeElJugador()
    {
        Assert.AreEqual("Pila de latas", TipoRecurso.PilaDeLatas.Nombre());
        Assert.AreEqual("Cantimplora", TipoRecurso.Cantimplora.Nombre());
        Assert.AreEqual("Bidón", TipoRecurso.Bidon.Nombre());
    }

    [Test]
    public void TextoCartel_MuestraTeclaYRecurso()
    {
        Assert.AreEqual("E para recoger Pila de latas", InteraccionRecursoEscenario.TextoCartel("E", TipoRecurso.PilaDeLatas));
        Assert.AreEqual("F para recoger Bidón", InteraccionRecursoEscenario.TextoCartel("F", TipoRecurso.Bidon));
    }

    [Test]
    public void CadaRecurso_ValeUnaUnidad()
    {
        Assert.AreEqual(1, RecursoEscenario.CantidadPorRecurso);
    }

    [Test]
    public void EstaAlAlcance_CercaSi_LejosNo()
    {
        Assert.IsTrue(RecursoEscenario.EstaAlAlcance(Vector3.zero, new Vector3(3f, 0f, 0f), 4.5f));
        Assert.IsFalse(RecursoEscenario.EstaAlAlcance(Vector3.zero, new Vector3(5f, 0f, 0f), 4.5f));
    }

    // ---------------------------------------------------------------
    // Opciones de puntos (US 19.1)
    // ---------------------------------------------------------------

    [Test]
    public void OpcionCon8_4_2_EstaCompleta()
    {
        OpcionRecursosEscenario opcion = CrearOpcion(8, 4, 2);

        Assert.IsTrue(opcion.EstaCompleta);
        Assert.AreEqual(14, opcion.Puntos.Length);
        Assert.AreEqual(8, opcion.Cantidad(TipoRecurso.PilaDeLatas));
        Assert.AreEqual(4, opcion.Cantidad(TipoRecurso.Cantimplora));
        Assert.AreEqual(2, opcion.Cantidad(TipoRecurso.Bidon));
    }

    [Test]
    public void OpcionConPuntosDeMasODeMenos_NoEstaCompleta()
    {
        Assert.IsFalse(CrearOpcion(7, 4, 2).EstaCompleta);
        Assert.IsFalse(CrearOpcion(8, 5, 2).EstaCompleta);
        Assert.IsFalse(CrearOpcion(8, 4, 0).EstaCompleta);
    }

    [Test]
    public void Resumen_DiceCuantosHayDeCadaTipo()
    {
        Assert.AreEqual("Pila de latas 8/8, Cantimplora 3/4, Bidón 2/2", CrearOpcion(8, 3, 2).Resumen());
    }

    [Test]
    public void ElegirOpcion_ReparteLaTiradaEntreLasTres()
    {
        Assert.AreEqual(0, GestorRecursosEscenario.ElegirOpcion(3, 0f));
        Assert.AreEqual(0, GestorRecursosEscenario.ElegirOpcion(3, 0.33f));
        Assert.AreEqual(1, GestorRecursosEscenario.ElegirOpcion(3, 0.34f));
        Assert.AreEqual(2, GestorRecursosEscenario.ElegirOpcion(3, 0.99f));
        Assert.AreEqual(2, GestorRecursosEscenario.ElegirOpcion(3, 1f));
    }

    [Test]
    public void ElegirOpcion_SinOpciones_DevuelveMenosUno()
    {
        Assert.AreEqual(-1, GestorRecursosEscenario.ElegirOpcion(0, 0.5f));
    }

    // ---------------------------------------------------------------
    // Reaparición a los 240 s
    // ---------------------------------------------------------------

    [Test]
    public void PuntoRecolectado_ReapareceRecienA240Segundos()
    {
        GestorRecursosEscenario.PuntoEnUso punto = new GestorRecursosEscenario.PuntoEnUso(null);

        punto.EmpezarEspera(100f, 240f);

        Assert.IsTrue(punto.Esperando);
        Assert.IsFalse(punto.TerminoLaEspera(339.9f));
        Assert.IsTrue(punto.TerminoLaEspera(340f));
    }

    [Test]
    public void CadaPunto_CuentaSuPropioTiempo()
    {
        GestorRecursosEscenario.PuntoEnUso primero = new GestorRecursosEscenario.PuntoEnUso(null);
        GestorRecursosEscenario.PuntoEnUso segundo = new GestorRecursosEscenario.PuntoEnUso(null);

        primero.EmpezarEspera(0f, 240f);
        segundo.EmpezarEspera(100f, 240f);

        Assert.IsTrue(primero.TerminoLaEspera(240f));
        Assert.IsFalse(segundo.TerminoLaEspera(240f));
        Assert.IsTrue(segundo.TerminoLaEspera(340f));
    }

    [Test]
    public void PuntoConSuRecurso_NoEstaEsperando()
    {
        GestorRecursosEscenario.PuntoEnUso punto = new GestorRecursosEscenario.PuntoEnUso(null);

        punto.EmpezarEspera(0f, 240f);
        punto.Reaparecio(null);

        Assert.IsFalse(punto.Esperando);
        Assert.IsFalse(punto.TerminoLaEspera(1000f));
    }

    // ---------------------------------------------------------------
    // Escenas: los puntos los ubica el equipo en el editor. Estos tests fallan hasta que estén todos.
    // ---------------------------------------------------------------

    [Test]
    public void CadaEscenario_Tiene3OpcionesCon8_4_2Puntos([ValueSource(nameof(EscenasPartida))] string escena)
    {
        EditorSceneManager.OpenScene(escena, OpenSceneMode.Single);
        List<string> errores = new List<string>();

        foreach (ConfiguracionEscenarioJugable escenario in Object.FindObjectsByType<ConfiguracionEscenarioJugable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            OpcionRecursosEscenario[] opciones = escenario.OpcionesRecursos;
            int cantidad = opciones != null ? opciones.Length : 0;

            if (cantidad != OpcionRecursosEscenario.OpcionesPorEscenario)
            {
                errores.Add(escenario.TipoEscenario + ": tiene " + cantidad + " opciones (van " + OpcionRecursosEscenario.OpcionesPorEscenario + ")");
                continue;
            }

            for (int i = 0; i < opciones.Length; i++)
            {
                if (opciones[i] == null)
                    errores.Add(escenario.TipoEscenario + ": la opción " + (i + 1) + " está vacía");
                else if (!opciones[i].EstaCompleta)
                    errores.Add(escenario.TipoEscenario + ", opción " + (i + 1) + ": " + opciones[i].Resumen());
            }
        }

        Assert.IsEmpty(errores, string.Join("\n", errores));
    }

    [Test]
    public void TodosLosPuntos_SeAlcanzanCaminandoDesdeElSpawn([ValueSource(nameof(EscenasPartida))] string escena)
    {
        EditorSceneManager.OpenScene(escena, OpenSceneMode.Single);
        List<string> errores = new List<string>();
        int revisados = 0;

        foreach (ConfiguracionEscenarioJugable escenario in Object.FindObjectsByType<ConfiguracionEscenarioJugable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (escenario.SpawnJugador == null || escenario.OpcionesRecursos == null)
                continue;

            if (!NavMesh.SamplePosition(escenario.SpawnJugador.position, out NavMeshHit inicio, 2f, NavMesh.AllAreas))
                continue;

            foreach (OpcionRecursosEscenario opcion in escenario.OpcionesRecursos)
            {
                if (opcion == null)
                    continue;

                foreach (PuntoRecursoEscenario punto in opcion.Puntos)
                {
                    revisados++;
                    string nombre = escenario.TipoEscenario + "/" + opcion.name + "/" + punto.name;

                    if (!NavMesh.SamplePosition(punto.transform.position, out NavMeshHit destino, 2f, NavMesh.AllAreas))
                    {
                        errores.Add(nombre + ": no hay piso caminable cerca");
                        continue;
                    }

                    NavMeshPath camino = new NavMeshPath();

                    if (!NavMesh.CalculatePath(inicio.position, destino.position, NavMesh.AllAreas, camino) ||
                        camino.status != NavMeshPathStatus.PathComplete)
                    {
                        errores.Add(nombre + ": no se llega caminando desde el spawn");
                    }
                }
            }
        }

        if (revisados == 0)
            Assert.Inconclusive("No hay puntos ubicados o la escena no tiene NavMesh cargado en el editor.");

        Assert.IsEmpty(errores, string.Join("\n", errores));
    }

    // ---------------------------------------------------------------

    private OpcionRecursosEscenario CrearOpcion(int latas, int cantimploras, int bidones)
    {
        GameObject objeto = new GameObject("Opcion");
        creados.Add(objeto);
        OpcionRecursosEscenario opcion = objeto.AddComponent<OpcionRecursosEscenario>();

        AgregarPuntos(objeto.transform, TipoRecurso.PilaDeLatas, latas);
        AgregarPuntos(objeto.transform, TipoRecurso.Cantimplora, cantimploras);
        AgregarPuntos(objeto.transform, TipoRecurso.Bidon, bidones);

        return opcion;
    }

    private static void AgregarPuntos(Transform padre, TipoRecurso tipo, int cantidad)
    {
        for (int i = 0; i < cantidad; i++)
        {
            GameObject punto = new GameObject(tipo + "_" + i);
            punto.transform.SetParent(padre);
            punto.AddComponent<PuntoRecursoEscenario>().Configurar(tipo);
        }
    }
}
