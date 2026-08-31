using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Header("Zombie")]
    [SerializeField] private GameObject zombiePrefab;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Rondas")]
    [SerializeField] private int startingZombies = 6;
    [SerializeField] private int zombiesPerRound = 6;
    [SerializeField] private int maxRounds = 5;
    [SerializeField] private float nextRoundDelay = 5f;
    [SerializeField] private float delayAntesDePanel = 3f;
    [SerializeField] private int damagePerShot = 25;
    [SerializeField] private int shotsIncreasePerRound = 2;

    [Header("Spawn escalonado")]
    [SerializeField] private float initialSpawnDelay = 5f;
    [SerializeField] private float minSpawnInterval = 2f;
    [SerializeField] private float maxSpawnInterval = 10f;

    [Header("HUD de inicio de ronda")]
    [SerializeField] private RoundStartHUD roundStartHUD;

    // =========================================================
    // VARIABLES SINCRONIZADAS POR RED
    // =========================================================

    public NetworkVariable<int> CurrentRoundNetwork =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<int> AliveZombiesNetwork =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<int> ZombiesThisRoundNetwork =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<int> CountdownNetwork =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<bool> GameLostNetwork =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // =========================================================
    // PROPIEDADES
    // =========================================================

    public int CurrentRound =>
        CurrentRoundNetwork.Value;

    public int MaxRounds =>
        maxRounds;

    public int AliveZombies =>
        AliveZombiesNetwork.Value;

    public int ZombiesThisRound =>
        ZombiesThisRoundNetwork.Value;

    public bool IsCountingDown =>
        countdownInProgress;

    public int CountdownRemaining =>
        CountdownNetwork.Value;

    // =========================================================
    // VARIABLES LOCALES
    // =========================================================

    private bool roundInProgress;
    private bool countdownInProgress;

    private int pendingNextRound;
    private Coroutine spawnRoutine;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        // Escuchar cambios de las variables de red
        CurrentRoundNetwork.OnValueChanged += OnRoundChanged;
        CountdownNetwork.OnValueChanged += OnCountdownChanged;
        GameLostNetwork.OnValueChanged += OnGameLostChanged;

        // Si ya se perdió la partida cuando este cliente entró
        if (GameLostNetwork.Value)
        {
            MostrarDerrota();
        }

        // Solamente el servidor controla las rondas
        if (!IsServer)
            return;

        if (roundStartHUD != null)
            roundStartHUD.Hide();

        // =====================================================
        // SINGLEPLAYER
        // =====================================================

        if (SceneManager.GetActiveScene().name ==
            "MainSceneSinglePlayer")
        {
            // NO iniciar la ronda todavía.
            //
            // El PauseController del PlayerMultiplayer
            // mostrará la historia y llamará a:
            //
            // RoundManager.Instance.StartRound(1);
            //
            // cuando se pulse CONTINUAR.

            return;
        }

        // =====================================================
        // MULTIPLAYER
        // =====================================================

        StartRound(1);
    }

    // =========================================================
    // NETWORK DESPAWN
    // =========================================================

    public override void OnNetworkDespawn()
    {
        CurrentRoundNetwork.OnValueChanged -= OnRoundChanged;
        CountdownNetwork.OnValueChanged -= OnCountdownChanged;
        GameLostNetwork.OnValueChanged -= OnGameLostChanged;
    }

    // =========================================================
    // CAMBIOS DE RED
    // =========================================================

    private void OnRoundChanged(
        int previousValue,
        int newValue
    )
    {
        // El HUD principal lee directamente
        // CurrentRoundNetwork.
    }

    private void OnCountdownChanged(
        int previousValue,
        int newValue
    )
    {
        if (roundStartHUD == null)
            return;

        if (newValue > 0)
        {
            roundStartHUD.Show(
                CurrentRoundNetwork.Value,
                newValue
            );
        }
        else if (previousValue > 0)
        {
            roundStartHUD.Show(
                CurrentRoundNetwork.Value,
                0
            );
        }
    }

    private void OnGameLostChanged(
        bool previousValue,
        bool newValue
    )
    {
        if (newValue)
        {
            MostrarDerrota();
        }
    }

    // =========================================================
    // INICIAR RONDA
    // =========================================================

    public void StartRound(int round)
    {
        if (!IsServer)
            return;

        CurrentRoundNetwork.Value = round;

        ZombiesThisRoundNetwork.Value =
            startingZombies +
            (round - 1) * zombiesPerRound;

        AliveZombiesNetwork.Value =
            ZombiesThisRoundNetwork.Value;

        roundInProgress = true;
        countdownInProgress = false;

        CountdownNetwork.Value = 0;

        if (roundStartHUD != null)
            roundStartHUD.Hide();

        if (GameplayPopupsController.Instance != null)
        {
            GameplayPopupsController.Instance
                .MostrarPanelRonda();
        }

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(
            SpawnZombiesRoutine(ZombiesThisRoundNetwork.Value)
        );

        Debug.Log(
            "Ronda " +
            CurrentRoundNetwork.Value +
            " iniciada. Zombies: " +
            ZombiesThisRoundNetwork.Value
        );
    }

    // =========================================================
    // SPAWN ZOMBIES
    // =========================================================

    private IEnumerator SpawnZombiesRoutine(int amount)
    {
        if (!IsServer)
            yield break;

        if (zombiePrefab == null)
        {
            Debug.LogError(
                "RoundManager: falta asignar Zombie Prefab."
            );

            yield break;
        }

        if (spawnPoints == null ||
            spawnPoints.Length == 0)
        {
            Debug.LogError(
                "RoundManager: no hay Spawn Points."
            );

            yield break;
        }

        int round = CurrentRoundNetwork.Value;

        int shotsNeeded = round * shotsIncreasePerRound;
        int zombieHealthThisRound = shotsNeeded * damagePerShot;

        float runChance = maxRounds > 1
            ? Mathf.Clamp01((round - 1) / (float)(maxRounds - 1))
            : 0f;

        Debug.Log(
            "[RoundManager] Ronda " + round +
            " | Vida por zombie: " + zombieHealthThisRound +
            " | Probabilidad de correr: " + (runChance * 100f) + "%"
        );

        yield return new WaitForSeconds(initialSpawnDelay);

        for (int i = 0; i < amount; i++)
        {
            Transform spawnPoint =
                spawnPoints[
                    i % spawnPoints.Length
                ];

            GameObject zombieInstance =
                Instantiate(
                    zombiePrefab,
                    spawnPoint.position,
                    spawnPoint.rotation
                );
                
                ZombieAppearance zombieAppearance =
                    zombieInstance.GetComponent<ZombieAppearance>();

                if (zombieAppearance != null)
                {
                    zombieAppearance.SelectRandomModel();
                }

            // Configurar vida según la ronda ANTES de spawnear en red.
            ZombieHealth zombieHealthComponent =
                zombieInstance.GetComponent<ZombieHealth>();

            if (zombieHealthComponent != null)
            {
                zombieHealthComponent.InitializeHealth(zombieHealthThisRound);
            }

            // Decidir si este zombie corre o camina.
            ZombieAI zombieAIComponent =
                zombieInstance.GetComponent<ZombieAI>();

            bool willRun = Random.value < runChance;

            NetworkObject netObj =
                zombieInstance.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                netObj.Spawn();

                // La velocidad se aplica después del Spawn, porque
                // ZombieAI.OnNetworkSpawn() recién ahí inicializa el
                // NavMeshAgent con la velocidad de caminar por defecto.
                if (zombieAIComponent != null)
                {
                    zombieAIComponent.SetRunning(willRun);
                }
            }
            else
            {
                Debug.LogError(
                    "RoundManager: el zombiePrefab no tiene " +
                    "NetworkObject asignado."
                );
            }

            if (i < amount - 1)
            {
                float wait = Random.Range(minSpawnInterval, maxSpawnInterval);
                yield return new WaitForSeconds(wait);
            }
        }
    }

    // =========================================================
    // ZOMBIE MUERE
    // =========================================================

    public void ZombieDied()
    {
        if (!IsServer)
            return;

        if (!roundInProgress)
            return;

        AliveZombiesNetwork.Value--;

        if (AliveZombiesNetwork.Value < 0)
        {
            AliveZombiesNetwork.Value = 0;
        }

        Debug.Log(
            "Zombie muerto. Restantes: " +
            AliveZombiesNetwork.Value
        );

        if (AliveZombiesNetwork.Value <= 0)
        {
            EndRound();
        }
    }

    // =========================================================
    // JUGADOR MUERE
    // =========================================================

    public void PlayerDied()
    {
        if (!IsServer)
            return;

        if (!roundInProgress)
            return;

        roundInProgress = false;

        GameLostNetwork.Value = true;
    }

    private void MostrarDerrota()
    {
        if (GameplayPopupsController.Instance != null)
        {
            GameplayPopupsController.Instance
                .MostrarPanelPerdedor();
        }
        else
        {
            Debug.LogError(
                "RoundManager: GameplayPopupsController.Instance es null."
            );
        }
    }

    // =========================================================
    // FIN DE RONDA
    // =========================================================

    private void EndRound()
    {
        if (!IsServer)
            return;

        roundInProgress = false;

        StartCoroutine(
            EndRoundDelay()
        );
    }

    private IEnumerator EndRoundDelay()
    {
        yield return new WaitForSeconds(
            delayAntesDePanel
        );

        if (GameplayPopupsController.Instance != null)
        {
            GameplayPopupsController.Instance
                .MostrarPanelGanador();
        }
        else
        {
            Debug.LogError(
                "RoundManager: GameplayPopupsController.Instance es null."
            );
        }

        if (CurrentRoundNetwork.Value >= maxRounds)
        {
            Debug.Log(
                "Ronda " +
                CurrentRoundNetwork.Value +
                " completada."
            );

            if (roundStartHUD != null)
                roundStartHUD.Hide();

            yield break;
        }

        pendingNextRound =
            CurrentRoundNetwork.Value + 1;
    }

    // =========================================================
    // SIGUIENTE RONDA
    // =========================================================

    public void ConfirmarSiguienteRonda()
    {
        if (!IsServer)
            return;

        if (pendingNextRound <= 0)
            return;

        int nextRound =
            pendingNextRound;

        pendingNextRound = 0;

        StartCoroutine(
            CountdownToNextRound(nextRound)
        );
    }

    // =========================================================
    // COUNTDOWN
    // =========================================================

    private IEnumerator CountdownToNextRound(
        int nextRound
    )
    {
        if (!IsServer)
            yield break;

        if (countdownInProgress)
            yield break;

        countdownInProgress = true;

        CurrentRoundNetwork.Value = nextRound;

        CountdownNetwork.Value = 5;

        yield return new WaitForSeconds(1f);

        CountdownNetwork.Value = 4;

        yield return new WaitForSeconds(1f);

        CountdownNetwork.Value = 3;

        yield return new WaitForSeconds(1f);

        CountdownNetwork.Value = 2;

        yield return new WaitForSeconds(1f);

        CountdownNetwork.Value = 1;

        yield return new WaitForSeconds(1f);

        CountdownNetwork.Value = 0;

        yield return new WaitForSeconds(0.2f);

        countdownInProgress = false;

        StartRound(nextRound);
    }
}