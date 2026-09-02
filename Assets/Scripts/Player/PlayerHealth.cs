using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System;
using System.Collections;

public class PlayerHealth : NetworkBehaviour
{
    public enum PlayerState
    {
        Alive,
        Downed,
        Spectating,
        Dead
    }

    [Header("Vida")]
    [SerializeField] private int maxHealth = 100;

    [Header("Vidas")]
    [SerializeField] private int startingLives = 1;

    [Header("Estado abatido")]
    [SerializeField] private float downedDuration = 5f;

    [Header("Regeneración")]
    [SerializeField] private float regenerationDelay = 5f;
    [SerializeField] private int healthRecoveredPerTick = 25;
    [SerializeField] private float regenerationInterval = 1f;

    [Header("Escenas")]
    [SerializeField] private string singleplayerSceneName =
        "MainSceneSinglePlayer";

    public NetworkVariable<int> CurrentHealth =
        new NetworkVariable<int>(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<int> Lives =
        new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<PlayerState> State =
        new NetworkVariable<PlayerState>(
            PlayerState.Alive,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<float> DownedTimeRemaining =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public int MaxHealth => maxHealth;

    public int StartingLives =>
        startingLives;

    public bool IsAlive =>
        State.Value ==
        PlayerState.Alive;

    public bool IsDowned =>
        State.Value ==
        PlayerState.Downed;

    public bool IsSpectating =>
        State.Value ==
        PlayerState.Spectating;

    public bool IsDead =>
        State.Value ==
        PlayerState.Dead;

    public event Action<int, int>
        OnHealthChanged;

    public event Action<int, int>
        OnLivesChanged;

    public event Action<PlayerState, PlayerState>
        OnStateChanged;

    private float lastDamageTime;
    private float nextRegenerationTime;

    private Coroutine downedCoroutine;

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentHealth.Value =
                maxHealth;

            Lives.Value =
                startingLives;

            State.Value =
                PlayerState.Alive;

            DownedTimeRemaining.Value =
                0f;

            lastDamageTime =
                Time.time;

            nextRegenerationTime =
                Time.time +
                regenerationDelay;
        }

        CurrentHealth.OnValueChanged +=
            HealthChanged;

        Lives.OnValueChanged +=
            LivesChanged;

        State.OnValueChanged +=
            StateChanged;

        OnHealthChanged?.Invoke(
            CurrentHealth.Value,
            CurrentHealth.Value
        );

        OnLivesChanged?.Invoke(
            Lives.Value,
            Lives.Value
        );

        OnStateChanged?.Invoke(
            State.Value,
            State.Value
        );
    }

    public override void OnNetworkDespawn()
    {
        CurrentHealth.OnValueChanged -=
            HealthChanged;

        Lives.OnValueChanged -=
            LivesChanged;

        State.OnValueChanged -=
            StateChanged;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (!IsServer)
            return;

        if (
            State.Value ==
            PlayerState.Alive
        )
        {
            RegenerateHealth();
        }
    }

    // =========================================================
    // REGENERACIÓN
    // =========================================================

    private void RegenerateHealth()
    {
        if (CurrentHealth.Value <= 0)
            return;

        if (CurrentHealth.Value >= maxHealth)
            return;

        if (
            Time.time <
            lastDamageTime +
            regenerationDelay
        )
        {
            return;
        }

        if (
            Time.time <
            nextRegenerationTime
        )
        {
            return;
        }

        CurrentHealth.Value =
            Mathf.Min(
                CurrentHealth.Value +
                healthRecoveredPerTick,
                maxHealth
            );

        nextRegenerationTime =
            Time.time +
            regenerationInterval;
    }

    // =========================================================
    // DAÑO
    // =========================================================

    public void TakeDamage(int amount)
    {
        if (!IsServer)
            return;

        if (
            State.Value !=
            PlayerState.Alive
        )
        {
            return;
        }

        if (CurrentHealth.Value <= 0)
            return;

        CurrentHealth.Value -=
            amount;

        CurrentHealth.Value =
            Mathf.Clamp(
                CurrentHealth.Value,
                0,
                maxHealth
            );

        lastDamageTime =
            Time.time;

        nextRegenerationTime =
            Time.time +
            regenerationDelay;

        if (CurrentHealth.Value <= 0)
        {
            if (Lives.Value > 0)
            {
                EnterDownedState();
            }
            else
            {
                EliminatePlayer();
            }
        }
    }

    // =========================================================
    // ABATIDO
    // =========================================================

    private void EnterDownedState()
    {
        if (!IsServer)
            return;

        if (
            State.Value !=
            PlayerState.Alive
        )
        {
            return;
        }

        State.Value =
            PlayerState.Downed;

        DownedTimeRemaining.Value =
            downedDuration;

        Debug.Log(
            "[PlayerHealth] " +
            gameObject.name +
            " está ABATIDO."
        );

        if (downedCoroutine != null)
        {
            StopCoroutine(
                downedCoroutine
            );
        }

        downedCoroutine =
            StartCoroutine(
                DownedRoutine()
            );
    }

    private IEnumerator DownedRoutine()
    {
        float remaining =
            downedDuration;

        while (remaining > 0f)
        {
            if (
                State.Value !=
                PlayerState.Downed
            )
            {
                yield break;
            }

            remaining -=
                Time.deltaTime;

            DownedTimeRemaining.Value =
                Mathf.Max(
                    remaining,
                    0f
                );

            yield return null;
        }

        DownedTimeRemaining.Value =
            0f;

        RecoverFromDowned();
    }

