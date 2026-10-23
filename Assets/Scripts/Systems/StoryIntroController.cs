using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class StoryIntroController : NetworkBehaviour
{
    [Header("1. Modo Historia (por Escenario)")]
    [SerializeField] private GameObject popupHistoria_Laboratorio;
    [SerializeField] private GameObject popupHistoria_Ciudad;
    [SerializeField] private GameObject popupHistoria_Cabana;

    [Header("2. Modo Ronda 5 / Normal (por Escenario)")]
    [SerializeField] private GameObject popupRonda5_Laboratorio;
    [SerializeField] private GameObject popupRonda5_Ciudad;
    [SerializeField] private GameObject popupRonda5_Cabana;

    [Header("3. Modo Supervivencia / Infinitas (por Escenario)")]
    [SerializeField] private GameObject popupSupervivencia_Laboratorio;
    [SerializeField] private GameObject popupSupervivencia_Ciudad;
    [SerializeField] private GameObject popupSupervivencia_Cabana;

    [Header("HUD del jugador")]
    [SerializeField] private GameObject hud;

    [Header("Controles del jugador")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLook playerLook;

    [Header("Cámara del arma (FPS Arms)")]
    [SerializeField] private GameObject weaponCameraObject;
    [Header("Transición desde zona segura")]
    [SerializeField] private GameObject popupContinuarSafeZone;

    private bool esperandoContinuarSafeZone;
    private WeaponSwitcher weaponSwitcher;

    private GameObject popupActivoActual;
    private bool historiaActiva;
    public bool HistoriaActiva => historiaActiva;

    private void Awake()
    {
        ApagarTodosLosPopups();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerLook == null)
            playerLook = GetComponentInChildren<PlayerLook>(true);
    }

    private void Update()
{
    if (!IsOwner || Keyboard.current == null)
        return;

    if (!Keyboard.current.enterKey.wasPressedThisFrame &&
        !Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        return;

    if (esperandoContinuarSafeZone)
    {
        IniciarTransicionACiudad();
        return;
    }

    if (historiaActiva)
        ContinuarHistoria();
}

public void MostrarAvisoContinuarSafeZone()
{
    if (!IsOwner)
        return;

    esperandoContinuarSafeZone = true;

    if (popupContinuarSafeZone != null)
        popupContinuarSafeZone.SetActive(true);

    Debug.Log("[StoryIntro] Esperando Enter para continuar a Ciudad.");
}

private void IniciarTransicionACiudad()
{
    esperandoContinuarSafeZone = false;

    if (popupContinuarSafeZone != null)
        popupContinuarSafeZone.SetActive(false);

    GestorEscenariosPartida gestor = GestorEscenariosPartida.Instance;

    if (gestor == null)
    {
        Debug.LogError("[StoryIntro] No se encontró GestorEscenariosPartida.");
        return;
    }

    if (popupHistoria_Ciudad == null)
    {
        Debug.LogError("[StoryIntro] No está asignado popupHistoria_Ciudad.");
        return;
    }

    historiaActiva = true;

    if (hud != null)
        hud.SetActive(false);

    BloquearJugador();

    // Activar el escenario de Ciudad.
    gestor.ActivarEscenario(
        ConfiguracionPartidaSeleccionada.Escenario.Ciudad
    );

    // Actualizar los puntos de aparición para el escenario recién activado.
    if (IsServer)
    {
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.ActualizarSpawnPointsDelEscenario();
        }
        else
        {
            Debug.LogWarning(
                "[StoryIntro] No se encontró RoundManager para actualizar los spawn points."
            );
        }

        // Iniciar la misión de Ciudad y crear la radio.
        if (GestorHistoriaPartida.Instance != null)
        {
            GestorHistoriaPartida.Instance.IniciarMisionCiudad();
        }
        else
        {
            Debug.LogError(
                "[StoryIntro] No se encontró GestorHistoriaPartida."
            );
        }
    }

    // Trasladar al jugador al punto inicial de Ciudad.
    gestor.TrasladarJugadorAInicioDeEscenario(transform.root);

    // Mostrar la introducción de Ciudad.
    ApagarTodosLosPopups();

    popupActivoActual = popupHistoria_Ciudad;
    popupActivoActual.SetActive(true);

    Cursor.visible = true;
    Cursor.lockState = CursorLockMode.None;

    Debug.Log(
        "[StoryIntro] Ciudad activada. Introducción mostrada y transición completada."
    );
}

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        if (EsSinglePlayer())
            MostrarHistoriaInicial();
    }

    private bool EsSinglePlayer()
    {
        return SceneManager.GetActiveScene().name == "MainSceneSinglePlayer";
    }

    private void MostrarHistoriaInicial()
    {
        historiaActiva = true;

        if (hud != null)
            hud.SetActive(false);

        BloquearJugador();

        // 1. Obtener Modo y Escenario seleccionados en el menú
        ConfiguracionPartidaSeleccionada.ModoJuego modo = ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5;
        ConfiguracionPartidaSeleccionada.Escenario escenario = ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;

        if (ConfiguracionPartidaSeleccionada.Instancia != null)
        {
            modo = ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado;
            escenario = ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado;
        }

        Debug.Log($"[StoryIntro] Modo elegido: {modo} | Escenario elegido: {escenario}");

        // 2. Elegir la pantalla correcta entre las 9 combinaciones
        popupActivoActual = ObtenerPopupSegunConfiguracion(modo, escenario);

        // 3. Activar la pantalla seleccionada
        if (popupActivoActual != null)
        {
            popupActivoActual.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[StoryIntro] No está asignado el popup para este Modo y Escenario.");
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private GameObject ObtenerPopupSegunConfiguracion(
        ConfiguracionPartidaSeleccionada.ModoJuego modo,
        ConfiguracionPartidaSeleccionada.Escenario esc)
    {
        switch (modo)
        {
            case ConfiguracionPartidaSeleccionada.ModoJuego.Historia:
                if (esc == ConfiguracionPartidaSeleccionada.Escenario.Ciudad) return popupHistoria_Ciudad;
                if (esc == ConfiguracionPartidaSeleccionada.Escenario.Cabana) return popupHistoria_Cabana;
                return popupHistoria_Laboratorio;

            case ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5:
                if (esc == ConfiguracionPartidaSeleccionada.Escenario.Ciudad) return popupRonda5_Ciudad;
                if (esc == ConfiguracionPartidaSeleccionada.Escenario.Cabana) return popupRonda5_Cabana;
                return popupRonda5_Laboratorio;

            case ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia:
                if (esc == ConfiguracionPartidaSeleccionada.Escenario.Ciudad) return popupSupervivencia_Ciudad;
                if (esc == ConfiguracionPartidaSeleccionada.Escenario.Cabana) return popupSupervivencia_Cabana;
                return popupSupervivencia_Laboratorio;

            default:
                return popupRonda5_Laboratorio;
        }
    }

    /// <summary>
    /// Asignar este método en el OnClick() del botón Aceptar/Continuar de cada una de las 9 historias.
    /// </summary>
    public void ContinuarHistoria()
    {
        Debug.Log("[StoryIntro] ContinuarHistoria() INICIO. IsOwner=" + IsOwner + " | historiaActiva=" + historiaActiva);

        if (!IsOwner || !historiaActiva)
            return;

        historiaActiva = false;

        if (popupContinuarSafeZone != null)
        popupContinuarSafeZone.SetActive(false);

        esperandoContinuarSafeZone = false;

        // Ocultar el popup que estaba visible
        if (popupActivoActual != null)
        {
            popupActivoActual.SetActive(false);
            popupActivoActual = null;
        }
        else
        {
            ApagarTodosLosPopups();
        }

        if (hud != null)
            hud.SetActive(true);

        HabilitarJugador();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;


        Debug.Log("[StoryIntro] ContinuarHistoria() FIN.");
    }

    private void ApagarTodosLosPopups()
    {
        if (popupHistoria_Laboratorio != null) popupHistoria_Laboratorio.SetActive(false);
        if (popupHistoria_Ciudad != null) popupHistoria_Ciudad.SetActive(false);
        if (popupHistoria_Cabana != null) popupHistoria_Cabana.SetActive(false);

        if (popupRonda5_Laboratorio != null) popupRonda5_Laboratorio.SetActive(false);
        if (popupRonda5_Ciudad != null) popupRonda5_Ciudad.SetActive(false);
        if (popupRonda5_Cabana != null) popupRonda5_Cabana.SetActive(false);

        if (popupSupervivencia_Laboratorio != null) popupSupervivencia_Laboratorio.SetActive(false);
        if (popupSupervivencia_Ciudad != null) popupSupervivencia_Ciudad.SetActive(false);
        if (popupSupervivencia_Cabana != null) popupSupervivencia_Cabana.SetActive(false);
    }

    private void BloquearJugador()
    {
        if (weaponSwitcher == null)
            weaponSwitcher = transform.root.GetComponentInChildren<WeaponSwitcher>(true);

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
            playerMovement.MovementLocked = true;
        }

        if (playerLook != null)
            playerLook.enabled = false;

        foreach (Weapon arma in transform.root.GetComponentsInChildren<Weapon>(true))
        {
            arma.enabled = false;
            arma.InputLocked = true;
        }
    }

    private void HabilitarJugador()
    {
        if (weaponSwitcher == null)
            weaponSwitcher = transform.root.GetComponentInChildren<WeaponSwitcher>(true);

        Debug.Log("[StoryIntro] HabilitarJugador() INICIO. weaponSwitcher=" + (weaponSwitcher != null) +
            " | CurrentWeapon=" + (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null ? weaponSwitcher.CurrentWeapon.name : "NULL"));

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
            playerMovement.MovementLocked = false;
        }

        if (playerLook != null)
            playerLook.enabled = true;

        if (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null)
        {
            weaponSwitcher.CurrentWeapon.enabled = true;
            weaponSwitcher.CurrentWeapon.InputLocked = false;
            Debug.Log("[StoryIntro] Arma desbloqueada: " + weaponSwitcher.CurrentWeapon.name);
        }
        else
        {
            Debug.LogWarning("[StoryIntro] Seguimos sin encontrar weaponSwitcher ni con el lookup manual.");
        }
    }
}