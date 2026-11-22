using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

/// <summary>Clima local de la mansión, compartido por partidas y fondos del menú.</summary>
[DisallowMultipleComponent]
public sealed class MansionWeather : MonoBehaviour
{
    [SerializeField] ParticleSystem rain;
    [SerializeField] ParticleSystem splashes;
    [SerializeField] Transform exteriorMist;
    [SerializeField] AudioSource rainAudio;
    [SerializeField] Bounds exteriorBounds = new(Vector3.zero, new Vector3(220,100,220));
    [SerializeField] Bounds interiorBounds;
    [SerializeField] float rainHeight = 32;
    [SerializeField, Range(0,1)] float outdoorVolume = .08f;
    [SerializeField, Range(0,1)] float shelteredVolume = .025f;
    [SerializeField] Camera targetCamera;
    [SerializeField] AudioSource thunderAudio;
    [SerializeField] ParticleSystem[] lightningEffects;
    static readonly HashSet<MansionWeather> activeWeather = new();
    static int nextOrder;
    int order;
    readonly List<float> previousLightningTimes = new();
    float nextThunder;
    Camera boundCamera;
    ParticleSystem[] fogSystems;
    ParticleSystem.Particle[] fogBuffer, rainBuffer, splashBuffer;
    ParticleSystemRenderer rainRenderer;
    bool running, sheltered;
    float nextShelterCheck;
    public ParticleSystem Rain => rain;
    public ParticleSystem Splashes => splashes;
    public AudioSource RainAudio => rainAudio;
    public bool IsSheltered => sheltered;
    public bool IsRunning => running;
    public static bool Loading => SceneTransition.IsLoading || GameStartCurtain.CargaEnCurso || DeadQuarantine.PvPvE.PvPvELoadingScreen.AnyVisible;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetWeather() { activeWeather.Clear(); nextOrder = 0; }
    void OnEnable() { order = ++nextOrder; activeWeather.Add(this); }
    public void Follow(Camera camera) { targetCamera = camera; boundCamera = camera; }
    void Awake()
    {
        GameAudio.Route(rainAudio, GameAudioChannel.Ambience);
        GameAudio.Route(thunderAudio, GameAudioChannel.Ambience);
        fogSystems = exteriorMist ? exteriorMist.GetComponentsInChildren<ParticleSystem>(true) : System.Array.Empty<ParticleSystem>();
        int capacity = 1; foreach (var system in fogSystems) capacity = Mathf.Max(capacity, system.main.maxParticles);
        fogBuffer = new ParticleSystem.Particle[capacity];
        rainBuffer = new ParticleSystem.Particle[rain.main.maxParticles];
        splashBuffer = new ParticleSystem.Particle[splashes.main.maxParticles];
        rainRenderer = rain.GetComponent<ParticleSystemRenderer>();
        if (rainAudio) rainAudio.ignoreListenerPause = false;
        if (thunderAudio) thunderAudio.ignoreListenerPause = false;
        if (lightningEffects != null) foreach (var effect in lightningEffects) previousLightningTimes.Add(-1);
        // Un fondo oculto no debe seguir la cámara de otro lobby situado en el mismo lugar.
        if (!targetCamera)
            for (Transform parent = transform.parent; parent && !boundCamera; parent = parent.parent)
                foreach (var candidate in parent.GetComponentsInChildren<Camera>(true))
                    if (candidate.name == "LobbyCinematicCamera" || candidate.name == "MainMenuCamera") { boundCamera = candidate; break; }
    }
    Camera FindCamera()
    {
        if (targetCamera) return targetCamera;
        if (boundCamera) return boundCamera;
        var visible = VisibleCamera();
        return visible && exteriorBounds.Contains(transform.InverseTransformPoint(visible.transform.position)) ? visible : null;
    }
    static Camera VisibleCamera()
    {
        Camera visible = null;
        foreach (var camera in Camera.allCameras)
        {
            if (!camera.isActiveAndEnabled || camera.cameraType != CameraType.Game || camera.targetTexture || camera.cullingMask == 0 ||
                camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.renderType != CameraRenderType.Base) continue;
            if (!visible || camera.depth > visible.depth) visible = camera;
        }
        return visible;
    }
    bool OwnsView(Camera camera)
    {
        if (!camera || camera != VisibleCamera()) return false;
        MansionWeather owner = null;
        foreach (var weather in activeWeather)
        {
            if (!weather || !weather.isActiveAndEnabled || weather.FindCamera() != camera ||
                !weather.exteriorBounds.Contains(weather.transform.InverseTransformPoint(camera.transform.position))) continue;
            if (!owner || (weather.boundCamera == camera && owner.boundCamera != camera) ||
                weather.boundCamera == owner.boundCamera && weather.order < owner.order) owner = weather;
        }
        return owner == this;
    }
    void LateUpdate()
    {
        var camera = FindCamera();
        bool visible = !Loading && camera && camera.isActiveAndEnabled && OwnsView(camera) && exteriorBounds.Contains(transform.InverseTransformPoint(camera.transform.position));
        if (!visible) { StopWeather(); return; }
        Vector3 position = camera.transform.position;
        float ceiling = transform.TransformPoint(new Vector3(0, rainHeight, 0)).y;
        rain.transform.SetPositionAndRotation(new Vector3(position.x, Mathf.Max(ceiling, position.y + 8), position.z), Quaternion.identity);
        if (!running)
        {
            running = true; rain.Play(false);
            foreach (var system in fogSystems) system.Play(false);
            if (rainAudio && rainAudio.clip) { rainAudio.volume = 0; rainAudio.Play(); }
        }
        if (Time.unscaledTime >= nextShelterCheck)
        {
            nextShelterCheck = Time.unscaledTime + .2f;
            sheltered = false;
            foreach (var hit in Physics.RaycastAll(position + Vector3.up * .1f, Vector3.up, Mathf.Max(12, ceiling - position.y + 5), ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<PlayerHealth>() || hit.collider.GetComponentInParent<ZombieHealth>() || hit.collider.GetComponentInParent<MansionWeather>()) continue;
                sheltered = true; break;
            }
            TrimInteriorFog();
        }
        // Los techos decorativos pueden no tener collider. Excluir también el volumen
        // interior y toda la estela visible, sin apagar la lluvia que se ve por las ventanas.
        TrimInteriorPrecipitation(rain, rainBuffer, true);
        TrimInteriorPrecipitation(splashes, splashBuffer, false);
        float rainVolume = boundCamera ? (sheltered ? .008f : .025f) : (sheltered ? shelteredVolume : outdoorVolume);
        if (rainAudio) rainAudio.volume = Mathf.MoveTowards(rainAudio.volume, rainVolume, Time.unscaledDeltaTime * .06f);
        UpdateThunder();
    }
    void UpdateThunder()
    {
        if (lightningEffects == null || !thunderAudio || !thunderAudio.clip) return;
        for (int i = 0; i < lightningEffects.Length; i++)
        {
            var effect = lightningEffects[i];
            if (!effect) continue;
            float time = effect.time;
            bool strike = effect.isPlaying && effect.particleCount > 0 &&
                (previousLightningTimes[i] < 0 || time < previousLightningTimes[i]);
            previousLightningTimes[i] = effect.isPlaying && effect.particleCount > 0 ? time : -1;
            if (!strike || Time.unscaledTime < nextThunder || AudioListener.pause) continue;
            PlayThunder();
        }
    }
    public void PlayThunder()
    {
        if (!running || Loading || AudioListener.pause || !thunderAudio || !thunderAudio.clip || Time.unscaledTime < nextThunder) return;
        thunderAudio.volume = sheltered ? .12f : .24f;
        thunderAudio.Play();
        nextThunder = Time.unscaledTime + 2;
    }
    void TrimInteriorPrecipitation(ParticleSystem system, ParticleSystem.Particle[] buffer, bool stretched)
    {
        int count = system.GetParticles(buffer);
        bool changed = false;
        var main = system.main;
        Transform space = main.simulationSpace == ParticleSystemSimulationSpace.Custom ? main.customSimulationSpace : system.transform;
        for (int i = 0; i < count; i++)
        {
            var particle = buffer[i];
            bool worldSpace = main.simulationSpace == ParticleSystemSimulationSpace.World;
            Vector3 world = worldSpace ? particle.position : space.TransformPoint(particle.position);
            Vector3 velocity = worldSpace ? particle.velocity : space.TransformVector(particle.velocity);
            float size = particle.GetCurrentSize(system);
            // Incluye el tamaño de la estela y el recorrido de este frame para que una
            // gota no asome por el techo antes de que su centro entre en la casa.
            float halfLength = stretched ? size * Mathf.Max(1, rainRenderer.lengthScale) * .5f : size * .5f;
            Vector3 reach = transform.InverseTransformVector(velocity.normalized * halfLength + velocity * Time.deltaTime);
            Vector3 width = transform.InverseTransformVector(Vector3.one * (size * .5f + .1f));
            var exclusion = interiorBounds;
            exclusion.Expand(new Vector3(Mathf.Abs(reach.x) + Mathf.Abs(width.x), Mathf.Abs(reach.y) + Mathf.Abs(width.y), Mathf.Abs(reach.z) + Mathf.Abs(width.z)) * 2);
            if (!exclusion.Contains(transform.InverseTransformPoint(world))) continue;
            particle.remainingLifetime = 0;
            buffer[i] = particle;
            changed = true;
        }
        if (changed) system.SetParticles(buffer, count);
    }
    void TrimInteriorFog()
    {
        foreach (var system in fogSystems)
        {
            int count = system.GetParticles(fogBuffer); bool changed = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 world = system.main.simulationSpace == ParticleSystemSimulationSpace.World ? fogBuffer[i].position : system.transform.TransformPoint(fogBuffer[i].position);
                if (!interiorBounds.Contains(transform.InverseTransformPoint(world))) continue;
                fogBuffer[i].remainingLifetime = 0; changed = true;
            }
            if (changed) system.SetParticles(fogBuffer, count);
        }
    }
    void StopWeather()
    {
        if (rain) rain.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (splashes) splashes.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (fogSystems != null) foreach (var system in fogSystems) if (system) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (rainAudio) { rainAudio.Stop(); rainAudio.volume = 0; }
        if (thunderAudio) thunderAudio.Stop();
        for (int i = 0; i < previousLightningTimes.Count; i++) previousLightningTimes[i] = -1;
        running = false;
    }
    void OnDisable() { activeWeather.Remove(this); StopWeather(); }
}
