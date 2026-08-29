using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class Casing : MonoBehaviour
{
    [Header("Fuerza de expulsión")]
    [SerializeField] private float minimumXForce = 0.5f;
    [SerializeField] private float maximumXForce = 1.5f;

    [SerializeField] private float minimumYForce = 0.5f;
    [SerializeField] private float maximumYForce = 1.5f;

    [SerializeField] private float minimumZForce = -0.2f;
    [SerializeField] private float maximumZForce = 0.2f;

    [Header("Rotación")]
    [SerializeField] private float minimumRotation = 5f;
    [SerializeField] private float maximumRotation = 20f;

    [Header("Desaparición")]
    [SerializeField] private float despawnTime = 5f;

    [Header("Sonido")]
    [SerializeField] private AudioClip[] casingSounds;
    [SerializeField] private float minSoundDelay = 0.25f;
    [SerializeField] private float maxSoundDelay = 0.85f;

    [Header("Giro")]
    [SerializeField] private float spinSpeed = 250f;

    private Rigidbody rb;
    private AudioSource audioSource;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();

        // Rotación inicial aleatoria.
        transform.rotation = Random.rotation;

        // Fuerza de expulsión.
        Vector3 ejectForce = new Vector3(
            Random.Range(minimumXForce, maximumXForce),
            Random.Range(minimumYForce, maximumYForce),
            Random.Range(minimumZForce, maximumZForce)
        );

        rb.AddRelativeForce(
            ejectForce,
            ForceMode.Impulse
        );

        // Rotación inicial.
        Vector3 randomTorque = new Vector3(
            Random.Range(minimumRotation, maximumRotation),
            Random.Range(minimumRotation, maximumRotation),
            Random.Range(minimumRotation, maximumRotation)
        );

        rb.AddRelativeTorque(
            randomTorque,
            ForceMode.Impulse
        );
    }

    private void Start()
    {
        StartCoroutine(RemoveCasing());
        StartCoroutine(PlaySound());
    }

    private void FixedUpdate()
    {
        // Giro visual adicional.
        transform.Rotate(
            Vector3.right,
            spinSpeed * Time.fixedDeltaTime,
            Space.Self
        );

        transform.Rotate(
            Vector3.down,
            spinSpeed * Time.fixedDeltaTime,
            Space.Self
        );
    }

    private IEnumerator PlaySound()
    {
        if (casingSounds == null || casingSounds.Length == 0)
            yield break;

        yield return new WaitForSeconds(
            Random.Range(
                minSoundDelay,
                maxSoundDelay
            )
        );

        if (audioSource == null)
            yield break;

        AudioClip chosenClip =
            casingSounds[
                Random.Range(0, casingSounds.Length)
            ];

        if (chosenClip != null)
        {
            audioSource.PlayOneShot(chosenClip);
        }
    }

    private IEnumerator RemoveCasing()
    {
        yield return new WaitForSeconds(despawnTime);

        Destroy(gameObject);
    }
}