    // =========================================================
    // LEVANTARSE
    // =========================================================

    private void RecoverFromDowned()
    {
        if (!IsServer)
            return;

        if (
            State.Value !=
            PlayerState.Downed
        )
        {
            return;
        }

        Lives.Value =
            Mathf.Max(
                Lives.Value - 1,
                0
            );

        CurrentHealth.Value =
            maxHealth;

        lastDamageTime =
            Time.time;

        nextRegenerationTime =
            Time.time +
            regenerationDelay;

        State.Value =
            PlayerState.Alive;

        Debug.Log(
            "[PlayerHealth] " +
            gameObject.name +
            " se levantó. " +
            "Vidas restantes: " +
            Lives.Value
        );
    }

    // =========================================================
    // ELIMINAR
    // =========================================================

    public void EliminatePlayer()
    {
        if (!IsServer)
            return;

        if (
            State.Value ==
                PlayerState.Dead ||
            State.Value ==
                PlayerState.Spectating
        )
        {
            return;
        }

        CurrentHealth.Value =
            0;

        DownedTimeRemaining.Value =
            0f;

        int connectedPlayers = 0;

        if (NetworkManager.Singleton != null)
        {
            connectedPlayers =
                NetworkManager.Singleton
                    .ConnectedClientsList.Count;
        }

        bool anotherPlayerIsAlive =
            false;

        if (NetworkManager.Singleton != null)
        {
            foreach (
                NetworkClient client
                in NetworkManager.Singleton
                    .ConnectedClientsList
            )
            {
                if (client.PlayerObject == null)
                    continue;

                if (
                    client.PlayerObject.NetworkObjectId ==
                    NetworkObject.NetworkObjectId
                )
                {
                    continue;
                }

                PlayerHealth otherPlayer =
                    client.PlayerObject
                        .GetComponent<PlayerHealth>();

                if (otherPlayer == null)
                    continue;

                if (otherPlayer.IsAlive)
                {
                    anotherPlayerIsAlive =
                        true;

                    break;
                }
            }
        }

        Debug.Log(
            "[PlayerHealth] " +
            "EliminatePlayer | " +
            "Jugadores conectados: " +
            connectedPlayers +
            " | Otro jugador vivo: " +
            anotherPlayerIsAlive
        );

        // =====================================================
        // SINGLEPLAYER
        // =====================================================

        if (!anotherPlayerIsAlive)
        {
            State.Value =
                PlayerState.Dead;

            Debug.Log(
                "[PlayerHealth] " +
                gameObject.name +
                " → DEAD"
            );
        }

        // =====================================================
        // MULTIPLAYER
        // =====================================================

        else
        {
            State.Value =
                PlayerState.Spectating;

            Debug.Log(
                "[PlayerHealth] " +
                gameObject.name +
                " → SPECTATING"
            );
        }

        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.PlayerDied();
        }
    }

    // =========================================================
    // FORZAR ESPECTADOR
    // =========================================================

    public void SetSpectating()
    {
        if (!IsServer)
            return;

        CurrentHealth.Value =
            0;

        DownedTimeRemaining.Value =
            0f;

        State.Value =
            PlayerState.Spectating;
    }

    // =========================================================
    // RECUPERAR VIDA EN NUEVA RONDA
    // =========================================================

    public void RecoverLifeAtNewRound()
    {
        if (!IsServer)
            return;

        // Solamente recupera una vida si tiene 0.
        if (Lives.Value > 0)
            return;

        Lives.Value = 1;

        Debug.Log(
            "[PlayerHealth] " +
            gameObject.name +
            " recuperó 1 vida por comenzar " +
            "una nueva ronda."
        );
    }

    // =========================================================
    // RESPAWN
    // =========================================================

    public void Respawn()
    {
        if (!IsServer)
            return;

        CurrentHealth.Value =
            maxHealth;

        State.Value =
            PlayerState.Alive;

        lastDamageTime =
            Time.time;

        nextRegenerationTime =
            Time.time +
            regenerationDelay;

        DownedTimeRemaining.Value =
            0f;

        Debug.Log(
            "[PlayerHealth] " +
            gameObject.name +
            " respawneado."
        );
    }

    // =========================================================
    // EVENTOS
    // =========================================================

    private void HealthChanged(
        int previousHealth,
        int newHealth
    )
    {
        OnHealthChanged?.Invoke(
            previousHealth,
            newHealth
        );

        Debug.Log(
            "[PlayerHealth] Vida: " +
            newHealth +
            "/" +
            maxHealth
        );
    }

    private void LivesChanged(
        int previousLives,
        int newLives
    )
    {
        OnLivesChanged?.Invoke(
            previousLives,
            newLives
        );

        Debug.Log(
            "[PlayerHealth] Vidas: " +
            newLives
        );
    }

    private void StateChanged(
        PlayerState previousState,
        PlayerState newState
    )
    {
        OnStateChanged?.Invoke(
            previousState,
            newState
        );

        Debug.Log(
            "[PlayerHealth] Estado: " +
            previousState +
            " → " +
            newState
        );
    }
}