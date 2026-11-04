
using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

/// <summary>
/// Administra el guardado y la carga de checkpoints del modo Historia.
/// Solo se guardan las dos zonas seguras.
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    public enum EscenarioCheckpoint
    {
        Ninguno = 0,
        SafeZone1 = 1,
        SafeZone2 = 2
    }

    [Header("Checkpoint actual")]
    [SerializeField]
    private EscenarioCheckpoint escenarioActual =
        EscenarioCheckpoint.Ninguno;

    public EscenarioCheckpoint EscenarioActual => escenarioActual;
    public bool CargandoPartida => cargandoPartida;

    private bool cargandoPartida;
    private Coroutine rutinaRestauracionDatos;
    private string escenaEsperadaCarga;
    private bool cargaPendienteDesdeMenu;

    public bool CargaPendiente =>
        cargaPendienteDesdeMenu || cargandoPartida;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += AlCargarEscena;
    }

  private void OnDestroy()
{
    SceneManager.sceneLoaded -= AlCargarEscena;

    if (Instance == this)
        Instance = null;
}

    // =====================================================
    // REGISTRAR ZONA SEGURA
    // =====================================================

    public void RegistrarZonaSegura(EscenarioCheckpoint escenario)
    {
        Debug.Log(
            "[CheckpointManager] Se llamó a RegistrarZonaSegura: " +
            escenario
        );

        if (!EsModoHistoria())
        {
            Debug.LogWarning(
                "[CheckpointManager] No se guardó: el modo de juego no es Historia."
            );
            return;
        }

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning(
                "[CheckpointManager] No se guardó: este cliente no es el servidor."
            );
            return;
        }

        if (escenario == EscenarioCheckpoint.Ninguno)
        {
            Debug.LogWarning(
                "[CheckpointManager] No se guardó: checkpoint inválido."
            );
            return;
        }

        if (escenario < escenarioActual)
        {
            Debug.LogWarning(
                "[CheckpointManager] No se guardó: el checkpoint es anterior al actual."
            );
            return;
        }

        escenarioActual = escenario;

        Debug.Log(
            "[CheckpointManager] Zona segura alcanzada: " +
            escenarioActual
        );

        GuardarCheckpoint();
    }

    // =====================================================
    // GUARDAR CHECKPOINT
    // =====================================================

    public void GuardarCheckpoint()
    {
        if (!EsModoHistoria())
            return;

        if (escenarioActual == EscenarioCheckpoint.Ninguno)
        {
            Debug.LogWarning(
                "[CheckpointManager] No se puede guardar: todavía no se alcanzó una zona segura."
            );
            return;
        }

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        HistoriaSaveData datosHistoria =
            SaveSystem.CargarHistoria();

        if (datosHistoria == null)
            datosHistoria = new HistoriaSaveData();

        if (datosHistoria.jugador == null)
            datosHistoria.jugador = new DatosJugadorSaveData();

        DatosJugadorSaveData datosJugador =
            datosHistoria.jugador;

        // Guardar vida, blindaje e inventario.
        PlayerHealth playerHealth =
            FindAnyObjectByType<PlayerHealth>();

        if (playerHealth != null && playerHealth.IsSpawned)
        {
            playerHealth.GuardarDatosJugador(datosJugador);
        }
        else
        {
            Debug.LogWarning(
                "[CheckpointManager] No se encontró un PlayerHealth inicializado."
            );
        }

        // Guardar recursos.
        PlayerRecursos playerRecursos =
            FindAnyObjectByType<PlayerRecursos>();

        if (playerRecursos != null && playerRecursos.IsSpawned)
        {
            playerRecursos.GuardarDatosRecursos(datosJugador);
        }
        else
        {
            Debug.LogWarning(
                "[CheckpointManager] No se encontró un PlayerRecursos inicializado."
            );
        }

        // Guardar armas y accesorios.
        WeaponSwitcher weaponSwitcher =
            FindAnyObjectByType<WeaponSwitcher>();

        if (weaponSwitcher != null && weaponSwitcher.IsInitialized)
        {
            datosJugador.armas =
                weaponSwitcher.ObtenerDatosArmas();

            datosJugador.armaEquipada =
                weaponSwitcher.ObtenerArmaEquipadaId();

            Debug.Log(
                "[CheckpointManager] Armas guardadas: " +
                datosJugador.armas.Count +
                " | Equipada: " +
                datosJugador.armaEquipada
            );
        }
        else
        {
            Debug.LogWarning(
                "[CheckpointManager] No se encontró un WeaponSwitcher inicializado. " +
                "Se conservarán los datos anteriores."
            );
        }

        // Guardar puntuación y estadísticas.
        PlayerScore playerScore =
            FindAnyObjectByType<PlayerScore>();

        if (playerScore != null && playerScore.IsSpawned)
        {
            playerScore.GuardarDatosPuntuacion(datosJugador);
        }
        else
        {
            Debug.LogWarning(
                "[CheckpointManager] No se encontró un PlayerScore inicializado."
            );
        }

        // Guardar información específica de Historia.
        datosHistoria.checkpoint = (int)escenarioActual;

        if (ConfiguracionPartidaSeleccionada.Instancia != null)
        {
            datosHistoria.dificultad =
                (int)ConfiguracionPartidaSeleccionada.Instancia.dificultadSeleccionada;
        }

        if (SaveSystem.GuardarHistoria(datosHistoria))
        {
            Debug.Log(
                "[CheckpointManager] Checkpoint guardado correctamente: " +
                escenarioActual
            );
        }
        else
        {
            Debug.LogError(
                "[CheckpointManager] No se pudo guardar el checkpoint."
            );
        }
    }

    // =====================================================
    // COMPROBAR GUARDADO
    // =====================================================

    public bool ExisteCheckpoint()
    {
        HistoriaSaveData datos =
            SaveSystem.CargarHistoria();

        if (datos == null)
            return false;

        return datos.checkpoint ==
                   (int)EscenarioCheckpoint.SafeZone1 ||
               datos.checkpoint ==
                   (int)EscenarioCheckpoint.SafeZone2;
    }

    public HistoriaSaveData ObtenerDatosCheckpoint()
    {
        if (!ExisteCheckpoint())
            return null;

        return SaveSystem.CargarHistoria();
    }

    // =====================================================
    // CARGAR CHECKPOINT
    // =====================================================

    public void CargarCheckpoint()
    {
        Debug.Log(
            "[Checkpoint] CargarCheckpoint() ejecutado."
        );

        if (cargandoPartida)
        {
            Debug.LogWarning(
                "[Checkpoint] Ya hay una carga en progreso."
            );
            return;
        }

        if (!EsModoHistoria())
        {
            Debug.LogWarning(
                "[CheckpointManager] Solo se pueden cargar checkpoints en modo Historia."
            );
            return;
        }

        HistoriaSaveData datos = ObtenerDatosCheckpoint();

        if (datos == null)
        {
            Debug.LogWarning(
                "[CheckpointManager] No existe un checkpoint válido para cargar."
            );
            return;
        }

        if (datos.jugador == null)
        {
            Debug.LogError(
                "[CheckpointManager] El guardado no contiene datos del jugador."
            );
            return;
        }

        if (datos.checkpoint != (int)EscenarioCheckpoint.SafeZone1 &&
            datos.checkpoint != (int)EscenarioCheckpoint.SafeZone2)
        {
            Debug.LogError(
                "[CheckpointManager] El valor del checkpoint guardado no es válido: " +
                datos.checkpoint
            );
            return;
        }

        escenarioActual =
            (EscenarioCheckpoint)datos.checkpoint;

        cargandoPartida = true;

        Debug.Log(
            "[CheckpointManager] Preparando carga desde: " +
            escenarioActual
        );

        if (rutinaRestauracionDatos != null)
        {
            StopCoroutine(rutinaRestauracionDatos);
            rutinaRestauracionDatos = null;
        }

        rutinaRestauracionDatos =
            StartCoroutine(
                RestaurarDatosCuandoJugadorEsteListo(datos)
            );
    }

    // =====================================================
    // ESPERAR AL JUGADOR Y RESTAURAR
    // =====================================================

    private IEnumerator RestaurarDatosCuandoJugadorEsteListo(
        HistoriaSaveData datos)
    {
        Debug.Log(
            "[Checkpoint] Iniciando espera para restaurar datos."
        );

        float tiempoEspera = 0f;
        const float tiempoMaximo = 30f;

        WeaponSwitcher weaponSwitcher = null;
        PlayerHealth playerHealth = null;
        PlayerRecursos playerRecursos = null;
        PlayerScore playerScore = null;
        GestorEscenariosPartida gestorEscenarios = null;
        GestorHistoriaPartida gestorHistoria = null;

        while (tiempoEspera < tiempoMaximo)
        {
            if (weaponSwitcher == null || !weaponSwitcher.IsInitialized)
            {
                weaponSwitcher =
                    FindAnyObjectByType<WeaponSwitcher>();
            }

            if (playerHealth == null || !playerHealth.IsSpawned)
            {
                playerHealth =
                    FindAnyObjectByType<PlayerHealth>();
            }

            if (playerRecursos == null || !playerRecursos.IsSpawned)
            {
                playerRecursos =
                    FindAnyObjectByType<PlayerRecursos>();
            }

            if (playerScore == null || !playerScore.IsSpawned)
            {
                playerScore =
                    FindAnyObjectByType<PlayerScore>();
            }

            if (gestorEscenarios == null)
            {
                gestorEscenarios =
                    FindAnyObjectByType<GestorEscenariosPartida>();
            }

            if (gestorHistoria == null || !gestorHistoria.IsSpawned)
            {
                gestorHistoria =
                    FindAnyObjectByType<GestorHistoriaPartida>();
            }

            bool sistemasListos =
                weaponSwitcher != null &&
                weaponSwitcher.IsInitialized &&
                playerHealth != null &&
                playerHealth.IsSpawned &&
                playerRecursos != null &&
                playerRecursos.IsSpawned &&
                playerScore != null &&
                playerScore.IsSpawned &&
                gestorEscenarios != null &&
                gestorHistoria != null &&
                gestorHistoria.IsSpawned;

            if (sistemasListos)
            {
                Debug.Log(
                    "[Checkpoint] Todos los sistemas están listos. " +
                    "Preparando checkpoint: " + escenarioActual
                );

                break;
            }

            tiempoEspera += Time.unscaledDeltaTime;
            yield return null;
        }

        // Comprobar nuevamente que todos los sistemas estén listos.
        if (weaponSwitcher == null || !weaponSwitcher.IsInitialized ||
            playerHealth == null || !playerHealth.IsSpawned ||
            playerRecursos == null || !playerRecursos.IsSpawned ||
            playerScore == null || !playerScore.IsSpawned ||
            gestorEscenarios == null ||
            gestorHistoria == null || !gestorHistoria.IsSpawned)
        {
            Debug.LogError(
                "[CheckpointManager] No se pudieron inicializar todos los sistemas " +
                "necesarios después de " + tiempoMaximo + " segundos. " +
                "WeaponSwitcher: " + (weaponSwitcher != null && weaponSwitcher.IsInitialized) +
                " | PlayerHealth: " + (playerHealth != null && playerHealth.IsSpawned) +
                " | PlayerRecursos: " + (playerRecursos != null && playerRecursos.IsSpawned) +
                " | PlayerScore: " + (playerScore != null && playerScore.IsSpawned) +
                " | GestorEscenarios: " + (gestorEscenarios != null) +
                " | GestorHistoria: " + (gestorHistoria != null && gestorHistoria.IsSpawned)
            );

            FinalizarCarga();
            yield break;
        }

        if (datos == null || datos.jugador == null)
        {
            Debug.LogError(
                "[CheckpointManager] Los datos del jugador del checkpoint son nulos."
            );

            FinalizarCarga();
            yield break;
        }

        // Comprobar que la carga se ejecute desde el servidor.
        if (!gestorHistoria.IsServer)
        {
            Debug.LogError(
                "[CheckpointManager] La carga del checkpoint debe ejecutarse en el servidor."
            );

            FinalizarCarga();
            yield break;
        }

        DatosJugadorSaveData datosJugador = datos.jugador;

        // Activar la zona segura y trasladar al jugador.
        Transform jugador = playerHealth.transform.root;

        Debug.Log(
            "[Checkpoint] Antes de preparar zona: " +
            escenarioActual +
            " | Posición jugador: " +
            jugador.position
        );

        bool zonaPreparada =
            gestorEscenarios.PrepararCheckpoint(
                escenarioActual,
                jugador
            );

        Debug.Log(
            "[Checkpoint] Después de preparar zona: " +
            escenarioActual +
            " | Resultado: " + zonaPreparada +
            " | Posición jugador: " +
            jugador.position
        );

        if (!zonaPreparada)
        {
            Debug.LogError(
                "[CheckpointManager] No se pudo preparar la zona segura: " +
                escenarioActual
            );

            FinalizarCarga();
            yield break;
        }

        // Restaurar el estado de la historia.
        gestorHistoria.RestaurarDesdeCheckpoint(escenarioActual);
        RoundManager roundManager =
            FindAnyObjectByType<RoundManager>();

        if (roundManager != null)
        {
            roundManager.PrepararHistoriaDesdeCheckpoint();
        }
        else
        {
            Debug.LogError(
                "[CheckpointManager] No se encontró RoundManager " +
                "para preparar la Historia desde el checkpoint."
            );
        }

        // RESTAURAR ARMAS Y ACCESORIOS.
        bool restauracionArmasCorrecta =
            weaponSwitcher.RestaurarArmas(
                datosJugador.armas,
                datosJugador.armaEquipada
            );

        if (restauracionArmasCorrecta)
        {
            Debug.Log(
                "[CheckpointManager] Armas y accesorios restaurados."
            );
        }
        else
        {
            Debug.LogError(
                "[CheckpointManager] No se pudieron restaurar las armas."
            );
        }

        // RESTAURAR VIDA, BLINDAJE E INVENTARIO.
        if (playerHealth.IsServer)
        {
            playerHealth.RestaurarDatosJugador(datosJugador);

            Debug.Log(
                "[CheckpointManager] Vida, blindaje e inventario restaurados."
            );
        }

        // RESTAURAR RECURSOS.
        if (playerRecursos.IsServer)
        {
            playerRecursos.RestaurarDatosRecursos(datosJugador);

            Debug.Log(
                "[CheckpointManager] Recursos restaurados."
            );
        }

        // RESTAURAR PUNTUACIÓN Y ESTADÍSTICAS.
        if (playerScore.IsServer)
        {
            playerScore.RestaurarDatosPuntuacion(datosJugador);

            Debug.Log(
                "[CheckpointManager] Puntuación y estadísticas restauradas."
            );
        }

        Debug.Log(
            "[CheckpointManager] Carga completada desde " +
            escenarioActual
        );

        FinalizarCarga();
    }

    // =====================================================
    // FINALIZAR CARGA
    // =====================================================

  
