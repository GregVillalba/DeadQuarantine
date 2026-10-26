
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class GestorMansionHistoria : MonoBehaviour
{
    public static GestorMansionHistoria Instance { get; private set; }

    public enum EstadoMision
    {
        Inactiva,
        Resistir,
        EsperandoBoss,
        IrAlHelipuerto,
        Completada
    }

    [Header("Estado de la misión")]
    [SerializeField] private EstadoMision estadoActual =
        EstadoMision.Inactiva;

    [Header("HUD de la misión")]
    [SerializeField] private TMP_Text textoObjetivo;
    [SerializeField] private TMP_Text textoDescripcion;
    [SerializeField] private TMP_Text textoTemporizador;

    [Header("Configuración de la misión")]
    [SerializeField] private Transform puntoSpawnBoss;

    [Header("Helicóptero")]
    [SerializeField] private Animator animatorHelicoptero;
    [SerializeField] private string triggerDescenso = "Llegada";

    [Header("Puerta trasera")]
    [SerializeField] private Animator animatorPuertaTrasera;
    [SerializeField] private string triggerAbrirPuerta = "Abrir";

    private Coroutine cuentaRegresivaRoutine;

    private bool EsServidor
    {
        get
        {
            if (NetworkManager.Singleton == null)
                return true;

            if (!NetworkManager.Singleton.IsListening)
                return true;

            return NetworkManager.Singleton.IsServer;
        }
    }

    public EstadoMision EstadoActual => estadoActual;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        BuscarHUDJugador();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        DetenerCuentaRegresiva();
    }

    public void IniciarMision()
    {
        Debug.Log("[Mansion] IniciarMision ejecutado.");
        Debug.Log($"[Mansion] EsServidor: {EsServidor}");

        if (!EsServidor ||
            estadoActual != EstadoMision.Inactiva)
        {
            return;
        }

        if (RoundManager.Instance == null)
        {
            Debug.LogError(
                "[Mansion] No se encontró RoundManager."
            );
            return;
        }

        if (puntoSpawnBoss == null)
        {
            Debug.LogError(
                "[Mansion] Asigná Punto Spawn Boss en el Inspector."
            );
            return;
        }

        estadoActual = EstadoMision.Resistir;

        BuscarHUDJugador();

        if (textoObjetivo != null)
            textoObjetivo.gameObject.SetActive(true);

        if (textoDescripcion != null)
            textoDescripcion.gameObject.SetActive(true);

        if (textoTemporizador != null)
            textoTemporizador.gameObject.SetActive(true);

        ActualizarObjetivo(
            "RESISTÍ EL ATAQUE DE LOS ZOMBIES."
        );

        ActualizarDescripcion(
            "El perímetro está siendo atacado. " +
            "Aguantá hasta que aparezca el boss."
        );

        ActualizarTemporizador(
            RoundManager.Instance.TiempoResistenciaMansion
        );

        cuentaRegresivaRoutine =
            StartCoroutine(MostrarCuentaRegresiva());

        RoundManager.Instance.IniciarMisionMansion(
            puntoSpawnBoss
        );
    }

    private IEnumerator MostrarCuentaRegresiva()
    {
        if (RoundManager.Instance == null)
        {
            cuentaRegresivaRoutine = null;
            yield break;
        }

        int segundosRestantes = Mathf.CeilToInt(
            RoundManager.Instance.TiempoResistenciaMansion
        );

        while (segundosRestantes > 0 &&
               estadoActual == EstadoMision.Resistir)
        {
            ActualizarTemporizador(segundosRestantes);

            yield return new WaitForSeconds(1f);

            segundosRestantes--;
        }

        if (estadoActual == EstadoMision.Resistir)
        {
            ActualizarTemporizador(0);

            ActualizarObjetivo("¡PREPARATE!");

            ActualizarDescripcion(
                "El boss está por aparecer. " +
                "Preparáte para enfrentarlo."
            );
        }

        cuentaRegresivaRoutine = null;
    }

    public void BossAparecio()
    {
        if (!EsServidor ||
            estadoActual != EstadoMision.Resistir)
        {
            return;
        }

        estadoActual = EstadoMision.EsperandoBoss;

        DetenerCuentaRegresiva();

        ActualizarObjetivo("¡EL BOSS APARECIÓ!");

        ActualizarDescripcion(
            "Eliminá al mutante alfa para poder escapar."
        );

        if (textoTemporizador != null)
            textoTemporizador.gameObject.SetActive(false);

        Debug.Log("[Mansion] El boss apareció.");
    }

    public void AlBossDerrotado()
    {
        if (!EsServidor ||
            estadoActual != EstadoMision.EsperandoBoss)
        {
            return;
        }

        estadoActual = EstadoMision.IrAlHelipuerto;

        DetenerCuentaRegresiva();

        if (animatorPuertaTrasera != null)
        {
            animatorPuertaTrasera.SetTrigger(
                triggerAbrirPuerta
            );
        }
        else
        {
            Debug.LogWarning(
                "[Mansion] No hay Animator asignado a la puerta trasera."
            );
        }

        if (animatorHelicoptero != null)
        {
            animatorHelicoptero.SetTrigger(
                triggerDescenso
            );
        }
        else
        {
            Debug.LogWarning(
                "[Mansion] No hay Animator asignado al helicóptero."
            );
        }

        ActualizarObjetivo("¡BOSS DERROTADO!");

        ActualizarDescripcion(
            "Corré hacia el helipuerto y alcanzá " +
            "el punto de extracción."
        );

        if (textoTemporizador != null)
            textoTemporizador.gameObject.SetActive(false);

        Debug.Log(
            "[Mansion] Boss derrotado. Objetivo: ir al helipuerto."
        );
    }

    public void CompletarMision()
    {
        if (!EsServidor ||
            estadoActual == EstadoMision.Completada ||
            estadoActual == EstadoMision.Inactiva)
        {
            return;
        }

        estadoActual = EstadoMision.Completada;

        DetenerCuentaRegresiva();

        ActualizarObjetivo("¡MISIÓN COMPLETADA!");

        ActualizarDescripcion(
            "Lograste escapar de la mansión."
        );

        if (textoTemporizador != null)
            textoTemporizador.gameObject.SetActive(false);

        Debug.Log("[Mansion] Misión completada.");
    }

    private void DetenerCuentaRegresiva()
    {
        if (cuentaRegresivaRoutine != null)
        {
            StopCoroutine(cuentaRegresivaRoutine);
            cuentaRegresivaRoutine = null;
        }
    }

    private void ActualizarObjetivo(string mensaje)
    {
        if (textoObjetivo == null)
            BuscarHUDJugador();

        if (textoObjetivo != null)
        {
            textoObjetivo.text = mensaje;
        }
        else
        {
            Debug.LogWarning(
                "[Mansion] No se encontró el texto Objetivo."
            );
        }
    }

    private void ActualizarDescripcion(string mensaje)
    {
        if (textoDescripcion == null)
            BuscarHUDJugador();

        if (textoDescripcion != null)
        {
            textoDescripcion.text = mensaje;
        }
        else
        {
            Debug.LogWarning(
                "[Mansion] No se encontró el texto Descripcion."
            );
        }
    }

    private void ActualizarTemporizador(float segundosTotales)
    {
        if (textoTemporizador == null)
            BuscarHUDJugador();

        if (textoTemporizador == null)
        {
            Debug.LogWarning(
                "[Mansion] No se encontró el texto Temporizador."
            );
            return;
        }

        int segundos = Mathf.Max(
            0,
            Mathf.CeilToInt(segundosTotales)
        );

        int minutos = segundos / 60;
        int restoSegundos = segundos % 60;

        textoTemporizador.text =
            $"{minutos:00}:{restoSegundos:00}";

        textoTemporizador.gameObject.SetActive(true);
    }

    private bool BuscarHUDJugador()
    {
        GameObject[] jugadores;

        try
        {
            jugadores = GameObject.FindGameObjectsWithTag("Player");
        }
        catch (UnityException)
        {
            Debug.LogWarning(
                "[Mansion] No existe el tag Player."
            );
            return false;
        }

        foreach (GameObject jugador in jugadores)
        {
            if (jugador == null)
                continue;

            NetworkObject networkObject =
                jugador.GetComponent<NetworkObject>();

            if (networkObject != null &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening &&
                !networkObject.IsOwner)
            {
                continue;
            }

            Transform hud = BuscarHijoPorNombre(
                jugador.transform,
                "HistoriaHUD"
            );

            if (hud == null)
                continue;

            Transform panelObjetivo = BuscarHijoPorNombre(
                hud,
                "Panel_Objetivo"
            );

            if (panelObjetivo == null)
            {
                Debug.LogWarning(
                    "[Mansion] No se encontró Panel_Objetivo."
                );
                continue;
            }

            Transform fila1 = BuscarHijoPorNombre(
                panelObjetivo,
                "FILA1"
            );

            Transform fila2 = BuscarHijoPorNombre(
                panelObjetivo,
                "FILA2"
            );

            Transform objetivo = fila1 != null
                ? BuscarHijoPorNombre(fila1, "Objetivo")
                : null;

            Transform descripcion = fila2 != null
                ? BuscarHijoPorNombre(fila2, "Descripcion")
                : null;

            Transform temporizador = BuscarHijoPorNombre(
                panelObjetivo,
                "Temporizador"
            );

            textoObjetivo = objetivo != null
                ? objetivo.GetComponent<TMP_Text>()
                : null;

            textoDescripcion = descripcion != null
                ? descripcion.GetComponent<TMP_Text>()
                : null;

            textoTemporizador = temporizador != null
                ? temporizador.GetComponent<TMP_Text>()
                : null;

            if (textoObjetivo != null &&
                textoDescripcion != null &&
                textoTemporizador != null)
            {
                Debug.Log(
                    "[Mansion] Objetivo, descripción y temporizador encontrados."
                );

                return true;
            }

            Debug.LogWarning(
                "[Mansion] Faltan referencias TMP_Text en uno " +
                "o más elementos del HUD."
            );
        }

        return false;
    }

    private Transform BuscarHijoPorNombre(
        Transform padre,
        string nombre
    )
    {
        if (padre == null)
            return null;

        foreach (Transform hijo in padre)
        {
            if (hijo.name == nombre)
                return hijo;

            Transform encontrado = BuscarHijoPorNombre(
                hijo,
                nombre
            );

            if (encontrado != null)
                return encontrado;
        }

        return null;
    }
}
