using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Va en el prefab del zombie. Al morir (lo llama ZombieHealth.Die, solo en el servidor) tira la tabla
/// de drop y deja en el piso el recurso que toque. En el tutorial no suelta nada.
/// </summary>
public class DropRecursosZombie : MonoBehaviour
{
    [Tooltip("Qué suelta y con qué probabilidad. Se ajusta en el asset (Assets/Data/Recursos).")]
    [SerializeField] private TablaDropRecursos tabla;

    [Tooltip("Prefab del recurso en el piso (RecursoDrop). Tiene que estar en DefaultNetworkPrefabs.")]
    [SerializeField] private RecursoDrop prefabRecurso;

    [Tooltip("En esta escena los zombies no sueltan recursos.")]
    [SerializeField] private string escenaSinDrops = "TutorialScene";

    [Header("Ubicación en el piso")]
    [Tooltip("Altura del recurso sobre el piso.")]
    [SerializeField] private float alturaSobreElPiso = 0.35f;
    [Tooltip("Desde qué altura sobre el zombie se busca el piso hacia abajo.")]
    [SerializeField] private float alturaBusquedaPiso = 1.5f;
    [SerializeField] private float distanciaBusquedaPiso = 6f;

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

        if (red == null || !red.IsServer || !PuedeSoltar())
            return null;

        TablaDropRecursos.Drop drop = tabla.Elegir(Random.value);

        if (drop == null)
            return null;

        RecursoDrop recurso = Instantiate(prefabRecurso, PosicionEnElPiso(), Quaternion.identity);
        recurso.Configurar(drop.tipo, drop.cantidad);
        recurso.NetworkObject.Spawn(true);

        ContarSoltado();
        return recurso;
    }

    private bool PuedeSoltar()
    {
        if (tabla == null || prefabRecurso == null)
            return false;

        // Tutorial: por escena y, por las dudas, por los zombies que reviven solos.
        if (SceneManager.GetActiveScene().name == escenaSinDrops || GetComponent<TutorialZombieRespawn>() != null)
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
