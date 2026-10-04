using UnityEngine;

/// <summary>
/// Detecta despegue / vuelo / aterrizaje a partir del CharacterController.
///
/// Por qué existe: CharacterController.isGrounded parpadea (false por 1-2
/// frames) al bajar escalones, rampas o bordes chicos. Si cada script de
/// arma/cámara lo lee directo, el bob y las poses "se cortan" y vuelven en
/// medio del suelo. Acá se exige que el jugador lleve un ratito en el aire
/// (groundGrace) o que esté subiendo de verdad (riseThreshold) para contar
/// como "en el aire".
///
/// Es una clase común (no MonoBehaviour): cada script crea la suya y llama
/// a Tick() UNA vez por frame. No hay nada que arrastrar en el Inspector.
/// </summary>
public class AirTracker
{
    public bool IsAirborne { get; private set; }
    public bool JustTookOff { get; private set; }
    public bool JustLanded { get; private set; }

    /// <summary>Segundos desde que despegó.</summary>
    public float AirTime { get; private set; }

    /// <summary>Velocidad vertical en m/s (positiva = subiendo).</summary>
    public float VerticalSpeed { get; private set; }

    /// <summary>Velocidad de caída (m/s, positiva) con la que pegó contra el piso en el último aterrizaje.</summary>
    public float LandImpactSpeed { get; private set; }

    private readonly float groundGrace;
    private readonly float riseThreshold;

    private float ungroundedTime;
    private float peakFallSpeed;

    public AirTracker(float groundGrace = 0.1f, float riseThreshold = 0.5f)
    {
        this.groundGrace = groundGrace;
        this.riseThreshold = riseThreshold;
    }

    public void Tick(CharacterController cc, float deltaTime)
    {
        JustTookOff = false;
        JustLanded = false;

        // Durante el parkour el CharacterController se apaga: no hay "aire" ni "aterrizaje".
        if (cc == null || !cc.enabled)
        {
            IsAirborne = false;
            ungroundedTime = 0f;
            AirTime = 0f;
            peakFallSpeed = 0f;
            VerticalSpeed = 0f;
            return;
        }

        bool grounded = cc.isGrounded;
        VerticalSpeed = cc.velocity.y;

        if (grounded)
            ungroundedTime = 0f;
        else
            ungroundedTime += deltaTime;

        bool airborneNow = !grounded &&
                           (VerticalSpeed > riseThreshold || ungroundedTime >= groundGrace);

        if (airborneNow && !IsAirborne)
        {
            JustTookOff = true;
            AirTime = 0f;
            peakFallSpeed = 0f;
        }

        if (airborneNow)
        {
            AirTime += deltaTime;
            peakFallSpeed = Mathf.Max(peakFallSpeed, -VerticalSpeed);
        }
        else if (IsAirborne)
        {
            JustLanded = true;
            LandImpactSpeed = peakFallSpeed;
            AirTime = 0f;
        }

        IsAirborne = airborneNow;
    }
}