using UnityEngine;

/// <summary>
/// Hitbox auxiliar de disparo para brazos de zombies humanoides.
/// Se crea automáticamente desde ZombieHealth y usa un Collider trigger,
/// por lo que no participa en la física ni empuja al jugador.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ZombieLimbHitbox : MonoBehaviour
{
    public ZombieHealth Owner { get; private set; }

    // 1 = brazo izquierdo, 2 = brazo derecho.
    public int HitVariant { get; private set; } = -1;

    private Collider hitboxCollider;

    public void Initialize(
        ZombieHealth owner,
        int hitVariant,
        Collider colliderReference)
    {
        Owner = owner;
        HitVariant = hitVariant;
        hitboxCollider = colliderReference != null
            ? colliderReference
            : GetComponent<Collider>();
    }

    private void Awake()
    {
        if (hitboxCollider == null)
            hitboxCollider = GetComponent<Collider>();

        if (Owner == null)
            Owner = GetComponentInParent<ZombieHealth>();
    }

    public void SetActive(bool active)
    {
        if (hitboxCollider != null)
            hitboxCollider.enabled = active;
    }
}
