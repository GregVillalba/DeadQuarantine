using Unity.Netcode;
using UnityEngine;

public partial class PlayerHealth
{
    public const float BossRockStunSeconds = 5f;
    public readonly NetworkVariable<double> BossStunUntil = new();
    public float BossStunRemaining => IsSpawned && IsAlive && NetworkManager != null ?
        Mathf.Max(0, (float)(BossStunUntil.Value - NetworkManager.ServerTime.Time)) : 0;
    public bool IsBossStunned => BossStunRemaining > 0;

    // Called only by the authoritative boss on a direct boulder impact.
    public void ApplyBossRockStunServer()
    {
        if (!IsSpawned || !IsServer || !IsAlive) return;
        BossStunUntil.Value = System.Math.Max(BossStunUntil.Value,
            NetworkManager.ServerTime.Time + BossRockStunSeconds);
        SlideUntil.Value = 0;
    }
}
