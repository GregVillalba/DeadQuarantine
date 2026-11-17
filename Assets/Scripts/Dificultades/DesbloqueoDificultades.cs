using System;
using System.Globalization;
using UnityEngine;
using Dificultad = ConfiguracionPartidaSeleccionada.Dificultad;
using Modo = ConfiguracionPartidaSeleccionada.ModoJuego;

/// <summary>
/// Progreso de desbloqueo de dificultades (Normal -> Dificil -> Pesadilla) por modo de juego.
///
/// - Cada modo (Historia, 5 Rondas, Supervivencia) guarda por separado hasta qué dificultad llegó el jugador.
/// - El progreso es LOCAL de cada máquina (PlayerPrefs) y lo comparten singleplayer y multiplayer.
/// - Los lobbies consultan EstaDesbloqueada / TextoRequisito; RoundManager llama a EvaluarPartida
///   en cada cliente cuando se cumple un hito (victoria o ronda superada).
/// - Los números de los requisitos están todos en la tabla de abajo (REQUISITOS).
/// </summary>
public static class DesbloqueoDificultades
{
    // =========================================================
    // REQUISITOS (balance)
    // =========================================================

    public struct Requisito
    {
        /// <summary>Puntaje mínimo al cumplir el hito. 0 = no se pide puntaje.</summary>
        public int puntajeMinimo;

        /// <summary>Ronda que hay que superar (terminar). 0 = hay que completar la partida (victoria).</summary>
        public int rondaASuperar;

        /// <summary>Las caídas tienen que ser MENORES a este número (1 = sin caídas). -1 = no se piden.</summary>
        public int caidasMenorQue;

        public Requisito(int puntajeMinimo, int rondaASuperar, int caidasMenorQue)
        {
            this.puntajeMinimo = puntajeMinimo;
            this.rondaASuperar = rondaASuperar;
            this.caidasMenorQue = caidasMenorQue;
        }
    }

    // El puntaje que se compara es el FINAL: ya tiene descontados los 500 puntos por caída
    // (PlayerScore.PuntajeFinal), el mismo que muestra la pantalla de resultado.

    // Historia: completar las misiones con un puntaje mínimo.
    private static readonly Requisito HistoriaDificil = new Requisito(10000, 0, -1);   // jugando en Normal
    private static readonly Requisito HistoriaPesadilla = new Requisito(50000, 0, -1); // jugando en Dificil
    private static readonly Requisito HistoriaEspecial = new Requisito(100000, 0, -1);  // jugando en Normal, sin nada desbloqueado

    // 5 Rondas: completar las 5 rondas con un puntaje mínimo y pocas caídas.
    private static readonly Requisito Ronda5Dificil = new Requisito(40000, 0, 2);
    private static readonly Requisito Ronda5Pesadilla = new Requisito(70000, 0, 1);
    private static readonly Requisito Ronda5Especial = new Requisito(100000, 0, 1);

    // Supervivencia: superar una ronda con pocas caídas.
    private static readonly Requisito SupervivenciaDificil = new Requisito(0, 10, 5);
    private static readonly Requisito SupervivenciaPesadilla = new Requisito(0, 20, 10);
    private static readonly Requisito SupervivenciaEspecial = new Requisito(0, 20, 1);

    /// <summary>Requisito para desbloquear esa dificultad jugando en la dificultad anterior.</summary>
    public static Requisito ObtenerRequisito(Modo modo, Dificultad aDesbloquear)
    {
        bool pesadilla = aDesbloquear == Dificultad.Pesadilla;

        switch (modo)
        {
            case Modo.Historia: return pesadilla ? HistoriaPesadilla : HistoriaDificil;
            case Modo.Ronda5: return pesadilla ? Ronda5Pesadilla : Ronda5Dificil;
            default: return pesadilla ? SupervivenciaPesadilla : SupervivenciaDificil;
        }
    }

