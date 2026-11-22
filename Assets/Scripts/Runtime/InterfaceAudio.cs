using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Música de navegación y sonidos compartidos por los botones reales del juego.</summary>
[DisallowMultipleComponent]
public sealed class InterfaceAudio : MonoBehaviour
{
    [SerializeField] AudioSource music;
    [SerializeField] AudioSource effects;
    [SerializeField] AudioClip hover;
    [SerializeField] AudioClip click;
    static InterfaceAudio instance;
    float nextButtons, nextHover;
    bool musicAllowed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() => instance = null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartAudio()
    {
        if (instance) return;
        var prefab = Resources.Load<GameObject>("InterfaceAudio");
        if (prefab) Instantiate(prefab);
    }
    void Awake()
    {
        if (instance && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        music.playOnAwake = effects.playOnAwake = false;
        music.loop = true;
        // El menú puede detener el audio de la partida anterior mientras se navega.
        // La música se corta explícitamente al cargar, sin depender de esa pausa.
        music.ignoreListenerPause = true;
        effects.ignoreListenerPause = true;
        music.volume = .38f;
        GameAudio.Route(music, GameAudioChannel.Music);
        GameAudio.Route(effects, GameAudioChannel.UI);
    }
    void Start() => GameAudio.ApplyVolumes();
    void Update()
    {
        GameAudio.RoutePending();
        string scene = SceneManager.GetActiveScene().name;
        bool wanted = (scene == "PantallasUI" || scene == "NetworkLobby") && !MansionWeather.Loading;
        if (wanted != musicAllowed)
        {
            musicAllowed = wanted;
            if (wanted && music.clip) music.Play(); else music.Stop();
        }
        if (Time.unscaledTime < nextButtons) return;
        nextButtons = Time.unscaledTime + .25f;
        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (!button.GetComponent<UIButtonAudio>()) button.gameObject.AddComponent<UIButtonAudio>();
    }
    public static void Hover()
    {
        if (!instance || Time.unscaledTime < instance.nextHover) return;
        instance.nextHover = Time.unscaledTime + .05f;
        instance.effects.PlayOneShot(instance.hover, .18f);
    }
    public static void Click()
    {
        if (instance) instance.effects.PlayOneShot(instance.click, .48f);
    }
    void OnDestroy() { if (instance == this) instance = null; }
}
