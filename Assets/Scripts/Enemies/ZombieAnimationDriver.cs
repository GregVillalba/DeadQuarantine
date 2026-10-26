using UnityEngine;

/// <summary>
/// Capa opcional entre la lógica de ZombieAI/ZombieHealth y un Animator específico.
/// Los zombies viejos no necesitan este componente: si no existe, los scripts usan
/// sus parámetros legacy (Speed / Attack / Hit) como hasta ahora.
/// </summary>
public abstract class ZombieAnimationDriver : MonoBehaviour
{
    /// <summary>
    /// Actualiza la locomoción visual.
    /// worldSpeed = velocidad real del NavMeshAgent.
    /// maxSpeed = velocidad objetivo actual del agente.
    /// </summary>
    public abstract void SetMovement(float worldSpeed, float maxSpeed);

    public abstract void PlayAttack();

    public virtual void PlayHit()
    {
    }

    public virtual void PlayDeath()
    {
    }
}
