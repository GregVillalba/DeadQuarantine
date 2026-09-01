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

    [Header("Boss")]
    [SerializeField] private GameObject[] bossPrefabs;
    [SerializeField] private int bossRound = 5;
    [SerializeField] private int bossHealthMultiplier = 10;

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

    [Header("Intervalos de spawn")]
    [SerializeField] private float round1MinSpawnInterval = 2f;
    [SerializeField] private float round1MaxSpawnInterval = 4f;

    [SerializeField] private float finalRoundMinSpawnInterval = 0.8f;
    [SerializeField] private float finalRoundMaxSpawnInterval = 1.6f;

    [Header("HUD")]
    [SerializeField] private RoundStartHUD roundStartHUD;

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

    private bool roundInProgress;
    private bool countdownInProgress;

    private int pendingNextRound;

    private Coroutine spawnRoutine;

    private void Awake()
    {
        if (
            Instance != null &&
            Instance != this
        )
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        CurrentRoundNetwork.OnValueChanged +=
            OnRoundChanged;

        CountdownNetwork.OnValueChanged +=
            OnCountdownChanged;

        GameLostNetwork.OnValueChanged +=
            OnGameLostChanged;

        if (GameLostNetwork.Value)
        {
            MostrarDerrota();
        }

        if (!IsServer)
            return;

        if (roundStartHUD != null)
        {
            roundStartHUD.Hide();
        }

        if (
            SceneManager.GetActiveScene().name ==
            "MainSceneSinglePlayer"
        )
        {
            return;
        }

        StartRound(1);
    }

    public override void OnNetworkDespawn()
    {
        CurrentRoundNetwork.OnValueChanged -=
            OnRoundChanged;

        CountdownNetwork.OnValueChanged -=
            OnCountdownChanged;

        GameLostNetwork.OnValueChanged -=
            OnGameLostChanged;
    }

    private void OnRoundChanged(
        int previousValue,
        int newValue
    )
    {
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

        CurrentRoundNetwork.Value =
            round;

        // Los espectadores vuelven al comenzar
        // una nueva ronda.
        if (round > 1)
        {
            RespawnSpectatingPlayers();
        }

        int zombiesThisRound =
            startingZombies +
            (round - 1) *
            zombiesPerRound;

        bool spawnBoss =
            round == bossRound &&
            HasBossPrefab();

        ZombiesThisRoundNetwork.Value =
            zombiesThisRound;

        AliveZombiesNetwork.Value =
            zombiesThisRound;

        roundInProgress =
            true;

        countdownInProgress =
            false;

        CountdownNetwork.Value =
            0;

        if (roundStartHUD != null)
        {
            roundStartHUD.Hide();
        }

        if (
            GameplayPopupsController.Instance !=
            null
        )
        {
            GameplayPopupsController.Instance
                .MostrarPanelRonda();
        }

        if (spawnRoutine != null)
        {
            StopCoroutine(
                spawnRoutine
            );
        }

        spawnRoutine =
            StartCoroutine(
                SpawnZombiesRoutine(
                    zombiesThisRound,
                    spawnBoss
                )
            );

        Debug.Log(
            "[RoundManager] Ronda " +
            round +
            " iniciada."
        );
    }

    // =========================================================
    // RESPAWN ESPECTADORES
    // =========================================================

    private void RespawnSpectatingPlayers()
    {
        if (!IsServer)
            return;

        foreach (
            NetworkClient client
            in NetworkManager.Singleton
                .ConnectedClientsList
        )
        {
            if (client.PlayerObject == null)
                continue;

            PlayerHealth playerHealth =
                client.PlayerObject
                    .GetComponent<PlayerHealth>();

            if (playerHealth == null)
                continue;

            if (!playerHealth.IsSpectating)
                continue;

            MultiplayerPlayerSpawnAssigner spawnAssigner =
                client.PlayerObject
                    .GetComponent<
                        MultiplayerPlayerSpawnAssigner
                    >();

            if (spawnAssigner != null)
            {
                spawnAssigner
                    .RespawnAtAssignedSpawn();
            }

            playerHealth.Respawn();

            PlayerMovement movement =
                client.PlayerObject
                    .GetComponent<PlayerMovement>();

            if (movement != null)
            {
                movement.ResetMovementState();
            }

            Debug.Log(
                "[RoundManager] " +
                client.PlayerObject.name +
                " vuelve a la partida."
            );
        }
    }

    // =========================================================
    // INTERVALOS DE SPAWN
    // =========================================================

    private float GetRandomSpawnInterval(
        int round
    )
    {
        if (maxRounds <= 1)
        {
            return Random.Range(
                round1MinSpawnInterval,
                round1MaxSpawnInterval
            );
        }

        float roundProgress =
            (round - 1f) /
            (maxRounds - 1f);

        float currentMinInterval =
            Mathf.Lerp(
                round1MinSpawnInterval,
                finalRoundMinSpawnInterval,
                roundProgress
            );

        float currentMaxInterval =
            Mathf.Lerp(
                round1MaxSpawnInterval,
                finalRoundMaxSpawnInterval,
                roundProgress
            );

        return Random.Range(
            currentMinInterval,
            currentMaxInterval
        );
    }

    // =========================================================
    // SPAWN ZOMBIES
    // =========================================================

    private IEnumerator SpawnZombiesRoutine(
        int amount,
        bool spawnBoss
    )
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

        if (
            spawnPoints == null ||
            spawnPoints.Length == 0
        )
        {
            Debug.LogError(
                "RoundManager: no hay Spawn Points."
            );

            yield break;
        }

        int round =
            CurrentRoundNetwork.Value;

        int shotsNeeded =
            round *
            shotsIncreasePerRound;

        int zombieHealthThisRound =
            shotsNeeded *
            damagePerShot;

        float runChance =
            maxRounds > 1
                ? Mathf.Clamp01(
                    (round - 1) /
                    (float)(maxRounds - 1)
                )
                : 0f;

        yield return new WaitForSeconds(
            initialSpawnDelay
        );

        int normalZombiesToSpawn =
            amount;

        // =====================================================
        // BOSS
        // =====================================================

        if (spawnBoss)
        {
            GameObject bossPrefab =
                GetRandomBossPrefab();

            if (bossPrefab != null)
            {
                Transform bossSpawnPoint =
                    spawnPoints[
                        Random.Range(
                            0,
                            spawnPoints.Length
                        )
                    ];

                int bossHealth =
                    zombieHealthThisRound *
                    bossHealthMultiplier;

                bool bossSpawned =
                    SpawnZombie(
                        bossPrefab,
                        bossSpawnPoint,
                        bossHealth,
                        true
                    );

                if (bossSpawned)
                {
                    normalZombiesToSpawn--;

                    if (
                        normalZombiesToSpawn >
                        0
                    )
                    {
                        float wait =
                            GetRandomSpawnInterval(
                                round
                            );

                        yield return
                            new WaitForSeconds(
                                wait
                            );
                    }
                }
            }
        }

        // =====================================================
        // ZOMBIES NORMALES
        // =====================================================

        for (
            int i = 0;
            i < normalZombiesToSpawn;
            i++
        )
        {
            Transform spawnPoint =
                spawnPoints[
                    i %
                    spawnPoints.Length
                ];

            bool willRun =
                Random.value <
                runChance;

            SpawnZombie(
                zombiePrefab,
                spawnPoint,
                zombieHealthThisRound,
                willRun
            );

            if (
                i <
                normalZombiesToSpawn - 1
            )
            {
                float wait =
                    GetRandomSpawnInterval(
                        round
                    );

                yield return
                    new WaitForSeconds(
                        wait
                    );
            }
        }
    }

    // =========================================================
    // CREAR ZOMBIE
    // =========================================================

    private bool SpawnZombie(
        GameObject prefab,
        Transform spawnPoint,
        int health,
        bool willRun
    )
    {
        if (
            prefab == null ||
            spawnPoint == null
        )
        {
            return false;
        }

        GameObject zombieInstance =
            Instantiate(
                prefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

        ZombieAppearance appearance =
            zombieInstance.GetComponent<
                ZombieAppearance
            >();

        if (appearance != null)
        {
            appearance.SelectRandomModel();
        }

        ZombieHealth zombieHealth =
            zombieInstance.GetComponent<
                ZombieHealth
            >();

        if (zombieHealth != null)
        {
            zombieHealth.InitializeHealth(
                health
            );
        }

        ZombieAI zombieAI =
            zombieInstance.GetComponent<
                ZombieAI
            >();

        NetworkObject netObj =
            zombieInstance.GetComponent<
                NetworkObject
            >();

        if (netObj == null)
        {
            Debug.LogError(
                "RoundManager: el prefab " +
                prefab.name +
                " no tiene NetworkObject."
            );

            Destroy(
                zombieInstance
            );

            return false;
        }

        netObj.Spawn();

        if (zombieAI != null)
        {
            zombieAI.SetRunning(
                willRun
            );
        }

        return true;
    }

    // =========================================================
    // BOSS
    // =========================================================

    private bool HasBossPrefab()
    {
        if (bossPrefabs == null)
            return false;

        foreach (
            GameObject bossPrefab
            in bossPrefabs
        )
        {
            if (bossPrefab != null)
                return true;
        }

        return false;
    }

    private GameObject GetRandomBossPrefab()
    {
        if (bossPrefabs == null)
            return null;

        int availableBosses = 0;

        foreach (
            GameObject bossPrefab
            in bossPrefabs
        )
        {
            if (bossPrefab != null)
            {
                availableBosses++;
            }
        }

        if (availableBosses == 0)
            return null;

        int selectedBoss =
            Random.Range(
                0,
                availableBosses
            );

        foreach (
            GameObject bossPrefab
            in bossPrefabs
        )
        {
            if (bossPrefab == null)
                continue;

            if (selectedBoss == 0)
                return bossPrefab;

            selectedBoss--;
        }

        return null;
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
            AliveZombiesNetwork.Value =
                0;
        }

        if (
            AliveZombiesNetwork.Value <= 0
        )
        {
            EndRound();
        }
    }

    // =========================================================
    // JUGADOR ELIMINADO
    // =========================================================

    public void PlayerDied()
    {
        if (!IsServer)
            return;

        if (GameLostNetwork.Value)
            return;

        bool anotherPlayerAlive =
            false;

        foreach (
            NetworkClient client
            in NetworkManager.Singleton
                .ConnectedClientsList
        )
        {
            if (client.PlayerObject == null)
                continue;

            PlayerHealth playerHealth =
                client.PlayerObject
                    .GetComponent<PlayerHealth>();

            if (playerHealth == null)
                continue;

            if (playerHealth.IsAlive)
            {
                anotherPlayerAlive =
                    true;

                break;
            }
        }

        if (!anotherPlayerAlive)
        {
            roundInProgress =
                false;

            GameLostNetwork.Value =
                true;

            Debug.Log(
                "[RoundManager] No quedan jugadores vivos. Game Over."
            );

            return;
        }

        Debug.Log(
            "[RoundManager] Jugador eliminado. " +
            "La partida continúa."
        );
    }

    private void MostrarDerrota()
    {
        if (
            GameplayPopupsController.Instance !=
            null
        )
        {
            GameplayPopupsController.Instance
                .MostrarPanelPerdedor();
        }
    }

    // =========================================================
    // FIN DE RONDA
    // =========================================================

    private void EndRound()
    {
        if (!IsServer)
            return;

        roundInProgress =
            false;

        StartCoroutine(
            EndRoundDelay()
        );
    }

    private IEnumerator EndRoundDelay()
    {
        yield return new WaitForSeconds(
            delayAntesDePanel
        );

        if (
            GameplayPopupsController.Instance !=
            null
        )
        {
            GameplayPopupsController.Instance
                .MostrarPanelGanador();
        }

        if (
            CurrentRoundNetwork.Value >=
            maxRounds
        )
        {
            Debug.Log(
                "Ronda " +
                CurrentRoundNetwork.Value +
                " completada."
            );

            if (roundStartHUD != null)
            {
                roundStartHUD.Hide();
            }

            yield break;
        }

        pendingNextRound =
            CurrentRoundNetwork.Value +
            1;
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

        pendingNextRound =
            0;

        StartCoroutine(
            CountdownToNextRound(
                nextRound
            )
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

        countdownInProgress =
            true;

        CurrentRoundNetwork.Value =
            nextRound;

        CountdownNetwork.Value =
            5;

        yield return new WaitForSeconds(1f);

        CountdownNetwork.Value =
            4;

        yield return new WaitForSeconds(1f);

        CountdownNetwork.Value =
            3;

        yield return new WaitForSeconds(1f);

        CountdownNetwork.Value =
            2;

        yield return new WaitForSeconds(1f);

        CountdownNetwork.Value =
            1;

        yield return new WaitForSeconds(1f);

        CountdownNetwork.Value =
            0;

        yield return new WaitForSeconds(0.2f);

        countdownInProgress =
            false;

        StartRound(
            nextRound
        );
    }
}