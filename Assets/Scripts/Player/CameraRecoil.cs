using UnityEngine;

/// <summary>
/// "Motion" de la CÁMARA: recoil + salto / caída / aterrizaje. Equivale al
/// "Character Root Motion" del original (Infima LPSP): un objeto SEPARADO que aplica
/// los motions de cámara, para no tocar la rotación que PlayerLook / PlayerMovement
/// escriben en la cámara. Acá ese objeto es el padre de la cámara (PlayerCamera) y se
/// detecta solo; si querés otro, arrastralo a "Motion Root".
///
/// Va en el mismo GameObject de siempre (el que tiene el componente Camera), así
/// Weapon.cs sigue llamando cameraRecoil.Fire() sin cambiar referencias.
///
/// RECOIL = RecoilMotion del original con SO_WEP_Recoil_Camera: se evalúan las curvas
/// de rotación con el nº de disparos seguidos. La cámara SUBE ~1,2° por disparo y llega
/// a ~12° en una ráfaga larga, con un leve vaivén de yaw/roll; al soltar vuelve sola.
/// SALTO / ATERRIZAJE = SO_AC_Jump / Fall / Land _Camera (cabeceo corto al saltar y
/// al caer). Todo con los keyframes y resortes reales del original.
///
/// Se quitó el CameraBob: el balanceo al caminar lo hace la animación del arma.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraRecoil : MonoBehaviour
{
    [Header("Dónde se aplica")]
    [Tooltip("Objeto que recibe el movimiento (el padre de la cámara). Vacío = se usa el padre automáticamente.")]
    [SerializeField] private Transform motionRoot;
    [Tooltip("true = rota alrededor de los ojos. El original gira alrededor de un punto casi a la altura de los ojos (la cámara está 5 cm más arriba), así que true es lo fiel.")]
    [SerializeField] private bool pivotAtEyes = true;

    [Header("Recoil (curvas del original)")]
    [SerializeField] private MotionCurves recoil = MotionData.RecoilCamera();
    [Header("Intensidad por tipo de arma (1 = igual que el original)")]
    [Tooltip("Rifles. Un poco menos que el original.")]
    [SerializeField] private float arCameraMultiplier = 0.8f;
    [SerializeField] private float smgCameraMultiplier = 1f;
    [Tooltip("Pistolas: casi sin patada de cámara.")]
    [SerializeField] private float handgunCameraMultiplier = 0.15f;

    [Header("Estados")]
    [Tooltip("Multiplicador de la cámara al apuntar (1 en el original: al apuntar la cámara patea igual).")]
    [SerializeField] private float aimingMultiplier = 1f;
    [Tooltip("Las curvas del original tienen yaw/roll distinto de cero en el disparo 0. Activado = se resta, así en reposo la cámara no queda torcida.")]
    [SerializeField] private bool removeRestOffset = true;
    [Tooltip("Si pasa más que esto sin disparar, el contador vuelve a 0 y la cámara baja. Mayor que el fireRate de tus armas.")]
    [SerializeField] private float comboResetDelay = 0.3f;

    [Header("Temblor extra (NO está en el original)")]
    [Tooltip("Temblor Perlin al disparar, en grados. 0 = exactamente como el original (que solo tiembla por el vaivén de yaw/roll de las curvas).")]
    [SerializeField] private float extraShakeDeg = 0.12f;
    [SerializeField] private float extraShakeFrequency = 26f;
    [SerializeField] private float extraShakeDecaySpeed = 7f;

    [Header("Salto / caída / aterrizaje de la cámara (valores del original)")]
    [SerializeField] private bool enableJumpMotion = true;
    [SerializeField] private JumpLandMotion jumpMotion = JumpLandMotion.Create(MotionProfile.Camera);

    [Header("Referencias (si están vacías se buscan solas)")]
    [SerializeField] private CharacterController characterController;

    private readonly SpringVector3 rotationSpring = new SpringVector3();
    private readonly SpringVector3 locationSpring = new SpringVector3();

    private Camera cam;
    private Vector3 rootBasePosition;
    private Quaternion rootBaseRotation = Quaternion.identity;
    private bool rootCached;

    private int shotsFired;
    private float lastFireTime = -999f;
    private bool lastAiming;
    private float lastIntensity = 1f;
    private float presetMultiplier = 1f;

    private float shakeTime;
    private float shakeAmount;
    private float seedX, seedY, seedZ;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        seedX = Random.Range(0f, 1000f);
        seedY = Random.Range(0f, 1000f);
        seedZ = Random.Range(0f, 1000f);

        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();

        ResolveMotionRoot();
    }

    [ContextMenu("Restaurar valores del original")]
    private void RestoreOriginal()
    {
        recoil = MotionData.RecoilCamera();
        aimingMultiplier = 1f;
        arCameraMultiplier = 0.8f;
        smgCameraMultiplier = 1f;
        handgunCameraMultiplier = 0.15f;
        jumpMotion.LoadOriginal(MotionProfile.Camera);
    }

    private void ResolveMotionRoot()
    {
        if (motionRoot == null)
        {
            Transform parent = transform.parent;

            // Nunca movemos al jugador completo: solo un padre "intermedio" (PlayerCamera).
            if (parent != null &&
                parent.GetComponent<PlayerMovement>() == null &&
                parent.GetComponent<CharacterController>() == null)
            {
                motionRoot = parent;
            }
        }

        if (motionRoot != null && motionRoot != transform)
        {
            rootBasePosition = motionRoot.localPosition;
            rootBaseRotation = motionRoot.localRotation;
            rootCached = true;
        }
    }

    private void OnDisable()
    {
        if (rootCached && motionRoot != null)
        {
            motionRoot.localPosition = rootBasePosition;
            motionRoot.localRotation = rootBaseRotation;
        }
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

        switch (preset)
        {
            case RecoilPreset.AR: presetMultiplier = arCameraMultiplier; break;
            case RecoilPreset.Handgun: presetMultiplier = handgunCameraMultiplier; break;
            default: presetMultiplier = smgCameraMultiplier; break;
        }

        // El temblor extra también se escala con el tipo de arma.
        shakeAmount = extraShakeDeg * intensityMultiplier * presetMultiplier;
    }

    /// <summary>Compatibilidad con la firma anterior (se trata como SMG).</summary>
    public void Fire(bool aiming, float intensityMultiplier = 1f)
    {
        Fire(RecoilPreset.SMG, aiming, intensityMultiplier);
    }

    /// <summary>Compatibilidad con la firma anterior (se trata como SMG).</summary>
    public void Fire(float intensityMultiplier = 1f)
    {
        Fire(RecoilPreset.SMG, false, intensityMultiplier);
    }

    /// <summary>Empuja la cámara con una rotación puntual (grados) que vuelve sola a 0.</summary>
    public void AddRotationImpulse(Vector3 eulerDegrees)
    {
        rotationSpring.AddImpulse(eulerDegrees);
    }

    private void LateUpdate()
    {
        // Jugadores remotos: su cámara está apagada, no hay nada que mover.
        if (cam != null && !cam.enabled)
            return;

        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        if (shotsFired > 0 && Time.time - lastFireTime > comboResetDelay)
            shotsFired = 0;

        // ---------------- RECOIL ----------------
        float stateMultiplier = (lastAiming ? aimingMultiplier : 1f) * lastIntensity * presetMultiplier;

        Vector3 rotationNow = recoil.EvaluateRotation(shotsFired);
        Vector3 locationNow = recoil.EvaluateLocation(shotsFired);

        if (removeRestOffset)
        {
            rotationNow -= recoil.EvaluateRotation(0f);
            locationNow -= recoil.EvaluateLocation(0f);
        }

        rotationSpring.Apply(recoil.rotationSpring);
        locationSpring.Apply(recoil.locationSpring);
        rotationSpring.SetTarget(rotationNow * (recoil.rotationMultiplier * stateMultiplier));
        locationSpring.SetTarget(locationNow * (recoil.locationMultiplier * stateMultiplier));

        Vector3 rotation = rotationSpring.Evaluate(dt);
        Vector3 position = locationSpring.Evaluate(dt);

        // ---------------- SALTO / CAÍDA / ATERRIZAJE ----------------
        if (enableJumpMotion)
        {
            jumpMotion.Tick(characterController, dt);
            rotation += jumpMotion.Rotation;
            position += jumpMotion.Location;
        }

        // ---------------- TEMBLOR EXTRA ----------------
        shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, extraShakeDecaySpeed * dt);

        if (shakeAmount > 0.0001f)
        {
            shakeTime += dt * extraShakeFrequency;

            rotation.x += (Mathf.PerlinNoise(seedX, shakeTime) - 0.5f) * 2f * shakeAmount;
            rotation.y += (Mathf.PerlinNoise(seedY, shakeTime) - 0.5f) * 2f * shakeAmount;
            rotation.z += (Mathf.PerlinNoise(seedZ, shakeTime) - 0.5f) * shakeAmount;
        }

        Apply(rotation, position);
    }

    private void Apply(Vector3 rotation, Vector3 position)
    {
        Quaternion q = Quaternion.Euler(rotation);

        if (rootCached && motionRoot != null)
        {
            Vector3 p = position;

            // Rotar alrededor de los ojos: el padre se corre lo justo para que la cámara no "barra" un arco.
            if (pivotAtEyes)
            {
                Vector3 pivot = transform.localPosition;
                p += pivot - q * pivot;
            }

            motionRoot.localPosition = rootBasePosition + p;
            motionRoot.localRotation = rootBaseRotation * q;
        }
        else
        {
            // Sin objeto padre disponible: modo de emergencia (solo rotación, sobre la propia cámara).
            transform.localRotation *= q;
        }
    }
}