using UnityEngine;

/// <summary>
/// Movimiento muy lento de cámara para el fondo del menú.
/// Los puntos del builder quedan todos FUERA de la mansión, por lo que el loop
/// no atraviesa paredes ni recorre el interior.
/// </summary>
public class MainMenuCinematicBackground : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform lookTarget;
    [SerializeField] private Transform[] cameraPoints;

    [Header("Recorrido")]
    [Min(8f)]
    [SerializeField] private float loopDuration = 28f;

    [Header("Movimiento sutil")]
    [SerializeField] private float handheldPositionAmount = 0.006f;
    [SerializeField] private float handheldRotationAmount = 0.055f;
    [SerializeField] private float handheldSpeed = 0.35f;

    [Header("Lente")]
    [SerializeField] private float baseFov = 46f;
    [SerializeField] private float fovBreathing = 0.22f;
    [SerializeField] private float fovBreathingSpeed = 0.10f;

    private float playbackStartTime;

    public void Configure(Camera camera, Transform target, Transform[] points)
    {
        targetCamera = camera;
        lookTarget = target;
        cameraPoints = points;

        if (targetCamera != null)
            targetCamera.fieldOfView = baseFov;
    }

    private void OnEnable()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        // El loop siempre arranca desde Point_01,
        // que es exactamente el Transform de tu segunda captura.
        playbackStartTime = Time.unscaledTime;

        if (targetCamera != null &&
            cameraPoints != null &&
            cameraPoints.Length > 0)
        {
            targetCamera.transform.position =
                cameraPoints[0].position;
        }

        if (targetCamera != null &&
            lookTarget != null)
        {
            Vector3 dir =
                lookTarget.position -
                targetCamera.transform.position;

            if (dir.sqrMagnitude > 0.0001f)
            {
                targetCamera.transform.rotation =
                    Quaternion.LookRotation(
                        dir.normalized,
                        Vector3.up
                    );
            }
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null ||
            lookTarget == null ||
            cameraPoints == null ||
            cameraPoints.Length < 2)
        {
            return;
        }

        float elapsed =
            Mathf.Max(
                0f,
                Time.unscaledTime - playbackStartTime
            );

        float t = Mathf.Repeat(
            elapsed / Mathf.Max(8f, loopDuration),
            1f
        );

        Vector3 desiredPosition = EvaluateClosedPath(t);

        float noiseTime = Time.unscaledTime * handheldSpeed;

        desiredPosition +=
            targetCamera.transform.right *
            ((Mathf.PerlinNoise(noiseTime, 1.31f) - 0.5f) * 2f) *
            handheldPositionAmount;

        desiredPosition +=
            targetCamera.transform.up *
            ((Mathf.PerlinNoise(4.77f, noiseTime) - 0.5f) * 2f) *
            handheldPositionAmount;

        targetCamera.transform.position = desiredPosition;

        Vector3 direction =
            lookTarget.position - targetCamera.transform.position;

        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction.normalized, Vector3.up);

            float pitch =
                (Mathf.PerlinNoise(noiseTime * 0.8f, 8.13f) - 0.5f) *
                2f * handheldRotationAmount;

            float yaw =
                (Mathf.PerlinNoise(12.71f, noiseTime * 0.8f) - 0.5f) *
                2f * handheldRotationAmount;

            targetRotation *= Quaternion.Euler(pitch, yaw, 0f);

            targetCamera.transform.rotation =
                Quaternion.Slerp(
                    targetCamera.transform.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-3.5f * Time.unscaledDeltaTime)
                );
        }

        targetCamera.fieldOfView =
            baseFov +
            Mathf.Sin(
                Time.unscaledTime *
                fovBreathingSpeed *
                Mathf.PI * 2f
            ) * fovBreathing;
    }

    private Vector3 EvaluateClosedPath(float t)
    {
        int count = cameraPoints.Length;

        if (count == 2)
        {
            float ping = Mathf.PingPong(t * 2f, 1f);
            return Vector3.Lerp(
                cameraPoints[0].position,
                cameraPoints[1].position,
                Mathf.SmoothStep(0f, 1f, ping)
            );
        }

        float scaled = t * count;
        int current = Mathf.FloorToInt(scaled) % count;
        float localT = scaled - Mathf.Floor(scaled);

        int p0 = (current - 1 + count) % count;
        int p1 = current;
        int p2 = (current + 1) % count;
        int p3 = (current + 2) % count;

        return CatmullRom(
            cameraPoints[p0].position,
            cameraPoints[p1].position,
            cameraPoints[p2].position,
            cameraPoints[p3].position,
            localT
        );
    }

    private static Vector3 CatmullRom(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }
}
