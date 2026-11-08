using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Va en el prefab de la máquina arcade (necesita un Collider NO trigger para que el raycast del jugador la detecte).
/// El cartel y la tecla E los maneja ArcadeInteractor, que está en el Player.
/// Este script solo abre la escena del minijuego (aditiva) y la cierra con Esc.
/// </summary>
public class MinijuegoArcadeTrigger : MonoBehaviour
{
    public static event Action MinijuegoIniciado;
    public static event Action MinijuegoTerminado;

    [Header("Configuración de Escena")]
    [Tooltip("Debe estar agregada en File > Build Settings > Scenes In Build")]
    [SerializeField] private string nombreEscenaMinijuego = "DEMO";

    [Header("Recompensa")]
    public Transform puntoDropRecompensa;

    private bool minijuegoActivo;
    private bool cargando;
    private bool cerrando;

    private GameObject jugadorObj;
    private Scene escenaOriginal;

    // Memoria de posición y rotación para evitar teletransporte al salir
    private Vector3 posicionPrevia;
    private Quaternion rotacionPrevia;

    private readonly List<Behaviour> componentesDesactivados = new List<Behaviour>();

    public static MinijuegoArcadeTrigger InstanciaActiva { get; private set; }
    public static bool MinijuegoEnCurso { get; private set; }
    public bool Ocupado => minijuegoActivo || cargando || cerrando;

    private void Update()
    {
        if (!Ocupado) return;

        if (PresionoEscape())
        {
            if (minijuegoActivo && !cargando && !cerrando)
                SalirDelMinijuego();
        }
    }

    private bool PresionoEscape()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    public void Jugar(GameObject jugador)
    {
        if (Ocupado || MinijuegoEnCurso) return;

        jugadorObj = jugador;
        StartCoroutine(CargarMinijuego());
    }

    private IEnumerator CargarMinijuego()
    {
        if (!Application.CanStreamedLevelBeLoaded(nombreEscenaMinijuego))
        {
            Debug.LogError($"[Arcade] La escena '{nombreEscenaMinijuego}' no está en Build Settings o el nombre no coincide.");
            yield break;
        }

        cargando = true;
        MinijuegoEnCurso = true;

        // Guardar la posición exacta antes de abrir el minijuego
        if (jugadorObj != null)
        {
            posicionPrevia = jugadorObj.transform.position;
            rotacionPrevia = jugadorObj.transform.rotation;
        }

        MinijuegoIniciado?.Invoke();

        if (jugadorObj != null)
        {
            jugadorObj.SendMessage("EntrarMinijuego", SendMessageOptions.DontRequireReceiver);
        }

        escenaOriginal = SceneManager.GetActiveScene();
        AlternarControlJugador(false);

        Scene yaCargada = SceneManager.GetSceneByName(nombreEscenaMinijuego);
        if (!yaCargada.isLoaded)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(nombreEscenaMinijuego, LoadSceneMode.Additive);
            while (op != null && !op.isDone) yield return null;
        }

        Scene escena = SceneManager.GetSceneByName(nombreEscenaMinijuego);
        if (escena.isLoaded)
        {
            SceneManager.SetActiveScene(escena);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        InstanciaActiva = this;
        minijuegoActivo = true;
        cargando = false;
    }

    public void SalirDelMinijuego()
    {
        if (!minijuegoActivo || cerrando) return;
        StartCoroutine(CerrarMinijuego());
    }

    public void ReiniciarMinijuego()
    {
        if (!minijuegoActivo || cargando || cerrando) return;
        StartCoroutine(RecargarMinijuego());
    }

