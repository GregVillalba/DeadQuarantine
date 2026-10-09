
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

    [Tooltip("Objeto que contiene el cartel 'Presione E'.")]
    private GameObject interactionPrompt;

    [Header("Collider")]
    [Tooltip("Collider invisible que recibe los disparos.")]
    [SerializeField] private Collider eggCollider;

    private PlayerControls controls;
    private Camera localPlayerCamera;

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
        controls = new PlayerControls();

        ConfiguracionesJuego.CargarRebinds(controls.asset);

        if (eggCollider == null)
            eggCollider = GetComponent<Collider>();

        OcultarPrompt();
    }

    // =========================================================
    // ENABLE / DISABLE
    // =========================================================

    private void OnEnable()
    {
        if (controls != null)
            controls.Player.Enable();
    }

    private void OnDisable()
    {
        if (controls != null)
            controls.Player.Disable();
    }
public override void OnDestroy()
{
    controls?.Dispose();

    base.OnDestroy();
}

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        currentHealthNetwork.OnValueChanged += OnHealthChanged;
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
        currentHealthNetwork.OnValueChanged -= OnHealthChanged;
        foundNetwork.OnValueChanged -= OnFoundChanged;
    }

    // =========================================================
    // UPDATE
    // =========================================================

private void Update()
{
    BuscarCamaraJugadorLocal();

    if (localPlayerCamera == null)
        return;

    if (EstaDisponibleParaInteraccion() &&
        controls.Player.Interact.triggered)
    {
        ActivarPorInteraccion();
    }
}

    // =========================================================
    // BUSCAR CÁMARA LOCAL
    // =========================================================

    private void BuscarCamaraJugadorLocal()
    {
        if (localPlayerCamera != null &&
            localPlayerCamera.isActiveAndEnabled)
        {
            return;
        }

        localPlayerCamera = null;

        Camera[] cameras =
            FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude
            );

        foreach (Camera cam in cameras)
        {
            if (cam.gameObject.name != "PlayerCamera")
                continue;

            if (!cam.isActiveAndEnabled)
                continue;

            NetworkObject networkObject =
                cam.GetComponentInParent<NetworkObject>();

            // MULTIPLAYER
            if (networkObject != null &&
                networkObject.IsSpawned &&
                networkObject.IsOwner)
            {
                localPlayerCamera = cam;
                return;
            }

            // SINGLEPLAYER
            if (networkObject == null &&
                cam == Camera.main)
            {
                localPlayerCamera = cam;
                return;
            }
        }

        // FALLBACK
        if (Camera.main != null &&
            Camera.main.isActiveAndEnabled &&
            Camera.main.gameObject.name == "PlayerCamera")
        {
            NetworkObject networkObject =
                Camera.main.GetComponentInParent<NetworkObject>();

            if (networkObject == null ||
                networkObject.IsOwner)
            {
                localPlayerCamera = Camera.main;
            }
        }
    }

    // =========================================================
    // INTERACCIÓN
    // =========================================================

    private bool EstaDisponibleParaInteraccion()
    {

        if (!PuedeMostrarCartel())
    return false;
    
        if (EstaEncontrado())
            return false;

        if (eggCollider == null || !eggCollider.enabled)
            return false;

        Vector3 origen = localPlayerCamera.transform.position;

        Vector3 direccion = eggCollider.bounds.center - origen;

        float distancia = direccion.magnitude;

        if (distancia > interactionDistance)
            return false;

        direccion.Normalize();

        Ray ray = new Ray(origen, direccion);

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactionDistance))
        {
            return false;
        }

        EasterEgg egg =
            hit.collider.GetComponentInParent<EasterEgg>();

        return egg == this;
    }

    private void ActivarPorInteraccion()
    {
        if (EstaEncontrado())
            return;

        if (!ValidarId())
            return;

        // MULTIPLAYER / NETCODE
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            ActivarRpc();
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
            TakeDamageRpc(amount);
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

    private void RegistrarEasterEggEnLogros()
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
        return foundNetwork.Value;
    }

    private void OnHealthChanged(
        int previousValue,
        int newValue)
    {
        if (newValue <= 0)
            OcultarPrompt();
    }

    private void OnFoundChanged(
        bool previousValue,
        bool newValue)
    {
        AplicarEstadoActual();
    }

    private void AplicarEstadoActual()
    {
        if (foundNetwork.Value)
        {
            OcultarPrompt();

            if (eggCollider != null)
                eggCollider.enabled = false;
        }
        else
        {
            if (eggCollider != null)
                eggCollider.enabled = true;
        }
    }

    // =========================================================
    // PROMPT
    // =========================================================

    private void MostrarPrompt()
    {
        if (interactionPrompt != null)
            interactionPrompt.SetActive(true);
    }

    private void OcultarPrompt()
    {
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
    }

    private void BuscarPromptJugadorLocal()
    {
        if (interactionPrompt != null)
            return;

        PlayerScore[] players =
            FindObjectsByType<PlayerScore>();

        foreach (PlayerScore player in players)
        {
            NetworkObject networkObject =
                player.GetComponentInParent<NetworkObject>();

            // MULTIPLAYER: solo el jugador local
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                if (networkObject == null || !networkObject.IsOwner)
                    continue;
            }

            Transform root = player.transform.root;

            Transform hudCanvas =
                BuscarHijoPorNombre(root, "HUD_Canvas");

            if (hudCanvas == null)
                continue;

            Transform easterEggUI =
                BuscarHijoPorNombre(
                    hudCanvas,
                    "EasterEggInteractionUI");

            if (easterEggUI == null)
                continue;

            Transform interfaz =
                BuscarHijoPorNombre(
                    easterEggUI,
                    "interface_EasterEgg");

            if (interfaz == null)
                continue;

            Transform texto =
                BuscarHijoPorNombre(
                    interfaz,
                    "Text_EasterEgg");

            if (texto == null)
                continue;

            interactionPrompt = texto.gameObject;

            Debug.Log(
                "[EasterEgg] Texto de interacción encontrado " +
                "en el jugador local."
            );

            return;
        }
    }

    private Transform BuscarHijoPorNombre(
        Transform padre,
        string nombre)
    {
        if (padre.name == nombre)
            return padre;

        foreach (Transform hijo in padre)
        {
            Transform encontrado =
                BuscarHijoPorNombre(hijo, nombre);

            if (encontrado != null)
                return encontrado;
        }

        return null;
    }

    public bool PuedeMostrarCartel()
{
    if (EstaEncontrado())
        return false;

    if (string.IsNullOrWhiteSpace(easterEggId))
        return false;

    if (AchievementsManager.Instance != null &&
        AchievementsManager.Instance.EasterEggYaEncontrado(easterEggId))
    {
        return false;
    }

    return true;
}
}