using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PvP (Team Deathmatch) reutiliza los 3 mapas pero no tiene comercio: el mercader se oculta
/// (MercaderSegunModo) y no aparecen recursos del mapa ni de los zombies (RecursosSegunModo).
/// </summary>
[TestFixture]
public class RecursosSegunModoTests
{
    private const ModosTienda RondasYSupervivencia = ModosTienda.Ronda5 | ModosTienda.Supervivencia;

    // ---------------------------------------------------------------
    // DisponibilidadTienda: flag del modo actual
    // ---------------------------------------------------------------

    [TestCase(ConfiguracionPartidaSeleccionada.ModoJuego.Ninguno)]
    [TestCase(ConfiguracionPartidaSeleccionada.ModoJuego.Historia)]
    [TestCase(ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5)]
    [TestCase(ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia)]
    public void AFlag_EnPvP_EsPvPSinImportarElModoDelMenu(ConfiguracionPartidaSeleccionada.ModoJuego modo)
    {
        Assert.AreEqual(ModosTienda.PvP, DisponibilidadTienda.AFlag(modo, true));
    }

    [TestCase(ConfiguracionPartidaSeleccionada.ModoJuego.Historia, ModosTienda.Historia)]
    [TestCase(ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5, ModosTienda.Ronda5)]
    [TestCase(ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia, ModosTienda.Supervivencia)]
    public void AFlag_FueraDePvP_EsElDelModo(ConfiguracionPartidaSeleccionada.ModoJuego modo, ModosTienda esperado)
    {
        Assert.AreEqual(esperado, DisponibilidadTienda.AFlag(modo, false));
    }

    [Test]
    public void AFlag_SinModoYFueraDePvP_EsCero()
    {
        Assert.AreEqual((ModosTienda)0, DisponibilidadTienda.AFlag(ConfiguracionPartidaSeleccionada.ModoJuego.Ninguno, false));
    }

    [Test]
    public void TodosLosModos_NoIncluyePvP()
    {
        Assert.AreEqual((ModosTienda)0, DisponibilidadTienda.TodosLosModos & ModosTienda.PvP);
    }

    [Test]
    public void SinPartidaEnCurso_NoEsPvP()
    {
        Assert.IsFalse(DisponibilidadTienda.EsPvP);
    }

    // ---------------------------------------------------------------
    // MercaderSegunModo
    // ---------------------------------------------------------------

    [TestCase(RondasYSupervivencia)]
    [TestCase(ModosTienda.Historia)]
    [TestCase(DisponibilidadTienda.TodosLosModos)]
    public void Mercader_EnPvP_NoSeVe(ModosTienda modosVisibles)
    {
        Assert.IsFalse(MercaderSegunModo.SeVeEn(modosVisibles, ModosTienda.PvP));
    }

    [Test]
    public void Mercader_ConPvPMarcado_SeVeEnPvP()
    {
        Assert.IsTrue(MercaderSegunModo.SeVeEn(RondasYSupervivencia | ModosTienda.PvP, ModosTienda.PvP));
    }

    [Test]
    public void Mercader_EnSusModos_SeVe_YEnLosDemasNo()
    {
        Assert.IsTrue(MercaderSegunModo.SeVeEn(RondasYSupervivencia, ModosTienda.Ronda5));
        Assert.IsTrue(MercaderSegunModo.SeVeEn(RondasYSupervivencia, ModosTienda.Supervivencia));
        Assert.IsFalse(MercaderSegunModo.SeVeEn(RondasYSupervivencia, ModosTienda.Historia));
    }

    [Test]
    public void Mercader_SinModo_NoSeOculta()
    {
        Assert.IsTrue(MercaderSegunModo.SeVeEn(RondasYSupervivencia, 0));
    }

    // ---------------------------------------------------------------
    // RecursosSegunModo
    // ---------------------------------------------------------------

    [Test]
    public void Recursos_PorDefecto_NoHayEnPvP()
    {
        Assert.IsFalse(RecursosSegunModo.HayRecursosEn(RecursosSegunModo.ModosPorDefecto, ModosTienda.PvP));
    }