    private IEnumerator RecargarMinijuego()
    {
        cargando = true;

        if (escenaOriginal.IsValid() && escenaOriginal.isLoaded)
        {
            SceneManager.SetActiveScene(escenaOriginal);
        }

        Scene vieja = SceneManager.GetSceneByName(nombreEscenaMinijuego);
        if (vieja.isLoaded)
        {
            AsyncOperation descarga = SceneManager.UnloadSceneAsync(vieja);
            while (descarga != null && !descarga.isDone) yield return null;
        }

        AsyncOperation carga = SceneManager.LoadSceneAsync(nombreEscenaMinijuego, LoadSceneMode.Additive);
        while (carga != null && !carga.isDone) yield return null;

        Scene nueva = SceneManager.GetSceneByName(nombreEscenaMinijuego);
        if (nueva.isLoaded)
        {
            SceneManager.SetActiveScene(nueva);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        cargando = false;
    }

    public IEnumerator CerrarMinijuego()
    {
        if (!minijuegoActivo || cerrando) yield break;
        cerrando = true;

        if (escenaOriginal.IsValid() && escenaOriginal.isLoaded)
        {
            SceneManager.SetActiveScene(escenaOriginal);
        }

        Scene escena = SceneManager.GetSceneByName(nombreEscenaMinijuego);
        if (escena.isLoaded)
        {
            AsyncOperation op = SceneManager.UnloadSceneAsync(escena);
            while (op != null && !op.isDone) yield return null;
        }

        // Restaurar posición y rotación antes de reactivar controles
        RestaurarPosicionJugador();

        AlternarControlJugador(true);

        if (jugadorObj != null)
        {
            jugadorObj.SendMessage("SalirMinijuego", SendMessageOptions.DontRequireReceiver);
        }

        minijuegoActivo = false;
        cerrando = false;
        InstanciaActiva = null;
        MinijuegoEnCurso = false;

        MinijuegoTerminado?.Invoke();
    }

    private void RestaurarPosicionJugador()
    {
        if (jugadorObj == null) return;

        CharacterController cc = jugadorObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Rigidbody rb = jugadorObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        jugadorObj.transform.position = posicionPrevia;
        jugadorObj.transform.rotation = rotacionPrevia;

        if (cc != null) cc.enabled = true;
    }

    private void AlternarControlJugador(bool activar)
    {
        if (jugadorObj == null) return;

        if (!activar)
        {
            componentesDesactivados.Clear();

            // 1. Apagar Canvases del HUD e interfaces del jugador
            Canvas[] canvases = jugadorObj.GetComponentsInChildren<Canvas>(true);
            foreach (var c in canvases)
            {
                if (c.name.Contains("HUD") || c.name.Contains("Interface"))
                {
                    if (c.enabled)
                    {
                        c.enabled = false;
                        componentesDesactivados.Add(c);
                    }
                }
            }

            // 2. Desactivar controles de movimiento, cámara y armas
            foreach (var s in jugadorObj.GetComponentsInChildren<MonoBehaviour>())
            {
                if (s == null || s == this || !s.enabled) continue;

                string n = s.GetType().Name;
                if (n.Contains("Movement") || n.Contains("Controller") || n.Contains("Look") || n.Contains("Weapon"))
                {
                    s.enabled = false;
                    componentesDesactivados.Add(s);
                }
            }

            // 3. Apagar cámaras y listeners 3D del jugador
            foreach (var cam in jugadorObj.GetComponentsInChildren<Camera>())
            {
                if (cam.enabled)
                {
                    cam.enabled = false;
                    componentesDesactivados.Add(cam);
                }
            }
            foreach (var al in jugadorObj.GetComponentsInChildren<AudioListener>())
            {
                if (al.enabled)
                {
                    al.enabled = false;
                    componentesDesactivados.Add(al);
                }
            }

            // 4. Activar invulnerabilidad en PlayerHealth
            PlayerHealth ph = jugadorObj.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                ph.invulnerablePorMinijuego = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Reactivar componentes
            foreach (var c in componentesDesactivados)
            {
                if (c != null) c.enabled = true;
            }
            componentesDesactivados.Clear();

            PlayerHealth ph = jugadorObj.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                ph.invulnerablePorMinijuego = false;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}