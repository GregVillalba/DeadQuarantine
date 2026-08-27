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

    

    private ParticleSystem particles;
    private Light flashLight;
    private Coroutine lightCoroutine;

    private void Awake()
    {
        if (socket == null)
            socket = transform;

    if (flashParticlesPrefab != null)
    {
        GameObject particlesObject =
            Instantiate(flashParticlesPrefab, socket);

        particlesObject.transform.localPosition = Vector3.zero;
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