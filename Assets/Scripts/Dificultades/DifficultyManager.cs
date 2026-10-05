
using UnityEngine;

/// <summary>
/// Guarda el modo y la dificultad elegidos en el menú.
/// Va en un GameObject RAÍZ de la escena del menú (primera escena que se carga)
/// y sobrevive al cambio de escena con DontDestroyOnLoad.
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    [Header("Assets (arrastrar desde Assets/Data)")]
    [SerializeField] private DifficultySettings[] difficulties;
    [SerializeField] private GameModeConfig[] modes;

    [Header("Valores por defecto")]
    [SerializeField] private DifficultyLevel defaultDifficulty = DifficultyLevel.Normal;
    [SerializeField] private int defaultModeIndex = 0;

    public DifficultyLevel CurrentDifficulty { get; private set; }
    public GameModeConfig CurrentMode { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Gana el más nuevo: es el que referencian los botones de la escena actual.
            Destroy(Instance.gameObject);
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentDifficulty = defaultDifficulty;
        SetModeIndex(defaultModeIndex);
    }

    // ---------- Dificultad ----------

    public void SetDifficulty(DifficultyLevel level)
    {
        CurrentDifficulty = level;
    }

    // Para usar directo desde el OnClick de un botón (0 = Normal, 1 = Difícil, 2 = Pesadilla).
    public void SetDifficultyIndex(int index)
    {
        SetDifficulty((DifficultyLevel)Mathf.Clamp(index, 0, 2));
    }

    public DifficultySettings GetCurrentSettings()
    {
        return GetSettingsFor(CurrentDifficulty);
    }

    // Busca el asset de una dificultad concreta (los clientes lo usan
    // con la dificultad que eligió el host).
    public DifficultySettings GetSettingsFor(DifficultyLevel level)
    {
        if (difficulties == null)
            return null;

        foreach (DifficultySettings d in difficulties)
        {
            if (d != null && d.level == level)
                return d;
        }

        return null;
    }

    // Para el botón "Modalidad Aleatoria" que solo sortea el modo.
    public void SetRandomMode()
    {
        if (modes != null && modes.Length > 0)
            SetModeIndex(Random.Range(0, modes.Length));
    }

    // Para el botón "Modalidad Aleatoria" de cada modo (dificultad al azar).
    public void SetRandomDifficulty()
    {
        SetDifficulty((DifficultyLevel)Random.Range(0, 3));
    }

    // Para el botón "Elegir Aleatoriamente" (modo y dificultad al azar).
    public void SetRandomModeAndDifficulty()
    {
        if (modes != null && modes.Length > 0)
            SetModeIndex(Random.Range(0, modes.Length));

        SetRandomDifficulty();
    }

    // ---------- Modo ----------

    public void SetMode(GameModeConfig mode)
    {
        CurrentMode = mode;
    }

    // Para usar desde el OnClick de los botones de modo.
    public void SetModeIndex(int index)
    {
        if (modes == null || modes.Length == 0)
        {
            CurrentMode = null;
            return;
        }

        CurrentMode = modes[Mathf.Clamp(index, 0, modes.Length - 1)];
    }

    // Toma el modo y la dificultad guardados en ConfiguracionPartidaSeleccionada.
    // Si no hay nada elegido, deja lo que ya habia.
    public void SincronizarConSeleccion()
    {
        ConfiguracionPartidaSeleccionada config = ConfiguracionPartidaSeleccionada.Instancia;

        if (config == null)
            return;

        switch (config.modoSeleccionado)
        {
            case ConfiguracionPartidaSeleccionada.ModoJuego.Historia:
                SetModeIndex(0);   // todavia sin asset
                break;

            case ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5:
                SetModeIndex(1);
                break;

            case ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia:
                SetModeIndex(2);
                break;
        }

        switch (config.dificultadSeleccionada)
        {
            case ConfiguracionPartidaSeleccionada.Dificultad.Normal:
                SetDifficulty(DifficultyLevel.Normal);
                break;

            case ConfiguracionPartidaSeleccionada.Dificultad.Dificil:
                SetDifficulty(DifficultyLevel.Dificil);
                break;

            case ConfiguracionPartidaSeleccionada.Dificultad.Pesadilla:
                SetDifficulty(DifficultyLevel.Pesadilla);
                break;
        }
    }
}

