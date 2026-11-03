using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Barril explosivo colocado por un jugador (ver ColocadorObjetos). Va en la raíz del prefab, junto al NetworkObject.
///
/// Tiene muy poca vida: un disparo de cualquier arma lo hace explotar (Weapon llama a RecibirDisparo).
/// Al explotar, el servidor daña a todos los zombies dentro del radio y el barril desaparece. Las kills y los puntos
/// son del jugador que le disparó. Solo daña zombies: a los jugadores no.
/// </summary>
public class BarrilExplosivo : NetworkBehaviour
{
    [Header("Vida")]
    [Tooltip("Daño que aguanta antes de explotar. 1 = explota con el primer disparo.")]
    [SerializeField] private int vida = 1;

    [Header("Explosión")]
    [SerializeField] private int dano = 200;
    [Tooltip("Radio de la explosión (m).")]
    [SerializeField] private float radio = 20f;
    [Tooltip("Activado = el daño baja con la distancia (200 en el centro, 0 en el borde), igual que la granada. " +
             "Desactivado = todos los zombies del radio reciben el daño completo.")]
    [SerializeField] private bool danoDecreceConDistancia = true;
    [SerializeField] private LayerMask capasDano = ~0;
    [Tooltip("Altura (m) sobre la base del barril desde la que sale la explosión.")]
    [SerializeField] private float alturaCentro = 0.6f;

    [Header("Efectos")]
    [Tooltip("Por ahora el mismo de la granada (el que tiene asignado P_LPSP_PROJ_Grenade_01).")]
    [SerializeField] private GameObject efectoExplosion;
    [Tooltip("Segundos que queda el efecto antes de borrarse.")]
    [SerializeField] private float duracionEfecto = 3f;
    [SerializeField] private AudioClip sonidoExplosion;

    // Solo servidor.
    private int vidaActual;
    private bool exploto;

    private Vector3 Centro => transform.position + transform.up * alturaCentro;

    public override void OnNetworkSpawn()
    {
        vidaActual = Mathf.Max(1, vida);
    }

    /// <summary>Lo llama Weapon en el cliente que disparó. El servidor resta la vida y, si llega a 0, lo hace explotar.</summary>
    public void RecibirDisparo(int cantidad)
    {
        if (exploto || !IsSpawned)
            return;

        RecibirDisparoServerRpc(cantidad);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RecibirDisparoServerRpc(int cantidad, ServerRpcParams rpcParams = default)
    {
        if (exploto || cantidad <= 0)
            return;

        vidaActual -= cantidad;

        if (vidaActual <= 0)
            Explotar(rpcParams.Receive.SenderClientId);
    }

    // Solo servidor.
    private void Explotar(ulong atacanteClientId)
    {
        exploto = true;

        Vector3 centro = Centro;

        foreach (KeyValuePair<ZombieHealth, Vector3> alcanzado in ZombiesEnRadio(centro))
        {
            Vector3 punto = alcanzado.Value;
            float distancia = Vector3.Distance(centro, punto);
            float factor = danoDecreceConDistancia ? 1f - Mathf.Clamp01(distancia / radio) : 1f;
            int danoFinal = Mathf.RoundToInt(dano * factor);

            if (danoFinal <= 0)
                continue;

            Vector3 normal = (punto - centro).normalized;
            alcanzado.Key.RecibirDanoServidor(danoFinal, punto, normal, atacanteClientId);
        }

        ReproducirExplosionClientRpc(centro);

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    // Un zombie por entrada aunque tenga varios colliders (cuerpo, cabeza...), con el punto más cercano al centro.
    private Dictionary<ZombieHealth, Vector3> ZombiesEnRadio(Vector3 centro)
    {
        Dictionary<ZombieHealth, Vector3> porZombie = new Dictionary<ZombieHealth, Vector3>();

        foreach (Collider hit in Physics.OverlapSphere(centro, radio, capasDano, QueryTriggerInteraction.Collide))
        {
            ZombieHealth zombie = hit.GetComponentInParent<ZombieHealth>();

            if (zombie == null || zombie.IsDead)
                continue;

            Vector3 punto = hit.ClosestPoint(centro);

            if (porZombie.TryGetValue(zombie, out Vector3 anterior) &&
                (anterior - centro).sqrMagnitude <= (punto - centro).sqrMagnitude)
                continue;

            porZombie[zombie] = punto;
        }

        return porZombie;
    }

    [ClientRpc]
    private void ReproducirExplosionClientRpc(Vector3 posicion)
    {
        if (efectoExplosion != null)
        {
            // Destroy con demora: el barril se despawnea enseguida y no puede borrar el efecto él mismo.
            GameObject efecto = Instantiate(efectoExplosion, posicion, Quaternion.identity);
            Destroy(efecto, duracionEfecto);
        }

        if (sonidoExplosion != null)
            AudioSource.PlayClipAtPoint(sonidoExplosion, posicion);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.6f);
        Gizmos.DrawWireSphere(Centro, radio);
    }
}
