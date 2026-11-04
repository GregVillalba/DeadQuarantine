
using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Administra el guardado y la restauración del modo Supervivencia.
/// Exclusivo para partidas singleplayer.
/// </summary>
public class SurvivalSaveManager : MonoBehaviour
{
    public static SurvivalSaveManager Instance { get; private set; }

    private bool restaurandoPartida;
    private bool restauracionEnCurso;
    private int ultimaRondaGuardada = -1;

    // =====================================================
    // INICIALIZACIÓN
    // =====================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =====================================================
    // VALIDACIONES
    // =====================================================

    private bool EsModoSupervivencia()
    {
        ConfiguracionPartidaSeleccionada configuracion =
            ConfiguracionPartidaSeleccionada.Instancia;

        return configuracion != null &&
               configuracion.modoSeleccionado ==
               ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;
    }

    private bool EsSingleplayer()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsListening)
            return true;

        return networkManager.IsServer &&
               networkManager.ConnectedClientsIds.Count <= 1;
    }

    // =====================================================
    // GUARDAR PARTIDA
    // =====================================================

public void GuardarPartida(int rondaActual)
{
    if (!EsModoSupervivencia())
        return;

    if (!EsSingleplayer())
        return;

    if (rondaActual < 5 || rondaActual % 5 != 0)
        return;

    if (ultimaRondaGuardada == rondaActual)
        return;

    DatosJugadorSaveData jugador = ObtenerDatosJugador();

    if (jugador == null)
    {
        Debug.LogError(
            "[SurvivalSaveManager] No se pudieron obtener los datos del jugador."
        );
        return;
    }

    SupervivenciaSaveData datos =
        SaveSystem.CargarSupervivencia();

    if (datos == null)
        datos = new SupervivenciaSaveData();

    datos.escenario =
        (int)ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado;

    datos.dificultad =
        (int)ConfiguracionPartidaSeleccionada.Instancia.dificultadSeleccionada;

    datos.rondaActual = rondaActual;
    datos.partidaPerdida = false;
    datos.jugador = jugador;
    datos.fechaGuardado = System.DateTime.Now.ToString("o");

    SaveSystem.GuardarSupervivencia(datos);

    ultimaRondaGuardada = rondaActual;

    Debug.Log(
        "[SurvivalSaveManager] Partida guardada correctamente. Ronda: "
        + rondaActual
    );
}
    // =====================================================
    // CAPTURAR DATOS DEL JUGADOR
    // =====================================================

    private DatosJugadorSaveData ObtenerDatosJugador()
    {
        PlayerHealth salud =
            FindAnyObjectByType<PlayerHealth>();

        PlayerRecursos recursos =
            FindAnyObjectByType<PlayerRecursos>();

        WeaponSwitcher armas =
            FindAnyObjectByType<WeaponSwitcher>();

        PlayerScore puntuacion =
            FindAnyObjectByType<PlayerScore>();

        if (salud == null ||
            recursos == null ||
            armas == null ||
            puntuacion == null)
        {
            Debug.LogError(
                "[SurvivalSaveManager] Faltan componentes del jugador."
            );
            return null;
        }

        DatosJugadorSaveData datos =
            new DatosJugadorSaveData();

        salud.GuardarDatosJugador(datos);
        recursos.GuardarDatosRecursos(datos);
        puntuacion.GuardarDatosPuntuacion(datos);

        datos.armas = armas.ObtenerDatosArmas();
        datos.armaEquipada = armas.ObtenerArmaEquipadaId();

        return datos;
    }

    // =====================================================
    // RESTAURAR PARTIDA
    // =====================================================
public void RestaurarPartida(
    SupervivenciaSaveData datos,
    System.Action onCompleted = null)
{
    if (datos == null)
    {
        Debug.LogError(
            "[SurvivalSaveManager] No hay datos para restaurar."
        );
        return;
    }

    if (restauracionEnCurso)
        return;

    StartCoroutine(
        RestaurarDatosCuandoJugadorEsteListo(datos, onCompleted)
    );
}

private IEnumerator RestaurarDatosCuandoJugadorEsteListo(
    SupervivenciaSaveData datos,
    System.Action onCompleted)
{
    restauracionEnCurso = true;

    PlayerHealth salud = null;
    PlayerRecursos recursos = null;
    PlayerScore puntuacion = null;
    WeaponSwitcher armas = null;

    float tiempoEspera = 0f;
    const float tiempoMaximoEspera = 30f;

    while (tiempoEspera < tiempoMaximoEspera)
    {
        salud = FindAnyObjectByType<PlayerHealth>();
        recursos = FindAnyObjectByType<PlayerRecursos>();
        puntuacion = FindAnyObjectByType<PlayerScore>();
        armas = FindAnyObjectByType<WeaponSwitcher>();

        if (salud != null &&
            recursos != null &&
            puntuacion != null &&
            armas != null)
        {
            break;
        }

        tiempoEspera += Time.deltaTime;
        yield return null;
    }

    if (salud == null ||
        recursos == null ||
        puntuacion == null ||
        armas == null)
    {
        Debug.LogError(
            "[SurvivalSaveManager] No se pudieron encontrar todos "
            + "los componentes del jugador para restaurar la partida."
        );

        restauracionEnCurso = false;
        yield break;
    }

    DatosJugadorSaveData jugador = datos.jugador;

    if (jugador == null)
    {
        Debug.LogError(
            "[SurvivalSaveManager] El guardado no contiene datos del jugador."
        );

        restauracionEnCurso = false;
        yield break;
    }

    salud.RestaurarDatosJugador(jugador);
    recursos.RestaurarDatosRecursos(jugador);
    puntuacion.RestaurarDatosPuntuacion(jugador);

    armas.RestaurarArmas(
        jugador.armas,
        jugador.armaEquipada
    );

    // Evita volver a guardar la misma ronda al cargarla.
    ultimaRondaGuardada = datos.rondaActual;

    Debug.Log(
        "[SurvivalSaveManager] Datos restaurados. Ronda: "
        + datos.rondaActual
    );

    restauracionEnCurso = false;

    onCompleted?.Invoke();
}
    public bool ExistePartidaGuardada()
    {
        return SaveSystem.ExisteGuardadoSupervivencia();
    }

    public SupervivenciaSaveData ObtenerPartidaGuardada()
    {
        return SaveSystem.CargarSupervivencia();
    }

    // =====================================================
    // ELIMINAR GUARDADO
    // =====================================================

    public void EliminarPartidaGuardada()
    {
        SaveSystem.EliminarGuardadoSupervivencia();

        Debug.Log(
            "[SurvivalSaveManager] Guardado de Supervivencia eliminado."
        );
    }
}