    /// <summary>Condición especial: desbloquea TODO el modo en una sola partida en Normal.</summary>
    public static Requisito ObtenerRequisitoEspecial(Modo modo)
    {
        switch (modo)
        {
            case Modo.Historia: return HistoriaEspecial;
            case Modo.Ronda5: return Ronda5Especial;
            default: return SupervivenciaEspecial;
        }
    }

    // =========================================================
    // PROGRESO GUARDADO
    // =========================================================

    private const string PrefijoClave = "DQ_DesbloqueoDificultad_";
    private const string ClaveForzarTodoEnEditor = "DQ_DesbloqueoDificultad_ForzarTodoEnEditor";

    /// <summary>Se dispara cuando cambia el progreso (desbloqueo real o cambio desde el Inspector).</summary>
    public static event Action AlCambiar;

    public static bool EsModoValido(Modo modo)
    {
        return modo == Modo.Historia || modo == Modo.Ronda5 || modo == Modo.Supervivencia;
    }

    /// <summary>Dificultad más alta desbloqueada de ese modo según el progreso guardado (mínimo Normal).</summary>
    public static Dificultad MaximaDesbloqueada(Modo modo)
    {
        if (!EsModoValido(modo))
            return Dificultad.Pesadilla;

        int valor = PlayerPrefs.GetInt(PrefijoClave + modo, (int)Dificultad.Normal);

        return (Dificultad)Mathf.Clamp(valor, (int)Dificultad.Normal, (int)Dificultad.Pesadilla);
    }

    /// <summary>
    /// ¿Está desbloqueada esa dificultad en ese modo? Normal siempre. Sin modo elegido no se bloquea nada
    /// (se evalúa cuando se elige el modo).
    /// </summary>
    public static bool EstaDesbloqueada(Modo modo, Dificultad dificultad)
    {
        if (!EsModoValido(modo) || dificultad <= Dificultad.Normal)
            return true;

        if (DesbloquearTodoEnEditor)
            return true;

        return dificultad <= MaximaDesbloqueada(modo);
    }

    /// <summary>
    /// Fija a mano hasta qué dificultad está desbloqueado un modo (Normal = todo bloqueado).
    /// Lo usa el Inspector de DifficultyManager para pruebas.
    /// </summary>
    public static void EstablecerMaximaDesbloqueada(Modo modo, Dificultad maxima)
    {
        if (!EsModoValido(modo))
            return;

        Guardar(modo, maxima);
    }

    /// <summary>Vuelve al estado de "primera vez": solo Normal en los tres modos.</summary>
    public static void ReiniciarProgreso()
    {
        PlayerPrefs.DeleteKey(PrefijoClave + Modo.Historia);
        PlayerPrefs.DeleteKey(PrefijoClave + Modo.Ronda5);
        PlayerPrefs.DeleteKey(PrefijoClave + Modo.Supervivencia);
        PlayerPrefs.Save();

        AlCambiar?.Invoke();
    }

    /// <summary>
    /// Solo para pruebas en el Editor: mientras está activo, todas las dificultades se pueden elegir.
    /// No toca el progreso guardado. En una build siempre es false.
    /// </summary>
    public static bool DesbloquearTodoEnEditor
    {
        get
        {
            return Application.isEditor && PlayerPrefs.GetInt(ClaveForzarTodoEnEditor, 0) == 1;
        }
        set
        {
            if (!Application.isEditor)
                return;

            PlayerPrefs.SetInt(ClaveForzarTodoEnEditor, value ? 1 : 0);
            PlayerPrefs.Save();

            AlCambiar?.Invoke();
        }
    }

    private static void Guardar(Modo modo, Dificultad maxima)
    {
        int valor = Mathf.Clamp((int)maxima, (int)Dificultad.Normal, (int)Dificultad.Pesadilla);

        PlayerPrefs.SetInt(PrefijoClave + modo, valor);
        PlayerPrefs.Save();

        AlCambiar?.Invoke();
    }

    // =========================================================
    // INICIO DE PARTIDA (para la condición especial)
    // =========================================================

