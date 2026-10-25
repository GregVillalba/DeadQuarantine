
using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class GestorHistoriaPartida : NetworkBehaviour
{
    private const int MisionInactiva = 0;
    private const int BuscarTarjeta = 1;
    private const int AbrirPuerta = 2;
    private const int MisionCompletada = 3;
    private const int BuscarRadio = 4;
    private const int BuscarSenal = 5;
    private const int ContactarSobrevivientes = 6;
    private const int HistoriaCiudadCompletada = 7;
    private const int EnZonaSegura = 8;
    private bool animacionAperturaEjecutada;
    private bool misionCiudadInicializada;
    private readonly HashSet<ulong> jugadoresEnZonaSegura = new HashSet<ulong>();
    private bool transicionSafeZone1Iniciada;
    private bool transicionSafeZone2Iniciada;   

    [Header("Tarjeta de acceso")]
    [SerializeField] private GameObject tarjetaPrefab;
    [SerializeField] private Transform puntoSpawnTarjeta;
    [SerializeField] private float distanciaRecogida = 3f;

    [Header("Puerta y lector")]
    [SerializeField] private Transform puntoLectorPuerta;
    [SerializeField] private PuertaAnimacionHistoria animacionPuerta;
    [SerializeField] private float distanciaInteraccion = 3f;

    [Header("Radio de Ciudad")]
    [SerializeField] private GameObject radioPrefab;
    [SerializeField] private Transform puntoSpawnRadio;
    [SerializeField] private float distanciaRecogidaRadio = 3f;

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
        
    public NetworkVariable<ulong> ClienteConRadioNetwork =
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

    if (EsHistoriaLaboratorio())
    {
        EstadoMisionNetwork.Value = BuscarTarjeta;
        ClienteConTarjetaNetwork.Value = ulong.MaxValue;
        PuertaAbiertaNetwork.Value = false;

        AplicarEstadoPuerta(false);
        CrearTarjeta();
    }
    
    else if (EsHistoriaCiudad())
    {
        EstadoMisionNetwork.Value = BuscarRadio;
        ClienteConTarjetaNetwork.Value = ulong.MaxValue;
        ClienteConRadioNetwork.Value = ulong.MaxValue;
        PuertaAbiertaNetwork.Value = false;

        if (CrearRadio())
            misionCiudadInicializada = true;
    }

    else
    {
        EstadoMisionNetwork.Value = MisionInactiva;
        ClienteConTarjetaNetwork.Value = ulong.MaxValue;
        PuertaAbiertaNetwork.Value = false;

        AplicarEstadoPuerta(false);
    }
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
    int estadoNuevo)
{
    ActualizarTextoObjetivo();

    if (estadoNuevo == HistoriaCiudadCompletada &&
        EsHistoriaCiudad())
    {
        IniciarTransicionASafeZone2();
    }
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
                case BuscarRadio:
    textoTituloObjetivo.color = Color.white;
    textoTituloObjetivo.text = "OBJETIVO";
    textoDescripcionObjetivo.text =
        "Busca una radio en la comisaría.";
    break;

    case BuscarSenal:
        textoTituloObjetivo.color = Color.white;
        textoTituloObjetivo.text = "OBJETIVO";
        textoDescripcionObjetivo.text =
            "Busca un lugar elevado para encontrar señal de radio.";
        break;

    case ContactarSobrevivientes:
        textoTituloObjetivo.color = Color.white;
        textoTituloObjetivo.text = "OBJETIVO";
        textoDescripcionObjetivo.text =
            "Intenta comunicarte con los sobrevivientes.";
        break;

    case HistoriaCiudadCompletada:
        textoTituloObjetivo.color = Color.green;
        textoTituloObjetivo.text = "MISIÓN COMPLETADA";
        textoDescripcionObjetivo.text =
            "Encontraste a los sobrevivientes. Prepárate para continuar.";
        break;

    case EnZonaSegura:
        textoTituloObjetivo.color = Color.green;
        textoTituloObjetivo.text = "ZONA SEGURA";
        textoDescripcionObjetivo.text =
            "Compra lo que necesites. Presiona ENTER para avanzar.";
        break;

            default:
                textoTituloObjetivo.text = "";
                textoDescripcionObjetivo.text = "";
                break;
        }
    }

    
