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
    public static bool MenuOpen =>
        DeadQuarantine.PvPvE.UI.PvPvEInitialClassView.BlocksLocalGameplay ||
        PauseController.LocalPlayerPaused ||
        NPCShopUI.LocalShopOpen;

    /// <summary>
    /// Disparo, recarga, apuntado, cambio de arma,
    /// granadas, cuchillo, etc.
    /// También mientras se coloca una torreta o un barril: el clic izquierdo lo coloca y el derecho cancela.
    /// </summary>
    public static bool ActionsBlocked =>
        MenuOpen ||
        ColocadorObjetos.ColocandoLocal;

    /// <summary>
    /// Bloquea movimientos derivados del mouse.
    /// Incluye unos frames de seguridad después de cerrar UI.
    /// </summary>
    public static bool MouseBlocked =>
        DeadQuarantine.PvPvE.UI.PvPvEInitialClassView.BlocksLocalGameplay ||
        PauseController.MouseInputBlocked ||
        NPCShopUI.MouseInputBlocked;
}
