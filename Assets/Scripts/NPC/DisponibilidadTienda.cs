using System;
using UnityEngine;

/// <summary>Dificultades en las que aparece una oferta. En el Inspector se elige con un desplegable múltiple.</summary>
[Flags]
public enum DificultadesTienda
{
    Normal = 1 << 0,
    Dificil = 1 << 1,
    Pesadilla = 1 << 2
}

/// <summary>Modos de juego en los que aparece una oferta. En el Inspector se elige con un desplegable múltiple.</summary>
[Flags]
public enum ModosTienda
{
    Historia = 1 << 0,
    Ronda5 = 1 << 1,
    Supervivencia = 1 << 2,

    // PvP (Team Deathmatch): reutiliza los 3 mapas, pero ahí no se comercia ni se juntan recursos.
    PvP = 1 << 3
}

/// <summary>
/// Decide si una oferta se muestra en la partida actual según su dificultad y modo.
/// Las ofertas que no corresponden no aparecen en la tienda (no se muestran como "no disponibles").
/// </summary>
public static class DisponibilidadTienda
{
    public const DificultadesTienda TodasLasDificultades =
        DificultadesTienda.Normal | DificultadesTienda.Dificil | DificultadesTienda.Pesadilla;

    // Los modos con mercader y recursos. PvP queda afuera a propósito: ahí no se comercia.
    public const ModosTienda TodosLosModos =
        ModosTienda.Historia | ModosTienda.Ronda5 | ModosTienda.Supervivencia;

    /// <summary>
    /// Partida PvP (Team Deathmatch). No es un ModoJuego de ConfiguracionPartidaSeleccionada (ahí queda en Ninguno):
    /// se reconoce por la escena de sus mapas (PvPvEMapContext) o por el roster de equipos de la sesión.
    /// </summary>
    public static bool EsPvP =>
        DeadQuarantine.PvPvE.Networking.PvPvEMapContext.Active != null ||
        DeadQuarantine.PvPvE.Networking.PvPvETeamRoster.Active != null;

    /// <summary>
    /// Modo de la partida actual como flag, contando PvP. 0 = ninguno (escena abierta directo desde el editor,
    /// tutorial): no se filtra nada. Lo usan MercaderSegunModo y RecursosSegunModo.
    /// </summary>
    public static ModosTienda FlagModoActual()
    {
        return AFlag(ModoActual(), EsPvP);
    }

    /// <summary>En PvP el flag es PvP sin importar el modo elegido en el menú; si no, el del modo.</summary>
    public static ModosTienda AFlag(ConfiguracionPartidaSeleccionada.ModoJuego modo, bool esPvP)
    {
        return esPvP ? ModosTienda.PvP : AFlag(modo);
    }

    public static bool SeMuestraEnPartidaActual(WeaponOffer oferta)
    {
        if (oferta == null)
            return false;

        DifficultyLevel? dificultad = DificultadActual();
        if (dificultad.HasValue && !oferta.SeMuestraEnDificultad(dificultad.Value))
            return false;

        ConfiguracionPartidaSeleccionada.ModoJuego modo = ModoActual();
        if (!oferta.SeMuestraEnModo(modo))
            return false;

        return true;
    }

    /// <summary>Escena del tutorial: el mercader muestra todo el catálogo (sin filtrar por dificultad ni modo).</summary>
    public static bool EsTutorial => TutorialEscena.EsActiva;

    // En partida manda la dificultad sincronizada por el host. Sin RoundManager (escenas de prueba)
    // se usa la elegida en el menú; sin nada de eso no se filtra. En el tutorial tampoco se filtra.
    private static DifficultyLevel? DificultadActual()
    {
        RoundManager rm = RoundManager.Instance;
        if (rm != null && rm.IsSpawned)
            return (DifficultyLevel)rm.DifficultyNetwork.Value;

        if (EsTutorial)
            return null;

        if (DifficultyManager.Instance != null)
            return DifficultyManager.Instance.CurrentDifficulty;

        return null;
    }

    public static ConfiguracionPartidaSeleccionada.ModoJuego ModoActual()
    {
        RoundManager rm = RoundManager.Instance;
        if (rm != null && rm.IsSpawned && rm.ModoNetwork.Value != (int)ConfiguracionPartidaSeleccionada.ModoJuego.Ninguno)
            return (ConfiguracionPartidaSeleccionada.ModoJuego)rm.ModoNetwork.Value;

        // La configuración del menú sobrevive entre escenas (DontDestroyOnLoad): en el tutorial no cuenta
        // el modo de la última partida, así se ve el mercader, todo su catálogo y se pueden colocar objetos.
        if (EsTutorial)
            return ConfiguracionPartidaSeleccionada.ModoJuego.Ninguno;

        ConfiguracionPartidaSeleccionada config = ConfiguracionPartidaSeleccionada.Instancia;
        return config != null ? config.modoSeleccionado : ConfiguracionPartidaSeleccionada.ModoJuego.Ninguno;
    }

    public static DificultadesTienda AFlag(DifficultyLevel nivel)
    {
        switch (nivel)
        {
            case DifficultyLevel.Dificil: return DificultadesTienda.Dificil;
            case DifficultyLevel.Pesadilla: return DificultadesTienda.Pesadilla;
            default: return DificultadesTienda.Normal;
        }
    }

    // Ninguno (escena abierta directo desde el editor) = 0: no filtra por modo.
    public static ModosTienda AFlag(ConfiguracionPartidaSeleccionada.ModoJuego modo)
    {
        switch (modo)
        {
            case ConfiguracionPartidaSeleccionada.ModoJuego.Historia: return ModosTienda.Historia;
            case ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5: return ModosTienda.Ronda5;
            case ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia: return ModosTienda.Supervivencia;
            default: return 0;
        }
    }
}
