using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Brillo de los recursos del mapa y de los dropeados (CA de recursos).</summary>
[TestFixture]
public class BrilloRecursoTests
{
    private const string PrefabEscenario = "Assets/Prefabs/RecursoEscenario.prefab";
    private const string PrefabDrop = "Assets/Prefabs/RecursoDrop.prefab";

    [Test]
    public void IntensidadEn_SiempreQuedaEntreMinimoYMaximo()
    {
        for (float t = 0f; t < 5f; t += 0.05f)
        {
            float intensidad = BrilloRecurso.IntensidadEn(t, 0.3f, 1f, 0.6f);
            Assert.GreaterOrEqual(intensidad, 0.3f - 0.0001f);
            Assert.LessOrEqual(intensidad, 1f + 0.0001f);
        }
    }

    [Test]
    public void IntensidadEn_LlegaAlMaximoYAlMinimoDelLatido()
    {
        // 1 latido por segundo: el seno tiene su pico a los 0,25 s y su valle a los 0,75 s.
        Assert.AreEqual(1f, BrilloRecurso.IntensidadEn(0.25f, 0.2f, 1f, 1f), 0.0001f);
        Assert.AreEqual(0.2f, BrilloRecurso.IntensidadEn(0.75f, 0.2f, 1f, 1f), 0.0001f);
    }

    [Test]
    public void IntensidadEn_SinLatido_QuedaFijaEnElMaximo()
    {
        Assert.AreEqual(0.8f, BrilloRecurso.IntensidadEn(3.7f, 0.1f, 0.8f, 0f));
    }

    [Test]
    public void LosDosPrefabsDeRecursos_TienenBrillo()
    {
        Assert.IsNotNull(Brillo(PrefabEscenario), "El recurso del mapa no tiene BrilloRecurso.");
        Assert.IsNotNull(Brillo(PrefabDrop), "El recurso dropeado no tiene BrilloRecurso.");
    }

    [Test]
    public void ElDropeado_BrillaMenosQueElDelMapa()
    {
        BrilloRecurso escenario = Brillo(PrefabEscenario);
        BrilloRecurso drop = Brillo(PrefabDrop);

        Assert.Less(drop.IntensidadMaxima, escenario.IntensidadMaxima);
        Assert.Less(drop.IntensidadMinima, escenario.IntensidadMinima);
        Assert.Greater(drop.IntensidadMaxima, 0f, "El dropeado tiene que brillar aunque sea un poco.");
    }

    [Test]
    public void ColorPorDefecto_RojoLatas_GrisCelesteCantimplora_VerdeBidon()
    {
        Color latas = BrilloRecurso.ColorPorDefecto(TipoRecurso.PilaDeLatas);
        Color cantimplora = BrilloRecurso.ColorPorDefecto(TipoRecurso.Cantimplora);
        Color bidon = BrilloRecurso.ColorPorDefecto(TipoRecurso.Bidon);

        Assert.That(latas.r > latas.g && latas.r > latas.b, "Las latas tienen que brillar en rojo.");
        Assert.That(bidon.g > bidon.r && bidon.g > bidon.b, "El bidón tiene que brillar en verde.");
        Assert.That(cantimplora.b >= cantimplora.g && cantimplora.g > cantimplora.r && cantimplora.r > 0.4f,
            "La cantimplora tiene que brillar en gris tirando a celeste.");
    }

    [Test]
    public void LosDosPrefabs_UsanLosMismosColoresPorTipo()
    {
        BrilloRecurso escenario = Brillo(PrefabEscenario);
        BrilloRecurso drop = Brillo(PrefabDrop);

        foreach (TipoRecurso tipo in new[] { TipoRecurso.PilaDeLatas, TipoRecurso.Cantimplora, TipoRecurso.Bidon })
        {
            Assert.AreEqual(BrilloRecurso.ColorPorDefecto(tipo), escenario.ColorDe(tipo), "Mapa: " + tipo);
            Assert.AreEqual(BrilloRecurso.ColorPorDefecto(tipo), drop.ColorDe(tipo), "Drop: " + tipo);
        }
    }

    [Test]
    public void SoloElDelMapa_SueltaParticulas()
    {
        BrilloRecurso escenario = Brillo(PrefabEscenario);

        Assert.IsTrue(escenario.ConParticulas);
        Assert.IsNotNull(escenario.MaterialParticulas, "Sin material las partículas no se crean.");
        Assert.IsFalse(Brillo(PrefabDrop).ConParticulas);
    }

    private static BrilloRecurso Brillo(string ruta)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        Assert.IsNotNull(prefab, "No se encontró " + ruta);
        return prefab.GetComponent<BrilloRecurso>();
    }
}
