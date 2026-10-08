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
    Supervivencia = 1 << 2
}

/// <summary>
/// Decide si una oferta se muestra en la partida actual según su dificultad y modo.
/// Las ofertas que no corresponden no aparecen en la tienda (no se muestran como "no disponibles").
/// </summary>
public static class DisponibilidadTienda
{
    public const DificultadesTienda TodasLasDificultades =
        DificultadesTienda.Normal | DificultadesTienda.Dificil | DificultadesTienda.Pesadilla;

    public const ModosTienda TodosLosModos =
        ModosTienda.Historia | ModosTienda.Ronda5 | ModosTienda.Supervivencia;

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

    // En partida manda la dificultad sincronizada por el host. Sin RoundManager (escenas de prueba)
    // se usa la elegida en el menú; sin nada de eso no se filtra.
    private static DifficultyLevel? DificultadActual()
    {
        RoundManager rm = RoundManager.Instance;
        if (rm != null && rm.IsSpawned)
            return (DifficultyLevel)rm.DifficultyNetwork.Value;

        if (DifficultyManager.Instance != null)
            return DifficultyManager.Instance.CurrentDifficulty;

        return null;
    }

    private static ConfiguracionPartidaSeleccionada.ModoJuego ModoActual()
    {
        RoundManager rm = RoundManager.Instance;
        if (rm != null && rm.IsSpawned && rm.ModoNetwork.Value != (int)ConfiguracionPartidaSeleccionada.ModoJuego.Ninguno)
            return (ConfiguracionPartidaSeleccionada.ModoJuego)rm.ModoNetwork.Value;

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
