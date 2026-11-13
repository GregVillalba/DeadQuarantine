using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MinijuegoArcadeTrigger : MonoBehaviour
{
    public static MinijuegoArcadeTrigger InstanciaActiva { get; private set; }
    public static bool MinijuegoEnCurso { get; private set; }

    [Header("Configuración de Escena")]
    [SerializeField] private string nombreEscenaMinijuego = "DEMO";

    [Header("Recompensa (Drop cada 100 puntos)")]
    [SerializeField] private GameObject prefabRecursoDrop;
    [SerializeField] private Transform puntoDropRecompensa;
    [SerializeField] private float fuerzaImpulso = 3.5f;

    private bool minijuegoActivo;
    private bool cargando;
    private bool cerrando;

    /// <summary>True si esta máquina está cargando, en partida o cerrando.</summary>
    public bool Ocupado => minijuegoActivo || cargando || cerrando;

    private GameObject jugadorObj;
    private Scene escenaOriginal;
    private Vector3 posicionPrevia;
    private Quaternion rotacionPrevia;

    private readonly List<Behaviour> componentesDesactivados = new List<Behaviour>();

    private void Awake()
    {
        InstanciaActiva = this;
    }

    private void OnEnable()
    {
        InstanciaActiva = this;
    }

    private void OnDestroy()
    {
        if (InstanciaActiva == this)
            InstanciaActiva = null;
    }

    public void Jugar(GameObject jugador)
    {
        if (Ocupado || MinijuegoEnCurso) return;
        jugadorObj = jugador;
        StartCoroutine(CargarMinijuego());
    }

    private void Update()
    {
        if (!minijuegoActivo || cargando || cerrando) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SalirDelMinijuego();
        }
    }

    private IEnumerator CargarMinijuego()
    {
        cargando = true;
        MinijuegoEnCurso = true;
        InstanciaActiva = this;

        if (jugadorObj != null)
        {
            posicionPrevia = jugadorObj.transform.position;
            rotacionPrevia = jugadorObj.transform.rotation;
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

        minijuegoActivo = true;
        cargando = false;
    }

    public void SalirDelMinijuego()
    {
        if (cerrando) return;
        StartCoroutine(CerrarMinijuego());
    }

    public void RegistrarInicioDePartida()
    {
        if (!minijuegoActivo || cargando || cerrando || jugadorObj == null)
            return;

        jugadorObj.BroadcastMessage("RegistrarPartidaArcade", SendMessageOptions.DontRequireReceiver);
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

    private IEnumerator CerrarMinijuego()
    {
        cerrando = true;
        minijuegoActivo = false;

        // 1. Restaurar la escena principal del juego 3D ANTES de descargar DEMO
        if (escenaOriginal.IsValid() && escenaOriginal.isLoaded)
        {
            SceneManager.SetActiveScene(escenaOriginal);
        }
        else
        {
            // Respaldo: si por alguna razón escenaOriginal se perdió, buscamos la primera que no sea el minijuego
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.name != nombreEscenaMinijuego && s.isLoaded)
                {
                    SceneManager.SetActiveScene(s);
                    break;
                }
            }
        }

        // 2. Descargar la escena DEMO solo si hay más de 1 escena cargada
        if (SceneManager.sceneCount > 1)
        {
            Scene escenaMinijuego = SceneManager.GetSceneByName(nombreEscenaMinijuego);
            if (escenaMinijuego.isLoaded)
            {
                AsyncOperation op = SceneManager.UnloadSceneAsync(escenaMinijuego);
                while (op != null && !op.isDone) yield return null;
            }
        }

        // 3. Restaurar posición en la máquina
        RestaurarPosicionJugador();

        // 4. Reactivar componentes del Player y notificar a PauseController
        AlternarControlJugador(true);

        if (jugadorObj != null)
        {
            jugadorObj.SendMessage("SalirMinijuego", SendMessageOptions.DontRequireReceiver);
        }

        // 5. Bloquear cursor de nuevo para el FPS
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        cerrando = false;
        MinijuegoEnCurso = false;
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

            foreach (var c in jugadorObj.GetComponentsInChildren<Canvas>(true))
            {
                if ((c.name.Contains("HUD") || c.name.Contains("Interface")) && c.enabled)
                {
                    c.enabled = false;
                    componentesDesactivados.Add(c);
                }
            }

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

            PlayerHealth ph = jugadorObj.GetComponent<PlayerHealth>();
            if (ph != null) ph.invulnerablePorMinijuego = true;
        }
        else
        {
            foreach (var c in componentesDesactivados)
            {
                if (c != null) c.enabled = true;
            }
            componentesDesactivados.Clear();

            PlayerHealth ph = jugadorObj.GetComponent<PlayerHealth>();
            if (ph != null) ph.invulnerablePorMinijuego = false;
        }
    }

    /// <summary>
    /// Dropea una cantimplora frente a la máquina arcade cada 100 puntos.
    /// </summary>
    /// <summary>
    /// Dropea una cantimplora frente a la máquina arcade cada 100 puntos y la protege
    /// asegurando que quede en la escena 3D principal (para que no se borre al cerrar DEMO).
    /// </summary>
    public void DropearRecompensa()
    {
        if (prefabRecursoDrop == null)
        {
            Debug.LogError("[Arcade] ERROR: Falta asignar 'Prefab Recurso Drop' en el Inspector.", this);
            return;
        }

        Transform origen = puntoDropRecompensa != null ? puntoDropRecompensa : transform;

        // Spawn frente a la máquina arcade (hacia adelante y un poco elevado del piso)
        Vector3 spawnPos = origen.position + (transform.forward * 0.7f) + (Vector3.up * 0.4f);
        Vector2 azar = UnityEngine.Random.insideUnitCircle * 0.2f;
        spawnPos += new Vector3(azar.x, 0f, azar.y);

        GameObject drop = Instantiate(prefabRecursoDrop, spawnPos, Quaternion.identity);

        // CLAVE: Asegurar que la cantimplora pertenezca a la escena 3D del juego (no a la escena DEMO)
        Scene escenaDestino = gameObject.scene;
        if (escenaDestino.IsValid() && escenaDestino.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(drop, escenaDestino);
        }

        // 1. Configurar tipo Cantimplora
        RecursoDrop recurso = drop.GetComponent<RecursoDrop>();
        if (recurso != null)
        {
            recurso.Configurar(TipoRecurso.Cantimplora, 1);
        }

        // 2. Encendido forzado del modelo de Cantimplora
        Transform visual = drop.transform.Find("Visual");
        if (visual != null)
        {
            Transform modeloLatas = visual.Find("Modelo_PilaDeLatas");
            Transform modeloBidon = visual.Find("Modelo_Bidon");
            Transform modeloCantimplora = visual.Find("Modelo_Cantimplora");

            if (modeloLatas != null) modeloLatas.gameObject.SetActive(false);
            if (modeloBidon != null) modeloBidon.gameObject.SetActive(false);
            if (modeloCantimplora != null) modeloCantimplora.gameObject.SetActive(true);
        }

        // 3. Física de impulso suave
        if (drop.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            Vector3 direccion = (transform.forward + Vector3.up * 0.8f).normalized;
            rb.AddForce(direccion * 1.5f, ForceMode.Impulse);
        }

        // 4. Sincronización en red si está en Netcode
        NetworkObject netObj = drop.GetComponent<NetworkObject>();
        if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn(true);
        }

        Debug.Log($"<color=green>[Arcade] ¡Cantimplora asegurada en '{escenaDestino.name}' en posición {spawnPos}!</color>");
    }

}