public void FinalizarCarga()
{
    cargandoPartida = false;
    cargaPendienteDesdeMenu = false;
    escenaEsperadaCarga = null;
    rutinaRestauracionDatos = null;

    LoadGameMenuController.FinalizarCargaDesdeMenu();

    Debug.Log(
        "[CheckpointManager] Proceso de carga finalizado."
    );
}


    // =====================================================
    // UTILIDADES
    // =====================================================

    private bool EsModoHistoria()
    {
        return ConfiguracionPartidaSeleccionada.Instancia != null &&
               ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado ==
               ConfiguracionPartidaSeleccionada.ModoJuego.Historia;
    }

    // =====================================================
// PREPARAR CARGA DESDE EL MENÚ
// =====================================================

public void PrepararCargaDesdeMenu(string nombreEscena)
{
    if (string.IsNullOrEmpty(nombreEscena))
    {
        Debug.LogError(
            "[CheckpointManager] El nombre de la escena está vacío."
        );
        return;
    }

    escenaEsperadaCarga = nombreEscena;
    cargaPendienteDesdeMenu = true;

    Debug.Log(
        "[CheckpointManager] Carga pendiente preparada para: " +
        nombreEscena
    );
}

// =====================================================
// DETECTAR CARGA DE ESCENA
// =====================================================

private void AlCargarEscena(Scene escena, LoadSceneMode modo)
{
    if (!cargaPendienteDesdeMenu)
        return;

    if (escena.name != escenaEsperadaCarga)
        return;

    Debug.Log(
        "[CheckpointManager] Escena de juego cargada. " +
        "Iniciando restauración del checkpoint."
    );

    escenaEsperadaCarga = null;

    CargarCheckpoint();
}
public void PrepararNuevaPartida()
{
    if (rutinaRestauracionDatos != null)
    {
        StopCoroutine(rutinaRestauracionDatos);
        rutinaRestauracionDatos = null;
    }

    escenarioActual = EscenarioCheckpoint.Ninguno;

    cargandoPartida = false;
    cargaPendienteDesdeMenu = false;
    escenaEsperadaCarga = null;

    Debug.Log(
        "[CheckpointManager] Nueva partida preparada. " +
        "El progreso de checkpoints fue reiniciado."
    );
}
}
