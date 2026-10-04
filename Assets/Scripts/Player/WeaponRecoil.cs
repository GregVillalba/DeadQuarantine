using UnityEngine;

/// <summary>
/// Recoil del ARMA: port de RecoilMotion (MotionType.Item) del original.
///
/// Funcionamiento idéntico:
///   1) Se cuentan los disparos seguidos (shotsFired).
///   2) Con ese número se evalúan las 3 curvas de posición y las 3 de rotación
///      (los datos reales de SO_WEP_Recoil_AR / SO_WEP_Recoil_SMG).
///   3) Se multiplica por el multiplicador de las curvas y por el del estado
///      (apuntando = 0,35 en el original).
///   4) Un resorte persigue ese valor; al dejar de disparar el objetivo vuelve a 0.
///
/// Ojo: en el original el arma casi no "sube": patea hacia ATRÁS (unos 2 cm) y da un
/// cabeceo corto en los primeros disparos. Lo que sube en vertical es la CÁMARA
/// (ver CameraRecoil, ~12° en una ráfaga larga); como las manos van pegadas a la
/// cámara, se ve que todo sube junto.
///
/// Es una fuente más para WeaponMotionApplier (no escribe el Transform). Los valores
/// salen en los ejes del original; el Applier los convierte a los de FPS_Arms.
/// </summary>
public class WeaponRecoil : MonoBehaviour
{
    [Header("Curvas del original (X = nº de disparo seguido)")]
    [SerializeField] private MotionCurves recoilAR = MotionData.RecoilAR();
    [SerializeField] private MotionCurves recoilSMG = MotionData.RecoilSMG();

    [Header("Intensidad por tipo de arma (1 = igual que el original)")]
    [Tooltip("Rifles. Un poco menos que el original.")]
    [SerializeField] private float arMultiplier = 0.8f;
    [SerializeField] private float smgMultiplier = 1f;
    [Tooltip("Pistolas: casi sin recoil. Usa las curvas SMG pero muy atenuadas.")]
    [SerializeField] private float handgunMultiplier = 0.2f;

    [Header("Estados")]
    [Tooltip("Multiplicador del recoil del arma al apuntar (0,35 en el original).")]
    [SerializeField] private float aimingMultiplier = 0.35f;

    [Header("Ráfaga")]
    [Tooltip("Si pasa más que esto sin disparar, el contador vuelve a 0 y el arma se asienta. Tiene que ser mayor que el fireRate de tus armas.")]
    [SerializeField] private float comboResetDelay = 0.3f;

    [Tooltip("Las curvas del original tienen un valor distinto de cero en el disparo 0 (una inclinación fija en reposo). " +
             "Activado = se resta, así el arma en reposo queda exactamente en su pose.")]
    [SerializeField] private bool removeRestOffset = true;

    private readonly SpringVector3 locationSpring = new SpringVector3();
    private readonly SpringVector3 rotationSpring = new SpringVector3();

    private MotionCurves activeCurves;
    private int shotsFired;
    private float lastFireTime = -999f;
    private bool lastAiming;
    private float lastIntensity = 1f;
    private float presetMultiplier = 1f;

    private int lastComputedFrame = -1;

    public int ShotsFired => shotsFired;

    [ContextMenu("Restaurar valores del original")]
    private void RestoreOriginal()
    {
        recoilAR = MotionData.RecoilAR();
        recoilSMG = MotionData.RecoilSMG();
        aimingMultiplier = 0.35f;
        arMultiplier = 0.8f;
        smgMultiplier = 1f;
        handgunMultiplier = 0.2f;
    }

    private void OnEnable()
    {
        shotsFired = 0;
        lastFireTime = -999f;
        locationSpring.Reset();
        rotationSpring.Reset();
        lastComputedFrame = -1;
    }

    /// <summary>Lo llama Weapon.cs en cada disparo.</summary>
    public void Fire(RecoilPreset preset, bool aiming, float intensityMultiplier = 1f)
    {
        if (Time.time - lastFireTime > comboResetDelay)
            shotsFired = 0;

        shotsFired++;
        lastFireTime = Time.time;
        lastAiming = aiming;
        lastIntensity = intensityMultiplier;
        activeCurves = preset == RecoilPreset.AR ? recoilAR : recoilSMG;

        switch (preset)
        {
            case RecoilPreset.AR: presetMultiplier = arMultiplier; break;
            case RecoilPreset.Handgun: presetMultiplier = handgunMultiplier; break;
            default: presetMultiplier = smgMultiplier; break;
        }
    }

    private void ComputeIfNeeded()
    {
        if (lastComputedFrame == Time.frameCount)
            return;

        lastComputedFrame = Time.frameCount;

        if (shotsFired > 0 && Time.time - lastFireTime > comboResetDelay)
            shotsFired = 0;

        Vector3 location = Vector3.zero;
        Vector3 rotation = Vector3.zero;

        if (activeCurves != null)
        {
            float stateMultiplier = (lastAiming ? aimingMultiplier : 1f) * lastIntensity * presetMultiplier;

            Vector3 locationNow = activeCurves.EvaluateLocation(shotsFired);
            Vector3 rotationNow = activeCurves.EvaluateRotation(shotsFired);

            if (removeRestOffset)
            {
                locationNow -= activeCurves.EvaluateLocation(0f);
                rotationNow -= activeCurves.EvaluateRotation(0f);
            }

            location = locationNow * (activeCurves.locationMultiplier * stateMultiplier);
            rotation = rotationNow * (activeCurves.rotationMultiplier * stateMultiplier);

            locationSpring.Apply(activeCurves.locationSpring);
            rotationSpring.Apply(activeCurves.rotationSpring);
        }

        locationSpring.SetTarget(location);
        rotationSpring.SetTarget(rotation);
    }

    // Los llama WeaponMotionApplier una vez por frame cada uno.

    public Vector3 TickPosition()
    {
        ComputeIfNeeded();
        return locationSpring.Evaluate(Time.deltaTime);
    }

    public Vector3 TickRotation()
    {
        ComputeIfNeeded();
        return rotationSpring.Evaluate(Time.deltaTime);
    }
}