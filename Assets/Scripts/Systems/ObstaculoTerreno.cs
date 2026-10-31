using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum TipoObstaculo
{
    CharcoDeBarro,
    AlambreDePuas
}

/// <summary>
/// Obstáculo del escenario que ralentiza (charco de barro) o ralentiza y lastima (alambre de púas).
///
/// Cómo armarlo en la escena: agregar este componente al modelo (alambre, charco). Si el objeto no tiene
/// un Collider que sirva (Box, Sphere, Capsule o Mesh convexo), al empezar la partida se crea solo una caja
/// del tamaño del modelo. Para ajustar la zona a mano, agregarle un BoxCollider al mismo objeto.
/// La zona se pone como Trigger, así se puede caminar por encima. Si el modelo tiene otros colliders
/// sólidos, conviene sacarlos o achicarlos.
///
/// - Jugadores: PlayerMovement (del dueño) pregunta cada cuadro si está dentro de algún obstáculo.
///   La ralentización depende de la clase: el % se multiplica por la velocidad del personaje
///   (Pesado 70% → 17,5%; Normal → 25%; Ligero 115% → 28,75%), así los lentos no quedan casi quietos.
/// - Zombies: ZombieAI (servidor) los ralentiza menos en el barro. El alambre lo rodean: se agrega un
///   NavMeshObstacle que recorta el NavMesh, así no los pueden hacer pasar por encima.
/// </summary>
public class ObstaculoTerreno : MonoBehaviour
{
    [SerializeField] private TipoObstaculo tipo = TipoObstaculo.CharcoDeBarro;

    [Tooltip("Zona del obstáculo: un Collider de ESTE objeto o de sus hijos (Box, Sphere, Capsule o Mesh convexo). " +
             "Vacío = se usa el Collider del objeto o, si no tiene, se crea una caja del tamaño del modelo.")]
    [SerializeField] private Collider zona;

    [Tooltip("Alto mínimo (m) de la caja que se crea sola: un charco plano igual tiene que alcanzar los pies.")]
    [SerializeField] private float alturaMinimaZona = 1f;

    [Tooltip("Margen (m) alrededor de la zona: cuenta como adentro aunque los pies estén apenas por encima.")]
    [SerializeField] private float margen = 0.25f;

    [Header("Jugadores")]
    [Tooltip("Cuánto ralentiza a un personaje de velocidad Normal (0.25 = 25%).")]
    [Range(0f, 0.9f)] [SerializeField] private float reduccionJugador = 0.25f;
    [Tooltip("La ralentización se multiplica por la velocidad de la clase: afecta menos a los más lentos.")]
    [SerializeField] private bool escalarConClase = true;

    [Header("Zombies")]
    [Tooltip("Ralentización para zombies que caminan (menor que la del jugador para que no se aproveche).")]
    [Range(0f, 0.9f)] [SerializeField] private float reduccionZombieCaminando = 0.10f;
    [Tooltip("Ralentización para zombies que corren.")]
    [Range(0f, 0.9f)] [SerializeField] private float reduccionZombieCorriendo = 0.15f;

    [Header("Alambre de púas")]
    [Tooltip("Daño al tocar el alambre.")]
    [Min(0)] [SerializeField] private int dano = 10;
    [Tooltip("Mientras sigue adentro, vuelve a lastimar cada estos segundos. 0 = solo al entrar.")]
    [Min(0f)] [SerializeField] private float intervaloDano = 1f;
    [Tooltip("El chaleco y el casco absorben primero el daño del alambre. Desmarcado = va directo a la vida.")]
    [SerializeField] private bool escudoAbsorbe = true;
    [Tooltip("Los zombies rodean el alambre (NavMeshObstacle con carving). Si el alambre cierra todo un paso, quedan trabados.")]
    [SerializeField] private bool zombiesLoRodean = true;

    private static readonly List<ObstaculoTerreno> activos = new List<ObstaculoTerreno>();

    public TipoObstaculo Tipo => tipo;
    public int Dano => tipo == TipoObstaculo.AlambreDePuas ? dano : 0;
    public float IntervaloDano => intervaloDano;
    public bool IgnoraEscudo => !escudoAbsorbe;