public bool RegistrarTarjeta(
    ulong clientId,
    TarjetaAccesoHistoria tarjeta)
{
    if (!IsServer || tarjeta == null)
        return false;

    if (EstadoMisionNetwork.Value != BuscarTarjeta)
        return false;

    ClienteConTarjetaNetwork.Value = clientId;
    EstadoMisionNetwork.Value = AbrirPuerta;

    tarjeta.DespawnearTarjeta();

    Debug.Log(
        "[Historia] Tarjeta registrada para el cliente " + clientId
    );

    return true;
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
        "[Historia] Misión completada. La zona segura queda detrás de la puerta."
    );
}

// Solo lo usa el servidor: ZombieAI ignora a estos jugadores, igual que a uno abatido.
public bool JugadorEnZonaSegura(ulong clientId)
{
    return jugadoresEnZonaSegura.Contains(clientId);
}

public void NotificarZonaSegura(bool dentro)
{
    CambiarZonaSeguraRpc(dentro);
}

[Rpc(
    SendTo.Server,
    InvokePermission = RpcInvokePermission.Everyone
)]
private void CambiarZonaSeguraRpc(
    bool dentro,
    RpcParams rpcParams = default
)
{
    if (!IsServer || !EsHistoriaLaboratorio())
        return;

    if (EstadoMisionNetwork.Value != MisionCompletada &&
        EstadoMisionNetwork.Value != EnZonaSegura)
        return;

    ulong clientId = rpcParams.Receive.SenderClientId;

    if (dentro)
        jugadoresEnZonaSegura.Add(clientId);
    else
        jugadoresEnZonaSegura.Remove(clientId);

    EstadoMisionNetwork.Value = jugadoresEnZonaSegura.Count > 0
        ? EnZonaSegura
        : MisionCompletada;

    // Con todos en la zona segura no aparecen zombies; si alguien vuelve
    // al laboratorio, siguen apareciendo a medida que los matan.
    if (RoundManager.Instance != null)
    {
        if (TodosEnZonaSegura())
            RoundManager.Instance.DetenerSpawnsHistoria();
        else
            RoundManager.Instance.ReanudarSpawnsHistoria();
    }

    Debug.Log(
        "[Historia] Jugador " + clientId +
        (dentro ? " entró a" : " salió de") + " la zona segura."
    );
}

private bool TodosEnZonaSegura()
{
    if (NetworkManager.Singleton == null)
        return false;

    foreach (NetworkClient cliente in NetworkManager.Singleton.ConnectedClientsList)
    {
        if (!jugadoresEnZonaSegura.Contains(cliente.ClientId))
            return false;
    }

    return true;
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
    animacionPuerta.AlFinalizarAnimacion -=
    AlFinalizarAperturaPuerta;

    animacionPuerta.AlFinalizarAnimacion +=
    AlFinalizarAperturaPuerta;

    Debug.Log("[Historia] Llamando a AbrirPuerta().", this);
    animacionPuerta.AbrirPuerta();

    // La zona segura aparece mientras se abre la puerta, para cruzarla caminando.
    if (SceneManager.GetActiveScene().name == "MainSceneSinglePlayer"
        && GestorEscenariosPartida.Instance != null)
    {
        GestorEscenariosPartida.Instance.MostrarSafeZone1();
    }
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
        "[Historia Debug] Misión completada manualmente por el cliente " +
        clientId + ". Se probará la animación de la puerta."
    );
}

private bool EsHistoriaCiudad()
{
    if (ConfiguracionPartidaSeleccionada.Instancia == null)
        return false;

    return
        ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado ==
            ConfiguracionPartidaSeleccionada.ModoJuego.Historia
        &&
        ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado ==
            ConfiguracionPartidaSeleccionada.Escenario.Ciudad;
}

private bool CrearRadio()
{
    if (!IsServer)
        return false;

    if (radioPrefab == null)
    {
        Debug.LogError(
            "[Historia] No asignaste el prefab de la radio.",
            this
        );
        return false;
    }

    if (puntoSpawnRadio == null)
    {
        Debug.LogError(
            "[Historia] No asignaste el punto de aparición de la radio.",
            this
        );
        return false;
    }

    GameObject objeto = Instantiate(
        radioPrefab,
        puntoSpawnRadio.position,
        puntoSpawnRadio.rotation
    );

    NetworkObject networkObject =
        objeto.GetComponent<NetworkObject>();

    if (networkObject == null)
    {
        Debug.LogError(
            "[Historia] El prefab de la radio necesita un NetworkObject.",
            this
        );

        Destroy(objeto);
        return false;
    }

    if (objeto.GetComponent<RadioHistoriaCiudad>() == null)
    {
        Debug.LogError(
            "[Historia] El prefab necesita RadioHistoriaCiudad.",
            this
        );

        Destroy(objeto);
        return false;
    }

    networkObject.Spawn(true);

    Debug.Log(
        "[Historia] Radio creada y sincronizada: " + objeto.name,
        this
    );

    return true;
}

