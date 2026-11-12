using UnityEngine;

/// <summary>
/// Apaga los recursos (los del mapa y los que sueltan los zombies) en los modos de juego que no corresponden.
/// Es el equivalente de MercaderSegunModo para los recursos: va al lado de GestorRecursosEscenario.
/// Ej.: PvP (Team Deathmatch) usa los mismos mapas pero no se comercia, así que no aparece ningún recurso.
/// Sin este componente en la escena vale la regla por defecto: recursos en todos los modos menos PvP.
/// </summary>
[DefaultExecutionOrder(-100)]
public class RecursosSegunModo : MonoBehaviour
{
    /// <summary>Modos con recursos cuando no hay un RecursosSegunModo en la escena (todos menos PvP).</summary>
    public const ModosTienda ModosPorDefecto = DisponibilidadTienda.TodosLosModos;

    [Tooltip("Modos en los que aparecen recursos (los del mapa y los que sueltan los zombies). " +
             "En el resto no aparece ninguno. PvP sin marcar = partidas PvP sin recursos.")]
    [SerializeField] private ModosTienda modosConRecursos = ModosPorDefecto;

    // El de la escena de la partida. Lo consultan GestorRecursosEscenario y DropRecursosZombie.
    private static RecursosSegunModo instancia;

    // Con "Enter Play Mode Options" (sin recargar dominio) los estaticos sobreviven entre partidas.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instancia = null;
    }

    private void OnEnable()
    {
        instancia = this;
    }

    private void OnDisable()
    {
        if (instancia == this)
            instancia = null;
    }

    /// <summary>true si en la partida actual aparecen recursos (del mapa y de los zombies).</summary>
    public static bool HayRecursosEnPartidaActual
    {
        get
        {
            ModosTienda modos = instancia != null ? instancia.modosConRecursos : ModosPorDefecto;
            return HayRecursosEn(modos, DisponibilidadTienda.FlagModoActual());
        }
    }

    /// <summary>
    /// true si con esos modos configurados hay recursos en el modo actual.
    /// Ninguno (escena abierta directo desde el editor, tutorial) = 0: siempre hay.
    /// </summary>
    public static bool HayRecursosEn(ModosTienda modosConRecursos, ModosTienda modoActual)
    {
        return modoActual == 0 || (modosConRecursos & modoActual) != 0;
    }
}