    private void Awake()
    {
        zona = ResolverZona();

        if (zona == null)
            return;

        zona.isTrigger = true;

        if (tipo == TipoObstaculo.AlambreDePuas && zombiesLoRodean)
            ConfigurarRodeoZombies();
    }

    private static bool SirveComoZona(Collider c)
    {
        return c is BoxCollider || c is SphereCollider || c is CapsuleCollider ||
               (c is MeshCollider mesh && mesh.convex);
    }

    // La zona asignada (si es de este objeto) → un Collider propio que sirva → una caja creada del tamaño del modelo.
    private Collider ResolverZona()
    {
        if (zona != null && !zona.transform.IsChildOf(transform))
        {
            // Un collider de otro objeto no marca dónde está ESTE obstáculo (y no hay que tocarle el Trigger).
            Debug.LogWarning("[ObstaculoTerreno] La Zona de '" + name + "' es un collider de otro objeto ('" +
                             zona.name + "'): se ignora y se usa una zona del propio obstáculo.", this);
            zona = null;
        }

        if (zona != null)
        {
            if (SirveComoZona(zona))
                return zona;

            Debug.LogWarning("[ObstaculoTerreno] La Zona de '" + name + "' tiene que ser Box, Sphere, Capsule o Mesh convexo: " +
                             "se usa otra del propio obstáculo.", this);
        }

        foreach (Collider propio in GetComponents<Collider>())
        {
            if (SirveComoZona(propio))
                return propio;
        }

        return CrearZonaAutomatica();
    }

