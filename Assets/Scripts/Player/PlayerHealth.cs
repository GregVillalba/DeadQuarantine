using UnityEngine;
using Unity.Netcode;
using System;

public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 100;

    public NetworkVariable<int> CurrentHealth =
        new NetworkVariable<int>(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public int MaxHealth => maxHealth;

    // Evento para que el HUD pueda actualizarse
    public event Action<int, int> OnHealthChanged;

    public override void OnNetworkSpawn()
    {
        // El servidor inicializa la vida
        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;
        }

        // Todos escuchan cambios de vida
        CurrentHealth.OnValueChanged += HealthChanged;

        // Actualizar inmediatamente el HUD
        OnHealthChanged?.Invoke(
            CurrentHealth.Value,
            CurrentHealth.Value
        );
    }

    public override void OnNetworkDespawn()
    {
        CurrentHealth.OnValueChanged -= HealthChanged;
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
        // El daño solamente lo procesa el servidor
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
    }

    private void Muerte()
    {
        // Solamente el servidor decide que el jugador murió
        if (!IsServer)
            return;

        RoundManager.Instance.PlayerDied();
    }
}