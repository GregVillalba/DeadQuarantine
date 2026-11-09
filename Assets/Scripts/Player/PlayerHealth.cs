using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    [HideInInspector] public bool invulnerablePorMinijuego = false;

    [Header("Vida")]
    [SerializeField] private int maxHealth = 100;

    [Header("Escudo (chaleco + casco)")]
    [Tooltip("Escudo máximo sumando todas las piezas. Lo que da cada pieza se configura en su oferta del mercader (Armadura).")]
    [SerializeField] private int maxArmor = 100;

    [Header("Vidas")]
    [SerializeField] private int startingLives = 1;

    [Header("Estado abatido")]
    [SerializeField] private float downedDurationSingleplayer = 5f;
    [SerializeField] private float downedDurationMultiplayer = 20f;

    [Header("Regeneración")]
    [SerializeField] private float regenerationDelay = 5f;
    [SerializeField] private int healthRecoveredPerTick = 25;
    [SerializeField] private float regenerationInterval = 1f;

    [Header("Escenas")]
    [SerializeField] private string singleplayerSceneName = "MainSceneSinglePlayer";

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

    public NetworkVariable<int> VestShield =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> VestShieldMax =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> HelmetShield =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> HelmetShieldMax =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int MaxArmor => maxArmor;

    public event Action OnBlindajeChanged;
    public event Action OnEscudoDanado;

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

    public void SetMaxHealth(int newMaxHealth, bool refill = true)
    {
        maxHealth = Mathf.Max(1, newMaxHealth);

        if (!IsSpawned)
            return;

        if (IsServer)
        {
            if (refill)
                CurrentHealth.Value = maxHealth;
            else if (CurrentHealth.Value > maxHealth)
                CurrentHealth.Value = maxHealth;
        }

        OnHealthChanged?.Invoke(CurrentHealth.Value, CurrentHealth.Value);
    }

    public int StartingLives => startingLives;
    public bool IsAlive => State.Value == PlayerState.Alive;
    public bool IsDowned => State.Value == PlayerState.Downed;
    public bool IsSpectating => State.Value == PlayerState.Spectating;
    public bool IsDead => State.Value == PlayerState.Dead;

    private bool EsSingleplayer =>
        SceneManager.GetActiveScene().name == "MainSceneSinglePlayer";

    private float DownedDuration =>
        EsSingleplayer ? downedDurationSingleplayer : downedDurationMultiplayer;

    public bool PuedeCurarse =>
        IsAlive &&
        CurrentHealth.Value > 0 &&
        CurrentHealth.Value < maxHealth;

    public event Action<int, int> OnHealthChanged;
    public event Action<int, int> OnLivesChanged;
    public event Action<PlayerState, PlayerState> OnStateChanged;

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

    private PauseController pausaController;

    private bool EnMinijuego
    {
        get
        {
            if (invulnerablePorMinijuego)
                return true;

            if (pausaController == null)
                pausaController = GetComponent<PauseController>();

            return pausaController != null && pausaController.EnMinijuegoNet.Value;
        }
    }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;
            Armor.Value = 0;
            VestShield.Value = 0;
            VestShieldMax.Value = 0;
            HelmetShield.Value = 0;
            HelmetShieldMax.Value = 0;

            InicializarInventarioServidor();
            InicializarPerksServidor();

            Lives.Value = startingLives;
            State.Value = PlayerState.Alive;
            DownedTimeRemaining.Value = 0f;

            lastDamageTime = Time.time;
            nextRegenerationTime = Time.time + RegenDelay;
        }

        CurrentHealth.OnValueChanged += HealthChanged;
        Lives.OnValueChanged += LivesChanged;
        State.OnValueChanged += StateChanged;

        VestShield.OnValueChanged += EscudoPiezaChanged;
        VestShieldMax.OnValueChanged += BlindajeChanged;
        HelmetShield.OnValueChanged += EscudoPiezaChanged;
        HelmetShieldMax.OnValueChanged += BlindajeChanged;

        SuscribirInventario();
        SuscribirPerks();
        SuscribirQuemadura();

        OnHealthChanged?.Invoke(CurrentHealth.Value, CurrentHealth.Value);
        OnLivesChanged?.Invoke(Lives.Value, Lives.Value);
        OnStateChanged?.Invoke(State.Value, State.Value);
    }

    public override void OnNetworkDespawn()
    {
        CurrentHealth.OnValueChanged -= HealthChanged;
        Lives.OnValueChanged -= LivesChanged;
        State.OnValueChanged -= StateChanged;

        VestShield.OnValueChanged -= EscudoPiezaChanged;
        VestShieldMax.OnValueChanged -= BlindajeChanged;
        HelmetShield.OnValueChanged -= EscudoPiezaChanged;
        HelmetShieldMax.OnValueChanged -= BlindajeChanged;

        DesuscribirInventario();
        DesuscribirPerks();
        DesuscribirQuemadura();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        LeerInputInventario();

        if (!IsServer)
            return;

        if (State.Value == PlayerState.Alive)
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

        if (Time.time < lastDamageTime + RegenDelay)
            return;

        if (Time.time < nextRegenerationTime)
            return;

        CurrentHealth.Value = Mathf.Min(
            CurrentHealth.Value + RegenAmount,
            maxHealth
        );

        nextRegenerationTime = Time.time + RegenInterval;
    }

    // =========================================================
    // CHALECO / CASCO
    // =========================================================

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

    public bool NecesitaReparacion(PiezaBlindaje pieza)
    {
        return TieneBlindaje(pieza) && EscudoDe(pieza) < EscudoMaximoDe(pieza);
    }

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

    public void TakeDamage(int amount, bool ignorarEscudo = false)
    {
        if (DeadQuarantine.PvPvE.Networking.PvPvETeamDeployment.BlocksDamage(this)) return;
        if (!IsServer)
            return;

        // Inmunidad completa durante el minijuego arcade
        if (EnMinijuego)
            return;

        if (State.Value != PlayerState.Alive)
            return;

        if (CurrentHealth.Value <= 0)
            return;

        if (!ignorarEscudo)
            amount = AbsorberConEscudo(amount);

        CurrentHealth.Value -= amount;
        CurrentHealth.Value = Mathf.Clamp(CurrentHealth.Value, 0, maxHealth);

        lastDamageTime = Time.time;
        nextRegenerationTime = Time.time + RegenDelay;

        if (CurrentHealth.Value <= 0)
        {
            if (Lives.Value > 0 && !DeadQuarantine.PvPvE.Networking.PvPvEPreparationFlow.OwnsStagingPlayer(NetworkObject))
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

        CurrentHealth.Value = Mathf.Min(
            CurrentHealth.Value + amount,
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

        if (State.Value != PlayerState.Alive)
            return;

        State.Value = PlayerState.Downed;
        DownedTimeRemaining.Value = DownedDuration;

        Debug.Log("[PlayerHealth] " + gameObject.name + " está ABATIDO.");

        PlayerScore playerScore = transform.root.GetComponentInChildren<PlayerScore>();
        if (playerScore != null)
            playerScore.SumarCaida();

        if (downedCoroutine != null)
            StopCoroutine(downedCoroutine);

        downedCoroutine = StartCoroutine(DownedRoutine());

        if (!EsSingleplayer && !HayOtroJugadorVivo())
        {
            EliminatePlayer();
        }
    }

    private IEnumerator DownedRoutine()
    {
        float remaining = DownedDuration;

        while (remaining > 0f)
        {
            if (State.Value != PlayerState.Downed)
                yield break;

            remaining -= Time.deltaTime;
            DownedTimeRemaining.Value = Mathf.Max(remaining, 0f);
            yield return null;
        }

        DownedTimeRemaining.Value = 0f;
        downedCoroutine = null;

        if (EsSingleplayer)
        {
            RecoverFromDowned();
        }
        else
        {
            EliminatePlayer();
        }
    }

    // =========================================================
    // LEVANTARSE
    // =========================================================

    private void RecoverFromDowned(bool consumirVida = true)
    {
        if (!IsServer)
            return;

        if (State.Value != PlayerState.Downed)
            return;

        if (consumirVida)
        {
            Lives.Value = Mathf.Max(Lives.Value - 1, 0);
        }

        CurrentHealth.Value = maxHealth;
        lastDamageTime = Time.time;
        nextRegenerationTime = Time.time + RegenDelay;
        State.Value = PlayerState.Alive;

        PlayerScore playerScore = transform.root.GetComponentInChildren<PlayerScore>();
        if (playerScore != null)
            playerScore.SumarReaparicion();

        Debug.Log("[PlayerHealth] " + gameObject.name + " se levantó. Vidas restantes: " + Lives.Value);
    }

    // =========================================================
    // REVIVIDO POR UN COMPAÑERO
    // =========================================================

    public void ReviveFromTeammate()
    {
        if (!IsServer)
            return;

        if (State.Value != PlayerState.Downed)
            return;

        if (downedCoroutine != null)
        {
            StopCoroutine(downedCoroutine);
            downedCoroutine = null;
        }

        DownedTimeRemaining.Value = 0f;
        RecoverFromDowned(false);
    }

    private bool HayOtroJugadorVivo()
    {
        if (NetworkManager.Singleton == null)
            return false;

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            if (client.PlayerObject.NetworkObjectId == NetworkObject.NetworkObjectId)
                continue;

            PlayerHealth otro = client.PlayerObject.GetComponent<PlayerHealth>();
            if (otro != null && otro.IsAlive)
                return true;
        }

        return false;
    }

    // =========================================================
    // ELIMINAR
    // =========================================================

    public void EliminatePlayer()
    {
        if (!IsServer)
            return;

        if (State.Value == PlayerState.Dead || State.Value == PlayerState.Spectating)
            return;

        CurrentHealth.Value = 0;
        DownedTimeRemaining.Value = 0f;

        bool anotherPlayerIsAlive = false;

        if (NetworkManager.Singleton != null)
        {
            foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null)
                    continue;

                if (client.PlayerObject.NetworkObjectId == NetworkObject.NetworkObjectId)
                    continue;

                PlayerHealth otherPlayer = client.PlayerObject.GetComponent<PlayerHealth>();
                if (otherPlayer != null && otherPlayer.IsAlive)
                {
                    anotherPlayerIsAlive = true;
                    break;
                }
            }
        }

        if (!anotherPlayerIsAlive)
        {
            State.Value = PlayerState.Dead;
            Debug.Log("[PlayerHealth] " + gameObject.name + " → DEAD");
        }
        else
        {
            State.Value = PlayerState.Spectating;
            Debug.Log("[PlayerHealth] " + gameObject.name + " → SPECTATING");
        }

        DeadQuarantine.PvPvE.Networking.PvPvECombat.Active?.RecordDeathServer(this);

        PlayerRecursos playerRecursos = transform.root.GetComponentInChildren<PlayerRecursos>();
        if (playerRecursos != null)
            playerRecursos.PerderTodosLosRecursos();

        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.PlayerDied();
        }
    }

    public void SetSpectating()
    {
        if (!IsServer)
            return;

        CurrentHealth.Value = 0;
        DownedTimeRemaining.Value = 0f;
        State.Value = PlayerState.Spectating;
    }

    public void RecoverLifeAtNewRound()
    {
        if (!IsServer)
            return;

        if (Lives.Value > 0)
            return;

        Lives.Value = 1;
    }

    public void Respawn()
    {
        if (!IsServer)
            return;

        CurrentHealth.Value = maxHealth;
        State.Value = PlayerState.Alive;
        lastDamageTime = Time.time;
        nextRegenerationTime = Time.time + RegenDelay;
        DownedTimeRemaining.Value = 0f;

        PlayerScore playerScore = transform.root.GetComponentInChildren<PlayerScore>();
        if (playerScore != null)
            playerScore.SumarReaparicion();

        Debug.Log("[PlayerHealth] " + gameObject.name + " respawneado.");
    }

    // =========================================================
    // EVENTOS
    // =========================================================

    private void HealthChanged(int previousHealth, int newHealth)
    {
        OnHealthChanged?.Invoke(previousHealth, newHealth);
    }

    private void BlindajeChanged(int previousValue, int newValue)
    {
        OnBlindajeChanged?.Invoke();
    }

    private void EscudoPiezaChanged(int previousValue, int newValue)
    {
        if (newValue < previousValue)
            OnEscudoDanado?.Invoke();

        OnBlindajeChanged?.Invoke();
    }

    private void LivesChanged(int previousLives, int newLives)
    {
        OnLivesChanged?.Invoke(previousLives, newLives);
    }

    private void StateChanged(PlayerState previousState, PlayerState newState)
    {
        OnStateChanged?.Invoke(previousState, newState);
    }
}