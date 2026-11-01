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
    /// Disparo, recarga, apuntado, cambio de arma,
    /// granadas, cuchillo, etc.
    /// </summary>
    public static bool ActionsBlocked =>
        PauseController.LocalPlayerPaused ||
        NPCShopUI.LocalShopOpen;

    /// <summary>
    /// Bloquea movimientos derivados del mouse.
    /// Incluye unos frames de seguridad después de cerrar UI.
    /// </summary>
    public static bool MouseBlocked =>
        PauseController.MouseInputBlocked ||
        NPCShopUI.MouseInputBlocked;
}