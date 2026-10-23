
using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class GestorHistoriaPartida : NetworkBehaviour
{
    private const int MisionInactiva = 0;
    private const int BuscarTarjeta = 1;
    private const int AbrirPuerta = 2;
    private const int MisionCompletada = 3;
    private bool animacionAperturaEjecutada;
    private bool transicionSafeZoneIniciada;

    [Header("Tarjeta de acceso")]
    [SerializeField] private GameObject tarjetaPrefab;
    [SerializeField] private Transform puntoSpawnTarjeta;
    [SerializeField] private float distanciaRecogida = 3f;

    [Header("Puerta y lector")]
    [SerializeField] private Transform puntoLectorPuerta;
    [SerializeField] private PuertaAnimacionHistoria animacionPuerta;
    [SerializeField] private float distanciaInteraccion = 3f;

    [Header("HUD local")]
    [SerializeField] private TMP_Text textoTituloObjetivo;
    [SerializeField] private TMP_Text textoDescripcionObjetivo;

    public static GestorHistoriaPartida Instance { get; private set; }

    public NetworkVariable<int> EstadoMisionNetwork =
        new NetworkVariable<int>(
            MisionInactiva,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<ulong> ClienteConTarjetaNetwork =
        new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<bool> PuertaAbiertaNetwork =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "[Historia] Hay más de un GestorHistoriaPartida en la escena."
            );
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        EstadoMisionNetwork.OnValueChanged += AlCambiarEstadoMision;
        PuertaAbiertaNetwork.OnValueChanged += AlCambiarEstadoPuerta;

        ActualizarTextoObjetivo();

        if (!IsServer)
        {
            AplicarEstadoPuerta(PuertaAbiertaNetwork.Value);
            return;
        }

        if (!EsHistoriaLaboratorio())
        {
            EstadoMisionNetwork.Value = MisionInactiva;
            ClienteConTarjetaNetwork.Value = ulong.MaxValue;
            PuertaAbiertaNetwork.Value = false;

            AplicarEstadoPuerta(false);
            return;
        }

        EstadoMisionNetwork.Value = BuscarTarjeta;
        ClienteConTarjetaNetwork.Value = ulong.MaxValue;
        PuertaAbiertaNetwork.Value = false;

        AplicarEstadoPuerta(false);

        CrearTarjeta();
    }

    public override void OnNetworkDespawn()
    {
        EstadoMisionNetwork.OnValueChanged -= AlCambiarEstadoMision;
        PuertaAbiertaNetwork.OnValueChanged -= AlCambiarEstadoPuerta;

        if (Instance == this)
            Instance = null;

        base.OnNetworkDespawn();
    }

    private bool EsHistoriaLaboratorio()
    {
        if (ConfiguracionPartidaSeleccionada.Instancia == null)
            return false;

        return
            ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado ==
                ConfiguracionPartidaSeleccionada.ModoJuego.Historia
            &&
            ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado ==
                ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;
    }

    private void CrearTarjeta()
    {
        if (!IsServer)
            return;

        if (tarjetaPrefab == null)
        {
            Debug.LogError(
                "[Historia] No asignaste el prefab de la tarjeta."
            );
            return;
        }

        if (puntoSpawnTarjeta == null)
        {
            Debug.LogError(
                "[Historia] No asignaste el punto de aparición de la tarjeta."
            );
            return;
        }

        GameObject tarjetaObjeto = Instantiate(
            tarjetaPrefab,
            puntoSpawnTarjeta.position,
            puntoSpawnTarjeta.rotation
        );

        NetworkObject networkObject =
            tarjetaObjeto.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError(
                "[Historia] El prefab de la tarjeta necesita un NetworkObject."
            );

            Destroy(tarjetaObjeto);
            return;
        }

        networkObject.Spawn(true);
    }

    public void RegistrarHUDLocal(
        TMP_Text titulo,
        TMP_Text descripcion
    )
    {
        if (titulo == null || descripcion == null)
            return;

        textoTituloObjetivo = titulo;
        textoDescripcionObjetivo = descripcion;

        ActualizarTextoObjetivo();
    }

    private void AlCambiarEstadoMision(
        int estadoAnterior,
        int estadoNuevo
    )
    {
        ActualizarTextoObjetivo();
    }

    private void ActualizarTextoObjetivo()
    {
        if (textoTituloObjetivo == null ||
            textoDescripcionObjetivo == null)
            return;

        switch (EstadoMisionNetwork.Value)
        {
            case BuscarTarjeta:
                textoTituloObjetivo.color = Color.white;
                textoTituloObjetivo.text = "OBJETIVO";
                textoDescripcionObjetivo.text =
                    "Busca la tarjeta de acceso.";
                break;

            case AbrirPuerta:
                textoTituloObjetivo.color = Color.white;
                textoTituloObjetivo.text = "OBJETIVO";
                textoDescripcionObjetivo.text =
                    "Lleva la tarjeta al lector de la puerta.";
                break;

            case MisionCompletada:
                textoTituloObjetivo.color = Color.green;
                textoTituloObjetivo.text = "MISIÓN COMPLETADA";
                textoDescripcionObjetivo.text =
                    "La puerta está abierta.";
                break;

            default:
                textoTituloObjetivo.text = "";
                textoDescripcionObjetivo.text = "";
                break;
        }
    }

    public void RegistrarTarjeta(
        ulong clientId,
        TarjetaAccesoHistoria tarjeta
    )
    {
        if (!IsServer || tarjeta == null)
            return;

        if (EstadoMisionNetwork.Value != BuscarTarjeta)
            return;

        ClienteConTarjetaNetwork.Value = clientId;
        EstadoMisionNetwork.Value = AbrirPuerta;

        tarjeta.DespawnearTarjeta();
    }

    public bool JugadorCercaDelPunto(
        ulong clientId,
        Vector3 punto,
        float distancia
    )
    {
        if (NetworkManager.Singleton == null)
            return false;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
            clientId,
            out NetworkClient cliente))
            return false;

        if (cliente.PlayerObject == null)
            return false;

        float distanciaActual = Vector3.Distance(
            cliente.PlayerObject.transform.position,
            punto
        );

        return distanciaActual <= distancia;
    }

    public void IntentarAbrirPuertaRpc()
    {
        SolicitarAbrirPuertaRpc();
    }

