using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Va en el prefab del zombie. Al morir (lo llama ZombieHealth.Die, solo en el servidor) tira la tabla
/// de drop y deja en el piso el recurso que toque. En el tutorial no se usa la tabla: los zombies que reviven
/// solos (TutorialZombieRespawn) sueltan siempre el recurso que tienen configurado, y el resto nada.
/// </summary>
public class DropRecursosZombie : MonoBehaviour
{
    [Tooltip("Qué suelta y con qué probabilidad. Se ajusta en el asset (Assets/Data/Recursos).")]
    [SerializeField] private TablaDropRecursos tabla;

    [Tooltip("Prefab del recurso en el piso (RecursoDrop). Tiene que estar en DefaultNetworkPrefabs.")]
    [SerializeField] private RecursoDrop prefabRecurso;

    [Tooltip("En esta escena no se usa la tabla: solo sueltan los zombies con TutorialZombieRespawn (su recurso fijo).")]
    [SerializeField] private string escenaSinDrops = "TutorialScene";

    [Header("Ubicación en el piso")]
    [Tooltip("Altura del recurso sobre el piso.")]
    [SerializeField] private float alturaSobreElPiso = 0.35f;
    [Tooltip("Desde qué altura sobre el zombie se busca el piso hacia abajo.")]
    [SerializeField] private float alturaBusquedaPiso = 1.5f;
    [SerializeField] private float distanciaBusquedaPiso = 6f;

    // Drop fijo puesto por código (ConfigurarDropTutorial). Solo servidor.
    private bool dropTutorialAsignado;
    private TipoRecurso tipoDropTutorial;
    private int cantidadDropTutorial;

    // Recursos soltados en la ronda actual por cada tabla (para TablaDropRecursos.MaximoPorRonda).
    private static readonly Dictionary<TablaDropRecursos, int> soltadosEnRonda = new Dictionary<TablaDropRecursos, int>();
    private static int rondaContada = int.MinValue;

    // Con "Enter Play Mode Options" (sin recargar dominio) los estaticos sobreviven entre partidas.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        soltadosEnRonda.Clear();
        rondaContada = int.MinValue;
    }

    /// <summary>Solo servidor. Devuelve el recurso soltado o null si no tocó nada.</summary>
    public RecursoDrop Soltar()
    {
        NetworkManager red = NetworkManager.Singleton;

        if (red == null || !red.IsServer || prefabRecurso == null)
            return null;

        TipoRecurso tipo;
        int cantidad;
        bool enTutorial = EsZombieDelTutorial();

        if (enTutorial)
        {
            if (!DropDelTutorial(out tipo, out cantidad))
                return null;
        }
        else
        {
            if (!PuedeSoltar())
                return null;

            TablaDropRecursos.Drop drop = tabla.Elegir(Random.value);

            if (drop == null)
                return null;

            tipo = drop.tipo;
            cantidad = drop.cantidad;
        }

        RecursoDrop recurso = Instantiate(prefabRecurso, PosicionEnElPiso(), Quaternion.identity);
        recurso.Configurar(tipo, cantidad);
        recurso.NetworkObject.Spawn(true);

        // El tope por ronda es de la tabla; el tutorial no tiene rondas.
        if (!enTutorial)
            ContarSoltado();

        return recurso;
    }

    private bool EsZombieDelTutorial()
    {
        return SceneManager.GetActiveScene().name == escenaSinDrops || GetComponent<TutorialZombieRespawn>() != null;
    }

    /// <summary>
    /// Solo servidor. Tutorial: este zombie (creado por código, sin TutorialZombieRespawn) suelta siempre este
    /// recurso al morir. Lo usa TutorialSecciones con los zombies que aparecen a atacar la torreta.
    /// </summary>
    public void ConfigurarDropTutorial(TipoRecurso tipo, int cantidad)
    {
        dropTutorialAsignado = true;
        tipoDropTutorial = tipo;
        cantidadDropTutorial = Mathf.Max(1, cantidad);
    }

    // Tutorial: sin tabla ni azar. Sueltan los zombies que reviven solos (siempre lo mismo) y los que
    // se configuraron por código; el resto nada.
    private bool DropDelTutorial(out TipoRecurso tipo, out int cantidad)
    {
        TutorialZombieRespawn tutorial = GetComponent<TutorialZombieRespawn>();

        if (tutorial != null)
            return tutorial.DropFijo(out tipo, out cantidad);

        tipo = tipoDropTutorial;
        cantidad = cantidadDropTutorial;
        return dropTutorialAsignado;
    }

    // Si puede tirar la tabla de drop. En el tutorial nunca (ahí suelta el recurso fijo de DropDelTutorial).
    private bool PuedeSoltar()
    {
        if (tabla == null || prefabRecurso == null)
            return false;

        if (EsZombieDelTutorial())
            return false;

        // Modo sin recursos (ej. PvP, ver RecursosSegunModo): los zombies no sueltan nada.
        if (!RecursosSegunModo.HayRecursosEnPartidaActual)
            return false;

        return tabla.MaximoPorRonda <= 0 || SoltadosEnEstaRonda() < tabla.MaximoPorRonda;
    }

    private int SoltadosEnEstaRonda()
    {
        int ronda = RoundManager.Instance != null ? RoundManager.Instance.CurrentRoundNetwork.Value : 0;

        if (ronda != rondaContada)
        {
            soltadosEnRonda.Clear();
            rondaContada = ronda;
        }

        return soltadosEnRonda.TryGetValue(tabla, out int cantidad) ? cantidad : 0;
    }

    private void ContarSoltado()
    {
        soltadosEnRonda[tabla] = SoltadosEnEstaRonda() + 1;
    }

    // Primer piso debajo del zombie, sin contar su propio cuerpo (ragdoll), jugadores ni otros recursos.
    private Vector3 PosicionEnElPiso()
    {
        Vector3 origen = transform.position + Vector3.up * alturaBusquedaPiso;
        RaycastHit[] golpes = Physics.RaycastAll(origen, Vector3.down, distanciaBusquedaPiso, ~0, QueryTriggerInteraction.Ignore);

        float masCerca = float.MaxValue;
        Vector3 piso = transform.position;

        foreach (RaycastHit golpe in golpes)
        {
            if (golpe.distance >= masCerca || EsZombieJugadorORecurso(golpe.collider))
                continue;

            masCerca = golpe.distance;
            piso = golpe.point;
        }

        return piso + Vector3.up * alturaSobreElPiso;
    }

    private static bool EsZombieJugadorORecurso(Collider collider)
    {
        return collider.GetComponentInParent<ZombieHealth>() != null ||
               collider.GetComponentInParent<PlayerRecursos>() != null ||
               collider.GetComponentInParent<RecursoDrop>() != null;
    }
}
