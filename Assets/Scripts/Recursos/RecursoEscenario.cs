using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Recurso del mapa (US 19.1 / 19.2). Lo hace aparecer GestorRecursosEscenario en un PuntoRecursoEscenario y
/// se queda ahí hasta que alguien lo junta con la tecla de interacción (InteraccionRecursoEscenario) o termina
/// la partida. Es compartido: cuando un jugador lo junta desaparece para todos.
/// </summary>
public class RecursoEscenario : NetworkBehaviour
{
    /// <summary>Unidades que da al recolectarlo.</summary>
    public const int CantidadPorRecurso = 1;

    [Header("Modelos (se muestra el del recurso que toca)")]
    [SerializeField] private GameObject modeloPilaDeLatas;
    [SerializeField] private GameObject modeloCantimplora;
    [SerializeField] private GameObject modeloBidon;

    [Tooltip("Distancia (m) entre el jugador y el recurso a la que el servidor todavía acepta la recolección. " +
             "Es la de la mira (3 m) más un margen por la altura de la cámara y la latencia.")]
    [SerializeField] private float distanciaMaximaServidor = 4.5f;

    public NetworkVariable<int> TipoNetwork = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public TipoRecurso Tipo => (TipoRecurso)TipoNetwork.Value;
    public bool Recolectado { get; private set; }

    // Lo carga el servidor antes de Spawn; se pasa a la NetworkVariable en OnNetworkSpawn
    // (así llega a los clientes con el primer sincronizado).
    private TipoRecurso tipoInicial;

    /// <summary>Solo servidor, antes de NetworkObject.Spawn().</summary>
    public void Configurar(TipoRecurso tipo)
    {
        tipoInicial = tipo;
        MostrarModelo(tipo);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            TipoNetwork.Value = (int)tipoInicial;

        MostrarModelo(Tipo);
    }

    /// <summary>
    /// Solo servidor (lo llama PlayerRecursos.RecolectarRecursoEscenarioServerRpc). Suma el recurso al inventario
    /// del jugador y lo saca del mapa. Si dos jugadores lo piden a la vez, las RPC se procesan de a una:
    /// el primero se lo lleva y el segundo encuentra Recolectado en true.
    /// </summary>
    public bool IntentarRecolectar(PlayerRecursos jugador)
    {
        if (!IsServer || !IsSpawned || Recolectado || jugador == null || !jugador.PuedeRecolectar)
            return false;

        if (!EstaAlAlcance(jugador.transform.position, transform.position, distanciaMaximaServidor))
            return false;

        Recolectado = true;
        jugador.SumarRecurso(Tipo, CantidadPorRecurso);
        NetworkObject.Despawn(true);
        return true;
    }

    public static bool EstaAlAlcance(Vector3 jugador, Vector3 recurso, float distanciaMaxima)
    {
        return Vector3.Distance(jugador, recurso) <= distanciaMaxima;
    }

    private void MostrarModelo(TipoRecurso tipo)
    {
        if (modeloPilaDeLatas != null)
            modeloPilaDeLatas.SetActive(tipo == TipoRecurso.PilaDeLatas);

        if (modeloCantimplora != null)
            modeloCantimplora.SetActive(tipo == TipoRecurso.Cantimplora);

        if (modeloBidon != null)
            modeloBidon.SetActive(tipo == TipoRecurso.Bidon);
    }
}
