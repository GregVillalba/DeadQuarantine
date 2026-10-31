
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
[SerializeField] private PuertaTraseraMansionHistoria puertaTrasera;
    private bool jugadorLlegoAlHelipuerto;
    private bool helicopteroTerminoDescenso;

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
        MostrarTemporizador(true);
  
        BuscarHUDJugador();

        if (textoObjetivo != null)
            textoObjetivo.gameObject.SetActive(true);

        if (textoDescripcion != null)
            textoDescripcion.gameObject.SetActive(true);

        if (textoTemporizador != null)
            textoTemporizador.gameObject.SetActive(true);

        ActualizarObjetivo(
            "OBJETIVO"
        );

        ActualizarDescripcion(
            "Resiste hasta que aparezca el Zombie Alfa."
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

            ActualizarObjetivo("OBJETIVO");

            ActualizarDescripcion(
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

        ActualizarObjetivo("OBJETIVO");

        ActualizarDescripcion(
            "Elimina al Zombie alfa para poder escapar."
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

    jugadorLlegoAlHelipuerto = false;
    helicopteroTerminoDescenso = false;

    DetenerCuentaRegresiva();

   if (puertaTrasera != null)
{
    puertaTrasera.AbrirPuerta();
}
else
{
    Debug.LogError(
        "[Mansion] No hay una puerta trasera asignada.",
        this
    );
}

    if (animatorHelicoptero != null)
    {
        animatorHelicoptero.SetTrigger(triggerDescenso);
    }
    else
    {
        Debug.LogError(
            "[Mansion] No hay Animator asignado al helicóptero."
        );
    }

    ActualizarObjetivo("OBJETIVO");

    ActualizarDescripcion(
        "Corré hacia el helipuerto y alcanzá el punto de extracción."
    );

    if (textoTemporizador != null)
        textoTemporizador.gameObject.SetActive(false);

    Debug.Log(
        "[Mansion] Boss derrotado. Esperando llegada al helipuerto y fin del descenso."
    );
}

public void RegistrarLlegadaAlHelipuerto()
{
    if (!EsServidor ||
        estadoActual != EstadoMision.IrAlHelipuerto)
    {
        return;
    }

    jugadorLlegoAlHelipuerto = true;

    Debug.Log(
        "[Helipuerto] Llegada registrada. " +
        $"Descenso terminado: {helicopteroTerminoDescenso}."
    );

    IntentarCompletarMision();
}

public void RegistrarFinDescensoHelicoptero()
{
    if (!EsServidor ||
        estadoActual != EstadoMision.IrAlHelipuerto)
    {
        return;
    }

    helicopteroTerminoDescenso = true;

    Debug.Log(
        "[Helipuerto] La animación de descenso terminó. " +
        $"Jugador llegó: {jugadorLlegoAlHelipuerto}."
    );

    IntentarCompletarMision();
}

private void IntentarCompletarMision()
{
    if (!EsServidor ||
        estadoActual != EstadoMision.IrAlHelipuerto ||
        !jugadorLlegoAlHelipuerto ||
        !helicopteroTerminoDescenso)
    {
        return;
    }

    CompletarMision();
}

 
public void CompletarMision()
{
    if (!EsServidor ||
        estadoActual != EstadoMision.IrAlHelipuerto ||
        !jugadorLlegoAlHelipuerto ||
        !helicopteroTerminoDescenso)
    {
        return;
    }

    estadoActual = EstadoMision.Completada;

    DetenerCuentaRegresiva();

    if (RoundManager.Instance != null)
        RoundManager.Instance.DetenerSpawnsHistoria();

    ActualizarObjetivo("MISIÓN COMPLETADA");

    ActualizarDescripcion(
        "Lograste escapar de la mansión."
    );

    if (textoTemporizador != null)
        textoTemporizador.gameObject.SetActive(false);

    Debug.Log(
        "[Mansion] Descenso terminado y jugador en el helipuerto. " +
        "Iniciando transición de victoria."
    );

    if (FadeTransicionHistoria.Instance == null)
    {
        Debug.LogError(
            "[Mansion] No se encontró FadeTransicionHistoria."
        );
        return;
    }

    FadeTransicionHistoria.Instance.EjecutarTransicion(() =>
    {
        PauseController pauseController =
            FindAnyObjectByType<PauseController>();

        if (pauseController == null)
        {
            Debug.LogError(
                "[Mansion] No se encontró PauseController para mostrar la victoria."
            );
            return;
        }

       pauseController.MostrarVictoria();

        // El logro aparece SOBRE la pantalla de victoria.
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.DesbloquearLogroHistoria();
        }
        else
        {
            Debug.LogError(
                "[Mansion] No se encontró RoundManager para desbloquear el logro."
            );
        }

        Debug.Log("[Mansion] Pantalla de victoria activada.");
    });
}



    private void DetenerCuentaRegresiva()
    {
        MostrarTemporizador(false);
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
    private void MostrarTemporizador(bool mostrar)
{
    if (textoTemporizador != null)
    {
        textoTemporizador.gameObject.SetActive(mostrar);
    }
}
}