    // Caja trigger que cubre todos los renderers del modelo (en espacio local), con un alto mínimo desde abajo.
    private Collider CrearZonaAutomatica()
    {
        Renderer[] renders = GetComponentsInChildren<Renderer>();

        if (renders.Length == 0)
        {
            Debug.LogWarning("[ObstaculoTerreno] '" + name + "' no tiene Collider ni modelo: agregale un BoxCollider que cubra la zona.", this);
            return null;
        }

        bool primero = true;
        Bounds local = new Bounds();

        foreach (Renderer render in renders)
        {
            Bounds mundo = render.bounds;

            for (int i = 0; i < 8; i++)
            {
                Vector3 esquina = new Vector3(
                    (i & 1) == 0 ? mundo.min.x : mundo.max.x,
                    (i & 2) == 0 ? mundo.min.y : mundo.max.y,
                    (i & 4) == 0 ? mundo.min.z : mundo.max.z);

                Vector3 enLocal = transform.InverseTransformPoint(esquina);

                if (primero)
                {
                    local = new Bounds(enLocal, Vector3.zero);
                    primero = false;
                }
                else
                {
                    local.Encapsulate(enLocal);
                }
            }
        }

        float altoMinimo = alturaMinimaZona / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));

        if (local.size.y < altoMinimo)
        {
            float abajo = local.min.y;
            local.SetMinMax(
                new Vector3(local.min.x, abajo, local.min.z),
                new Vector3(local.max.x, abajo + altoMinimo, local.max.z));
        }

        BoxCollider caja = gameObject.AddComponent<BoxCollider>();
        caja.center = local.center;
        caja.size = local.size;
        caja.isTrigger = true;
        return caja;
    }

    private void OnEnable()
    {
        activos.Add(this);
    }

    private void OnDisable()
    {
        activos.Remove(this);
    }

    // Recorta el NavMesh con la forma de la zona: los zombies arman el camino por afuera.
    private void ConfigurarRodeoZombies()
    {
        NavMeshObstacle obstaculo = GetComponent<NavMeshObstacle>();

        // Si ya tenía uno configurado a mano, se respeta su forma.
        if (obstaculo == null)
        {
            obstaculo = gameObject.AddComponent<NavMeshObstacle>();
            obstaculo.shape = NavMeshObstacleShape.Box;

            if (zona is BoxCollider caja && caja.transform == transform)
            {
                obstaculo.center = caja.center;
                obstaculo.size = caja.size;
            }
            else
            {
                Bounds limites = zona.bounds;
                Vector3 escala = transform.lossyScale;
                obstaculo.center = transform.InverseTransformPoint(limites.center);
                obstaculo.size = new Vector3(
                    limites.size.x / Mathf.Max(0.0001f, Mathf.Abs(escala.x)),
                    limites.size.y / Mathf.Max(0.0001f, Mathf.Abs(escala.y)),
                    limites.size.z / Mathf.Max(0.0001f, Mathf.Abs(escala.z)));
            }
        }

        obstaculo.carving = true;
        obstaculo.carveOnlyStationary = true;
    }

    /// <summary>true si el punto (los pies del personaje) está dentro de la zona.</summary>
    public bool Contiene(Vector3 punto)
    {
        if (zona == null || !zona.enabled || !zona.gameObject.activeInHierarchy)
            return false;

        Bounds limites = zona.bounds;
        limites.Expand(margen * 2f);

        if (!limites.Contains(punto))
            return false;

        return (zona.ClosestPoint(punto) - punto).sqrMagnitude <= margen * margen;
    }

    /// <summary>Velocidad que le queda al jugador (1 = sin cambios). multiplicadorClase: 0.7 Pesado, 1 Normal, 1.15 Ligero.</summary>
    public float MultiplicadorJugador(float multiplicadorClase)
    {
        float reduccion = reduccionJugador * (escalarConClase ? multiplicadorClase : 1f);
        return 1f - Mathf.Clamp(reduccion, 0f, 0.9f);
    }

    public float MultiplicadorZombie(bool corriendo)
    {
        return 1f - (corriendo ? reduccionZombieCorriendo : reduccionZombieCaminando);
    }

    /// <summary>
    /// Velocidad que le queda al jugador en ese punto (los obstáculos no se suman: vale el que más frena).
    /// enBarro / alambre: en qué obstáculos está (alambre = el que lo lastima, o null).
    /// </summary>
    public static float MultiplicadorJugadorEn(Vector3 punto, float multiplicadorClase, out bool enBarro, out ObstaculoTerreno alambre)
    {
        float multiplicador = 1f;
        enBarro = false;
        alambre = null;

        for (int i = 0; i < activos.Count; i++)
        {
            ObstaculoTerreno obstaculo = activos[i];

            if (!obstaculo.Contiene(punto))
                continue;

            multiplicador = Mathf.Min(multiplicador, obstaculo.MultiplicadorJugador(multiplicadorClase));

            if (obstaculo.tipo == TipoObstaculo.CharcoDeBarro)
                enBarro = true;
            else if (alambre == null)
                alambre = obstaculo;
        }

        return multiplicador;
    }

    /// <summary>Velocidad que le queda al zombie en ese punto (1 = sin cambios).</summary>
    public static float MultiplicadorZombieEn(Vector3 punto, bool corriendo)
    {
        float multiplicador = 1f;

        for (int i = 0; i < activos.Count; i++)
        {
            if (activos[i].Contiene(punto))
                multiplicador = Mathf.Min(multiplicador, activos[i].MultiplicadorZombie(corriendo));
        }

        return multiplicador;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = tipo == TipoObstaculo.CharcoDeBarro
            ? new Color(0.55f, 0.35f, 0.15f, 0.35f)
            : new Color(0.9f, 0.1f, 0.1f, 0.35f);

        Collider c = zona != null && zona.transform.IsChildOf(transform) ? zona : GetComponent<Collider>();

        if (c != null)
        {
            Gizmos.DrawCube(c.bounds.center, c.bounds.size);
            return;
        }

        // Todavía sin collider: muestra aproximadamente la caja que se va a crear al empezar la partida.
        Renderer[] renders = GetComponentsInChildren<Renderer>();
        if (renders.Length == 0)
            return;

        Bounds limites = renders[0].bounds;
        for (int i = 1; i < renders.Length; i++)
            limites.Encapsulate(renders[i].bounds);

        if (limites.size.y < alturaMinimaZona)
        {
            float abajo = limites.min.y;
            limites.SetMinMax(limites.min, new Vector3(limites.max.x, abajo + alturaMinimaZona, limites.max.z));
        }

        Gizmos.DrawCube(limites.center, limites.size);
    }
}
