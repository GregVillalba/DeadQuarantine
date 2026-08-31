using UnityEngine;
using Unity.Netcode;
using System;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Vida")]
    [SerializeField] private int maxHealth = 100;

    [Header("Regeneración")]
    [SerializeField] private float regenerationDelay = 5f;
    [SerializeField] private int healthRecoveredPerTick = 25;
    [SerializeField] private float regenerationInterval = 1f;

    public NetworkVariable<int> CurrentHealth =
        new NetworkVariable<int>(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public int MaxHealth => maxHealth;

    public event Action<int, int> OnHealthChanged;

    private float lastDamageTime;
    private float nextRegenerationTime;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;

            lastDamageTime = Time.time;
            nextRegenerationTime =
                Time.time + regenerationDelay;
        }

        CurrentHealth.OnValueChanged +=
            HealthChanged;

        OnHealthChanged?.Invoke(
            CurrentHealth.Value,
            CurrentHealth.Value
        );
    }

    public override void OnNetworkDespawn()
    {
        CurrentHealth.OnValueChanged -=
            HealthChanged;
    }

    private void Update()
    {
        if (!IsServer)
            return;

        RegenerateHealth();
    }

    private void RegenerateHealth()
    {
        if (CurrentHealth.Value <= 0)
            return;

        if (CurrentHealth.Value >= maxHealth)
            return;

        if (Time.time <
            lastDamageTime + regenerationDelay)
        {
            return;
        }

        if (Time.time < nextRegenerationTime)
            return;

        CurrentHealth.Value =
            Mathf.Min(
                CurrentHealth.Value +
                healthRecoveredPerTick,
                maxHealth
            );

        nextRegenerationTime =
            Time.time + regenerationInterval;
    }

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
            newHealth + "/" + maxHealth
        );

        if (newHealth <= 0)
        {
            Muerte();
        }
    }

    public void TakeDamage(int amount)
    {
        if (!IsServer)
            return;

        if (CurrentHealth.Value <= 0)
            return;

        CurrentHealth.Value -= amount;

        CurrentHealth.Value = Mathf.Clamp(
            CurrentHealth.Value,
            0,
            maxHealth
        );

        // Cada golpe reinicia los cinco segundos
        // antes de comenzar la regeneración.
        lastDamageTime = Time.time;

        nextRegenerationTime =
            Time.time + regenerationDelay;
    }

    private void Muerte()
    {
        if (!IsServer)
            return;

        RoundManager.Instance.PlayerDied();
    }
}