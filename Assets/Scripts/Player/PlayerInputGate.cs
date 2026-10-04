using UnityEngine;

/// <summary>
/// Un solo lugar para saber si el jugador LOCAL tiene un menu abierto (pausa o tienda) y por lo tanto
/// no debe tirar granadas, acuchillar, cambiar de arma ni mover el sway con el mouse.
/// Si mas adelante agregas otro menu, sumalo aca y listo: los scripts de arma no hay que tocarlos.
/// </summary>
public static class PlayerInputGate
{
    /// <summary>Acciones del jugador (granada, cuchillo, cambiar arma...) bloqueadas por un menu.</summary>
    public static bool ActionsBlocked =>
        PauseController.LocalPlayerPaused || NPCShopUI.LocalShopOpen;

    /// <summary>Ignorar el delta del mouse (sway): menu abierto o 2 frames despues de cerrarlo.</summary>
    public static bool MouseBlocked =>
        PauseController.MouseInputBlocked || NPCShopUI.MouseInputBlocked;
}