public void RegistrarRadio(
    ulong clientId,
    RadioHistoriaCiudad radio
)
{
    if (!IsServer || radio == null)
        return;

    if (!EsHistoriaCiudad() ||
        EstadoMisionNetwork.Value != BuscarRadio)
        return;

    if (!JugadorCercaDelPunto(
        clientId,
        radio.transform.position,
        distanciaRecogidaRadio))
    {
        Debug.LogWarning(
            "[Historia] El jugador está demasiado lejos de la radio."
        );
        return;
    }

    ClienteConRadioNetwork.Value = clientId;
    radio.DespawnearRadio();

    EstadoMisionNetwork.Value = BuscarSenal;

    Debug.Log(
        "[Historia] Radio recogida. Comienza la búsqueda de señal."
    );
}

public void IntentarRegistrarSenalEncontrada()
{
    RegistrarSenalEncontradaRpc();
}


[Rpc(
    SendTo.Server,
    InvokePermission = RpcInvokePermission.Everyone
)]
private void RegistrarSenalEncontradaRpc(
    RpcParams rpcParams = default
)
{
    Debug.Log(
        $"[PRUEBA HISTORIA] RPC recibida. " +
        $"IsServer={IsServer}, IsSpawned={IsSpawned}, " +
        $"Estado={EstadoMisionNetwork.Value}, " +
        $"Instancia={GetEntityId()}",
        this
    );

    if (!IsServer)
    {
        Debug.LogWarning("[PRUEBA HISTORIA] Se detuvo: no es servidor.", this);
        return;
    }

    if (!EsHistoriaCiudad())
    {
        Debug.LogWarning("[PRUEBA HISTORIA] Se detuvo: EsHistoriaCiudad() es false.", this);
        return;
    }

    if (EstadoMisionNetwork.Value != BuscarSenal)
    {
        Debug.LogWarning(
            $"[PRUEBA HISTORIA] Se detuvo: el estado es {EstadoMisionNetwork.Value}, " +
            $"pero se esperaba {BuscarSenal}.",
            this
        );
        return;
    }

    ulong clientId = rpcParams.Receive.SenderClientId;

    Debug.Log(
        $"[PRUEBA HISTORIA] Cliente que encontró la señal: {clientId}.",
        this
    );

    bool detectorValido = JugadorCercaDeUnJugadorConDetector(clientId);

    Debug.Log(
        $"[PRUEBA HISTORIA] Detector válido: {detectorValido}.",
        this
    );

    if (!detectorValido)
    {
        Debug.LogWarning(
            "[PRUEBA HISTORIA] Se detuvo: el detector no se encontró " +
            "o su propiedad SenalEncontrada es false.",
            this
        );
        return;
    }

    EstadoMisionNetwork.Value = ContactarSobrevivientes;

    Debug.Log(
        $"[PRUEBA HISTORIA] Estado cambiado correctamente a " +
        $"{EstadoMisionNetwork.Value}. Instancia={GetEntityId()}",
        this
    );
}


private bool JugadorCercaDeUnJugadorConDetector(ulong clientId)
{
    if (NetworkManager.Singleton == null)
        return false;

    if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
        clientId,
        out NetworkClient cliente))
        return false;

    if (cliente.PlayerObject == null)
        return false;

    DetectorAlturaRadioHistoria detector =
        cliente.PlayerObject.GetComponentInChildren<
            DetectorAlturaRadioHistoria
        >();

    return detector != null && detector.SenalEncontrada;
}

public void IniciarMisionCiudad()
{
    if (!IsServer || !EsHistoriaCiudad())
        return;

    if (misionCiudadInicializada)
        return;

    // La zona segura se apaga al pasar a la Ciudad: nadie queda adentro.
    jugadoresEnZonaSegura.Clear();

    if (!CrearRadio())
    {
        Debug.LogError(
            "[Historia] No se pudo iniciar la misión de Ciudad porque " +
            "no se creó la radio."
        );
        return;
    }

    ClienteConTarjetaNetwork.Value = ulong.MaxValue;
    ClienteConRadioNetwork.Value = ulong.MaxValue;
    EstadoMisionNetwork.Value = BuscarRadio;

    misionCiudadInicializada = true;

    Debug.Log(
        "[Historia] Misión de Ciudad iniciada. Radio creada."
    );
}
public void ActivarDetectorSenal(ulong clientId)
{
    if (!IsServer || NetworkManager.Singleton == null)
        return;

    if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
        clientId, out NetworkClient cliente))
    {
        Debug.LogError(
            "[Historia] No se encontró el cliente para activar el detector."
        );
        return;
    }

    if (cliente.PlayerObject == null)
    {
        Debug.LogError(
            "[Historia] El cliente no tiene PlayerObject."
        );
        return;
    }

    ActivarDetectorSenalRpc(
        RpcTarget.Single(clientId, RpcTargetUse.Temp)
    );
}

