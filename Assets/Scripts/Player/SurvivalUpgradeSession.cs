using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>Libro del Host asociado a la sesión de red, no a la vida ni a la escena del jugador.</summary>
public sealed class SurvivalUpgradeSession : MonoBehaviour
{
    readonly Dictionary<string, SurvivalUpgradeState> states = new();
    readonly Dictionary<ulong, string> players = new();
    readonly HashSet<ulong> identified = new();
    NetworkManager network;
    int lastCompletedRound;
    internal static SurvivalUpgradeSession For(NetworkManager manager)
    {
        return manager.TryGetComponent<SurvivalUpgradeSession>(out var session) ? session : manager.gameObject.AddComponent<SurvivalUpgradeSession>();
    }
    void Awake()
    {
        network = GetComponent<NetworkManager>();
        network.OnServerStopped += OnStopped;
        network.OnClientDisconnectCallback += Disconnected;
    }
    void OnDestroy()
    {
        if (!network) return;
        network.OnServerStopped -= OnStopped;
        network.OnClientDisconnectCallback -= Disconnected;
    }
    void OnStopped(bool _) { states.Clear(); players.Clear(); identified.Clear(); lastCompletedRound = 0; }
    void Disconnected(ulong id) { players.Remove(id); identified.Remove(id); }
    string Key(ulong id)
    {
        if (!players.TryGetValue(id, out string key)) players[id] = key = "connection:" + id;
        return key;
    }
    internal SurvivalUpgradeState Read(ulong id) => states.TryGetValue(Key(id), out var state) ? state : default;
    internal SurvivalUpgradeState Bind(ulong id, string identity)
    {
        if (!identified.Add(id) || string.IsNullOrWhiteSpace(identity) || identity.Length > 128) return Read(id);
        string key = "player:" + identity.Trim();
        // Dos clientes conectados nunca pueden compartir la misma distribución.
        foreach (var pair in players) if (pair.Key != id && pair.Value == key) return Read(id);
        string prior = Key(id);
        if (!states.ContainsKey(key)) states[key] = Read(id);
        players[id] = key;
        states.Remove(prior);
        return Read(id);
    }
    internal void Store(ulong id, SurvivalUpgradeState state) { states[Key(id)] = state; }
    internal void AwardRound(RoundManager rounds, int round)
    {
        if (!network.IsServer || !rounds.IsSurvivalUpgradeMode || round <= lastCompletedRound) return;
        lastCompletedRound = round;
        foreach (var client in network.ConnectedClientsList)
        {
            if (!client.PlayerObject || !client.PlayerObject.TryGetComponent<SurvivalUpgrades>(out var upgrades)) continue;
            var state = upgrades.Progress.Value;
            state.Reward(round);
            upgrades.SetProgressServer(state);
        }
    }
}
