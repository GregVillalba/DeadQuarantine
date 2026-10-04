using UnityEngine;

public enum DifficultyLevel
{
    Normal = 0,
    Dificil = 1,
    Pesadilla = 2
}

/// <summary>
/// Multiplicadores de una dificultad. Se crea un asset por dificultad
/// (Create > Game > Difficulty Settings). Son independientes del modo.
/// </summary>
[CreateAssetMenu(fileName = "Difficulty_", menuName = "Game/Difficulty Settings")]
public class DifficultySettings : ScriptableObject
{
    public DifficultyLevel level = DifficultyLevel.Normal;
    public string displayName = "Normal";

    [Header("Multiplicadores (1 = sin cambios)")]
    [Min(0.1f)] public float zombieHealthMultiplier = 1f;
    [Min(0.1f)] public float zombieCountMultiplier = 1f;

    [Tooltip("Menor a 1 = zombies aparecen más rápido.")]
    [Min(0.1f)] public float spawnIntervalMultiplier = 1f;

    [Header("Zombies corredores")]
    [Tooltip("Multiplica lo que sube la probabilidad de correr por ronda. 1 = normal, 2 = el doble de rápido.")]
    [Min(0f)] public float runChanceRampMultiplier = 1f;

    [Tooltip("Se suma a la probabilidad de correr que ya sube por ronda.")]
    [Range(0f, 1f)] public float runChanceBonus = 0f;

    [Header("Zombies: ataque")]
    [Tooltip("Daño de cada ataque de zombie. 0 = usar el valor del prefab del zombie.")]
    [Min(0)] public int zombieDamage = 0;

    [Header("Jugador: arma")]
    [Tooltip("Capacidad del cargador de TODAS las armas. 0 = usar la de cada arma.")]
    [Min(0)] public int magazineSize = 0;

    [Tooltip("Si está apagado, el jugador no puede recargar: al gastar las balas no consigue más.")]
    public bool allowReload = true;

    [Header("Jugador: munición de reserva")]
    [Tooltip("Apagado = recargas ilimitadas (Normal). Encendido = cada arma tiene una reserva limitada: " +
             "cada bala que se pasa al cargador se descuenta de la reserva.")]
    public bool limitedReserveAmmo = false;

    [Tooltip("Multiplica la reserva de cada arma (la reserva base se configura en el propio arma: " +
             "rifle 120, pistola 100...). 1 = tal cual, 0.5 = la mitad. Solo se usa con 'Limited Reserve Ammo' activado.")]
    [Min(0f)] public float reserveAmmoMultiplier = 1f;

    [Header("Jugador: regeneración de vida")]
    [Tooltip("Si está apagado, se usan los valores que ya tiene PlayerHealth en el Inspector.")]
    public bool overrideHealthRegen = false;
    [Tooltip("Segundos sin recibir daño antes de empezar a regenerar.")]
    [Min(0f)] public float healthRegenDelay = 5f;
    [Tooltip("Puntos de vida que se recuperan en cada tick.")]
    [Min(0)] public int healthRegenAmount = 25;
    [Tooltip("Segundos entre cada tick de regeneración.")]
    [Min(0.1f)] public float healthRegenInterval = 1f;
}