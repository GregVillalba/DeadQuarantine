using UnityEngine;

/// <summary>
/// Punto único para saber si las acciones del jugador local
/// están bloqueadas por una interfaz.
///
/// No modifica Weapon.InputLocked.
/// Solamente informa si un menú está abierto.
/// </summary>
public static class PlayerInputGate
{
    /// <summary>
    /// Pausa o tienda abierta.
    /// </summary>
    public static bool MatchLost => RoundManager.Instance && RoundManager.Instance.GameLostNetwork.Value;

    static bool LocalPlayerEliminated
    {
        get
        {
            var network = Unity.Netcode.NetworkManager.Singleton;
            var player = network && network.IsClient ? network.LocalClient?.PlayerObject : null;
            return player && player.TryGetComponent(out PlayerHealth health) && (health.IsSpectating || health.IsDead);
        }
    }

    private static bool LocalBossStunned
    {
        get
        {
            var network = Unity.Netcode.NetworkManager.Singleton;
            var player = network && network.IsClient ? network.LocalClient?.PlayerObject : null;
            return player && player.TryGetComponent<PlayerHealth>(out var health) && health.IsBossStunned;
        }
    }

    public static bool MenuOpen =>
        PlayerSpectator.LocalRespawnPending || SurvivalUpgradeUI.BlocksLocalGameplay ||
        DeadQuarantine.PvPvE.UI.PvPvEInitialClassView.BlocksLocalGameplay ||
        PauseController.LocalPlayerPaused ||
        NPCShopUI.LocalShopOpen;

    /// <summary>
    /// Disparo, recarga, apuntado, cambio de arma,
    /// granadas, cuchillo, etc.
    /// También mientras se coloca una torreta o un barril: el clic izquierdo lo coloca y el derecho cancela.
    /// </summary>
    public static bool ActionsBlocked =>
        PlayerSpectator.LocalRespawnPending || MenuOpen || MatchLost || LocalPlayerEliminated || LocalBossStunned ||
        DeadQuarantine.PvPvE.Networking.PvPvECombat.LocalBlocked ||
        ColocadorObjetos.ColocandoLocal;

    /// <summary>
    /// Bloquea movimientos derivados del mouse.
    /// Incluye unos frames de seguridad después de cerrar UI.
    /// </summary>
    public static bool MouseBlocked =>
        PlayerSpectator.LocalRespawnPending || SurvivalUpgradeUI.BlocksMouse ||
        DeadQuarantine.PvPvE.UI.PvPvEInitialClassView.BlocksLocalGameplay ||
        PauseController.MouseInputBlocked ||
        NPCShopUI.MouseInputBlocked || MatchLost || LocalPlayerEliminated || LocalBossStunned;
}
