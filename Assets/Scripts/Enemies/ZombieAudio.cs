using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(AudioSource))]
public class ZombieAudio : MonoBehaviour
{
    [Header("Gruñidos ambientales (caminando)")]
    [SerializeField] private AudioClip[] growlClips;
    [SerializeField] private float growlIntervalMin = 3f;
    [SerializeField] private float growlIntervalMax = 8f;

    [Header("Gruñidos al correr")]
    [SerializeField] private AudioClip[] runGrowlClips;
    [SerializeField] private float runGrowlIntervalMin = 1.5f;
    [SerializeField] private float runGrowlIntervalMax = 4f;

    [Header("Pasos")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private float footstepInterval = 0.5f;
    [SerializeField] private float movementThreshold = 0.1f;

    [Header("Sonido al estar detrás del jugador")]
    [SerializeField] private AudioClip[] behindPlayerClips;
    [SerializeField] private float behindPlayerIntervalMin = 2f;
    [SerializeField] private float behindPlayerIntervalMax = 5f;

    [Header("Otros sonidos (opcionales)")]
    [SerializeField] private AudioClip[] attackClips;
    [SerializeField] private AudioClip[] hitClips;
    [SerializeField] private AudioClip[] deathClips;

    private AudioSource audioSource;
    private AudioSource footstepSource;
    private NavMeshAgent agent;
    private ZombieHealth zombieHealth;
    private ZombieAI zombieAI;

    private float nextGrowlTime;
    private float nextFootstepTime;
    private float nextBehindPlayerTime;

    // Control propio de "ocupado", para no depender solo de
    // audioSource.isPlaying (que puede tener un frame de margen
    // de error y permitir un disparo doble).
    private float mainSourceBusyUntil;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        agent = GetComponent<NavMeshAgent>();
        zombieHealth = GetComponent<ZombieHealth>();
        zombieAI = GetComponent<ZombieAI>();

        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;
        footstepSource.loop = false;
        footstepSource.spatialBlend = 1f;
        footstepSource.minDistance = audioSource.minDistance;
        footstepSource.maxDistance = audioSource.maxDistance;
        footstepSource.volume = 0.5f;

        ScheduleNextGrowl();
        ScheduleNextBehindPlayer();
    }

    private void Update()
    {
        if (zombieHealth != null && zombieHealth.IsDead) return;

        HandleGrowl();
        HandleFootsteps();
        HandleBehindPlayer();
    }

    private bool IsMainSourceBusy()
    {
        return Time.time < mainSourceBusyUntil;
    }

    private void HandleGrowl()
    {
        if (IsMainSourceBusy()) return;
        if (Time.time < nextGrowlTime) return;

        bool isRunning = zombieAI != null && zombieAI.IsRunning;

        AudioClip[] clipSet = isRunning ? runGrowlClips : growlClips;

        PlayOnMainSource(clipSet);
        ScheduleNextGrowl(isRunning);
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

    private void HandleBehindPlayer()
    {
        if (zombieAI == null) return;
        if (IsMainSourceBusy()) return;
        if (Time.time < nextBehindPlayerTime) return;

        if (zombieAI.IsBehindTarget())
        {
            PlayOnMainSource(behindPlayerClips);
        }

        ScheduleNextBehindPlayer();
    }

    // Reproduce en el audioSource principal y marca "ocupado"
    // hasta que termine el clip, sin importar de qué evento venga
    // (gruñido normal, gruñido de correr, o sonido de "detrás").
    private void PlayOnMainSource(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip chosen = clips[Random.Range(0, clips.Length)];

        audioSource.PlayOneShot(chosen);

        mainSourceBusyUntil = Time.time + chosen.length;
    }

    private void ScheduleNextGrowl(bool isRunning = false)
    {
        float min = isRunning ? runGrowlIntervalMin : growlIntervalMin;
        float max = isRunning ? runGrowlIntervalMax : growlIntervalMax;

        nextGrowlTime = Time.time + Random.Range(min, max);
    }

    private void ScheduleNextBehindPlayer()
    {
        nextBehindPlayerTime = Time.time + Random.Range(behindPlayerIntervalMin, behindPlayerIntervalMax);
    }

    public void PlayAttackSound() => PlayOnMainSource(attackClips);
    public void PlayHitSound() => PlayOnMainSource(hitClips);

    public void PlayDeathSound()
    {
        audioSource.Stop();
        footstepSource.Stop();

        PlayOnMainSource(deathClips);
    }

    private void PlayRandomClip(AudioSource source, AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip chosen = clips[Random.Range(0, clips.Length)];
        source.PlayOneShot(chosen);
    }
}