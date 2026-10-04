using System;
using UnityEngine;

/// <summary>Offset del arma para un estado (parado, agachado, apuntando, corriendo) y los resortes que lo siguen.</summary>
[Serializable]
public class WeaponStateOffsetData
{
    [Tooltip("Metros, en los ejes del original (X derecha, Y atrás, Z arriba). WeaponMotionApplier los convierte.")]
    public Vector3 location;

    [Tooltip("Grados, en los ejes del original (X cabeceo, Y roll/inclinación, Z giro). WeaponMotionApplier los convierte.")]
    public Vector3 rotation;

    public SpringSettings locationSpring = new SpringSettings(20f, 200f, 1f, 1f);
    public SpringSettings rotationSpring = new SpringSettings(20f, 200f, 1f, 1f);

    public WeaponStateOffsetData() { }

    public WeaponStateOffsetData(Vector3 location, Vector3 rotation, SpringSettings locationSpring, SpringSettings rotationSpring)
    {
        this.location = location;
        this.rotation = rotation;
        this.locationSpring = locationSpring;
        this.rotationSpring = rotationSpring;
    }
}

/// <summary>
/// OffsetMotion del original: según el estado (Running > Aiming > Crouching > Standing)
/// el arma recibe un offset distinto y un resorte lo persigue. Es lo que da la
/// inclinación del arma al AGACHARSE.
///
/// Valores reales del original (ItemOffsets: son iguales en TODAS las armas):
///   Parado:   rotación (0, -3, 0)
///   Agachado: rotación (0, -15, 0)   <- el eje Y del original es el roll del arma
/// Los resortes salen de los FeelStateOffset (SO_FSO_*). Agachado: la posición va al doble de velocidad.
///
/// Aiming y Running vienen en cero: en el original esos offsets compensan cada animación
/// de cada arma, y tus animaciones ya quedaron bien. Si querés probarlos, los valores de
/// las pistolas del original son: Aiming ubicación (0, 0.01, 0); Running ubicación (0.06, 0.095, 0.01).
///
/// Es una fuente más para WeaponMotionApplier (no escribe el Transform). Si no lo
/// agregás a mano en FPS_Arms, el Applier lo agrega solo al iniciar.
/// </summary>
public class WeaponStateOffset : MonoBehaviour
{
    [Header("Referencias (si están vacías se buscan solas)")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private WeaponSway sway;

    [Header("Offsets por estado (valores del original)")]
    [SerializeField] private WeaponStateOffsetData standing = new WeaponStateOffsetData(
        Vector3.zero, new Vector3(0f, -3f, 0f),
        new SpringSettings(20f, 200f, 1f, 1f), new SpringSettings(20f, 200f, 1f, 1f));

    [SerializeField] private WeaponStateOffsetData crouching = new WeaponStateOffsetData(
        Vector3.zero, new Vector3(0f, -15f, 0f),
        new SpringSettings(15f, 150f, 1f, 2f), new SpringSettings(15f, 150f, 1f, 1f));

    [SerializeField] private WeaponStateOffsetData aiming = new WeaponStateOffsetData(
        Vector3.zero, Vector3.zero,
        new SpringSettings(20f, 200f, 1f, 1f), new SpringSettings(20f, 200f, 1f, 1f));

    [SerializeField] private WeaponStateOffsetData running = new WeaponStateOffsetData(
        Vector3.zero, Vector3.zero,
        new SpringSettings(15f, 150f, 1f, 1f), new SpringSettings(15f, 150f, 1f, 1f));

    private readonly SpringVector3 locationSpring = new SpringVector3();
    private readonly SpringVector3 rotationSpring = new SpringVector3();

    private WeaponStateOffsetData current;
    private int lastComputedFrame = -1;

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();

        if (sway == null)
            sway = GetComponentInParent<WeaponSway>();

        if (sway == null)
            sway = GetComponent<WeaponSway>();

        current = standing;
    }

    private void ComputeIfNeeded()
    {
        if (lastComputedFrame == Time.frameCount)
            return;

        lastComputedFrame = Time.frameCount;

        bool isRunning = playerMovement != null && playerMovement.IsSprinting;
        bool isAiming = sway != null && sway.IsAiming;
        bool isCrouching = playerMovement != null && playerMovement.IsCrouching;

        // Misma prioridad que OffsetMotion: Running > Aiming > Crouching > Standing.
        current = isRunning ? running :
                  isAiming ? aiming :
                  isCrouching ? crouching :
                  standing;

        locationSpring.Apply(current.locationSpring);
        rotationSpring.Apply(current.rotationSpring);
        locationSpring.SetTarget(current.location);
        rotationSpring.SetTarget(current.rotation);
    }

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