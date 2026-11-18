using System.Collections;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerHealth
{
    public readonly NetworkVariable<double> StalkerAcidUntil = new();
    public bool HasStalkerAcid => IsSpawned && IsAlive && NetworkManager != null &&
        StalkerAcidUntil.Value > NetworkManager.ServerTime.Time;
    Coroutine stalkerAcidRoutine;
    int stalkerAcidDamage;
    Vector3 stalkerAcidSource;

    public void ApplyStalkerAcidServer(float duration, int damagePerSecond, Vector3 source)
    {
        if (!IsServer || !IsSpawned || !IsAlive || EnMinijuego || perksMatchEnded || PlayerInputGate.MatchLost ||
            duration <= 0 || damagePerSecond <= 0) return;
        StalkerAcidUntil.Value = NetworkManager.ServerTime.Time + duration;
        stalkerAcidDamage = damagePerSecond;
        stalkerAcidSource = source;
        // Refresh the existing effect, preserving its single tick schedule.
        if (stalkerAcidRoutine == null) stalkerAcidRoutine = StartCoroutine(StalkerAcidRoutine());
    }
    IEnumerator StalkerAcidRoutine()
    {
        double nextTick = NetworkManager.ServerTime.Time + 1;
        while (IsSpawned && IsAlive && !perksMatchEnded && !PlayerInputGate.MatchLost)
        {
            double now = NetworkManager.ServerTime.Time;
            if (now >= nextTick && nextTick <= StalkerAcidUntil.Value + .001)
            {
                TakeDamage(stalkerAcidDamage, source: stalkerAcidSource);
                nextTick += 1;
            }
            if (now >= StalkerAcidUntil.Value) break;
            yield return null;
        }
        stalkerAcidRoutine = null;
        if (IsSpawned && IsServer) StalkerAcidUntil.Value = 0;
    }
    void ClearStalkerAcid()
    {
        if (stalkerAcidRoutine != null) { StopCoroutine(stalkerAcidRoutine); stalkerAcidRoutine = null; }
        if (IsServer) StalkerAcidUntil.Value = 0;
    }
}
