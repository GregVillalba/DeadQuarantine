using System.Collections;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class GrenadeProjectile : NetworkBehaviour
{
    [Header("Mecha")]
    [SerializeField] private float fuseTime = 3.5f;

    [Header("Explosión")]
    [SerializeField] private float explosionRadius = 6f;
    [SerializeField] private int explosionDamage = 150;
    [SerializeField] private LayerMask damageMask = ~0;
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private AudioClip explosionSound;

    private Rigidbody rb;
    private bool hasExploded;

    private void Awake()
    {
        Debug.Log("[GrenadeProjectile] Awake ejecutado en " + gameObject.name);

        rb = GetComponent<Rigidbody>();

        Debug.Log("[GrenadeProjectile] Rigidbody encontrado: " + rb);
    }

    public void Launch(Vector3 velocity)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("[GrenadeProjectile] No se encontró Rigidbody.");
            return;
        }

        Debug.Log("[GrenadeProjectile] rb = " + rb);

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

        // Daño en área — con caída lineal según distancia al centro.
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, damageMask);

        foreach (Collider hit in hits)
        {
            ZombieHealth zombie = hit.GetComponentInParent<ZombieHealth>();
            if (zombie == null)
                continue;

            float distance = Vector3.Distance(transform.position, hit.ClosestPoint(transform.position));
            float falloff = 1f - Mathf.Clamp01(distance / explosionRadius);
            int damage = Mathf.RoundToInt(explosionDamage * falloff);

            if (damage <= 0)
                continue;

            Vector3 hitPoint = hit.ClosestPoint(transform.position);
            Vector3 hitNormal = (hitPoint - transform.position).normalized;

            zombie.TakeDamage(damage, hitPoint, hitNormal);
        }

        PlayExplosionEffectsClientRpc(transform.position);

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

            StartCoroutine(DestroyExplosionEffect(effect));
        }

        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, position);
    }

    private IEnumerator DestroyExplosionEffect(GameObject effect)
    {
        yield return new WaitForSeconds(0.25f);

        if (effect != null)
            Destroy(effect);
    }
}