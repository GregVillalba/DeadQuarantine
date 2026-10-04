using UnityEngine;

/// <summary>
/// Resorte físico (masa / rigidez / amortiguación / velocidad) para Vector3.
/// Equivale al Spring del original: se le da un valor objetivo con SetTarget()
/// (UpdateEndValue en el original) y un resorte lo persigue. Si el objetivo vuelve
/// a cero, el resorte vuelve solo.
///
/// "speed" escala el tiempo (2 = el resorte avanza al doble de rápido). La
/// simulación se parte en pasos chicos para que no se vuelva inestable con
/// framerates bajos o valores altos de rigidez/speed.
/// </summary>
[System.Serializable]
public class SpringVector3
{
    [Tooltip("Qué tan fuerte tira el resorte hacia el objetivo. Más alto = más rápido/seco.")]
    public float stiffness = 150f;

    [Tooltip("Qué tanto frena el movimiento. Más alto = menos rebote.")]
    public float damping = 25f;

    [Tooltip("Masa en la punta del resorte. Más alto = más lento/pesado.")]
    public float mass = 1f;

    [Tooltip("Multiplicador de velocidad del resorte (escala el tiempo).")]
    public float speed = 1f;

    private const float MaxStep = 1f / 120f;

    private Vector3 velocity;
    private Vector3 current;
    private Vector3 target;

    public Vector3 Current => current;
    public Vector3 Target => target;

    /// <summary>Copia los valores de un SpringSettings (damping, stiffness, mass, speed).</summary>
    public void Apply(SpringSettings settings)
    {
        stiffness = settings.stiffness;
        damping = settings.damping;
        mass = settings.mass;
        speed = settings.speed;
    }

    /// <summary>Empuja el resorte con un golpe puntual; sigue tirando hacia el objetivo.</summary>
    public void AddImpulse(Vector3 impulse)
    {
        current += impulse;
    }

    /// <summary>Hacia dónde tira el resorte AHORA (UpdateEndValue del original).</summary>
    public void SetTarget(Vector3 newTarget)
    {
        target = newTarget;
    }

    /// <summary>Avanza la simulación y devuelve el valor actual. Llamar una vez por frame.</summary>
    public Vector3 Evaluate(float deltaTime)
    {
        float scaledTime = Mathf.Min(deltaTime, 0.05f) * Mathf.Max(speed, 0f);

        if (scaledTime <= 0f)
            return current;

        int steps = Mathf.Max(1, Mathf.CeilToInt(scaledTime / MaxStep));
        float h = scaledTime / steps;
        float invMass = 1f / Mathf.Max(mass, 0.0001f);

        for (int i = 0; i < steps; i++)
        {
            Vector3 force = (-stiffness * (current - target)) - (damping * velocity);
            velocity += force * invMass * h;
            current += velocity * h;
        }

        return current;
    }

    public void Reset()
    {
        current = Vector3.zero;
        velocity = Vector3.zero;
        target = Vector3.zero;
    }
}