using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Clips")]
    [SerializeField] private AudioClip[] walkClips;
    [SerializeField] private AudioClip[] runClips;
    [SerializeField] private AudioClip[] crouchClips;

    [Header("Movimiento")]
    [SerializeField] private float movementThreshold = 0.1f;

    private AudioSource audioSource;
    private PlayerControls controls;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();

        if (audioSource != null)
            audioSource.Stop();
    }

    private void Update()
    {
        if (characterController == null)
            return;

        // Si está en el aire, no hay pasos.
        if (!characterController.isGrounded)
        {
            if (audioSource.isPlaying)
                audioSource.Stop();

            return;
        }

        // Leer el estado actual de WASD.
        Vector2 movementInput =
            controls.Player.Move.ReadValue<Vector2>();

        bool isMoving =
            movementInput.sqrMagnitude >
            movementThreshold * movementThreshold;

        // Si soltó todas las teclas, detener el sonido.
        if (!isMoving)
        {
            if (audioSource.isPlaying)
                audioSource.Stop();

            return;
        }

        // Si todavía está sonando el paso anterior,
        // esperamos a que termine.
        if (audioSource.isPlaying)
            return;

        PlayStep();
    }

    private void PlayStep()
    {
        AudioClip[] clipSet;

        if (playerMovement != null &&
            playerMovement.IsCrouching)
        {
            clipSet = crouchClips;
        }
        else if (playerMovement != null &&
                 playerMovement.IsSprinting)
        {
            clipSet = runClips;
        }
        else
        {
            clipSet = walkClips;
        }

        if (clipSet == null || clipSet.Length == 0)
            return;

        AudioClip chosen =
            clipSet[Random.Range(0, clipSet.Length)];

        audioSource.PlayOneShot(chosen);
    }
}