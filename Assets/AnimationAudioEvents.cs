using UnityEngine;

public class AnimationAudioEvents : StateMachineBehaviour
{
    [Header("Setup")]
    [SerializeField] private AudioClip clip;

    [Header("Settings")]
    [SerializeField, Range(0f, 1f)]
    private float volume = 1f;

    public override void OnStateEnter(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        if (clip == null)
            return;

        AudioSource audioSource =
            animator.GetComponent<AudioSource>();

        if (audioSource == null)
            return;

        audioSource.spatialBlend = 0f;
        audioSource.panStereo = 0f;

        audioSource.PlayOneShot(
            clip,
            volume
        );
    }
}