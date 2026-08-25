using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(AudioSource))]
public class ZombieAudio : MonoBehaviour
{
    [Header("Gruñidos ambientales")]
    [SerializeField] private AudioClip[] growlClips;
    [SerializeField] private float growlIntervalMin = 3f;
    [SerializeField] private float growlIntervalMax = 8f;

    [Header("Pasos")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private float footstepInterval = 0.5f;
    [SerializeField] private float movementThreshold = 0.1f;

    [Header("Otros sonidos (opcionales)")]
    [SerializeField] private AudioClip[] attackClips;
    [SerializeField] private AudioClip[] hitClips;
    [SerializeField] private AudioClip[] deathClips;

    private AudioSource audioSource;
    private AudioSource footstepSource;
    private NavMeshAgent agent;
    private ZombieHealth zombieHealth;

    private float nextGrowlTime;
    private float nextFootstepTime;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        agent = GetComponent<NavMeshAgent>();
        zombieHealth = GetComponent<ZombieHealth>();

        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;
        footstepSource.loop = false;
        footstepSource.spatialBlend = 1f;
        footstepSource.minDistance = audioSource.minDistance;
        footstepSource.maxDistance = audioSource.maxDistance;
        footstepSource.volume = 0.5f;

        ScheduleNextGrowl();
    }

    private void Update()
    {
        if (zombieHealth != null && zombieHealth.IsDead) return;

        HandleGrowl();
        HandleFootsteps();
    }

    private void HandleGrowl()
    {
        if (audioSource.isPlaying) return;

        if (Time.time >= nextGrowlTime)
        {
            PlayRandomClip(audioSource, growlClips);
            ScheduleNextGrowl();
        }
    }

    private void HandleFootsteps()
    {
        bool isMoving = agent != null && agent.velocity.magnitude > movementThreshold;

        if (isMoving && Time.time >= nextFootstepTime)
        {
            PlayRandomClip(footstepSource, footstepClips);
            nextFootstepTime = Time.time + footstepInterval;
        }
    }

    private void ScheduleNextGrowl()
    {
        nextGrowlTime = Time.time + Random.Range(growlIntervalMin, growlIntervalMax);
    }

    public void PlayAttackSound() => PlayRandomClip(audioSource, attackClips);
    public void PlayHitSound() => PlayRandomClip(audioSource, hitClips);
    
    public void PlayDeathSound()
    {
        audioSource.Stop();
        footstepSource.Stop();

        PlayRandomClip(audioSource, deathClips);
    }

    private void PlayRandomClip(AudioSource source, AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip chosen = clips[Random.Range(0, clips.Length)];
        source.PlayOneShot(chosen);
    }
}