    // La condición especial exige que el jugador NO tuviera nada desbloqueado de ese modo al EMPEZAR
    // la partida. En Supervivencia Dificil se desbloquea a mitad de partida (ronda 10) y la especial
    // se cumple después (ronda 20), por eso se guarda cómo estaba todo al empezar.
    private static readonly Dificultad[] maximaAlEmpezar = new Dificultad[4];
    private static bool inicioRegistrado;

    /// <summary>Lo llama RoundManager en cada cliente cuando arranca una partida.</summary>
    public static void RegistrarInicioDePartida()
    {
        maximaAlEmpezar[(int)Modo.Historia] = MaximaDesbloqueada(Modo.Historia);
        maximaAlEmpezar[(int)Modo.Ronda5] = MaximaDesbloqueada(Modo.Ronda5);
        maximaAlEmpezar[(int)Modo.Supervivencia] = MaximaDesbloqueada(Modo.Supervivencia);
        inicioRegistrado = true;
    }

    /// <summary>¿La partida en curso empezó sin ninguna dificultad desbloqueada de ese modo?</summary>
    public static bool EmpezoSinDesbloqueos(Modo modo)
    {
        if (!EsModoValido(modo))
            return false;

        if (!inicioRegistrado)
            RegistrarInicioDePartida();

        return maximaAlEmpezar[(int)modo] <= Dificultad.Normal;
    }

    /// <summary>
    /// Al cargar una partida guardada: restaura si esa partida había empezado sin desbloqueos
    /// (la partida guardada sigue siendo "la misma partida").
    /// </summary>
    public static void RestaurarEmpezoSinDesbloqueos(Modo modo, bool empezoSinDesbloqueos)
    {
        if (!EsModoValido(modo))
            return;

        if (!inicioRegistrado)
            RegistrarInicioDePartida();

        maximaAlEmpezar[(int)modo] = empezoSinDesbloqueos
            ? Dificultad.Normal
            : (Dificultad)Mathf.Max((int)MaximaDesbloqueada(modo), (int)Dificultad.Dificil);
    }

    // =========================================================
    // EVALUACIÓN
    // =========================================================

    /// <summary>
    /// Evalúa si el jugador LOCAL desbloquea algo con lo que acaba de lograr.
    /// </summary>
    /// <param name="modo">Modo de la partida.</param>
    /// <param name="jugada">Dificultad de la partida (en multiplayer, la que eligió el host).</param>
    /// <param name="puntaje">Puntaje del jugador local en este momento.</param>
    /// <param name="caidas">Caídas del jugador local en este momento.</param>
    /// <param name="rondaSuperada">Última ronda terminada (Supervivencia). 0 si no aplica.</param>
    /// <param name="partidaCompletada">True si se ganó la partida (Historia / 5 Rondas).</param>
    /// <param name="anterior">Dificultad máxima que tenía antes.</param>
    /// <param name="nueva">Dificultad máxima que tiene ahora.</param>
    /// <returns>True si se desbloqueó algo nuevo.</returns>
    public static bool EvaluarPartida(
        Modo modo,
        Dificultad jugada,
        int puntaje,
        int caidas,
        int rondaSuperada,
        bool partidaCompletada,
        out Dificultad anterior,
        out Dificultad nueva)
    {
        anterior = MaximaDesbloqueada(modo);
        nueva = anterior;

        if (!EsModoValido(modo) || jugada < Dificultad.Normal || jugada > Dificultad.Pesadilla)
            return false;

        if (anterior >= Dificultad.Pesadilla)
            return false;

        // Condición especial: en Normal, habiendo empezado la partida sin nada desbloqueado de este modo.
        if (jugada == Dificultad.Normal &&
            EmpezoSinDesbloqueos(modo) &&
            Cumple(ObtenerRequisitoEspecial(modo), puntaje, caidas, rondaSuperada, partidaCompletada))
        {
            nueva = Dificultad.Pesadilla;
        }

        // Regla general: cumplir el requisito en una dificultad desbloquea la siguiente.
        // Solo cuenta si el jugador tiene desbloqueada la dificultad que se está jugando: un invitado
        // que juega una dificultad que no tiene (la eligió el host) no desbloquea nada con esa partida.
        if (nueva < Dificultad.Pesadilla &&
            jugada < Dificultad.Pesadilla &&
            EstaDesbloqueada(modo, jugada))
        {
            Dificultad siguiente = jugada + 1;

            if (siguiente > nueva &&
                Cumple(ObtenerRequisito(modo, siguiente), puntaje, caidas, rondaSuperada, partidaCompletada))
            {
                nueva = siguiente;
            }
        }

        if (nueva <= anterior)
            return false;

        Guardar(modo, nueva);

        Debug.Log(
            "[DesbloqueoDificultades] " + modo + ": " + anterior + " -> " + nueva +
            " (jugada en " + jugada + ", puntaje " + puntaje + ", caidas " + caidas +
            ", ronda superada " + rondaSuperada + ", completada " + partidaCompletada + ")."
        );

        return true;
    }

