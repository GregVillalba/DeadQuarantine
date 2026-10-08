using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System;
using System.Collections;

// El inventario de curas está en PlayerHealthInventario.cs.
public partial class PlayerHealth : NetworkBehaviour
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

    [Header("Escudo (chaleco + casco)")]
    [Tooltip("Escudo máximo sumando todas las piezas. Lo que da cada pieza se configura en su oferta del mercader (Armadura).")]
    [SerializeField] private int maxArmor = 100;

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

    // Escudo total (chaleco + casco). Absorbe daño antes que la vida. Lo lee el HUD.
    public NetworkVariable<int> Armor =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Escudo de cada pieza. Max = 0 -> la pieza no se compró. Escudo = 0 con Max > 0 -> pieza "rota".
    public NetworkVariable<int> VestShield =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> VestShieldMax =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> HelmetShield =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> HelmetShieldMax =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int MaxArmor => maxArmor;

    /// <summary>Cambió el escudo de alguna pieza (compra, reparación o daño). Lo usa la tienda.</summary>
    public event Action OnBlindajeChanged;

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

    // Solo se cura estando de pie y sin la vida llena.
    public bool PuedeCurarse =>
        IsAlive &&
        CurrentHealth.Value > 0 &&
        CurrentHealth.Value < maxHealth;

    public event Action<int, int>
        OnHealthChanged;

    public event Action<int, int>
        OnLivesChanged;

    public event Action<PlayerState, PlayerState>
        OnStateChanged;

    private float lastDamageTime;
    private float nextRegenerationTime;

    private Coroutine downedCoroutine;
    private DifficultySettings ActiveDifficulty =>
    RoundManager.Instance != null
        ? RoundManager.Instance.ActiveSettings
        : null;

    private bool UseDifficultyRegen =>
        ActiveDifficulty != null && ActiveDifficulty.overrideHealthRegen;

    private float RegenDelay =>
        UseDifficultyRegen ? ActiveDifficulty.healthRegenDelay : regenerationDelay;

    private int RegenAmount =>
        UseDifficultyRegen ? ActiveDifficulty.healthRegenAmount : healthRecoveredPerTick;

    private float RegenInterval =>
        UseDifficultyRegen ? ActiveDifficulty.healthRegenInterval : regenerationInterval;

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentHealth.Value =
                maxHealth;

            Armor.Value = 0;
            VestShield.Value = 0;
            VestShieldMax.Value = 0;
            HelmetShield.Value = 0;
            HelmetShieldMax.Value = 0;

            InicializarInventarioServidor();

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
               // regenerationDelay;
                RegenDelay;
        }

        CurrentHealth.OnValueChanged +=
            HealthChanged;

        Lives.OnValueChanged +=
            LivesChanged;

        State.OnValueChanged +=
            StateChanged;

        VestShield.OnValueChanged += BlindajeChanged;
        VestShieldMax.OnValueChanged += BlindajeChanged;
        HelmetShield.OnValueChanged += BlindajeChanged;
        HelmetShieldMax.OnValueChanged += BlindajeChanged;

        SuscribirInventario();

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

        VestShield.OnValueChanged -= BlindajeChanged;
        VestShieldMax.OnValueChanged -= BlindajeChanged;
        HelmetShield.OnValueChanged -= BlindajeChanged;
        HelmetShieldMax.OnValueChanged -= BlindajeChanged;

        DesuscribirInventario();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        LeerInputInventario();

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
          //  regenerationDelay
            RegenDelay
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
               // healthRecoveredPerTick,
               RegenAmount,
                maxHealth
            );

        nextRegenerationTime =
            Time.time +
           // regenerationInterval;
            RegenInterval;
    }

    // =========================================================
    // CHALECO / CASCO
    // =========================================================

    // Una pieza comprada no desaparece: si pierde todo su escudo queda "rota" (0 puntos) hasta repararla.

    public bool TieneBlindaje(PiezaBlindaje pieza)
    {
        return EscudoMaximoDe(pieza) > 0;
    }

    public int EscudoDe(PiezaBlindaje pieza)
    {
        switch (pieza)
        {
            case PiezaBlindaje.Chaleco: return VestShield.Value;
            case PiezaBlindaje.Casco: return HelmetShield.Value;
            default: return 0;
        }
    }

    public int EscudoMaximoDe(PiezaBlindaje pieza)
    {
        switch (pieza)
        {
            case PiezaBlindaje.Chaleco: return VestShieldMax.Value;
            case PiezaBlindaje.Casco: return HelmetShieldMax.Value;
            default: return 0;
        }
    }

    /// <summary>La pieza está comprada pero le falta escudo (dañada o rota).</summary>
    public bool NecesitaReparacion(PiezaBlindaje pieza)
    {
        return TieneBlindaje(pieza) && EscudoDe(pieza) < EscudoMaximoDe(pieza);
    }

    /// <summary>Solo servidor. Equipa la pieza con el escudo lleno. El total no pasa de Max Armor.</summary>
    public bool EquiparBlindaje(PiezaBlindaje pieza, int escudo)
    {
        if (!IsServer || pieza == PiezaBlindaje.Ninguna || escudo <= 0 || TieneBlindaje(pieza))
            return false;

        PiezaBlindaje otra = pieza == PiezaBlindaje.Chaleco ? PiezaBlindaje.Casco : PiezaBlindaje.Chaleco;
        escudo = Mathf.Min(escudo, maxArmor - EscudoMaximoDe(otra));

        if (escudo <= 0)
            return false;

        SetEscudo(pieza, escudo, escudo);
        return true;
    }

    /// <summary>Solo servidor. Devuelve la pieza a su escudo máximo.</summary>
    public bool RepararBlindaje(PiezaBlindaje pieza)
    {
        if (!IsServer || !NecesitaReparacion(pieza))
            return false;

        SetEscudo(pieza, EscudoMaximoDe(pieza), EscudoMaximoDe(pieza));
        return true;
    }

    private void SetEscudo(PiezaBlindaje pieza, int escudo, int maximo)
    {
        if (pieza == PiezaBlindaje.Chaleco)
        {
            VestShieldMax.Value = maximo;
            VestShield.Value = Mathf.Clamp(escudo, 0, maximo);
        }
        else if (pieza == PiezaBlindaje.Casco)
        {
            HelmetShieldMax.Value = maximo;
            HelmetShield.Value = Mathf.Clamp(escudo, 0, maximo);
        }

        Armor.Value = VestShield.Value + HelmetShield.Value;
    }

    // El escudo absorbe el daño antes que la vida (1 punto de escudo = 1 de daño).
    // Primero se gasta el casco y después el chaleco. Devuelve el daño que sobra para la vida.
    private int AbsorberConEscudo(int amount)
    {
        amount = AbsorberConPieza(PiezaBlindaje.Casco, amount);
        amount = AbsorberConPieza(PiezaBlindaje.Chaleco, amount);
        return amount;
    }

    private int AbsorberConPieza(PiezaBlindaje pieza, int amount)
    {
        int escudo = EscudoDe(pieza);

        if (amount <= 0 || escudo <= 0)
            return amount;

        int absorbido = Mathf.Min(escudo, amount);
        SetEscudo(pieza, escudo - absorbido, EscudoMaximoDe(pieza));
        return amount - absorbido;
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

        amount = AbsorberConEscudo(amount);

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
           // regenerationDelay;
           RegenDelay;

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
    // CURACIÓN
    // =========================================================

    public void Heal(int amount)
    {
        if (!IsServer)
            return;

        if (amount <= 0 || !PuedeCurarse)
            return;

        CurrentHealth.Value =
            Mathf.Min(
                CurrentHealth.Value +
                amount,
                maxHealth
            );
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

        PlayerScore playerScore =
        transform.root.GetComponentInChildren<PlayerScore>();

    if (playerScore != null)
        playerScore.SumarCaida();

    if (downedCoroutine != null)
        StopCoroutine(downedCoroutine);

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
         //   regenerationDelay;
         RegenDelay;

        State.Value =
            PlayerState.Alive;

        PlayerScore playerScore =
        transform.root.GetComponentInChildren<PlayerScore>();

    if (playerScore != null)
        playerScore.SumarReaparicion();

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
          //  regenerationDelay;
          RegenDelay;

        DownedTimeRemaining.Value =
            0f;

        PlayerScore playerScore =
        transform.root.GetComponentInChildren<PlayerScore>();

    if (playerScore != null)
        playerScore.SumarReaparicion();

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

    private void BlindajeChanged(int previousValue, int newValue)
    {
        OnBlindajeChanged?.Invoke();
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