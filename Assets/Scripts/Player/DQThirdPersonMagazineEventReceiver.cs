using UnityEngine;

/// <summary>
/// Receptor para el AnimationEvent OnDropMagazine incluido en clips TP del pack SMG.
/// Este evento se emite en el GameObject del Animator del cuerpo, no en el arma.
/// Las piezas del cargador ya se animan con su propio Animator; el evento no debe
/// generar un cargador extra ni lanzar una excepción en cada recarga.
/// </summary>
[DisallowMultipleComponent]
public sealed class DQThirdPersonMagazineEventReceiver : MonoBehaviour
{
    public void OnDropMagazine()
    {
        // Punto de extensión para un casquillo/cargador físico TP futuro.
        // La animación de la pieza se maneja por el controller de partes del arma.
    }
}
