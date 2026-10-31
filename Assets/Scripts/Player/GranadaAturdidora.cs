using UnityEngine;

/// <summary>
/// Granada aturdidora: al explotar deja inmóviles a los zombies del radio durante unos segundos
/// (no caminan ni atacan, ver ZombieAI.Aturdir). Hace poco daño ("Explosion Damage") y solo afecta a zombies.
/// </summary>
public class GranadaAturdidora : GrenadeProjectile
{
    [Header("Aturdimiento")]
    [SerializeField] private float duracionAturdimiento = 4f;
    [Tooltip("El boss de historia queda aturdido esta fracción del tiempo (0.5 = la mitad).")]
    [Range(0f, 1f)]
    [SerializeField] private float multiplicadorBoss = 0.5f;

    protected override void Detonar()
    {
        foreach (ZombieAlcanzado alcanzado in ZombiesEnRadio(explosionRadius))
        {
            // Primero el daño: si lo mata, Aturdir no hace nada.
            Danar(alcanzado, explosionRadius, explosionDamage, true);

            float duracion = alcanzado.zombie.EsBossHistoria
                ? duracionAturdimiento * multiplicadorBoss
                : duracionAturdimiento;

            ZombieAI ia = alcanzado.zombie.GetComponent<ZombieAI>();
            if (ia != null)
                ia.Aturdir(duracion);
        }

        ReproducirEfectosExplosion();
        DespawnGranada();
    }
}
