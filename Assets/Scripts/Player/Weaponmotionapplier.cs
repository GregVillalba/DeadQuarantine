using UnityEngine;

/// <summary>
/// Combina WeaponSway, WeaponRecoil, WeaponJumpMotion y WeaponStateOffset y los aplica al Transform de
/// FPS_Arms UNA sola vez por frame (MotionApplier del original, versión chica).
///
/// IMPORTANTE - ejes: los motions del arma del original se aplican al hueso
/// ik_hand_gun, que vive dentro del Armature (rotado -90° en X). Su espacio local es:
///     X = derecha,  Y = ATRÁS,  Z = ARRIBA
/// y FPS_Arms usa ejes normales (X derecha, Y arriba, Z adelante). Por eso acá se
/// convierte: posición (x, z, -y) y rotación Euler (x, z, -y). Sin esto el recoil
/// empujaría el arma hacia arriba en vez de hacia atrás, etc.
///
/// Además, el original rota el hueso de la mano (alrededor del arma), mientras que acá se
/// rota FPS_Arms entero. "Rotation Pivot Local" es el punto de FPS_Arms (en su espacio
/// local) alrededor del cual gira el arma: ponelo donde está el arma en pantalla
/// (se ve como una esfera cyan al seleccionar el objeto). Con (0,0,0) giraría alrededor
/// del origen de FPS_Arms (cerca de los ojos) y el arma barrería un arco más grande.
/// </summary>
public class WeaponMotionApplier : MonoBehaviour
{
    [Header("Fuentes")]
    [SerializeField] private WeaponSway sway;
    [SerializeField] private WeaponRecoil recoil;
    [SerializeField] private WeaponJumpMotion jumpMotion;
    [Tooltip("Offsets por estado (inclinación al agacharse). Si está vacío se agrega solo.")]
    [SerializeField] private WeaponStateOffset stateOffset;

    [Header("Ejes")]
    [Tooltip("Convierte los valores del original (X derecha, Y atrás, Z arriba) a los ejes de FPS_Arms. Dejalo activado.")]
    [SerializeField] private bool convertFromOriginalAxes = true;

    [Tooltip("Punto (en espacio local de FPS_Arms) alrededor del cual gira el arma. Ajustalo mirando la esfera cyan.")]
    [SerializeField] private Vector3 rotationPivotLocal = new Vector3(0.12f, -0.2f, 0.45f);

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;

    private void Awake()
    {
        // Si falta alguna referencia en el Inspector, se busca en este objeto o sus hijos.
        if (sway == null) sway = GetComponentInChildren<WeaponSway>(true);
        if (recoil == null) recoil = GetComponentInChildren<WeaponRecoil>(true);
        if (jumpMotion == null) jumpMotion = GetComponentInChildren<WeaponJumpMotion>(true);

        if (stateOffset == null) stateOffset = GetComponentInChildren<WeaponStateOffset>(true);
        if (stateOffset == null) stateOffset = gameObject.AddComponent<WeaponStateOffset>();

        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
    }

    private void LateUpdate()
    {
        Vector3 positionOffset = Vector3.zero;
        Vector3 rotationOffset = Vector3.zero;

        if (sway != null)
        {
            positionOffset += sway.TickPosition();
            rotationOffset += sway.TickRotation();
        }

        if (recoil != null)
        {
            positionOffset += recoil.TickPosition();
            rotationOffset += recoil.TickRotation();
        }

        if (stateOffset != null)
        {
            positionOffset += stateOffset.TickPosition();
            rotationOffset += stateOffset.TickRotation();
        }

        if (jumpMotion != null)
        {
            positionOffset += jumpMotion.TickPosition();
            rotationOffset += jumpMotion.TickRotation();
        }

        if (convertFromOriginalAxes)
        {
            positionOffset = new Vector3(positionOffset.x, positionOffset.z, -positionOffset.y);
            rotationOffset = new Vector3(rotationOffset.x, rotationOffset.z, -rotationOffset.y);
        }

        Quaternion q = Quaternion.Euler(rotationOffset);

        // Girar alrededor del arma y no del origen de FPS_Arms: el arma no "barre" un arco.
        Vector3 pivotShift = rotationPivotLocal - q * rotationPivotLocal;

        transform.localPosition = initialLocalPosition + positionOffset + initialLocalRotation * pivotShift;
        transform.localRotation = initialLocalRotation * q;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.TransformPoint(rotationPivotLocal), 0.03f);
    }
}