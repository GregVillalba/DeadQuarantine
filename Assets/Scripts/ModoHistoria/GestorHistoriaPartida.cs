
using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;
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
    private const int MisionFinalCabana = 9;
    private const int MansionResistir = 1;
    private const int MansionEsperandoAlfa = 2;
    private const int MansionHelipuerto = 3;
    private const int MansionCompletada = 4;
    private const int IrAlCaminoSalida = 10;
    private const int MinJugadoresMultiplayer = 2;

    private bool animacionAperturaEjecutada;
    private bool misionCiudadInicializada;
    // Indica si el jugador local ya confirmó el avance.
    private bool localConfirmoAvanceZonaSegura;

    private readonly HashSet<ulong> jugadoresEnZonaSegura =
        new HashSet<ulong>();
    private readonly HashSet<ulong> jugadoresConfirmaronAvanceZonaSegura =
        new HashSet<ulong>();
    private readonly HashSet<ulong> jugadoresEnTriggerLaboratorio =
        new HashSet<ulong>();
    private readonly HashSet<ulong> jugadoresEnTriggerSalidaCiudad =
        new HashSet<ulong>();
    private readonly HashSet<ulong> clientesConCortinaTransicionVisible =
        new HashSet<ulong>();

    private bool transicionSafeZone1Iniciada;
    private bool transicionSafeZone2Iniciada;
    private bool transicionSafeZone1MultiplayerIniciada;
    private bool transicionZonaSeguraMultiplayerIniciada;
    private int estadoObjetivoRecibido = -1;
    private float proximaSincronizacionObjetivo;

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

    [Header("Salida de Ciudad")]
    [SerializeField] private Transform puntoSalidaCiudad;
    [SerializeField] private float distanciaActivacionSalidaCiudad = 6f;

    [Header("Trigger de salida del Laboratorio")]
    [SerializeField] private Transform puntoTriggerLaboratorio;
    [SerializeField] private float distanciaActivacionTriggerLaboratorio = 7f;

    [Header("HUD local")]
    [SerializeField] private TMP_Text textoTituloObjetivo;
    [SerializeField] private TMP_Text textoDescripcionObjetivo;

    public bool EsperandoSalidaCiudad =>
        IsSpawned &&
        EstadoMisionActual == IrAlCaminoSalida;

    public int EstadoMisionActual => estadoObjetivoRecibido >= 0
        ? estadoObjetivoRecibido
        : EstadoMisionNetwork.Value;

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

    public NetworkVariable<int> ConfirmacionesZonaSeguraNetwork =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<int> JugadoresZonaSeguraNetwork =
        new NetworkVariable<int>(
            2,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<int> EtapaMansionNetwork =
        new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public NetworkVariable<int> TemporizadorMansionNetwork =
        new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

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
        EtapaMansionNetwork.OnValueChanged += AlCambiarEtapaMansion;
        TemporizadorMansionNetwork.OnValueChanged += AlCambiarTemporizadorMansion;
        PuertaAbiertaNetwork.OnValueChanged += AlCambiarEstadoPuerta;

        ConfirmacionesZonaSeguraNetwork.OnValueChanged +=
    AlCambiarConfirmacionesZonaSegura;

JugadoresZonaSeguraNetwork.OnValueChanged +=
    AlCambiarConfirmacionesZonaSegura;

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += AlConectarJugadorZonaSegura;
            NetworkManager.Singleton.OnClientDisconnectCallback += AlDesconectarJugadorZonaSegura;
        }

        ActualizarTextoObjetivo();

        // El mapa multiplayer se carga como escena aditiva después de esta escena.
        GestorEscenariosPartida gestorEscenarios = GestorEscenariosPartida.Instance;
        if (gestorEscenarios != null && gestorEscenarios.UsaEscenasAditivasMultiplayer)
        {
            gestorEscenarios.EventoMapaMultiplayerListo += AlCargarMapaMultiplayer;
            if (gestorEscenarios.MapaMultiplayerListo)
                AlCargarMapaMultiplayer();
            return;
        }

        InicializarMisionEscenarioActual();
    }

    private void AlCargarMapaMultiplayer()
    {
        VincularReferenciasEscenarioMultiplayer();

        localConfirmoAvanceZonaSegura = false;
        string mapaActual = SceneManager.GetActiveScene().name;
        jugadoresEnTriggerLaboratorio.Clear();
        jugadoresEnTriggerSalidaCiudad.Clear();

        if (mapaActual == "MP_Laboratorio")
            transicionSafeZone1MultiplayerIniciada = false;

        if (mapaActual == "MP_ZonaSegura1" || mapaActual == "MP_ZonaSegura2")
        {
            if (IsServer)
            {
                EstadoMisionNetwork.Value = EnZonaSegura;
                ReiniciarConfirmacionesZonaSegura();
            }
            if (!IsServer)
                AplicarEstadoPuerta(PuertaAbiertaNetwork.Value);
            return;
        }

        if (IsServer)
        {
            InicializarMisionEscenarioActual();
            if (mapaActual == "MP_Ciudad")
            {
                if (RoundManager.Instance != null)
                {
                    RoundManager.Instance.ActualizarSpawnPointsDelEscenario();
                    RoundManager.Instance.EliminarZombiesHistoria();
                    RoundManager.Instance.ReanudarSpawnsHistoria();
                }
                IniciarMisionCiudad();
            }
            else if (mapaActual == "MP_Cabana")
            {
                if (RoundManager.Instance != null)
                {
                    RoundManager.Instance.ActualizarSpawnPointsDelEscenario();
                    RoundManager.Instance.EliminarZombiesHistoria();
                }
                IniciarMisionCabana();
                GestorMansionHistoria mansion = FindAnyObjectByType<GestorMansionHistoria>();
                if (mansion != null)
                    mansion.IniciarMision();
            }
        }
        else
            AplicarEstadoPuerta(PuertaAbiertaNetwork.Value);
    }

    private void InicializarMisionEscenarioActual()
    {

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
            // Al cargar desde SafeZone1, todavía no debe comenzar la misión
            // ni aparecer la radio. Se inicia cuando el jugador avance a Ciudad.
            EstadoMisionNetwork.Value = EnZonaSegura;

            ClienteConTarjetaNetwork.Value = ulong.MaxValue;
            ClienteConRadioNetwork.Value = ulong.MaxValue;
            PuertaAbiertaNetwork.Value = false;

            misionCiudadInicializada = false;
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
        GestorEscenariosPartida gestorEscenarios = GestorEscenariosPartida.Instance;
        if (gestorEscenarios != null)
            gestorEscenarios.EventoMapaMultiplayerListo -= AlCargarMapaMultiplayer;

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= AlConectarJugadorZonaSegura;
            NetworkManager.Singleton.OnClientDisconnectCallback -= AlDesconectarJugadorZonaSegura;
        }

        EstadoMisionNetwork.OnValueChanged -= AlCambiarEstadoMision;
        EtapaMansionNetwork.OnValueChanged -= AlCambiarEtapaMansion;
        TemporizadorMansionNetwork.OnValueChanged -= AlCambiarTemporizadorMansion;
        PuertaAbiertaNetwork.OnValueChanged -= AlCambiarEstadoPuerta;

        if (Instance == this)
            Instance = null;

        ConfirmacionesZonaSeguraNetwork.OnValueChanged -=
             AlCambiarConfirmacionesZonaSegura;

        JugadoresZonaSeguraNetwork.OnValueChanged -=
            AlCambiarConfirmacionesZonaSegura;
        base.OnNetworkDespawn();
    }

  public void SolicitarAvanceDesdeZonaSegura()
{
    if (!IsSpawned || !EsZonaSeguraMultiplayerActiva())
        return;

    // Evita enviar varias confirmaciones desde el mismo jugador.
    if (localConfirmoAvanceZonaSegura)
        return;

    localConfirmoAvanceZonaSegura = true;

    // Actualización inmediata del HUD local.
    ActualizarTextoObjetivo();

    // El servidor registra la confirmación.
    ConfirmarAvanceZonaSeguraRpc();
}

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ConfirmarAvanceZonaSeguraRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || NetworkManager.Singleton == null ||
            !EsZonaSeguraMultiplayerActiva())
            return;

        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
            return;

        ActualizarCantidadJugadoresZonaSegura();
        if (!jugadoresConfirmaronAvanceZonaSegura.Add(clientId))
            return;

        ConfirmacionesZonaSeguraNetwork.Value =
            jugadoresConfirmaronAvanceZonaSegura.Count;

        if (ConfirmacionesZonaSeguraNetwork.Value >=
            Mathf.Max(MinJugadoresMultiplayer, JugadoresZonaSeguraNetwork.Value))
            AvanzarAlSiguienteEscenarioDesdeZonaSegura();
    }

    private void ReiniciarConfirmacionesZonaSegura()
    {
        jugadoresConfirmaronAvanceZonaSegura.Clear();
        transicionZonaSeguraMultiplayerIniciada = false;
        ActualizarCantidadJugadoresZonaSegura();
        ConfirmacionesZonaSeguraNetwork.Value = 0;
    }

    private void ActualizarCantidadJugadoresZonaSegura()
    {
        if (!IsServer || NetworkManager.Singleton == null ||
            !EsZonaSeguraMultiplayerActiva())
            return;

        JugadoresZonaSeguraNetwork.Value =
            NetworkManager.Singleton.ConnectedClientsList.Count;
    }

    private void AlConectarJugadorZonaSegura(ulong clientId)
    {
        ActualizarCantidadJugadoresZonaSegura();
    }

    private void AlDesconectarJugadorZonaSegura(ulong clientId)
    {
        jugadoresEnTriggerLaboratorio.Remove(clientId);
        jugadoresEnTriggerSalidaCiudad.Remove(clientId);

        if (!EsZonaSeguraMultiplayerActiva())
            return;

        jugadoresConfirmaronAvanceZonaSegura.Remove(clientId);
        ActualizarCantidadJugadoresZonaSegura();
        ConfirmacionesZonaSeguraNetwork.Value =
            jugadoresConfirmaronAvanceZonaSegura.Count;

        if (ConfirmacionesZonaSeguraNetwork.Value >=
            Mathf.Max(MinJugadoresMultiplayer, JugadoresZonaSeguraNetwork.Value))
            AvanzarAlSiguienteEscenarioDesdeZonaSegura();
    }

    private bool EsZonaSeguraMultiplayerActiva()
    {
        string escena = SceneManager.GetActiveScene().name;
        return escena == "MP_ZonaSegura1" || escena == "MP_ZonaSegura2";
    }

    private void AvanzarAlSiguienteEscenarioDesdeZonaSegura()
    {
        if (!IsServer || transicionZonaSeguraMultiplayerIniciada ||
            !EsZonaSeguraMultiplayerActiva())
            return;

        GestorEscenariosPartida escenarios = GestorEscenariosPartida.Instance;
        if (escenarios == null)
        {
            Debug.LogError("[Historia] No se encontró el gestor para avanzar desde la zona segura.");
            return;
        }

        transicionZonaSeguraMultiplayerIniciada = true;
        ConfiguracionPartidaSeleccionada.Escenario siguiente =
            SceneManager.GetActiveScene().name == "MP_ZonaSegura1"
                ? ConfiguracionPartidaSeleccionada.Escenario.Ciudad
                : ConfiguracionPartidaSeleccionada.Escenario.Cabana;

        if (RoundManager.Instance != null)
            RoundManager.Instance.EliminarZombiesHistoria();

        StartCoroutine(ActivarEscenarioConCortina(siguiente));
    }

    private void VincularReferenciasEscenarioMultiplayer()
    {
        GestorEscenariosPartida escenarios = GestorEscenariosPartida.Instance;
        if (escenarios == null || !escenarios.UsaEscenasAditivasMultiplayer)
            return;

        Scene escena = SceneManager.GetActiveScene();
        if (!escena.IsValid() || !escena.isLoaded)
            return;

        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            foreach (Transform candidato in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (puntoSpawnTarjeta == null && candidato.name == "spawnCard")
                    puntoSpawnTarjeta = candidato;
                if (puntoSpawnRadio == null && candidato.name == "Punto_Radio_Comisaria")
                    puntoSpawnRadio = candidato;
                if (puntoSalidaCiudad == null && candidato.name == "TriggerContinuarHistoria")
                    puntoSalidaCiudad = candidato;
                if (puntoTriggerLaboratorio == null &&
                    candidato.name == "TriggerSafeZoneLaboratorio")
                    puntoTriggerLaboratorio = candidato;
            }

            if (animacionPuerta == null)
                animacionPuerta = raiz.GetComponentInChildren<PuertaAnimacionHistoria>(true);
            if (puntoLectorPuerta == null)
            {
                LectorTarjetaHistoria lector = raiz.GetComponentInChildren<LectorTarjetaHistoria>(true);
                if (lector != null)
                    puntoLectorPuerta = lector.transform;
            }
        }

        if (SceneManager.GetActiveScene().name == "MP_Laboratorio" &&
            puntoTriggerLaboratorio == null)
            Debug.LogError("[Historia MP] Falta TriggerSafeZoneLaboratorio en Escenario_1.", this);
        if (SceneManager.GetActiveScene().name == "MP_Ciudad" &&
            puntoSalidaCiudad == null)
            Debug.LogError("[Historia MP] Falta TriggerContinuarHistoria en Escenario_2.", this);

        Debug.Log("[Historia MP] Referencias del escenario vinculadas. " +
            $"Tarjeta={puntoSpawnTarjeta != null}, lector={puntoLectorPuerta != null}, " +
            $"puerta={animacionPuerta != null}, radio={puntoSpawnRadio != null}, " +
            $"salida={puntoSalidaCiudad != null}, " +
            $"triggerLaboratorio={puntoTriggerLaboratorio != null}.", this);
    }

    private void Update()
    {
        if (IsSpawned && Time.unscaledTime >= proximaSincronizacionObjetivo)
        {
            proximaSincronizacionObjetivo = Time.unscaledTime + 5f;
            if (IsServer)
                SincronizarObjetivoClientRpc(EstadoMisionNetwork.Value);
            ActualizarTextoObjetivo();
        }

        if (IsServer && !transicionSafeZone2Iniciada &&
            EsHistoriaCiudad() &&
            SceneManager.GetActiveScene().name == "MP_Ciudad" &&
            EstadoMisionNetwork.Value == IrAlCaminoSalida &&
            puntoSalidaCiudad != null && NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectedClientsList.Count >= MinJugadoresMultiplayer &&
            TodosLosJugadoresEnTrigger(puntoSalidaCiudad, distanciaActivacionSalidaCiudad))
            EstadoMisionNetwork.Value = HistoriaCiudadCompletada;

        if (!IsServer || transicionSafeZone1MultiplayerIniciada ||
            !EsHistoriaLaboratorio() ||
            SceneManager.GetActiveScene().name != "MP_Laboratorio" ||
            EstadoMisionNetwork.Value != MisionCompletada ||
            animacionPuerta == null || !animacionPuerta.AnimacionFinalizada ||
            puntoTriggerLaboratorio == null || NetworkManager.Singleton == null)
            return;

        // Los jugadores pueden estar frente a la puerta antes de que termine
        // la misión. La comprobación directa evita depender de OnTriggerEnter.
        foreach (NetworkClient cliente in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (!JugadorDentroDelTrigger(
                    cliente.ClientId,
                    puntoTriggerLaboratorio,
                    distanciaActivacionTriggerLaboratorio))
                return;
        }

        if (NetworkManager.Singleton.ConnectedClientsList.Count < MinJugadoresMultiplayer)
            return;

        transicionSafeZone1MultiplayerIniciada = true;
        StartCoroutine(TransicionarSafeZone1ConCortina());
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

    public void SolicitarActivacionEasterEgg(
        string easterEggId,
        int amount,
        bool porInteraccion)
    {
        if (!IsSpawned || string.IsNullOrWhiteSpace(easterEggId))
            return;

        SolicitarActivacionEasterEggRpc(easterEggId, amount, porInteraccion);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SolicitarActivacionEasterEggRpc(
        string easterEggId,
        int amount,
        bool porInteraccion,
        RpcParams rpcParams = default)
    {
        if (!IsServer || NetworkManager.Singleton == null)
            return;

        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                clientId, out NetworkClient cliente) || cliente.PlayerObject == null)
            return;

        EasterEgg[] easterEggs =
            FindObjectsByType<EasterEgg>(FindObjectsSortMode.None);

        foreach (EasterEgg easterEgg in easterEggs)
        {
            if (easterEgg == null || easterEgg.EasterEggId != easterEggId ||
                easterEgg.IsSpawned)
                continue;

            float distancia = Vector3.Distance(
                cliente.PlayerObject.transform.position,
                easterEgg.transform.position);
            float distanciaMaxima = porInteraccion
                ? easterEgg.InteractionDistance + 1.5f
                : 100f;
            if (distancia > distanciaMaxima)
                return;

            if (!easterEgg.ProcesarSolicitudDesdeGestorHistoria(amount, porInteraccion))
                return;

            PlayerScore score =
                cliente.PlayerObject.GetComponentInChildren<PlayerScore>();
            if (score != null)
                score.SumarPuntos(easterEgg.PuntosRecompensa);

            ActualizarEasterEggFallbackClientRpc(easterEggId, clientId);
            return;
        }
    }

    [ClientRpc]
    private void ActualizarEasterEggFallbackClientRpc(
        string easterEggId,
        ulong clientId)
    {
        EasterEgg[] easterEggs =
            FindObjectsByType<EasterEgg>(FindObjectsSortMode.None);

        foreach (EasterEgg easterEgg in easterEggs)
        {
            if (easterEgg == null || easterEgg.EasterEggId != easterEggId ||
                easterEgg.IsSpawned)
                continue;

            easterEgg.MarcarEncontradoPorGestorHistoria();
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.LocalClientId == clientId)
                easterEgg.RegistrarEasterEggEnLogros();
        }
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

    [ClientRpc]
    private void SincronizarObjetivoClientRpc(int estadoServidor)
    {
        estadoObjetivoRecibido = estadoServidor;
        ActualizarTextoObjetivo();
    }

    private void AlCambiarEstadoMision(
        int estadoAnterior,
        int estadoNuevo
    )
    {
        estadoObjetivoRecibido = estadoNuevo;
        ActualizarTextoObjetivo();
        if (IsServer && IsSpawned)
            SincronizarObjetivoClientRpc(estadoNuevo);

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

        switch (EstadoMisionActual)
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

            case IrAlCaminoSalida:
                textoTituloObjetivo.color = Color.white;
                textoTituloObjetivo.text = "OBJETIVO";
                textoDescripcionObjetivo.text =
                    "Dirígete al camino de salida de la ciudad.";
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

                if (EsZonaSeguraMultiplayerActiva() &&
                    NetworkManager.Singleton != null &&
                    NetworkManager.Singleton.ConnectedClientsIds.Count <
                        MinJugadoresMultiplayer)
                {
                    textoDescripcionObjetivo.text =
                        "Se necesitan al menos 2 jugadores conectados " +
                        "para continuar. (" +
                        NetworkManager.Singleton.ConnectedClientsIds.Count +
                        "/2)";
                }
                else if (EsZonaSeguraMultiplayerActiva() &&
                         localConfirmoAvanceZonaSegura)
                {
                    textoDescripcionObjetivo.text =
                        "Esperá que todos estén listos. (" +
                        ConfirmacionesZonaSeguraNetwork.Value +
                        "/" +
                        JugadoresZonaSeguraNetwork.Value +
                        ")";
                }
                else
                {
                    textoDescripcionObjetivo.text =
                        EsZonaSeguraMultiplayerActiva()
                            ? "Compra lo que necesites. Presiona ENTER para continuar."
                            : "Compra lo que necesites. Presiona ENTER para avanzar.";
                }

                break;

            case MisionFinalCabana:
                textoTituloObjetivo.color = EtapaMansionNetwork.Value >= MansionCompletada ? Color.green : Color.white;
                textoTituloObjetivo.text = EtapaMansionNetwork.Value >= MansionCompletada ? "MISIÓN COMPLETADA" : "OBJETIVO FINAL";
                switch (EtapaMansionNetwork.Value)
                {
                    case MansionEsperandoAlfa:
                        textoDescripcionObjetivo.text = "Elimina al Zombie Alfa para poder escapar.";
                        break;
                    case MansionHelipuerto:
                        textoDescripcionObjetivo.text = "Corre hacia el helipuerto y alcanza el punto de extracción.";
                        break;
                    case MansionCompletada:
                        textoDescripcionObjetivo.text = "Lograste escapar de la mansión.";
                        break;
                    default:
                        textoDescripcionObjetivo.text = TemporizadorMansionNetwork.Value > 0
                            ? "Resiste hasta que aparezca el Zombie Alfa."
                            : "Prepárate para enfrentarlo.";
                        break;
                }
                break;

            default:
                textoTituloObjetivo.text = "";
                textoDescripcionObjetivo.text = "";
                break;
        }
    }

    public bool RegistrarTarjeta(
        ulong clientId,
        TarjetaAccesoHistoria tarjeta
    )
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

    public void NotificarPresenciaEnTriggerHistoria(
        bool esTriggerLaboratorio,
        bool dentro)
    {
        if (!IsSpawned)
            return;

        ActualizarPresenciaTriggerHistoriaRpc(esTriggerLaboratorio, dentro);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ActualizarPresenciaTriggerHistoriaRpc(
        bool esTriggerLaboratorio,
        bool dentro,
        RpcParams rpcParams = default)
    {
        if (!IsServer || NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening)
        {
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
            return;

        HashSet<ulong> jugadoresDentro;
        Transform puntoTrigger;
        float distancia;

        if (esTriggerLaboratorio)
        {
            if (!EsHistoriaLaboratorio() ||
                SceneManager.GetActiveScene().name != "MP_Laboratorio" ||
                EstadoMisionNetwork.Value != MisionCompletada ||
                animacionPuerta == null || !animacionPuerta.AnimacionFinalizada)
            {
                return;
            }

            jugadoresDentro = jugadoresEnTriggerLaboratorio;
            puntoTrigger = puntoTriggerLaboratorio;
            distancia = distanciaActivacionTriggerLaboratorio;
        }
        else
        {
            if (!EsHistoriaCiudad() ||
                SceneManager.GetActiveScene().name != "MP_Ciudad" ||
                EstadoMisionNetwork.Value != IrAlCaminoSalida)
            {
                return;
            }

            jugadoresDentro = jugadoresEnTriggerSalidaCiudad;
            puntoTrigger = puntoSalidaCiudad;
            distancia = distanciaActivacionSalidaCiudad;
        }

        if (dentro)
        {
            if (puntoTrigger == null ||
                !JugadorDentroDelTrigger(clientId, puntoTrigger, distancia))
            {
                return;
            }

            jugadoresDentro.Add(clientId);
            if (!TodosLosJugadoresConectadosEstanDentro(jugadoresDentro))
                return;

            if (esTriggerLaboratorio)
            {
                if (transicionSafeZone1MultiplayerIniciada)
                    return;

                transicionSafeZone1MultiplayerIniciada = true;
                StartCoroutine(TransicionarSafeZone1ConCortina());
            }
            else
            {
                EstadoMisionNetwork.Value = HistoriaCiudadCompletada;
            }
        }
        else
        {
            jugadoresDentro.Remove(clientId);
        }
    }

    private bool TodosLosJugadoresConectadosEstanDentro(
        HashSet<ulong> jugadoresDentro)
    {
        if (NetworkManager.Singleton == null)
            return false;

        int cantidadConectada =
            NetworkManager.Singleton.ConnectedClientsList.Count;

        if (cantidadConectada < MinJugadoresMultiplayer ||
            cantidadConectada > NetworkBootstrap.MaxPlayersPerRoom)
        {
            return false;
        }

        foreach (NetworkClient cliente in
                 NetworkManager.Singleton.ConnectedClientsList)
        {
            if (!jugadoresDentro.Contains(cliente.ClientId))
                return false;
        }

        return true;
    }

    private bool TodosLosJugadoresEnTrigger(Transform punto, float distancia)
    {
        if (NetworkManager.Singleton == null)
            return false;

        foreach (NetworkClient cliente in NetworkManager.Singleton.ConnectedClientsList)
            if (!JugadorDentroDelTrigger(cliente.ClientId, punto, distancia))
                return false;

        return true;
    }

    private bool JugadorDentroDelTrigger(
        ulong clientId,
        Transform puntoTrigger,
        float distanciaFallback)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.ConnectedClients.TryGetValue(
                clientId,
                out NetworkClient cliente) ||
            cliente.PlayerObject == null)
        {
            return false;
        }

        Collider colliderTrigger = puntoTrigger.GetComponent<Collider>();
        if (colliderTrigger == null)
        {
            return JugadorCercaDelPunto(
                clientId,
                puntoTrigger.position,
                distanciaFallback
            );
        }

        Bounds limites = colliderTrigger.bounds;
        limites.Expand(new Vector3(1f, 2f, 1f));
        return limites.Contains(cliente.PlayerObject.transform.position);
    }

    private IEnumerator TransicionarSafeZone1ConCortina()
    {
        yield return MostrarCortinaYEsperarClientes();

        if (!IsServer || GestorEscenariosPartida.Instance == null)
        {
            Debug.LogError(
                "[Historia] No se pudo iniciar la transición a SafeZone1."
            );
            yield break;
        }

        EstadoMisionNetwork.Value = EnZonaSegura;
        GestorEscenariosPartida.Instance.ActivarSafeZone1();
    }

    private IEnumerator ActivarEscenarioConCortina(
        ConfiguracionPartidaSeleccionada.Escenario escenario)
    {
        yield return MostrarCortinaYEsperarClientes();

        if (IsServer && GestorEscenariosPartida.Instance != null)
            GestorEscenariosPartida.Instance.ActivarEscenario(escenario);
    }

    private IEnumerator EjecutarConCortina(System.Action accion)
    {
        yield return MostrarCortinaYEsperarClientes();
        accion?.Invoke();
    }

    private IEnumerator MostrarCortinaYEsperarClientes()
    {
        clientesConCortinaTransicionVisible.Clear();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            // El anfitrión muestra la cortina localmente junto con el RPC.
            clientesConCortinaTransicionVisible.Add(
                NetworkManager.ServerClientId
            );
        }

        MostrarCortinaTransicionClientRpc();

        float tiempoEspera = 0f;
        const float tiempoMaximoEspera = 3f;

        while (!TodosLosClientesConfirmaronCortina() &&
               tiempoEspera < tiempoMaximoEspera)
        {
            tiempoEspera += Time.unscaledDeltaTime;
            yield return null;
        }

        // Da un frame de render con la cortina antes de pedir el cambio de escena.
        yield return new WaitForSecondsRealtime(0.1f);
    }

    private bool TodosLosClientesConfirmaronCortina()
    {
        if (NetworkManager.Singleton == null)
            return true;

        foreach (NetworkClient cliente in
                 NetworkManager.Singleton.ConnectedClientsList)
        {
            if (!clientesConCortinaTransicionVisible.Contains(cliente.ClientId))
                return false;
        }

        return true;
    }

    [ClientRpc]
    private void MostrarCortinaTransicionClientRpc()
    {
        GameStartCurtain.MostrarCortinaTransicion();
        ConfirmarCortinaTransicionListaRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ConfirmarCortinaTransicionListaRpc(
        RpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        clientesConCortinaTransicionVisible.Add(
            rpcParams.Receive.SenderClientId
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

    private bool EsEscenaHistoriaActiva()
    {
        if (SceneManager.GetActiveScene().name == "MainSceneSinglePlayer")
            return true;

        GestorEscenariosPartida escenarios = GestorEscenariosPartida.Instance;
        return escenarios != null && escenarios.UsaEscenasAditivasMultiplayer &&
            SceneManager.GetActiveScene().name.StartsWith("MP_");
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
    RadioHistoriaCiudad radio)
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
        "[Historia] Radio recogida. Activando detector de señal para el jugador " +
        clientId + "."
    );

    // Activa la búsqueda únicamente en el jugador que recogió la radio.
    ActivarDetectorSenal(clientId);
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
    RpcParams rpcParams = default)
{
    if (!IsServer || !EsHistoriaCiudad())
        return;

    if (EstadoMisionNetwork.Value != BuscarSenal)
        return;

    ulong clientId = rpcParams.Receive.SenderClientId;

    // Solamente puede completar esta misión quien recogió la radio.
    if (ClienteConRadioNetwork.Value != clientId)
    {
        Debug.LogWarning(
            "[Radio] El jugador que notificó la señal no tiene la radio."
        );
        return;
    }

    // El servidor comprueba la altura real del jugador.
    if (!JugadorCercaDeUnJugadorConDetector(
        clientId,
        out float altura))
    {
        Debug.LogWarning(
            "[Radio] El servidor todavía no ve al jugador a la altura " +
            "requerida. Se reactivará la búsqueda para reintentar."
        );
        ReactivarDetectorSenal(clientId);
        return;
    }

    Debug.Log(
        $"[Radio] Señal validada por el servidor. " +
        $"Jugador={clientId}, altura={altura:F2} m."
    );

    EstadoMisionNetwork.Value = ContactarSobrevivientes;

    Debug.Log(
        "[Historia] Señal encontrada. Nuevo objetivo: contactar sobrevivientes."
    );
    }

    private bool JugadorCercaDeUnJugadorConDetector(
        ulong clientId,
        out float altura)
    {
        altura = 0f;

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
                DetectorAlturaRadioHistoria>(true);

        if (detector == null)
        {
            Debug.LogError(
                "[Radio] El jugador no tiene DetectorAlturaRadioHistoria."
            );

            return false;
        }

        if (!detector.TryGetAlturaActual(out altura))
            return false;

        return altura >= detector.AlturaMinima;
    }

    private void ReactivarDetectorSenal(ulong clientId)
    {
        if (!IsServer || NetworkManager.Singleton == null ||
            EstadoMisionNetwork.Value != BuscarSenal ||
            ClienteConRadioNetwork.Value != clientId ||
            !NetworkManager.Singleton.ConnectedClients.TryGetValue(
                clientId,
                out NetworkClient cliente) ||
            cliente.PlayerObject == null)
        {
            return;
        }

        DetectorAlturaRadioHistoria detector =
            cliente.PlayerObject.GetComponentInChildren<
                DetectorAlturaRadioHistoria>(true);

        if (detector == null)
            return;

        ReactivarDetectorSenalRpc(
            RpcTarget.Single(clientId, RpcTargetUse.Temp)
        );
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReactivarDetectorSenalRpc(
        RpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton == null ||
            EstadoMisionNetwork.Value != BuscarSenal ||
            ClienteConRadioNetwork.Value !=
                NetworkManager.Singleton.LocalClientId)
        {
            return;
        }

        NetworkObject jugador =
            NetworkManager.Singleton.LocalClient.PlayerObject;

        if (jugador == null)
            return;

        DetectorAlturaRadioHistoria detector =
            jugador.GetComponentInChildren<DetectorAlturaRadioHistoria>(true);

        if (detector != null)
            detector.ReintentarBusquedaDeSenal();
    }

    public void IniciarMisionCiudad()
    {
        if (!IsServer || !EsHistoriaCiudad())
            return;

        if (misionCiudadInicializada)
            return;

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
        RpcParams rpcParams = default
    )
    {
        if (!IsServer || !EsHistoriaCiudad())
            return;

        if (EstadoMisionNetwork.Value != ContactarSobrevivientes)
            return;

        ulong clientId = rpcParams.Receive.SenderClientId;

        if (ClienteConRadioNetwork.Value != clientId)
            return;

        EstadoMisionNetwork.Value = IrAlCaminoSalida;

        Debug.Log(
            "[Historia] Contacto establecido con los sobrevivientes. " +
            "Nuevo objetivo: dirigirse al camino de salida."
        );
    }

    private void TrasladarASafeZone1(Transform jugador)
    {
        GestorEscenariosPartida gestor =
            GestorEscenariosPartida.Instance;

        if (gestor == null)
        {
            Debug.LogError(
                "[Historia] No se encontró GestorEscenariosPartida."
            );
            return;
        }

        gestor.TransicionarASafeZone1(jugador);

        if (IsServer)
        {
            EstadoMisionNetwork.Value = EnZonaSegura;

            Debug.Log(
                "[Historia] Jugador trasladado a SafeZone1. " +
                "Buscando CheckpointManager."
            );

            if (CheckpointManager.Instance != null)
            {
                Debug.Log(
                    "[Historia] CheckpointManager encontrado. " +
                    "Registrando SafeZone1."
                );

                CheckpointManager.Instance.RegistrarZonaSegura(
                    CheckpointManager.EscenarioCheckpoint.SafeZone1
                );
            }
            else
            {
                Debug.LogError(
                    "[Historia] No se puede guardar SafeZone1: " +
                    "CheckpointManager.Instance es null."
                );
            }
        }
    }

    private void AlFinalizarAperturaPuerta()
    {
        if (transicionSafeZone1Iniciada)
            return;

        if (!EsEscenaHistoriaActiva())
            return;

        if (GestorEscenariosPartida.Instance != null &&
            GestorEscenariosPartida.Instance.UsaEscenasAditivasMultiplayer &&
            !IsServer)
            return;

        if (GestorEscenariosPartida.Instance != null &&
            GestorEscenariosPartida.Instance.UsaEscenasAditivasMultiplayer)
        {
            Debug.Log(
                "[Historia] Puerta del laboratorio abierta. " +
                "Esperando que todos entren al trigger frente a la puerta.",
                this
            );
            return;
        }

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
                TrasladarASafeZone1(jugador);
            });
        }
        else
        {
            Debug.LogError(
                "[Historia] No se encontró FadeTransicionHistoria."
            );

            TrasladarASafeZone1(jugador);
        }
    }

    private void IniciarTransicionASafeZone2()
    {
        Debug.Log(
            "[Historia] IniciarTransicionASafeZone2() ejecutado. " +
            "IsServer=" + IsServer +
            " | TransicionIniciada=" + transicionSafeZone2Iniciada +
            " | Escena=" + SceneManager.GetActiveScene().name,
            this
        );

        if (transicionSafeZone2Iniciada)
        {
            Debug.LogWarning(
                "[Historia] La transición a SafeZone2 ya fue iniciada.",
                this
            );
            return;
        }

        if (!EsEscenaHistoriaActiva())
        {
            Debug.LogWarning(
                "[Historia] La transición se canceló porque no estamos en una escena activa de historia.",
                this
            );
            return;
        }

        if (GestorEscenariosPartida.Instance != null &&
            GestorEscenariosPartida.Instance.UsaEscenasAditivasMultiplayer &&
            !IsServer)
            return;

        if (NetworkManager.Singleton == null ||
            NetworkManager.Singleton.LocalClient.PlayerObject == null)
        {
            Debug.LogError(
                "[Historia] No se encontró el jugador local para SafeZone2.",
                this
            );
            return;
        }

        transicionSafeZone2Iniciada = true;

        Transform jugador =
            NetworkManager.Singleton.LocalClient.PlayerObject.transform;

        System.Action trasladarJugador = () =>
        {
            Debug.Log(
                "[Historia] Ejecutando traslado a SafeZone2.",
                this
            );

            GestorEscenariosPartida gestor =
                GestorEscenariosPartida.Instance;

            if (gestor == null)
            {
                Debug.LogError(
                    "[Historia] No se encontró GestorEscenariosPartida.",
                    this
                );
                return;
            }

            if (IsServer && RoundManager.Instance != null)
            {
                RoundManager.Instance.DetenerSpawnsHistoria();
                RoundManager.Instance.EliminarZombiesHistoria();
            }

            gestor.TransicionarASafeZone2(jugador);

            if (IsServer)
            {
                EstadoMisionNetwork.Value = EnZonaSegura;

                Debug.Log(
                    "[Historia] Jugador trasladado a SafeZone2. " +
                    "Buscando CheckpointManager."
                );

                if (CheckpointManager.Instance != null)
                {
                    Debug.Log(
                        "[Historia] CheckpointManager encontrado. " +
                        "Registrando SafeZone2."
                    );

                    CheckpointManager.Instance.RegistrarZonaSegura(
                        CheckpointManager.EscenarioCheckpoint.SafeZone2
                    );
                }
                else
                {
                    Debug.LogError(
                        "[Historia] No se puede guardar SafeZone2: " +
                        "CheckpointManager.Instance es null."
                    );
                }
            }

            Debug.Log(
                "[Historia] Traslado a SafeZone2 ejecutado. " +
                "Buscando StoryIntroController en el jugador.",
                this
            );

            StoryIntroController intro =
                jugador.GetComponentInChildren<StoryIntroController>(true);

            if (intro == null)
            {
                Debug.LogError(
                    "[Historia] No se encontró StoryIntroController " +
                    "en el jugador ni en sus objetos hijos.",
                    this
                );
                return;
            }

            Debug.Log(
                "[Historia] StoryIntroController encontrado. " +
                "Mostrando aviso para continuar a la Mansión.",
                intro
            );

            intro.MostrarAvisoContinuarAMansion();
        };

        if (GestorEscenariosPartida.Instance != null &&
            GestorEscenariosPartida.Instance.UsaEscenasAditivasMultiplayer)
        {
            StartCoroutine(EjecutarConCortina(trasladarJugador));
            return;
        }

        if (FadeTransicionHistoria.Instance != null)
        {
            Debug.Log(
                "[Historia] Iniciando fade para SafeZone2.",
                this
            );

            FadeTransicionHistoria.Instance.EjecutarTransicion(
                trasladarJugador
            );
        }
        else
        {
            Debug.LogWarning(
                "[Historia] No se encontró FadeTransicionHistoria. " +
                "Se ejecutará el traslado sin fade.",
                this
            );

            trasladarJugador();
        }
    }

    public void IniciarMisionCabana()
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "[Historia] No se puede iniciar la misión final: no es el servidor.",
                this
            );
            return;
        }

        if (ConfiguracionPartidaSeleccionada.Instancia == null ||
            ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado !=
                ConfiguracionPartidaSeleccionada.ModoJuego.Historia)
        {
            Debug.LogWarning(
                "[Historia] No se inició la misión final porque el modo no es Historia.",
                this
            );
            return;
        }

        EstadoMisionNetwork.Value = MisionFinalCabana;

        ActualizarTextoObjetivo();

        Debug.Log(
            "[Historia] Misión final iniciada. Objetivo: eliminar al mutante alfa.",
            this
        );
    }

    public void IntentarCompletarSalidaCiudad()
    {
        if (!IsSpawned)
            return;

        SolicitarLlegadaSalidaCiudadRpc();
    }

    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Everyone
    )]
    private void SolicitarLlegadaSalidaCiudadRpc(
        RpcParams rpcParams = default
    )
    {
        if (!IsServer || !EsHistoriaCiudad())
            return;

        if (EstadoMisionNetwork.Value != IrAlCaminoSalida)
            return;

        if (puntoSalidaCiudad == null)
        {
            Debug.LogError(
                "[Historia] No asignaste el punto de salida de Ciudad."
            );
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;

        if (!JugadorCercaDelPunto(
            clientId,
            puntoSalidaCiudad.position,
            distanciaActivacionSalidaCiudad))
        {
            Debug.LogWarning(
                "[Historia] El jugador está demasiado lejos del camino de salida."
            );
            return;
        }

        EstadoMisionNetwork.Value = HistoriaCiudadCompletada;

        Debug.Log(
            "[Historia] Jugador llegó al camino de salida. " +
            "Iniciando transición a SafeZone2."
        );
    }

    public void RestaurarDesdeCheckpoint(
        CheckpointManager.EscenarioCheckpoint checkpoint
    )
    {
        if (!IsServer || !IsSpawned)
        {
            Debug.LogWarning(
                "[Historia] No se puede restaurar el checkpoint: " +
                "el gestor no está iniciado en el servidor."
            );
            return;
        }

        jugadoresEnZonaSegura.Clear();

        transicionSafeZone1Iniciada = false;
        transicionSafeZone2Iniciada = false;

        misionCiudadInicializada = false;

        ClienteConTarjetaNetwork.Value = ulong.MaxValue;
        ClienteConRadioNetwork.Value = ulong.MaxValue;

        PuertaAbiertaNetwork.Value = false;

        switch (checkpoint)
        {
            case CheckpointManager.EscenarioCheckpoint.SafeZone1:

                EstadoMisionNetwork.Value = EnZonaSegura;

                Debug.Log(
                    "[Historia] Partida restaurada en SafeZone1. " +
                    "La misión de Ciudad todavía no comenzó."
                );

                break;

            case CheckpointManager.EscenarioCheckpoint.SafeZone2:

                EstadoMisionNetwork.Value = EnZonaSegura;

                Debug.Log(
                    "[Historia] Partida restaurada en SafeZone2. " +
                    "La misión de Cabana todavía no comenzó."
                );

                break;

            default:

                Debug.LogError(
                    "[Historia] Se intentó restaurar un checkpoint inválido."
                );

                break;
        }
    }
    public void PublicarEstadoMansion(int etapa, int segundos)
    {
        if (!IsServer || !IsSpawned)
            return;

        EtapaMansionNetwork.Value = etapa;
        TemporizadorMansionNetwork.Value = Mathf.Max(0, segundos);
        ActualizarTextoObjetivo();
    }

    public void SincronizarHUDMansionLocal()
    {
        if (GestorMansionHistoria.Instance == null)
            return;

        GestorMansionHistoria.Instance.AplicarEstadoMisionSincronizado(
            EtapaMansionNetwork.Value,
            TemporizadorMansionNetwork.Value);
    }

    private void AlCambiarEtapaMansion(int anterior, int nuevo)
    {
        ActualizarTextoObjetivo();
        SincronizarHUDMansionLocal();
    }

    private void AlCambiarTemporizadorMansion(int anterior, int nuevo)
    {
        ActualizarTextoObjetivo();
        SincronizarHUDMansionLocal();
    }
    private void AlCambiarConfirmacionesZonaSegura(
    int valorAnterior,
    int valorNuevo)
{
    ActualizarTextoObjetivo();
}
}
