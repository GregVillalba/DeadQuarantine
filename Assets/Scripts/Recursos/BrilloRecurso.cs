using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Brillo de los recursos (los del mapa y los que sueltan los zombies) para que se encuentren más fácil y se
/// sepa de lejos cuál es: cada tipo brilla con su color (rojo = pila de latas, gris celeste = cantimplora,
/// verde = bidón).
///
/// Es el mismo recurso visual que el easter egg de la ciudad: la emisión del material (URP Lit, _EMISSION).
/// Cada renderer del recurso recibe una copia de sus materiales con la emisión encendida en un color parejo
/// (sin mapa de emisión), que se suma a la textura: el recurso se tiñe del color de su tipo y late suave entre
/// intensidadMinima e intensidadMaxima. Cada recurso arranca en otro punto del latido para que no titilen juntos.
///
/// Opcionalmente suelta unas partículas leves del mismo color que suben despacio (las usan los del mapa).
/// Son aditivas, así que se siguen viendo aunque la linterna ilumine el recurso.
///
/// Es solo visual: corre igual en cada cliente y no toca la red. RecursoEscenario y RecursoDrop le avisan el
/// tipo con AplicarTipo cuando muestran el modelo. Los valores de cada uno están en su prefab: los dropeados
/// brillan menos que los del mapa porque aparecen seguido y cerca del jugador.
/// </summary>
public class BrilloRecurso : MonoBehaviour
{
    [Header("Color por tipo de recurso")]
    [SerializeField] private Color colorPilaDeLatas = ColorPorDefecto(TipoRecurso.PilaDeLatas);
    [SerializeField] private Color colorCantimplora = ColorPorDefecto(TipoRecurso.Cantimplora);
    [SerializeField] private Color colorBidon = ColorPorDefecto(TipoRecurso.Bidon);

    [Header("Latido del brillo")]
    [Tooltip("Intensidad de la emisión en el punto más bajo del latido.")]
    [SerializeField, Min(0f)] private float intensidadMinima = 0.4f;

    [Tooltip("Intensidad de la emisión en el punto más alto del latido. Más de 1 deja un halo si la escena tiene bloom.")]
    [SerializeField, Min(0f)] private float intensidadMaxima = 1.5f;

    [Tooltip("Latidos por segundo. 0 = brillo fijo en la intensidad máxima.")]
    [SerializeField, Min(0f)] private float latidosPorSegundo = 0.6f;

    [Header("Partículas")]
    [Tooltip("Suelta partículas leves del color del recurso que suben despacio.")]
    [SerializeField] private bool conParticulas;

    [Tooltip("Material de partículas URP aditivo (M_RecursoParticula). Sin material no se crean partículas.")]
    [SerializeField] private Material materialParticulas;

    [Tooltip("Partículas por segundo.")]
    [SerializeField, Min(0f)] private float particulasPorSegundo = 4f;

    [Tooltip("Tamaño de cada partícula (m).")]
    [SerializeField, Min(0f)] private float tamanioParticula = 0.07f;

    [Tooltip("Altura (m) desde el pie del recurso donde nacen las partículas.")]
    [SerializeField] private float alturaParticulas = 0.15f;

    [Tooltip("Radio (m) alrededor del recurso donde nacen las partículas.")]
    [SerializeField, Min(0f)] private float radioParticulas = 0.25f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

    private readonly List<Material> materiales = new List<Material>();
    private ParticleSystem particulas;
    private Color colorActual = Color.white;
    private float desfase;

    public float IntensidadMinima => intensidadMinima;
    public float IntensidadMaxima => intensidadMaxima;
    public bool ConParticulas => conParticulas;
    public Material MaterialParticulas => materialParticulas;
    public Color ColorActual => colorActual;

    /// <summary>Colores pedidos para cada recurso: rojo latas, gris celeste cantimplora, verde bidón.</summary>
    public static Color ColorPorDefecto(TipoRecurso tipo)
    {
        switch (tipo)
        {
            case TipoRecurso.Cantimplora: return new Color(0.6f, 0.8f, 0.95f);
            case TipoRecurso.Bidon: return new Color(0.2f, 1f, 0.3f);
            default: return new Color(1f, 0.15f, 0.1f);
        }
    }

    /// <summary>Color configurado en este prefab para el tipo dado.</summary>
    public Color ColorDe(TipoRecurso tipo)
    {
        switch (tipo)
        {
            case TipoRecurso.Cantimplora: return colorCantimplora;
            case TipoRecurso.Bidon: return colorBidon;
            default: return colorPilaDeLatas;
        }
    }