    [TestCase(ModosTienda.Historia)]
    [TestCase(ModosTienda.Ronda5)]
    [TestCase(ModosTienda.Supervivencia)]
    public void Recursos_PorDefecto_HayEnLosModosOriginales(ModosTienda modo)
    {
        Assert.IsTrue(RecursosSegunModo.HayRecursosEn(RecursosSegunModo.ModosPorDefecto, modo));
    }

    [Test]
    public void Recursos_SinModo_SiempreHay()
    {
        Assert.IsTrue(RecursosSegunModo.HayRecursosEn(RecursosSegunModo.ModosPorDefecto, 0));
        Assert.IsTrue(RecursosSegunModo.HayRecursosEn(0, 0));
    }

    [Test]
    public void Recursos_ConUnModoDesmarcado_NoHayEnEseModo()
    {
        Assert.IsFalse(RecursosSegunModo.HayRecursosEn(RondasYSupervivencia, ModosTienda.Historia));
    }

    [Test]
    public void Recursos_SinPartidaEnCurso_Hay()
    {
        Assert.IsTrue(RecursosSegunModo.HayRecursosEnPartidaActual);
    }

    // ---------------------------------------------------------------
    // Escenas de partida
    // ---------------------------------------------------------------

    [TestCase("Assets/Scenes/MainSceneSinglePlayer.unity")]
    [TestCase("Assets/Scenes/MainSceneMultiPlayer.unity")]
    [TestCase("Assets/PvPvE/Scenes/PvPvEMatch.unity")]
    public void Escena_ElGestorDeRecursos_TieneRecursosSegunModoSinPvP(string ruta)
    {
        Scene escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);

        try
        {
            int gestores = 0;

            foreach (GameObject raiz in escena.GetRootGameObjects())
            {
                foreach (GestorRecursosEscenario gestor in raiz.GetComponentsInChildren<GestorRecursosEscenario>(true))
                {
                    gestores++;

                    RecursosSegunModo segunModo = gestor.GetComponent<RecursosSegunModo>();
                    Assert.IsNotNull(segunModo, "Falta RecursosSegunModo al lado de GestorRecursosEscenario en " + ruta);

                    ModosTienda modos = (ModosTienda)new SerializedObject(segunModo).FindProperty("modosConRecursos").intValue;
                    Assert.IsFalse(RecursosSegunModo.HayRecursosEn(modos, ModosTienda.PvP), "En PvP no tiene que haber recursos (" + ruta + ")");
                    Assert.IsTrue(RecursosSegunModo.HayRecursosEn(modos, ModosTienda.Supervivencia), "Supervivencia tiene que conservar los recursos (" + ruta + ")");
                }
            }

            Assert.AreEqual(1, gestores, "Se esperaba un GestorRecursosEscenario en " + ruta);
        }
        finally
        {
            EditorSceneManager.CloseScene(escena, true);
        }
    }

    [Test]
    public void EscenaPvP_NingunMercaderSeVeEnPvP()
    {
        const string ruta = "Assets/PvPvE/Scenes/PvPvEMatch.unity";
        Scene escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);

        try
        {
            int mercaderes = 0;

            foreach (GameObject raiz in escena.GetRootGameObjects())
            {
                foreach (NPCWeaponVendor vendedor in raiz.GetComponentsInChildren<NPCWeaponVendor>(true))
                {
                    mercaderes++;

                    MercaderSegunModo segunModo = vendedor.GetComponentInParent<MercaderSegunModo>(true);
                    Assert.IsNotNull(segunModo, "El mercader '" + vendedor.name + "' no tiene MercaderSegunModo: quedaría a la vista en PvP");

                    ModosTienda modos = (ModosTienda)new SerializedObject(segunModo).FindProperty("modosVisibles").intValue;
                    Assert.IsFalse(MercaderSegunModo.SeVeEn(modos, ModosTienda.PvP), "El mercader '" + vendedor.name + "' se vería en PvP");
                }
            }

            Assert.Greater(mercaderes, 0, "La escena PvP reutiliza los mapas: tendría que traer sus mercaderes");
        }
        finally
        {
            EditorSceneManager.CloseScene(escena, true);
        }
    }
}
