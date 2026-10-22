using UnityEngine;

// Va en el collider de la cabeza del zombie (hijo del hueso Head).
// Solo sirve para que Weapon pueda distinguir un impacto en la cabeza
// de un impacto en el resto del cuerpo.
[RequireComponent(typeof(Collider))]
public class ZombieHeadHitbox : MonoBehaviour
{
    private Collider hitboxCollider;

    // Zombie al que pertenece esta cabeza.
    public ZombieHealth Owner { get; private set; }

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider>();
        Owner = GetComponentInParent<ZombieHealth>();
    }

    public void SetActive(bool active)
    {
        if (hitboxCollider != null)
            hitboxCollider.enabled = active;
    }
}