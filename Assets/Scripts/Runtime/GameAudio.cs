using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public enum GameAudioChannel { Music, Ambience, Effects, UI }

/// <summary>Canales de los sonidos existentes. El volumen general sigue usando ConfiguracionesJuego.</summary>
public static class GameAudio
{
    static AudioMixer mixer;
    static readonly Dictionary<GameAudioChannel, AudioMixerGroup> groups = new();
    static bool initialized;
    static float nextScan;
    public static float Volume(GameAudioChannel channel) => Mathf.Clamp01(PlayerPrefs.GetFloat("Audio_" + channel, 1));
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        initialized = false; mixer = null; groups.Clear(); nextScan = 0;
    }
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        mixer = Resources.Load<AudioMixer>("GameAudioMixer");
        if (mixer) foreach (GameAudioChannel channel in Enum.GetValues(typeof(GameAudioChannel)))
        {
            var matches = mixer.FindMatchingGroups(channel.ToString());
            if (matches.Length > 0) groups[channel] = matches[0];
        }
        SceneManager.sceneLoaded += SceneLoaded;
        RoutePending(true);
    }
    static void SceneLoaded(Scene scene, LoadSceneMode mode) => RoutePending(true);
    public static void SetVolume(GameAudioChannel channel, float value)
    {
        PlayerPrefs.SetFloat("Audio_" + channel, Mathf.Clamp01(value));
        ApplyVolumes();
    }
    public static void ApplyVolumes()
    {
        Initialize();
        if (!mixer) return;
        foreach (GameAudioChannel channel in Enum.GetValues(typeof(GameAudioChannel)))
        {
            float v = Volume(channel);
            mixer.SetFloat(channel.ToString(), v <= 0 ? -80 : Mathf.Max(-80, 20 * Mathf.Log10(v)));
        }
    }
    public static void Route(AudioSource source, GameAudioChannel channel = GameAudioChannel.Effects)
    {
        if (!source) return;
        Initialize();
        if (groups.TryGetValue(channel, out var group)) source.outputAudioMixerGroup = group;
    }
    public static GameAudioChannel Classify(AudioSource source)
    {
        if (source.GetComponentInParent<InterfaceAudio>())
            return source.loop ? GameAudioChannel.Music : GameAudioChannel.UI;
        if (source.GetComponentInParent<MansionWeather>()) return GameAudioChannel.Ambience;
        string clip = source.clip ? source.clip.name.ToLowerInvariant() : "";
        if (clip == "menusound" || clip == "synthtrack" || clip == "upbeatfuture" || source.name == "BackgroundMusic") return GameAudioChannel.Music;
        if (clip == "ambientsound" || clip == "fan_st" || clip == "background_rumble" || source.name == "AmbientAudio" || source.name == "Constant Rumble" || source.name == "Wall Fan") return GameAudioChannel.Ambience;
        if (source.GetComponent<NPCShopUI>() || source.GetComponent<NPCWeaponVendor>()) return GameAudioChannel.UI;
        return GameAudioChannel.Effects;
    }
    public static void RoutePending(bool force = false)
    {
        if (!force && Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .2f;
        foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!source.outputAudioMixerGroup) Route(source, Classify(source));
    }
    public static void RouteTree(GameObject owner)
    {
        if (!owner) return;
        foreach (var source in owner.GetComponentsInChildren<AudioSource>(true))
            if (!source.outputAudioMixerGroup) Route(source, Classify(source));
    }
    public static void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1)
    {
        if (!clip) return;
        var owner = new GameObject("Efecto de sonido"); owner.transform.position = position;
        var source = owner.AddComponent<AudioSource>(); source.clip = clip; source.spatialBlend = 1;
        source.volume = volume; Route(source); source.Play();
        UnityEngine.Object.Destroy(owner, clip.length + .25f);
    }
}
