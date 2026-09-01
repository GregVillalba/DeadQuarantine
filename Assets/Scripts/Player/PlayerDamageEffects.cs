using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class PlayerDamageEffects : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Transform cameraShakeTransform;
    [SerializeField] private Image damageVignette;

    [Header("Shake de cámara")]
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeIntensity = 0.08f;
    [SerializeField] private float shakeRotation = 2f;

    [Header("Borde rojo")]
    [SerializeField] private float highHealthThreshold = 0.90f;
    [SerializeField] private float healthWarningThreshold = 0.50f;
    [SerializeField] private float criticalHealthThreshold = 0.25f;

    [SerializeField] private float softVignetteAlpha = 0.05f;
    [SerializeField] private float normalVignetteAlpha = 0.20f;
    [SerializeField] private float criticalVignetteAlpha = 0.45f;
    private NetworkObject networkObject;

    private Vector3 originalPosition;
    private Quaternion originalRotation;

    private Coroutine shakeCoroutine;

    private void Awake()
    {
        networkObject =
            GetComponentInParent<NetworkObject>();

        if (cameraShakeTransform == null)
        {
            cameraShakeTransform = transform;
        }

        originalPosition =
            cameraShakeTransform.localPosition;

        originalRotation =
            cameraShakeTransform.localRotation;
    }

    private void Start()
    {
        // Este efecto solamente funciona
        // para el jugador local.
        if (networkObject != null &&
            !networkObject.IsOwner)
        {
            enabled = false;
            return;
        }

        if (playerHealth == null)
        {
            playerHealth =
                GetComponentInParent<PlayerHealth>();
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged +=
                OnHealthChanged;

            UpdateVignette(
                playerHealth.CurrentHealth.Value
            );
        }
        else
        {
            Debug.LogError(
                "[PlayerDamageEffects] " +
                "No se encontró PlayerHealth."
            );
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -=
                OnHealthChanged;
        }
    }

    private void OnHealthChanged(
        int previousHealth,
        int newHealth
    )
    {
        // Si perdió vida, hacer shake.
        if (newHealth < previousHealth)
        {
            StartDamageShake();
        }

        // Actualizar borde según la vida actual.
        UpdateVignette(newHealth);
    }

    // =========================================================
    // SHAKE
    // =========================================================

    private void StartDamageShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }

        shakeCoroutine =
            StartCoroutine(
                DamageShakeRoutine()
            );
    }

    private IEnumerator DamageShakeRoutine()
    {
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                elapsed / shakeDuration;

            float strength =
                Mathf.Lerp(
                    shakeIntensity,
                    0f,
                    progress
                );

            Vector3 randomPosition =
                Random.insideUnitSphere *
                strength;

            randomPosition.z = 0f;

            float randomRotation =
                Random.Range(
                    -shakeRotation,
                    shakeRotation
                ) *
                (1f - progress);

            cameraShakeTransform.localPosition =
                originalPosition +
                randomPosition;

            cameraShakeTransform.localRotation =
                originalRotation *
                Quaternion.Euler(
                    0f,
                    0f,
                    randomRotation
                );

            yield return null;
        }

        cameraShakeTransform.localPosition =
            originalPosition;

        cameraShakeTransform.localRotation =
            originalRotation;

        shakeCoroutine = null;
    }

    // =========================================================
    // BORDE ROJO
    // =========================================================

    private void UpdateVignette(int currentHealth)
    {
        if (damageVignette == null ||
            playerHealth == null)
        {
            return;
        }

        float healthPercentage =
            currentHealth /
            (float)playerHealth.MaxHealth;

        float alpha = 0f;

        // Más del 90%:
        // completamente invisible.
        if (healthPercentage > highHealthThreshold)
        {
            alpha = 0f;
        }
        // Entre 90% y 50%:
        // intensidad muy suave.
        else if (healthPercentage > healthWarningThreshold)
        {
            alpha = softVignetteAlpha;
        }
        // Entre 50% y 25%:
        // intensidad normal.
        else if (healthPercentage > criticalHealthThreshold)
        {
            alpha = normalVignetteAlpha;
        }
        // 25% o menos:
        // intensidad fuerte.
        else
        {
            alpha = criticalVignetteAlpha;
        }

        Color color = damageVignette.color;
        color.a = alpha;
        damageVignette.color = color;
    }
}