[Rpc(SendTo.SpecifiedInParams)]
private void ActivarDetectorSenalRpc(
    RpcParams rpcParams = default)
{
    if (NetworkManager.Singleton == null)
        return;

    NetworkObject jugador =
        NetworkManager.Singleton.LocalClient.PlayerObject;

    if (jugador == null)
    {
        Debug.LogError(
            "[Historia] No se encontró el jugador local."
        );
        return;
    }

    DetectorAlturaRadioHistoria detector =
        jugador.GetComponentInChildren<DetectorAlturaRadioHistoria>(true);

    if (detector == null)
    {
        Debug.LogError(
            "[Historia] El jugador no tiene DetectorAlturaRadioHistoria."
        );
        return;
    }

    detector.ActivarBusquedaDeSenal();
}


public void ConfirmarContactoSobrevivientes()
{
    if (!IsSpawned)
        return;

    ConfirmarContactoSobrevivientesRpc();
}

[Rpc(
    SendTo.Server,
    InvokePermission = RpcInvokePermission.Everyone
)]
private void ConfirmarContactoSobrevivientesRpc(
    RpcParams rpcParams = default)
{
    if (!IsServer || !EsHistoriaCiudad())
        return;

    if (EstadoMisionNetwork.Value != ContactarSobrevivientes)
        return;

    ulong clientId = rpcParams.Receive.SenderClientId;

    // Solo puede confirmar el jugador que recogio la radio.
    if (ClienteConRadioNetwork.Value != clientId)
        return;

    EstadoMisionNetwork.Value = HistoriaCiudadCompletada;

    Debug.Log(
        "[Historia] Contacto establecido con los sobrevivientes. " +
        "Mision de Ciudad completada."
    );
}

private void AlFinalizarAperturaPuerta()
{
    if (transicionSafeZone1Iniciada)
        return;

    if (SceneManager.GetActiveScene().name !=
        "MainSceneSinglePlayer")
        return;

    if (NetworkManager.Singleton == null ||
        NetworkManager.Singleton.LocalClient.PlayerObject == null)
        return;

    transicionSafeZone1Iniciada = true;

    Transform jugador =
        NetworkManager.Singleton.LocalClient.PlayerObject.transform;

    if (FadeTransicionHistoria.Instance != null)
    {
        FadeTransicionHistoria.Instance.EjecutarTransicion(() =>
        {
            if (GestorEscenariosPartida.Instance != null)
            {
                GestorEscenariosPartida.Instance
                    .TransicionarASafeZone1(jugador);
            }
        });
    }
    else
    {
        Debug.LogError(
            "[Historia] No se encontró FadeTransicionHistoria."
        );

        GestorEscenariosPartida.Instance
            ?.TransicionarASafeZone1(jugador);
    }
}

private void IniciarTransicionASafeZone2()
{
    if (transicionSafeZone2Iniciada)
        return;

    if (SceneManager.GetActiveScene().name !=
        "MainSceneSinglePlayer")
        return;

    if (NetworkManager.Singleton == null ||
        NetworkManager.Singleton.LocalClient.PlayerObject == null)
        return;

    transicionSafeZone2Iniciada = true;

    Transform jugador =
        NetworkManager.Singleton.LocalClient.PlayerObject.transform;

    System.Action trasladarJugador = () =>
    {
        if (GestorEscenariosPartida.Instance == null)
        {
            Debug.LogError(
                "[Historia] No se encontró GestorEscenariosPartida."
            );
            return;
        }

        GestorEscenariosPartida.Instance
            .TransicionarASafeZone2(jugador);

        if (IsServer)
            EstadoMisionNetwork.Value = EnZonaSegura;
    };

    if (FadeTransicionHistoria.Instance != null)
    {
        FadeTransicionHistoria.Instance.EjecutarTransicion(
            trasladarJugador
        );
    }
    else
    {
        Debug.LogError(
            "[Historia] No se encontró FadeTransicionHistoria."
        );

        trasladarJugador();
    }
}
}
