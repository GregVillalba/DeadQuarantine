using UnityEngine;

/// <summary>
/// Oculta el mercader (con su puesto) en los modos de juego que no corresponden.
/// Ej.: el mercader del laboratorio o de la cabaña solo está en 5 Rondas / Supervivencia;
/// en Historia se comercia con el de la zona segura.
/// </summary>
public class MercaderSegunModo : MonoBehaviour
{
    [Tooltip("Modos en los que este mercader se ve. En el resto se desactiva al empezar la partida.")]
    [SerializeField] private ModosTienda modosVisibles = ModosTienda.Ronda5 | ModosTienda.Supervivencia;

    // En Start y no en Awake/OnEnable: desactivarse mientras se activa el escenario padre da error.
    private void Start()
    {
        ModosTienda modo = DisponibilidadTienda.AFlag(DisponibilidadTienda.ModoActual());

        // Ninguno (escena abierta directo desde el editor) = 0: no se oculta.
        if (modo == 0 || (modosVisibles & modo) != 0)
            return;

        gameObject.SetActive(false);
    }
}