[Rpc(
    SendTo.Server,
    InvokePermission = RpcInvokePermission.Everyone
)]
private void SolicitarAbrirPuertaRpc(
    RpcParams rpcParams = default
)
{
    if (!IsServer || !EsHistoriaLaboratorio())
        return;

    if (EstadoMisionNetwork.Value != AbrirPuerta)
        return;

    ulong clientId = rpcParams.Receive.SenderClientId;

    if (ClienteConTarjetaNetwork.Value != clientId)
    {
        Debug.LogWarning(
            "[Historia] El jugador no tiene la tarjeta de acceso."
        );
        return;
    }

    if (puntoLectorPuerta == null)
    {
        Debug.LogError(
            "[Historia] No asignaste el punto del lector."
        );
        return;
    }

    if (!JugadorCercaDelPunto(
        clientId,
        puntoLectorPuerta.position,
        distanciaInteraccion))
    {
        Debug.LogWarning(
            "[Historia] El jugador está demasiado lejos del lector."
        );
        return;
    }

    if (animacionPuerta == null)
    {
        Debug.LogError(
            "[Historia] No asignaste PuertaAnimacionHistoria."
        );
        return;
    }

    PuertaAbiertaNetwork.Value = true;
    EstadoMisionNetwork.Value = MisionCompletada;

    if (RoundManager.Instance != null)
        RoundManager.Instance.DetenerSpawnsHistoria();

    Debug.Log(
        "[Historia] Misión completada. Esperando la animación de la puerta."
    );

    if (SceneManager.GetActiveScene().name == "MainSceneSinglePlayer"
        && !transicionSafeZoneIniciada)
    {
        transicionSafeZoneIniciada = true;
        StartCoroutine(EsperarAnimacionYActivarSafeZone1(clientId));
    }
}