    private void Awake()
    {
        desfase = Random.value * 10f;

        // Incluye los modelos inactivos: el recurso muestra uno u otro según el tipo, y RecursoDrop apaga
        // el visual cuando parpadea antes de desaparecer.
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer)
                continue;

            // renderer.materials devuelve copias propias de este renderer: el material compartido del
            // escenario (las latas usan el mismo que el easter egg) no se toca.
            Material[] copias = renderer.materials;

            foreach (Material material in copias)
            {
                if (material == null || !material.HasProperty(EmissionColorId))
                    continue;

                PrepararEmision(material);
                materiales.Add(material);
            }
        }

        if (conParticulas && materialParticulas != null)
            particulas = CrearParticulas();

        AplicarIntensidad(Intensidad(0f));
    }

    /// <summary>Pinta el brillo y las partículas con el color del tipo (lo llaman RecursoEscenario y RecursoDrop).</summary>
    public void AplicarTipo(TipoRecurso tipo)
    {
        colorActual = ColorDe(tipo);

        if (particulas != null)
        {
            ParticleSystem.MainModule main = particulas.main;
            main.startColor = colorActual;
            // Las que ya estaban en el aire quedan del color anterior: se limpian para que no se mezclen.
            particulas.Clear();
        }

        AplicarIntensidad(Intensidad(Time.time));
    }

    private void Update()
    {
        AplicarIntensidad(Intensidad(Time.time));
    }

    private void OnDestroy()
    {
        foreach (Material material in materiales)
        {
            if (material != null)
                Destroy(material);
        }

        materiales.Clear();
    }

    private float Intensidad(float tiempo)
    {
        return IntensidadEn(tiempo + desfase, intensidadMinima, intensidadMaxima, latidosPorSegundo);
    }

    /// <summary>
    /// Intensidad del latido en un momento dado: va y vuelve entre min y max con forma de seno.
    /// Sin latidos (latidosPorSegundo &lt;= 0) queda fija en max.
    /// </summary>
    public static float IntensidadEn(float tiempo, float min, float max, float latidosPorSegundo)
    {
        if (latidosPorSegundo <= 0f)
            return max;

        float onda = 0.5f + 0.5f * Mathf.Sin(tiempo * latidosPorSegundo * 2f * Mathf.PI);
        return Mathf.Lerp(min, max, onda);
    }

    private static void PrepararEmision(Material material)
    {
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

        // Sin mapa de emisión el color es parejo en todo el objeto y se suma a su textura. El mapa del atlas
        // original es casi todo negro (dejaría al recurso apagado) y usar su propia textura lo ensuciaría:
        // una textura roja por un brillo verde da casi negro.
        if (material.HasProperty(EmissionMapId))
            material.SetTexture(EmissionMapId, null);
    }

    private void AplicarIntensidad(float intensidad)
    {
        Color emision = colorActual * intensidad;
        emision.a = 1f;

        foreach (Material material in materiales)
        {
            if (material != null)
                material.SetColor(EmissionColorId, emision);
        }
    }

    // Partículas chicas que nacen alrededor del recurso, suben despacio y se apagan. Se arman por código
    // para poder pintarlas con el color del tipo y no depender de un efecto de un pack.
    private ParticleSystem CrearParticulas()
    {
        GameObject objeto = new GameObject("Particulas_Brillo");
        objeto.transform.SetParent(transform, false);
        objeto.transform.localPosition = Vector3.up * alturaParticulas;

        ParticleSystem sistema = objeto.AddComponent<ParticleSystem>();
        sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = sistema.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(tamanioParticula * 0.6f, tamanioParticula);
        main.startColor = colorActual;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 16;

        ParticleSystem.EmissionModule emision = sistema.emission;
        emision.rateOverTime = particulasPorSegundo;

        ParticleSystem.ShapeModule forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = radioParticulas;
        forma.radiusThickness = 1f;

        // Las tres curvas tienen que estar en el mismo modo (dos constantes).
        ParticleSystem.VelocityOverLifetimeModule velocidad = sistema.velocityOverLifetime;
        velocidad.enabled = true;
        velocidad.space = ParticleSystemSimulationSpace.World;
        velocidad.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        velocidad.y = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        velocidad.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);

        ParticleSystem.ColorOverLifetimeModule colorVida = sistema.colorOverLifetime;
        colorVida.enabled = true;
        Gradient degradado = new Gradient();
        degradado.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        colorVida.color = degradado;

        ParticleSystem.SizeOverLifetimeModule tamanioVida = sistema.sizeOverLifetime;
        tamanioVida.enabled = true;
        tamanioVida.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

        ParticleSystemRenderer render = objeto.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = materialParticulas;
        render.renderMode = ParticleSystemRenderMode.Billboard;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;

        sistema.Play();
        return sistema;
    }
}
