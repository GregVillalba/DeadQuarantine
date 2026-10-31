using System.Collections;
using UnityEngine;

public class Muzzle : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform socket;

    [Header("Partículas")]
    [SerializeField] private GameObject flashParticlesPrefab;
    [SerializeField] private int flashParticlesCount = 5;

    [Header("Flash de luz")]
    [SerializeField] private GameObject flashLightPrefab;
    [SerializeField] private float flashLightDuration = 0.05f;
    [SerializeField] private Vector3 flashLightOffset;

    

    public bool HasParticleEffect => particles != null;
    private ParticleSystem particles;
    private Light flashLight;
    private Coroutine lightCoroutine;

    public void CopySettingsFrom(Muzzle source)
    {
        flashParticlesPrefab=source.flashParticlesPrefab;flashParticlesCount=source.flashParticlesCount;
        flashLightPrefab=source.flashLightPrefab;flashLightDuration=source.flashLightDuration;flashLightOffset=source.flashLightOffset;
    }
    private void Awake()
    {
        if (socket == null)
            socket = transform;

    if (flashParticlesPrefab != null)
    {
        GameObject particlesObject =
            Instantiate(flashParticlesPrefab, socket);

        particlesObject.transform.localPosition = Vector3.zero;
        // The effect asset is authored facing local -Y (approximately +90 degrees around X).
        // Preserve that rotation on the original weapon socket; do not add another barrel LookRotation.
        particlesObject.transform.localRotation = flashParticlesPrefab.transform.localRotation;
        particlesObject.layer = gameObject.layer;

        particles =
            particlesObject.GetComponent<ParticleSystem>();
    }

        if (flashLightPrefab != null)
        {
            GameObject lightObject =
                Instantiate(flashLightPrefab, socket);

            lightObject.transform.localPosition = flashLightOffset;
            lightObject.transform.localRotation = Quaternion.identity;
            lightObject.layer = gameObject.layer;

            flashLight =
                lightObject.GetComponent<Light>();

            if (flashLight != null)
                flashLight.enabled = false;
        }
    }

    public void PlayEffect()
    {
        // Partículas.
        if (particles != null)
        {
            particles.Emit(flashParticlesCount);
        }

        // Flash.
        if (flashLight != null)
        {
            flashLight.enabled = true;

            if (lightCoroutine != null)
                StopCoroutine(lightCoroutine);

            lightCoroutine = StartCoroutine(DisableLight());
        }
    }

    private IEnumerator DisableLight()
    {
        yield return new WaitForSeconds(flashLightDuration);

        if (flashLight != null)
            flashLight.enabled = false;

        lightCoroutine = null;
    }
}