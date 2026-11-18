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
    private Image damageArc;
    private Image acidVignette;
    private Texture2D arcTexture;
    private Sprite arcSprite;
    private float arcStarted;
    private Vector3 arcSource;

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
            playerHealth.OnDirectionalDamage += ShowDamageArc;
            playerHealth.OnHealthChanged +=
                OnHealthChanged;

            // Si el golpe lo absorbe el escudo la vida no cambia: también hace el shake de cámara.
            playerHealth.OnEscudoDanado +=
                StartDamageShake;

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
        if (damageArc) Destroy(damageArc.gameObject);
        if (acidVignette) Destroy(acidVignette.gameObject);
        if (arcSprite) Destroy(arcSprite);
        if (arcTexture) Destroy(arcTexture);
        if (playerHealth != null)
        {
            playerHealth.OnDirectionalDamage -= ShowDamageArc;
            playerHealth.OnHealthChanged -=
                OnHealthChanged;

            playerHealth.OnEscudoDanado -=
                StartDamageShake;
        }
    }

    private void ShowDamageArc(Vector3 source)
    {
        if (!damageVignette || !cameraShakeTransform) return;
        if (!damageArc) CreateDamageArc();
        arcSource = source;
        arcStarted = Time.unscaledTime;
        damageArc.enabled = true;
        UpdateDamageArc();
    }

    private void LateUpdate()
    {
        if (damageArc && damageArc.enabled) UpdateDamageArc();
        bool acid = playerHealth && playerHealth.HasStalkerAcid && networkObject && networkObject.IsOwner;
        if (acid && !acidVignette && damageVignette)
        {
            acidVignette = new GameObject("Acido del Acechador", typeof(Image)).GetComponent<Image>();
            acidVignette.transform.SetParent(damageVignette.transform.parent, false);
            acidVignette.sprite = damageVignette.sprite;
            acidVignette.type = damageVignette.type;
            acidVignette.raycastTarget = false;
            var rect = acidVignette.rectTransform;
            rect.anchorMin = damageVignette.rectTransform.anchorMin; rect.anchorMax = damageVignette.rectTransform.anchorMax;
            rect.pivot = damageVignette.rectTransform.pivot; rect.anchoredPosition = damageVignette.rectTransform.anchoredPosition;
            rect.sizeDelta = damageVignette.rectTransform.sizeDelta;
            acidVignette.transform.SetSiblingIndex(damageVignette.transform.GetSiblingIndex() + 1);
        }
        if (acidVignette)
        {
            acidVignette.enabled = acid;
            acidVignette.color = new Color(.35f, .85f, .05f, .09f + Mathf.Sin(Time.unscaledTime * 4) * .025f);
        }
    }

    private void UpdateDamageArc()
    {
        float alpha = 1f - (Time.unscaledTime - arcStarted);
        if (alpha <= 0)
        {
            damageArc.enabled = false;
            return;
        }

        Vector3 delta = Vector3.ProjectOnPlane(arcSource - playerHealth.transform.position, Vector3.up);
        Vector3 forward = Vector3.Cross(cameraShakeTransform.right, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float bearing = Mathf.Atan2(Vector3.Dot(delta, right), Vector3.Dot(delta, forward)) * Mathf.Rad2Deg;
        damageArc.rectTransform.localRotation = Quaternion.Euler(0, 0, -bearing);
        damageArc.color = new Color(1f, .22f, .18f, alpha * .9f);
    }

    private void CreateDamageArc()
    {
        const int size = 192;
        arcTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        arcTexture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var point = new Vector2(x + .5f - size / 2f, y + .5f - size / 2f);
            float radius = point.magnitude;
            float angle = Mathf.Abs(Mathf.Atan2(point.x, point.y) * Mathf.Rad2Deg);
            float edge = Mathf.Clamp01(3.8f - Mathf.Abs(radius - 79f));
            float taper = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(18f, 34f, angle));
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255 * edge * taper));
        }
        arcTexture.SetPixels32(pixels);
        arcTexture.Apply(false, true);
        arcSprite = Sprite.Create(arcTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        damageArc = new GameObject("Dirección de daño", typeof(Image)).GetComponent<Image>();
        damageArc.transform.SetParent(damageVignette.transform.parent, false);
        damageArc.transform.SetSiblingIndex(damageVignette.transform.GetSiblingIndex() + 1);
        damageArc.sprite = arcSprite;
        damageArc.raycastTarget = false;
        damageArc.rectTransform.sizeDelta = new Vector2(264, 264);
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
        var equipped=GetComponentInParent<WeaponSwitcher>()?.CurrentWeapon;
        if(!equipped)equipped=GetComponentInParent<PlayerHealth>()?.GetComponentInChildren<WeaponSwitcher>()?.CurrentWeapon;
        float attachmentShake=equipped?equipped.AttachmentIncomingShake:1;

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
                strength*attachmentShake;

            randomPosition.z = 0f;

            float randomRotation =
                Random.Range(
                    -shakeRotation,
                    shakeRotation
                ) *
                (1f - progress)*attachmentShake;

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