    private static bool Cumple(
        Requisito requisito,
        int puntaje,
        int caidas,
        int rondaSuperada,
        bool partidaCompletada)
    {
        if (requisito.rondaASuperar > 0)
        {
            if (rondaSuperada < requisito.rondaASuperar)
                return false;
        }
        else if (!partidaCompletada)
        {
            return false;
        }

        if (puntaje < requisito.puntajeMinimo)
            return false;

        if (requisito.caidasMenorQue >= 0 && caidas >= requisito.caidasMenorQue)
            return false;

        return true;
    }

    // =========================================================
    // TEXTOS
    // =========================================================

    /// <summary>Qué hay que hacer para desbloquear esa dificultad.</summary>
    public static string TextoRequisito(Modo modo, Dificultad aDesbloquear)
    {
        if (!EsModoValido(modo) || aDesbloquear <= Dificultad.Normal)
            return string.Empty;

        Requisito requisito = ObtenerRequisito(modo, aDesbloquear);
        string texto;

        switch (modo)
        {
            case Modo.Historia:
                texto = "COMPLETÁ LAS MISIONES";
                break;

            case Modo.Ronda5:
                texto = "COMPLETÁ 5 RONDAS";
                break;

            default:
                texto = "SUPERÁ LA RONDA " + requisito.rondaASuperar;
                break;
        }

        bool pidePuntaje = requisito.puntajeMinimo > 0;
        bool pideCaidas = requisito.caidasMenorQue >= 0;

        if (pidePuntaje)
            texto += " CON " + FormatearPuntaje(requisito.puntajeMinimo) + " PUNTOS O MÁS";

        if (pideCaidas)
        {
            if (requisito.caidasMenorQue <= 1)
                texto += pidePuntaje ? " Y SIN CAÍDAS" : " SIN CAÍDAS";
            else
                texto += (pidePuntaje ? " Y MENOS DE " : " CON MENOS DE ") + requisito.caidasMenorQue + " CAÍDAS";
        }

        return texto + " EN " + NombreDificultad(aDesbloquear - 1);
    }

    public static string NombreDificultad(Dificultad dificultad)
    {
        switch (dificultad)
        {
            case Dificultad.Normal: return "NORMAL";
            case Dificultad.Dificil: return "DIFÍCIL";
            case Dificultad.Pesadilla: return "PESADILLA";
            default: return "-";
        }
    }

    public static string NombreModo(Modo modo)
    {
        switch (modo)
        {
            case Modo.Historia: return "HISTORIA";
            case Modo.Ronda5: return "RONDA 5";
            case Modo.Supervivencia: return "SUPERVIVENCIA";
            default: return "-";
        }
    }

    // 5000 -> "5.000"
    public static string FormatearPuntaje(int puntaje)
    {
        return puntaje.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
    }
}
