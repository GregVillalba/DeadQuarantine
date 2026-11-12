using UnityEngine;

/// <summary>
/// Oculta el mercader (con su puesto) en los modos de juego que no corresponden.
/// Ej.: el mercader del laboratorio o de la cabaña solo está en 5 Rondas / Supervivencia;
/// en Historia se comercia con el de la zona segura. En PvP (Team Deathmatch) se usan los mismos
/// mapas pero no se comercia: el mercader se oculta salvo que se marque PvP en 'Modos Visibles'.
/// </summary>
public class MercaderSegunModo : MonoBehaviour
{
    [Tooltip("Modos en los que este mercader se ve. En el resto se desactiva al empezar la partida. " +
             "PvP sin marcar = no aparece en las partidas PvP.")]
    [SerializeField] private ModosTienda modosVisibles = ModosTienda.Ronda5 | ModosTienda.Supervivencia;

    // En Start y no en Awake/OnEnable: desactivarse mientras se activa el escenario padre da error.
    private void Start()
    {
        if (!SeVeEn(modosVisibles, DisponibilidadTienda.FlagModoActual()))
            gameObject.SetActive(false);
    }

    /// <summary>
    /// true si un mercader con esos modos visibles se ve en el modo actual.
    /// Ninguno (escena abierta directo desde el editor, tutorial) = 0: no se oculta.
    /// </summary>
    public static bool SeVeEn(ModosTienda modosVisibles, ModosTienda modoActual)
    {
        return modoActual == 0 || (modosVisibles & modoActual) != 0;
    }
}
