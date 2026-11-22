using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public enum SurvivalUpgradeKind : byte { Health, Damage, StaminaRegen, Movement }

[Serializable]
public struct SurvivalUpgradeState : INetworkSerializable, IEquatable<SurvivalUpgradeState>
{
    public int health, damage, staminaRegen, movement, points, lastRewardRound;
    public int Spent => health + damage + staminaRegen + movement;
    public int Level(int kind) => kind switch { 0 => health, 1 => damage, 2 => staminaRegen, 3 => movement, _ => 0 };
    public static int Limit(int kind) => kind switch { 0 => 20, 1 => 40, 2 => 10, 3 => 40, _ => 0 };
    public static float Step(int kind) => kind switch { 0 => 10, 1 => 5, 2 => 10, 3 => 2.5f, _ => 0 };
    public float Percent(int kind) => Level(kind) * Step(kind);
    public float Multiplier(int kind) => 1 + Percent(kind) / 100f;
    public bool Reward(int round)
    {
        if (round <= lastRewardRound || round < 1) return false;
        lastRewardRound = round;
        if (Spent + points >= 110) return false;
        points++;
        return true;
    }
    public bool Spend(int kind)
    {
        if (points <= 0 || kind < 0 || kind > 3 || Level(kind) >= Limit(kind)) return false;
        switch (kind) { case 0: health++; break; case 1: damage++; break; case 2: staminaRegen++; break; case 3: movement++; break; }
        points--;
        return true;
    }
    public SurvivalUpgradeState Clamped()
    {
        var value = this;
        value.health = Mathf.Clamp(health, 0, 20); value.damage = Mathf.Clamp(damage, 0, 40);
        value.staminaRegen = Mathf.Clamp(staminaRegen, 0, 10); value.movement = Mathf.Clamp(movement, 0, 40);
        value.points = Mathf.Clamp(points, 0, 110 - value.Spent); value.lastRewardRound = Mathf.Max(0, lastRewardRound);
        return value;
    }
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref health); s.SerializeValue(ref damage); s.SerializeValue(ref staminaRegen);
        s.SerializeValue(ref movement); s.SerializeValue(ref points); s.SerializeValue(ref lastRewardRound);
    }
    public bool Equals(SurvivalUpgradeState v) => health == v.health && damage == v.damage && staminaRegen == v.staminaRegen && movement == v.movement && points == v.points && lastRewardRound == v.lastRewardRound;
}

/// <summary>Estado individual replicado; el Host conserva y valida el progreso de la partida.</summary>
[RequireComponent(typeof(PlayerHealth))]
public sealed class SurvivalUpgrades : NetworkBehaviour
{
    public NetworkVariable<SurvivalUpgradeState> Progress = new(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public event Action Changed;
    public event Action<bool> SelectionReplied;
    PlayerHealth health;
    RoundManager rounds;
    bool wasActive;
    public RoundManager Rounds
    {
        get
        {
            if (rounds && rounds.IsSpawned && rounds.NetworkManager == NetworkManager) return rounds;
            if (NetworkManager && NetworkManager.SpawnManager != null)
                foreach (var obj in NetworkManager.SpawnManager.SpawnedObjectsList)
                    if (obj.TryGetComponent<RoundManager>(out var found)) return rounds = found;
            return null;
        }
    }
    public bool Active
    {
        get
        {
            if (!IsSpawned || TutorialEscena.EsActiva || DeadQuarantine.PvPvE.PvPvEZombieDirector.Active) return false;
            var current = Rounds;
            // Una carga aditiva no debe quitar los modificadores ni recortar la vida actual.
            if (!current || current.ModoNetwork.Value == 0 && current.CurrentRound == 0) return wasActive;
            return current.IsSurvivalUpgradeMode;
        }
    }
    public bool CanChoose => Active && Rounds && Rounds.SurvivalUpgradeWindow.Value && !Rounds.GameLostNetwork.Value && health && health.IsAlive;
    public float HealthMultiplier => Active ? Progress.Value.Multiplier(0) : 1;
    public float DamageMultiplier => Active ? Progress.Value.Multiplier(1) : 1;
    public float RegenMultiplier => Active ? Progress.Value.Multiplier(2) : 1;
    public float MovementMultiplier => Active ? Progress.Value.Multiplier(3) : 1;
    public override void OnNetworkSpawn()
    {
        health = GetComponent<PlayerHealth>();
        Progress.OnValueChanged += OnProgress;
        if (IsServer) Progress.Value = SurvivalUpgradeSession.For(NetworkManager).Read(OwnerClientId);
        wasActive = Active;
        health.RefreshSurvivalMaxHealth(false);
        if (IsOwner) StartCoroutine(RegisterWhenSpawned());
    }
    IEnumerator RegisterWhenSpawned()
    {
        yield return null;
        if (IsSpawned && IsOwner) RegisterIdentityServerRpc(PlayerIdentity.GetUserId());
    }
    void Update()
    {
        if (!IsSpawned || Active == wasActive) return;
        wasActive = Active;
        health.RefreshSurvivalMaxHealth(wasActive);
        Changed?.Invoke();
    }
    public override void OnNetworkDespawn()
    {
        Progress.OnValueChanged -= OnProgress;
        StopAllCoroutines();
    }
    void OnProgress(SurvivalUpgradeState previous, SurvivalUpgradeState current)
    {
        if (health) health.RefreshSurvivalMaxHealth(current.health > previous.health);
        Changed?.Invoke();
    }
    [ServerRpc]
    void RegisterIdentityServerRpc(string identity)
    {
        var session = SurvivalUpgradeSession.For(NetworkManager);
        Progress.Value = session.Bind(OwnerClientId, identity);
    }
    public void Choose(int kind)
    {
        if (IsOwner && CanChoose && Progress.Value.points > 0 && kind >= 0 && kind < 4)
            SpendServerRpc((byte)kind, Progress.Value.Spent);
    }
    [ServerRpc]
    void SpendServerRpc(byte kind, int expectedSpent)
    {
        var state = Progress.Value;
        bool accepted = CanChoose && state.Spent == expectedSpent && state.Spend(kind);
        if (accepted) SetProgressServer(state);
        SelectionReplyClientRpc(accepted);
    }
    [ClientRpc]
    void SelectionReplyClientRpc(bool accepted) { if (IsOwner) SelectionReplied?.Invoke(accepted); }
    internal void SetProgressServer(SurvivalUpgradeState state)
    {
        if (!IsServer) return;
        state = state.Clamped();
        SurvivalUpgradeSession.For(NetworkManager).Store(OwnerClientId, state);
        Progress.Value = state;
    }
    // Un único punto del cálculo de daño; nunca se modifican las estadísticas de las armas.
    internal static float OutgoingDamage(NetworkManager network, ulong clientId, float original)
    {
        if (network && network.IsServer && network.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject &&
            client.PlayerObject.TryGetComponent<SurvivalUpgrades>(out var upgrades)) return original * upgrades.DamageMultiplier;
        return original;
    }
}
