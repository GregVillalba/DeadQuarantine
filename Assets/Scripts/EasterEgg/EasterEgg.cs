
using UnityEngine;
using Unity.Netcode;

public class EasterEgg : NetworkBehaviour
{
    [Header("Easter Egg")]
    [SerializeField] private string easterEggId;
    [SerializeField] private int maxHealth = 1;
    [SerializeField] private int rewardPoints = 1000;

    [Header("Interacción")]
    [SerializeField] private float interactionDistance = 3f;

    [Header("Collider")]
    [Tooltip("Collider invisible que recibe los disparos.")]
    [SerializeField] private Collider eggCollider;

    private bool encontradoPorGestorHistoria;
    private int saludFallback;

    private NetworkVariable<int> currentHealthNetwork =
        new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private NetworkVariable<bool> foundNetwork =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        saludFallback = maxHealth;
        if (eggCollider == null)
            eggCollider = GetComponent<Collider>();
    }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        foundNetwork.OnValueChanged += OnFoundChanged;

        if (IsServer)
        {
            currentHealthNetwork.Value = maxHealth;
            foundNetwork.Value = false;
        }

        AplicarEstadoActual();
    }

    public override void OnNetworkDespawn()
    {
        foundNetwork.OnValueChanged -= OnFoundChanged;
    }

    internal void ActivarPorInteraccion()
    {
        if (EstaEncontrado())
            return;

        if (!ValidarId())
            return;

        // MULTIPLAYER / NETCODE
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            if (IsSpawned)
                ActivarRpc();
            else if (GestorHistoriaPartida.Instance != null)
                GestorHistoriaPartida.Instance.SolicitarActivacionEasterEgg(easterEggId, 0, true);
            return;
        }

        // SINGLEPLAYER SIN NETWORK
        ActivarLocalmente();
    }

    // =========================================================
    // ACTIVAR POR E
    // =========================================================

    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Everyone
    )]
    private void ActivarRpc(
        RpcParams rpcParams = default)
    {
        if (foundNetwork.Value)
            return;

        if (!ValidarId())
            return;

        ulong clientId = rpcParams.Receive.SenderClientId;

        ActivarEnServidor(clientId);
    }

    // =========================================================
    // DAÑO POR DISPARO
    // =========================================================

    public void TakeDamage(
        int amount,
        Vector3 hitPoint,
        Vector3 hitNormal)
    {
        if (EstaEncontrado())
            return;

        if (amount <= 0)
            return;

        if (!ValidarId())
            return;

        // MULTIPLAYER / NETCODE
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            if (IsSpawned)
                TakeDamageRpc(amount);
            else if (GestorHistoriaPartida.Instance != null)
                GestorHistoriaPartida.Instance.SolicitarActivacionEasterEgg(easterEggId, amount, false);
            return;
        }

        // SINGLEPLAYER SIN NETWORK
        currentHealthNetwork.Value -= amount;

        if (currentHealthNetwork.Value <= 0)
        {
            currentHealthNetwork.Value = 0;
            ActivarLocalmente();
        }
    }

    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Everyone
    )]
    private void TakeDamageRpc(
        int amount,
        RpcParams rpcParams = default)
    {
        if (foundNetwork.Value || amount <= 0)
            return;

        if (!ValidarId())
            return;

        currentHealthNetwork.Value -= amount;

        if (currentHealthNetwork.Value <= 0)
        {
            currentHealthNetwork.Value = 0;

            ulong shooterClientId =
                rpcParams.Receive.SenderClientId;

            ActivarEnServidor(shooterClientId);
        }
    }

    // =========================================================
    // ACTIVAR EN SERVIDOR
    // =========================================================

    private void ActivarEnServidor(ulong clientId)
    {
        if (!IsServer || foundNetwork.Value)
            return;

        if (!ValidarId())
            return;

        foundNetwork.Value = true;
        currentHealthNetwork.Value = 0;

        DarRecompensa(clientId);
    }

    // =========================================================
    // RECOMPENSA
    // =========================================================

    private void DarRecompensa(ulong clientId)
    {
        if (!IsServer)
            return;

        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
            clientId,
            out NetworkClient client))
        {
            return;
        }

        if (client.PlayerObject == null)
            return;

        PlayerScore playerScore =
            client.PlayerObject.GetComponentInChildren<PlayerScore>();

        if (playerScore != null)
        {
            playerScore.SumarPuntos(rewardPoints);
        }

        MostrarLogroAlJugadorClientRpc(
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[]
                    {
                        clientId
                    }
                }
            }
        );
    }

    // =========================================================
    // LOGRO MULTIPLAYER
    // =========================================================

    [ClientRpc]
    private void MostrarLogroAlJugadorClientRpc(
        ClientRpcParams clientRpcParams = default)
    {
        RegistrarEasterEggEnLogros();
    }

    // =========================================================
    // SINGLEPLAYER SIN NETWORK
    // =========================================================

    private void ActivarLocalmente()
    {
        if (EstaEncontrado())
            return;

        if (!ValidarId())
            return;

        foundNetwork.Value = true;
        currentHealthNetwork.Value = 0;

        PlayerScore score = ObtenerPlayerScoreLocal();

        if (score != null)
        {
            score.SumarPuntos(rewardPoints);
        }

        RegistrarEasterEggEnLogros();

        Debug.Log(
            "[EASTER EGG] Encontrado localmente. +" +
            rewardPoints + " puntos."
        );
    }

    // =========================================================
    // REGISTRO PERSISTENTE DEL EASTER EGG
    // =========================================================

    public void RegistrarEasterEggEnLogros()
    {
        if (!ValidarId())
            return;

        if (AchievementsManager.Instance == null)
        {
            Debug.LogWarning(
                "[EASTER EGG] No existe AchievementsManager."
            );

            return;
        }

        bool registrado =
            AchievementsManager.Instance
                .RegistrarEasterEggEncontrado(easterEggId);

        if (registrado)
        {
            Debug.Log(
                "[EASTER EGG] Nuevo Easter Egg registrado: " +
                easterEggId
            );
        }
        else
        {
            Debug.Log(
                "[EASTER EGG] Este Easter Egg ya estaba registrado: " +
                easterEggId
            );
        }
    }

    private bool ValidarId()
    {
        if (!string.IsNullOrWhiteSpace(easterEggId))
            return true;

        Debug.LogError(
            "[EASTER EGG] Falta asignar un ID en el Inspector: " +
            gameObject.name,
            this
        );

        return false;
    }

    // =========================================================
    // PLAYER SCORE LOCAL
    // =========================================================

    private PlayerScore ObtenerPlayerScoreLocal()
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.LocalClient != null &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            return NetworkManager.Singleton
                .LocalClient
                .PlayerObject
                .GetComponentInChildren<PlayerScore>();
        }

        PlayerScore[] scores =
            FindObjectsByType<PlayerScore>();

        foreach (PlayerScore score in scores)
        {
            return score;
        }

        return null;
    }

    // =========================================================
    // ESTADO
    // =========================================================

    private bool EstaEncontrado()
    {
        return encontradoPorGestorHistoria || foundNetwork.Value;
    }

    public string EasterEggId => easterEggId;
    public int PuntosRecompensa => rewardPoints;
    public float InteractionDistance => interactionDistance;

    public bool ProcesarSolicitudDesdeGestorHistoria(
        int amount,
        bool porInteraccion)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer ||
            IsSpawned ||
            EstaEncontrado())
            return false;

        if (!porInteraccion)
        {
            saludFallback -= Mathf.Max(1, amount);
            if (saludFallback > 0)
                return false;
        }

        encontradoPorGestorHistoria = true;
        saludFallback = 0;
        AplicarEstadoActual();
        return true;
    }

    public void MarcarEncontradoPorGestorHistoria()
    {
        encontradoPorGestorHistoria = true;
        saludFallback = 0;
        AplicarEstadoActual();
    }

    private void OnFoundChanged(
        bool previousValue,
        bool newValue)
    {
        AplicarEstadoActual();
    }

    private void AplicarEstadoActual()
    {
        if (eggCollider != null)
            eggCollider.enabled = !EstaEncontrado();
    }

    public bool PuedeMostrarCartel()
    {
        return !EstaEncontrado() &&
            !string.IsNullOrWhiteSpace(easterEggId) &&
            (AchievementsManager.Instance == null ||
             !AchievementsManager.Instance.EasterEggYaEncontrado(easterEggId));
    }
}
