
using UnityEngine;
using Unity.Netcode;
using TMPro;

public class GestorHistoriaPartida : NetworkBehaviour
{
    private const int MisionInactiva = 0;
    private const int BuscarTarjeta = 1;
    private const int AbrirPuerta = 2;
    private const int MisionCompletada = 3;
    private bool animacionAperturaEjecutada;

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
            return;

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
            return;

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
        {
            RoundManager.Instance.DetenerSpawnsHistoria();
        }
        else
        {
            Debug.LogWarning(
                "[Historia] No se encontró RoundManager " +
                "para detener los spawns."
            );
        }

        Debug.Log(
            "[Historia] Misión completada. Iniciando apertura de la puerta."
        );
    }

    private void AlCambiarEstadoPuerta(
        bool estadoAnterior,
        bool estadoNuevo
    )
    {
        AplicarEstadoPuerta(estadoNuevo);
    }

   

private void AplicarEstadoPuerta(bool abierta)
{
    if (!abierta)
        return;

    if (animacionAperturaEjecutada)
        return;

    if (animacionPuerta == null)
    {
        Debug.LogError(
            "[Historia] No asignaste PuertaAnimacionHistoria.",
            this
        );
        return;
    }

    // Evita ejecutar la animación más de una vez
    // en esta instancia.
    animacionAperturaEjecutada = true;

    Debug.Log(
        "[Historia] Misión completada. Reproduciendo apertura de puerta.",
        this
    );

    animacionPuerta.AbrirPuerta();
}


}
