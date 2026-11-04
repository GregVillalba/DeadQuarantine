using UnityEngine;

/// <summary>
/// Destellos sutiles de tormenta para el menú.
/// </summary>
public class MainMenuLightning : MonoBehaviour
{
    [SerializeField] private Light lightningLight;
    [SerializeField] private Vector2 intervalRange = new Vector2(8f, 18f);
    [SerializeField] private Vector2 intensityRange = new Vector2(1.2f, 2.6f);
    [SerializeField] private float flashDuration = 0.10f;

    private float nextFlash;
    private float flashEnd;

    public void Configure(Light light)
    {
        lightningLight = light;
    }

    private void OnEnable()
    {
        ScheduleNext();
        if (lightningLight != null)
            lightningLight.intensity = 0f;
    }

    private void Update()
    {
        if (lightningLight == null)
            return;

        float now = Time.unscaledTime;

        if (lightningLight.intensity > 0f)
        {
            if (now >= flashEnd)
            {
                lightningLight.intensity = 0f;
                ScheduleNext();
            }
            return;
        }

        if (now >= nextFlash)
        {
            lightningLight.intensity =
                Random.Range(
                    Mathf.Min(intensityRange.x, intensityRange.y),
                    Mathf.Max(intensityRange.x, intensityRange.y)
                );

            flashEnd = now + flashDuration;
        }
    }

    private void ScheduleNext()
    {
        nextFlash =
            Time.unscaledTime +
            Random.Range(
                Mathf.Min(intervalRange.x, intervalRange.y),
                Mathf.Max(intervalRange.x, intervalRange.y)
            );
    }
}