private IEnumerator EsperarAnimacionYActivarSafeZone1(
    ulong clientId
)
{
    // Esperar hasta que la animación termine.
    float tiempoMaximoEspera = 15f;
    float tiempoTranscurrido = 0f;

    while (!animacionPuerta.AnimacionFinalizada
        && tiempoTranscurrido < tiempoMaximoEspera)
    {
        tiempoTranscurrido += Time.deltaTime;
        yield return null;
    }

    if (!animacionPuerta.AnimacionFinalizada)
    {
        Debug.LogError(
            "[Historia] No se pudo confirmar el final de la animación. " +
            "No se realizará la transición."
        );
        transicionSafeZoneIniciada = false;
        yield break;
    }

    if (NetworkManager.Singleton == null
        || !NetworkManager.Singleton.ConnectedClients.TryGetValue(
            clientId,
            out NetworkClient cliente)
        || cliente.PlayerObject == null)
    {
        Debug.LogError(
            "[Historia] No se encontró el jugador para trasladarlo."
        );
        yield break;
    }

    GestorEscenariosPartida gestorEscenarios =
        GestorEscenariosPartida.Instance;

    if (gestorEscenarios == null)
    {
        Debug.LogError(
            "[Historia] No se encontró GestorEscenariosPartida."
        );
        yield break;
    }

    gestorEscenarios.TransicionarASafeZone1(
        cliente.PlayerObject.transform
    );

    Debug.Log(
        "[Historia] Puerta abierta. Jugador trasladado a SafeZone1."
    );
}
private void AlCambiarEstadoPuerta(
    bool estadoAnterior,
    bool estadoNuevo)
{
    AplicarEstadoPuerta(estadoNuevo);
}
   

private void AplicarEstadoPuerta(bool abierta)
{
    Debug.Log(
        $"[Historia] AplicarEstadoPuerta({abierta}). " +
        $"Animator asignado: {animacionPuerta != null}.",
        this
    );

    if (!abierta)
        return;

    if (animacionAperturaEjecutada)
    {
        Debug.Log("[Historia] La apertura ya se había ejecutado.", this);
        return;
    }

    if (animacionPuerta == null)
    {
        Debug.LogError(
            "[Historia] No asignaste PuertaAnimacionHistoria.",
            this
        );
        return;
    }

    animacionAperturaEjecutada = true;

    Debug.Log("[Historia] Llamando a AbrirPuerta().", this);
    animacionPuerta.AbrirPuerta();
}


public void CompletarMisionLaboratorioParaPruebas()
{
    if (!Application.isEditor && !Debug.isDebugBuild)
        return;

    if (!IsSpawned)
    {
        Debug.LogError(
            "[Historia Debug] GestorHistoriaPartida todavía no está iniciado."
        );
        return;
    }

    CompletarMisionLaboratorioPruebaRpc();
}

[Rpc(
    SendTo.Server,
    InvokePermission = RpcInvokePermission.Everyone
)]
private void CompletarMisionLaboratorioPruebaRpc(
    RpcParams rpcParams = default
)
{
    if (!IsServer)
        return;

    if (!Application.isEditor && !Debug.isDebugBuild)
        return;

    if (!EsHistoriaLaboratorio())
    {
        Debug.LogWarning(
            "[Historia Debug] La prueba corresponde al modo Historia del Laboratorio."
        );
        return;
    }

    if (EstadoMisionNetwork.Value == MisionCompletada)
    {
        Debug.LogWarning(
            "[Historia Debug] La misión ya está completada."
        );
        return;
    }

    if (animacionPuerta == null)
    {
        Debug.LogError(
            "[Historia Debug] No hay una animación de puerta asignada."
        );
        return;
    }

    ulong clientId = rpcParams.Receive.SenderClientId;

    PuertaAbiertaNetwork.Value = true;
    EstadoMisionNetwork.Value = MisionCompletada;

    if (RoundManager.Instance != null)
        RoundManager.Instance.DetenerSpawnsHistoria();

    Debug.Log(
        "[Historia Debug] Misión completada manualmente. " +
        "Se probará la animación de la puerta."
    );

    if (SceneManager.GetActiveScene().name == "MainSceneSinglePlayer"
        && !transicionSafeZoneIniciada)
    {
        transicionSafeZoneIniciada = true;
        StartCoroutine(
            EsperarAnimacionYActivarSafeZone1(clientId)
        );

    }
    
}

}
