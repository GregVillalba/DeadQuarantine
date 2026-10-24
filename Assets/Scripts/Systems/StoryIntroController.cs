using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

[System.Serializable]
public struct DatosHistoria
{
    public string subtitulo;
    public string titulo;
    public string descripcionConObjetivo;

    public DatosHistoria(string subtitulo, string titulo, string descripcionConObjetivo)
    {
        this.subtitulo = subtitulo;
        this.titulo = titulo;
        this.descripcionConObjetivo = descripcionConObjetivo;
    }
}

public class StoryIntroController : NetworkBehaviour
{
    [Header("Panel Principal")]
    [SerializeField] private GameObject popupHistoria; // Asignar el GameObject 'popUpHistoria'

    [Header("Campos de Texto (TextMeshPro)")]
    [SerializeField] private TextMeshProUGUI textoSubtitulo;   // Asignar 'chekpoint}'
    [SerializeField] private TextMeshProUGUI textoTitulo;      // Asignar 'tit'
    [SerializeField] private TextMeshProUGUI textoDescripcion; // Asignar 'desc+obj'

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

    private bool historiaActiva;
    public bool HistoriaActiva => historiaActiva;

    private void Awake()
    {
        if (popupHistoria != null)
            popupHistoria.SetActive(false);

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerLook == null)
            playerLook = GetComponentInChildren<PlayerLook>(true);
    }

    private void Update()
    {
        if (!IsOwner || Keyboard.current == null)
            return;

        // Con la tienda del mercader (o la pausa) abierta el aviso se oculta y ENTER no avanza.
        if (esperandoContinuarSafeZone && popupContinuarSafeZone != null)
            popupContinuarSafeZone.SetActive(!PlayerInputGate.ActionsBlocked);

        if (PlayerInputGate.ActionsBlocked)
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

    public void OcultarAvisoContinuarSafeZone()
    {
        if (!IsOwner)
            return;

        esperandoContinuarSafeZone = false;

        if (popupContinuarSafeZone != null)
            popupContinuarSafeZone.SetActive(false);
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

                // Los que quedaron en el laboratorio no pasan a la Ciudad
                // ni ocupan lugar en el máximo de zombies vivos.
                RoundManager.Instance.EliminarZombiesHistoria();

                // En la zona segura no aparecían zombies: vuelven en la Ciudad.
                RoundManager.Instance.ReanudarSpawnsHistoria();
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

        // Cargar los datos de historia para Ciudad
        DatosHistoria datos = ObtenerDatosHistoria(
            ConfiguracionPartidaSeleccionada.ModoJuego.Historia,
            ConfiguracionPartidaSeleccionada.Escenario.Ciudad
        );

        if (textoSubtitulo != null)
        {
            textoSubtitulo.text = datos.subtitulo;
            textoSubtitulo.gameObject.SetActive(!string.IsNullOrEmpty(datos.subtitulo));
        }

        if (textoTitulo != null)
            textoTitulo.text = datos.titulo;

        if (textoDescripcion != null)
            textoDescripcion.text = datos.descripcionConObjetivo;

        if (popupHistoria != null)
        {
            Transform current = popupHistoria.transform.parent;
            while (current != null)
            {
                current.gameObject.SetActive(true);
                current = current.parent;
            }

            popupHistoria.SetActive(true);
        }

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

        // 1. Obtener Modo y Escenario
        ConfiguracionPartidaSeleccionada.ModoJuego modo = ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5;
        ConfiguracionPartidaSeleccionada.Escenario escenario = ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;

        if (ConfiguracionPartidaSeleccionada.Instancia != null)
        {
            modo = ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado;
            escenario = ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado;
        }

        Debug.Log($"[StoryIntro] Modo: {modo} | Escenario: {escenario}");

        // 2. Obtener los textos correspondientes
        DatosHistoria datos = ObtenerDatosHistoria(modo, escenario);

        // 3. Asignar la información a la UI
        if (textoSubtitulo != null)
        {
            textoSubtitulo.text = datos.subtitulo;
            textoSubtitulo.gameObject.SetActive(!string.IsNullOrEmpty(datos.subtitulo));
        }

        if (textoTitulo != null)
            textoTitulo.text = datos.titulo;

        if (textoDescripcion != null)
            textoDescripcion.text = datos.descripcionConObjetivo;

        // 4. Asegurar que los padres estén encendidos y mostrar el popup
        if (popupHistoria != null)
        {
            Transform current = popupHistoria.transform.parent;
            while (current != null)
            {
                current.gameObject.SetActive(true);
                current = current.parent;
            }

            popupHistoria.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[StoryIntro] No está asignado el popupHistoria en el Inspector.");
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private DatosHistoria ObtenerDatosHistoria(
        ConfiguracionPartidaSeleccionada.ModoJuego modo,
        ConfiguracionPartidaSeleccionada.Escenario esc)
    {
        // ------------------------- MODO HISTORIA -------------------------
        if (modo == ConfiguracionPartidaSeleccionada.ModoJuego.Historia)
        {
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Laboratorio)
            {
                return new DatosHistoria(
                    "PUNTO DE PARTIDA",
                    "HISTORIA: ¿Cómo empezó todo?",
                    "Nos encontramos en el complejo donde ocurrió la brecha biológica: un patógeno experimental que muta violentamente a cualquiera que entre en contacto con él. Intentamos avanzar hacia la salida sin llamar la atención, pero el sistema de contención selló las instalaciones. Tu misión consiste en localizar la tarjeta de acceso principal para desbloquear la salida.\n\nOBJETIVO: Encontrar la tarjeta magnética y escapar del laboratorio."
                );
            }
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Ciudad)
            {
                return new DatosHistoria(
                    "CHECKPOINT",
                    "HISTORIA: La superficie",
                    "Tras lograr salir del laboratorio, el escenario exterior es desolador: las calles están tomadas y el perímetro urbano colapsó por completo. La única ruta de escape factible es aérea. Debemos cruzar el distrito hasta la torre de comunicaciones para emitir una señal de auxilio militar y coordinar la extracción con otros supervivientes.\n\nOBJETIVO: Alcanzar la antena de radio y solicitar apoyo de evacuación."
                );
            }
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Cabana)
            {
                return new DatosHistoria(
                    "ENCLAVE FINAL",
                    "HISTORIA: Punto de reunión",
                    "El convoy de evacuación se dispersó en los bosques periféricos. Mientras nos reagrupamos en una cabaña táctica, el mando informa de una amenaza crítica: un espécimen mutante alfa capaz de coordinar los ataques de la horda. Para asegurar el aterrizaje del rescate y frenar la propagación, el mutante debe ser neutralizado a toda costa.\n\nOBJETIVO: Proteger el perímetro y eliminar al infectado mutante."
                );
            }
        }

        // ------------------------- MODO RONDA 5 -------------------------
        if (modo == ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5)
        {
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Laboratorio)
            {
                return new DatosHistoria(
                    "",
                    "OPERACIÓN: Falla de contención",
                    "El laboratorio ha sido tomado por oleadas de sujetos infectados. Las lecturas confirman la presencia de un espécimen alfa mutado liderando las acometidas. Asegura la zona, gestiona tus municiones y resiste el embate de cinco asaltos antes de intentar cualquier repliegue.\n\nOBJETIVO: Sobrevivir a 5 oleadas y abatir al líder mutante."
                );
            }
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Ciudad)
            {
                return new DatosHistoria(
                    "",
                    "OPERACIÓN: Asalto urbano",
                    "Las barricadas de la ciudad no resistirán por mucho tiempo. Múltiples grupos de infectados avanzan por las avenidas coordinados por una variante mutada de gran resistencia. Debemos mantener la posición y limpiar la zona durante las próximas oleadas hostiles.\n\nOBJETIVO: Sobrevivir a 5 oleadas y eliminar la amenaza mutante."
                );
            }
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Cabana)
            {
                return new DatosHistoria(
                    "",
                    "OPERACIÓN: Aislamiento",
                    "El refugio en el bosque quedó rodeado por las hordas que emergieron de la espesura. Un enorme mutante está dirigiendo los ataques contra la estructura. Mantén la defensa del perímetro y contiene los cinco asaltos consecutivos para asegurar el sector.\n\nOBJETIVO: Sobrevivir a 5 oleadas y erradicar al mutante alfa."
                );
            }
        }

        // ------------------------- MODO SUPERVIVENCIA -------------------------
        if (modo == ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia)
        {
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Laboratorio)
            {
                return new DatosHistoria(
                    "",
                    "SUPERVIVENCIA: Zona cero",
                    "No hay vías de escape ni refuerzos en camino dentro del laboratorio subterráneo. El aire está cargado y las hordas continúan llegando sin descanso. La única directiva es aguantar en pie todo el tiempo que sea posible ante oleadas incesantes y especímenes mutantes.\n\nOBJETIVO: Resistir tantas oleadas de infectados como puedas."
                );
            }
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Ciudad)
            {
                return new DatosHistoria(
                    "",
                    "SUPERVIVENCIA: Ruinas urbanas",
                    "La metrópoli cayó por completo y las oleadas de infectados convergen hacia tu posición. En este punto la evacuación es imposible; solo queda resistir, administrar los recursos y contener los constantes ataques de la horda y sus mutaciones.\n\nOBJETIVO: Sobrevivir el mayor número de rondas posible."
                );
            }
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Cabana)
            {
                return new DatosHistoria(
                    "",
                    "SUPERVIVENCIA: Último bastión",
                    "La cabaña en lo profundo del bosque está completamente asediada. La noche avanza y los infectados emergen de la oscuridad en números incontables, comandados por mutantes que no darán tregua. Lucha hasta el último cartucho.\n\nOBJETIVO: Aguantar y resistir oleadas continuas sin límite."
                );
            }
        }

        // ------------------------- VALOR POR DEFECTO -------------------------
        return new DatosHistoria(
            "",
            "COMBATE TÁCTICO",
            "El área de despliegue ha sido comprometida por fuerzas hostiles no identificadas. Mantén la posición, neutraliza las amenazas en el sector y procura no caer.\n\nOBJETIVO: Sobrevivir a las oleadas enemigas."
        );
    }

    public void ContinuarHistoria()
    {
        Debug.Log("[StoryIntro] ContinuarHistoria() INICIO. IsOwner=" + IsOwner + " | historiaActiva=" + historiaActiva);

        if (!IsOwner || !historiaActiva)
            return;

        historiaActiva = false;

        if (popupHistoria != null)
            popupHistoria.SetActive(false);

        if (hud != null)
            hud.SetActive(true);

        HabilitarJugador();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        Debug.Log("[StoryIntro] ContinuarHistoria() FIN.");
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
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
            playerMovement.MovementLocked = false;
        }

        if (playerLook != null)
            playerLook.enabled = true;
    }
}