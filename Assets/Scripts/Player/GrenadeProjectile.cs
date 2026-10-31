using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Granada explosiva (la de siempre) y base de las demás granadas.
/// Las otras (incendiaria, aturdidora) heredan de esta y cambian solo qué pasa al detonar.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GrenadeProjectile : NetworkBehaviour
{
    [Header("Mecha")]
    [SerializeField] private float fuseTime = 3.5f;

    [Header("Explosión")]
    [SerializeField] protected float explosionRadius = 6f;
    [SerializeField] protected int explosionDamage = 150;
    [SerializeField] private LayerMask damageMask = ~0;
    [SerializeField] private GameObject explosionEffectPrefab;
    [Tooltip("Segundos que queda el efecto de la explosión antes de borrarse.")]
    [SerializeField] private float explosionEffectDuration = 3f;
    [SerializeField] private AudioClip explosionSound;

    protected Rigidbody rb;
    private bool hasExploded;

    /// <summary>Cliente que tiró la granada: se le dan las kills y los puntos.</summary>
    protected ulong LanzadorClientId { get; private set; }

    /// <summary>Un zombie alcanzado por la granada (uno solo aunque tenga varios colliders).</summary>
    protected struct ZombieAlcanzado
    {
        public ZombieHealth zombie;
        public Vector3 punto;
        public float distancia;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Launch(Vector3 velocity, ulong lanzadorClientId)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("[GrenadeProjectile] No se encontró Rigidbody.");
            return;
        }

        LanzadorClientId = lanzadorClientId;
        rb.linearVelocity = velocity;

        if (IsServer)
            StartCoroutine(FuseRoutine());
    }

    private IEnumerator FuseRoutine()
    {
        yield return new WaitForSeconds(fuseTime);
        Explode();
    }

    private void Explode()
    {
        if (!IsServer || hasExploded)
            return;

        hasExploded = true;
        Detonar();
    }

    /// <summary>Solo servidor. Qué hace la granada al explotar. La explosiva daña en área y desaparece.</summary>
    protected virtual void Detonar()
    {
        DanarEnArea(explosionRadius, explosionDamage, true);
        ReproducirEfectosExplosion();
        DespawnGranada();
    }

    /// <summary>
    /// Solo servidor. Zombies vivos dentro del radio, uno por zombie aunque tenga varios colliders
    /// (cuerpo, cabeza...), con el punto más cercano al centro de la granada.
    /// </summary>
    protected List<ZombieAlcanzado> ZombiesEnRadio(float radio)
    {
        Dictionary<ZombieHealth, ZombieAlcanzado> porZombie = new Dictionary<ZombieHealth, ZombieAlcanzado>();
        Collider[] hits = Physics.OverlapSphere(transform.position, radio, damageMask, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            ZombieHealth zombie = hit.GetComponentInParent<ZombieHealth>();
            if (zombie == null || zombie.IsDead)
                continue;

            Vector3 punto = hit.ClosestPoint(transform.position);
            float distancia = Vector3.Distance(transform.position, punto);

            if (porZombie.TryGetValue(zombie, out ZombieAlcanzado anterior) && anterior.distancia <= distancia)
                continue;

            porZombie[zombie] = new ZombieAlcanzado { zombie = zombie, punto = punto, distancia = distancia };
        }

        return new List<ZombieAlcanzado>(porZombie.Values);
    }

    /// <summary>Solo servidor. Daño en área con caída lineal según distancia al centro.</summary>
    protected void DanarEnArea(float radio, int dano, bool efectosDeImpacto)
    {
        if (dano <= 0)
            return;

        foreach (ZombieAlcanzado alcanzado in ZombiesEnRadio(radio))
            Danar(alcanzado, radio, dano, efectosDeImpacto);
    }

    protected void Danar(ZombieAlcanzado alcanzado, float radio, int dano, bool efectosDeImpacto)
    {
        float falloff = 1f - Mathf.Clamp01(alcanzado.distancia / radio);
        int danoFinal = Mathf.RoundToInt(dano * falloff);

        if (danoFinal <= 0)
            return;

        Vector3 hitNormal = (alcanzado.punto - transform.position).normalized;
        alcanzado.zombie.RecibirDanoServidor(danoFinal, alcanzado.punto, hitNormal, LanzadorClientId, efectosDeImpacto);
    }

    protected void ReproducirEfectosExplosion()
    {
        PlayExplosionEffectsClientRpc(transform.position);
    }

    protected void DespawnGranada()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    [ClientRpc]
    private void PlayExplosionEffectsClientRpc(Vector3 position)
    {
        if (explosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(
                explosionEffectPrefab,
                position,
                Quaternion.identity
            );

            // Destroy con demora y no una corrutina: la granada se despawnea enseguida
            // y una corrutina suya moriría antes de borrar el efecto.
            Destroy(effect, explosionEffectDuration);
        }

        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, position);
    }
}
