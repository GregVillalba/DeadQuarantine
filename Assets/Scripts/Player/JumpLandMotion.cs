using System;
using UnityEngine;

/// <summary>Qué juego de curvas usar: las del arma (SO_AC_*_Item) o las de la cámara (SO_AC_*_Camera).</summary>
public enum MotionProfile { Weapon, Camera }

/// <summary>
/// JumpMotion + LandMotion del original (Infima LPSP) en una sola clase, con las
/// curvas y resortes REALES de los .asset (Jumping/SO_AC_Jump|Fall|Land_Item|Camera).
///
/// Cómo funciona (igual que el original):
///  - DESPEGUE: mientras estás en el aire por un salto, se evalúan las curvas de salto
///    con el tiempo en el aire. Son pulsos cortos (0,1 a 0,25 s) que un resorte suaviza;
///    al terminar el pulso queda la pose final de la curva (el arma queda levemente
///    levantada mientras estás en el aire).
///  - CAÍDA: si no saltaste (te caíste de un borde) se usan las curvas de caída.
///  - ATERRIZAJE: al tocar el piso se evalúan las curvas de aterrizaje con el tiempo
///    desde el impacto. Es un golpe fuerte y corto que el resorte convierte en un
///    cabeceo del arma y un sacudón de cámara.
///  - Salto y aterrizaje tienen cada uno sus resortes; el resultado es la suma.
///
/// Es una clase común (no MonoBehaviour). La usan WeaponJumpMotion (arma) y
/// CameraRecoil (cámara). Valores de arma en ejes del original: ver MotionData.cs.
/// </summary>
[Serializable]
public class JumpLandMotion
{
    [Header("Curvas (valores del original)")]
    public MotionCurves jumping = new MotionCurves();
    public MotionCurves falling = new MotionCurves();
    public MotionCurves landing = new MotionCurves();

    [Header("Aterrizaje")]
    [Tooltip("Por debajo de esta velocidad de caída (m/s) no hay aterrizaje: bajar un escalón no cuenta. " +
             "(El original no filtra, pero acá el detector de aire es más sensible.)")]
    public float landMinFallSpeed = 1.5f;

    // ---------- Estado de ejecución (no se serializa) ----------
    private readonly AirTracker air = new AirTracker();
    private readonly SpringVector3 jumpLocation = new SpringVector3();
    private readonly SpringVector3 jumpRotation = new SpringVector3();
    private readonly SpringVector3 landLocation = new SpringVector3();
    private readonly SpringVector3 landRotation = new SpringVector3();

    private MotionCurves jumpPlayed;
    private bool jumped;
    private float timeSinceLand = 999f;

    public Vector3 Location { get; private set; }
    public Vector3 Rotation { get; private set; }

    public bool IsAirborne => air.IsAirborne;
    public bool JustTookOff => air.JustTookOff;
    public bool JustLanded => air.JustLanded;
    public float LandImpactSpeed => air.LandImpactSpeed;

    /// <summary>Crea el movimiento con los valores originales del perfil indicado.</summary>
    public static JumpLandMotion Create(MotionProfile profile)
    {
        JumpLandMotion motion = new JumpLandMotion();
        motion.LoadOriginal(profile);
        return motion;
    }

    /// <summary>Vuelve a cargar los valores originales (los del Inspector se pierden).</summary>
    public void LoadOriginal(MotionProfile profile)
    {
        if (profile == MotionProfile.Weapon)
        {
            jumping = MotionData.JumpWeapon();
            falling = MotionData.FallWeapon();
            landing = MotionData.LandWeapon();
        }
        else
        {
            jumping = MotionData.JumpCamera();
            falling = MotionData.FallCamera();
            landing = MotionData.LandCamera();
        }
    }

    /// <summary>Avanza la simulación un frame. Llamar UNA vez por frame.</summary>
    public void Tick(CharacterController cc, float deltaTime)
    {
        float dt = Mathf.Min(deltaTime, 0.05f);

        air.Tick(cc, dt);

        // Despegó subiendo = salto. Despegó sin subir = se cayó de un borde.
        if (air.JustTookOff)
            jumped = air.VerticalSpeed > 0.5f;

        if (air.JustLanded && air.LandImpactSpeed >= landMinFallSpeed)
            timeSinceLand = 0f;
        else
            timeSinceLand += dt;

        // ---------------- SALTO / CAÍDA (JumpMotion) ----------------
        Vector3 jumpLocationTarget = Vector3.zero;
        Vector3 jumpRotationTarget = Vector3.zero;

        if (air.IsAirborne)
        {
            float airTime = air.AirTime;

            if (jumped)
            {
                float maxCurveLength = jumping.MaxKeyCount;

                if (airTime >= maxCurveLength)
                {
                    airTime -= maxCurveLength;
                    jumpPlayed = falling;
                }
                else
                {
                    jumpPlayed = jumping;
                }
            }
            else
            {
                jumpPlayed = falling;
            }

            jumpLocationTarget = jumpPlayed.EvaluateLocation(airTime);
            jumpRotationTarget = jumpPlayed.EvaluateRotation(airTime);
        }

        jumpLocation.SetTarget(jumpLocationTarget);
        jumpRotation.SetTarget(jumpRotationTarget);

        Vector3 location = Vector3.zero;
        Vector3 rotation = Vector3.zero;

        if (jumpPlayed != null)
        {
            jumpLocation.Apply(jumpPlayed.locationSpring);
            jumpRotation.Apply(jumpPlayed.rotationSpring);
            location += jumpLocation.Evaluate(dt);
            rotation += jumpRotation.Evaluate(dt);
        }

        // ---------------- ATERRIZAJE (LandMotion) ----------------
        landLocation.SetTarget(landing.EvaluateLocation(timeSinceLand));
        landRotation.SetTarget(landing.EvaluateRotation(timeSinceLand));

        landLocation.Apply(landing.locationSpring);
        landRotation.Apply(landing.rotationSpring);

        location += landLocation.Evaluate(dt);
        rotation += landRotation.Evaluate(dt);

        Location = location;
        Rotation = rotation;
    }
}