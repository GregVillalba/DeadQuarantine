using UnityEngine;

/// <summary>
/// Salto, caída y aterrizaje del ARMA (JumpMotion + LandMotion del original con
/// MotionType.Item). Va en FPS_Arms. Es una fuente más para WeaponMotionApplier:
/// no escribe el Transform directamente.
///
/// Los valores son las curvas y resortes reales del original (MotionData.cs) y se
/// ajustan en el Inspector. Salen en los ejes del original; el Applier los convierte.
/// </summary>
public class WeaponJumpMotion : MonoBehaviour
{
    [Header("Referencias (si están vacías se buscan solas)")]
    [SerializeField] private CharacterController characterController;

    [Header("Salto / caída / aterrizaje del arma (valores del original)")]
    [SerializeField] private JumpLandMotion motion = JumpLandMotion.Create(MotionProfile.Weapon);

    [Header("Diagnóstico")]
    [Tooltip("Escribe en la consola cuando detecta despegue y aterrizaje.")]
    [SerializeField] private bool debugLog = false;

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();
    }

    [ContextMenu("Restaurar valores del original")]
    private void RestoreOriginal()
    {
        motion.LoadOriginal(MotionProfile.Weapon);
    }

    private void Update()
    {
        motion.Tick(characterController, Time.deltaTime);

        if (!debugLog)
            return;

        if (characterController == null)
            Debug.LogWarning("[WeaponJumpMotion] Sin CharacterController: no puede detectar saltos.", this);

        if (motion.JustTookOff)
            Debug.Log("[WeaponJumpMotion] Despegue");

        if (motion.JustLanded)
            Debug.Log("[WeaponJumpMotion] Aterrizaje: velocidad de caída " + motion.LandImpactSpeed.ToString("F1") + " m/s");
    }

    public Vector3 TickPosition() => motion.Location;

    public Vector3 TickRotation() => motion.Rotation;
}