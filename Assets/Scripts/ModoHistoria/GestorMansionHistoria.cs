
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
    [SerializeField] private Collider triggerHelipuerto;

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
        if (GestorHistoriaPartida.Instance != null)
            GestorHistoriaPartida.Instance.SincronizarHUDMansionLocal();
    }

    private void Update()
    {
        if (!EsServidor || estadoActual != EstadoMision.IrAlHelipuerto ||
            NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainSceneSinglePlayer")
            return;

        // Respaldo del AnimationEvent: si el clip terminó pero el evento se
        // perdió, la extracción sigue pudiendo completarse.
        if (!helicopteroTerminoDescenso && animatorHelicoptero != null &&
            animatorHelicoptero.GetCurrentAnimatorStateInfo(0).IsName("descenso") &&
            animatorHelicoptero.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f &&
            !animatorHelicoptero.IsInTransition(0))
            helicopteroTerminoDescenso = true;

        jugadorLlegoAlHelipuerto = TodosEnHelipuerto();
        if (jugadorLlegoAlHelipuerto)
            IntentarCompletarMision();
    }

    private bool TodosEnHelipuerto()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainSceneSinglePlayer")
            return true;

        if (triggerHelipuerto == null)
        {
            ActivadorHelipuertoHistoria activador =
                FindAnyObjectByType<ActivadorHelipuertoHistoria>();
            if (activador != null)
                triggerHelipuerto = activador.GetComponent<Collider>();
        }

        if (triggerHelipuerto == null ||
            NetworkManager.Singleton.ConnectedClientsList.Count < 2)
            return false;

        Bounds zona = triggerHelipuerto.bounds;
        foreach (NetworkClient cliente in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (cliente.PlayerObject == null)
                return false;

            CharacterController cuerpo =
                cliente.PlayerObject.GetComponent<CharacterController>();
            if (cuerpo != null && cuerpo.enabled)
            {
                if (!zona.Intersects(cuerpo.bounds))
                    return false;
            }
            else if (!zona.Contains(cliente.PlayerObject.transform.position))
                return false;
        }

        return true;
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
        PublicarEstadoSincronizado(Mathf.CeilToInt(RoundManager.Instance.TiempoResistenciaMansion));
  
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
            PublicarEstadoSincronizado(segundosRestantes);

            yield return new WaitForSeconds(1f);

            segundosRestantes--;
        }

        if (estadoActual == EstadoMision.Resistir)
        {
            ActualizarTemporizador(0);

            ActualizarObjetivo("OBJETIVO");

            ActualizarDescripcion(
                "Prepárate para enfrentarlo."
            );
            PublicarEstadoSincronizado(0);
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
        PublicarEstadoSincronizado(0);

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
    PublicarEstadoSincronizado(0);

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

    jugadorLlegoAlHelipuerto = TodosEnHelipuerto();

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
        !TodosEnHelipuerto())
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
        !TodosEnHelipuerto())
    {
        return;
    }

    estadoActual = EstadoMision.Completada;
    PublicarEstadoSincronizado(0);

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
        "[Mansion] Todos los jugadores llegaron al helipuerto. " +
        "Iniciando transición de victoria."
    );

    if (RoundManager.Instance == null)
    {
        Debug.LogError("[Mansion] No se encontró RoundManager para mostrar la victoria.");
        return;
    }

    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainSceneSinglePlayer")
    {
        RoundManager.Instance.MostrarVictoriaHistoria();
        RoundManager.Instance.DesbloquearLogroHistoria();
        return;
    }

    if (FadeTransicionHistoria.Instance != null)
        FadeTransicionHistoria.Instance.EjecutarTransicion(() =>
        {
            RoundManager.Instance.MostrarVictoriaHistoria();
            RoundManager.Instance.DesbloquearLogroHistoria();
        });
    else
    {
        Debug.LogWarning("[Mansion] No se encontró FadeTransicionHistoria; se muestra la victoria.");
        RoundManager.Instance.MostrarVictoriaHistoria();
        RoundManager.Instance.DesbloquearLogroHistoria();
    }
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

    private void PublicarEstadoSincronizado(int segundos)
    {
        if (GestorHistoriaPartida.Instance != null && EsServidor)
            GestorHistoriaPartida.Instance.PublicarEstadoMansion((int)estadoActual, segundos);
    }

    public void AplicarEstadoMisionSincronizado(int etapa, int segundos)
    {
        if (EsServidor || etapa < (int)EstadoMision.Inactiva || etapa > (int)EstadoMision.Completada)
            return;

        EstadoMision estadoAnterior = estadoActual;
        estadoActual = (EstadoMision)etapa;
        BuscarHUDJugador();

        switch (estadoActual)
        {
            case EstadoMision.Resistir:
                MostrarTemporizador(true);
                ActualizarObjetivo("OBJETIVO");
                ActualizarDescripcion(segundos > 0
                    ? "Resiste hasta que aparezca el Zombie Alfa."
                    : "Prepárate para enfrentarlo.");
                ActualizarTemporizador(segundos);
                break;
            case EstadoMision.EsperandoBoss:
                MostrarTemporizador(false);
                ActualizarObjetivo("OBJETIVO");
                ActualizarDescripcion("Elimina al Zombie Alfa para poder escapar.");
                break;
            case EstadoMision.IrAlHelipuerto:
                MostrarTemporizador(false);
                ActualizarObjetivo("OBJETIVO");
                ActualizarDescripcion("Corre hacia el helipuerto y alcanza el punto de extracción.");
                if (estadoAnterior != EstadoMision.IrAlHelipuerto)
                {
                    if (puertaTrasera != null)
                        puertaTrasera.AbrirPuerta();
                    if (animatorHelicoptero != null)
                        animatorHelicoptero.SetTrigger(triggerDescenso);
                }
                break;
            case EstadoMision.Completada:
                MostrarTemporizador(false);
                ActualizarObjetivo("MISIÓN COMPLETADA");
                ActualizarDescripcion("Lograste escapar de la mansión.");
                break;
        }
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
