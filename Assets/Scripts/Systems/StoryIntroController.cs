using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class StoryIntroController : NetworkBehaviour
{
    [Header("Panel Único de Historia")]
    [SerializeField] private GameObject panelHistoria;
    [SerializeField] private TextMeshProUGUI textoHeaderVerde; // "PUNTO DE PARTIDA", "CHECKPOINT!", etc.
    [SerializeField] private TextMeshProUGUI textoTitulo;      // "HISTORIA: ¿Cómo..."
    [SerializeField] private TextMeshProUGUI textoCuerpoCompleto; // Descripción + Objetivo juntos
    [SerializeField] private Button botonContinuar;

    [Header("HUD del jugador")]
    [SerializeField] private GameObject hud;

    [Header("Controles del jugador")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLook playerLook;

    [Header("Cámara del arma (FPS Arms)")]
    [SerializeField] private GameObject weaponCameraObject;
    private WeaponSwitcher weaponSwitcher;

    private bool historiaActiva;
    public bool HistoriaActiva => historiaActiva;

    private struct DatosHistoria
    {
        public string headerVerde;
        public string titulo;
        public string cuerpo; // Incluye descripción y objetivo

        public DatosHistoria(string header, string tit, string cuerpoTexto)
        {
            headerVerde = header;
            titulo = tit;
            cuerpo = cuerpoTexto;
        }
    }

    private void Awake()
    {
        if (panelHistoria != null)
            panelHistoria.SetActive(false);

        if (botonContinuar != null)
        {
            botonContinuar.onClick.RemoveListener(ContinuarHistoria);
            botonContinuar.onClick.AddListener(ContinuarHistoria);
        }

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerLook == null)
            playerLook = GetComponentInChildren<PlayerLook>(true);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner && !EsSinglePlayer())
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

        ConfiguracionPartidaSeleccionada.ModoJuego modo = ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5;
        ConfiguracionPartidaSeleccionada.Escenario escenario = ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;

        if (ConfiguracionPartidaSeleccionada.Instancia != null)
        {
            modo = ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado;
            escenario = ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado;
        }

        Debug.Log($"[StoryIntro] Modo elegido: {modo} | Escenario elegido: {escenario}");

        DatosHistoria datos = ObtenerDatosHistoria(modo, escenario);

        if (textoHeaderVerde != null)
        {
            if (string.IsNullOrEmpty(datos.headerVerde))
            {
                textoHeaderVerde.gameObject.SetActive(false);
            }
            else
            {
                textoHeaderVerde.gameObject.SetActive(true);
                textoHeaderVerde.text = datos.headerVerde;
            }
        }

        if (textoTitulo != null)
            textoTitulo.text = datos.titulo;

        if (textoCuerpoCompleto != null)
            textoCuerpoCompleto.text = datos.cuerpo;

        if (panelHistoria != null)
            panelHistoria.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    private void LateUpdate()
    {
        if (!historiaActiva) return;

        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible) Cursor.visible = true;
    }
    private void Update()
    {
        if (!historiaActiva) return;

        // Detectar espacio o enter para continuar automáticamente
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame ||
             UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame))
        {
            ContinuarHistoria();
        }
#else
    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
    {
        ContinuarHistoria();
    }
#endif
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
                    "HISTORIA: ¿Cómo empezo todo?",
                    "Nos encontramos en el laboratorio donde todo empezó: un virus desatado que transforma a todo aquel que lo inhala. Avanzamos sigilosamente hacia la salida, tratando de no alertar a nadie. Sin embargo, como era de esperarse, la puerta está bloqueada. Tu misión será encontrar la tarjeta magnética de la puerta principal.\n\nOBJETIVO: Encontrar la tarjeta y escapar."
                );
            }
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Ciudad)
            {
                return new DatosHistoria(
                    "CHECKPOINT!",
                    "HISTORIA: ¿Cómo terminamos aqui?",
                    "Tras escapar del laboratorio hacia una ciudad infestada, el objetivo es llegar a la antena de radio para pedir auxilio, ya que la única salida viable es por aire. Abriéndose paso entre los infectados y tras una breve parada táctica, logran entrar a la estación junto a otros refugiados para resistir hasta la evacuación militar.\n\nOBJETIVO: Comunicarte con otros sobrevivientes."
                );
            }
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Cabana)
            {
                return new DatosHistoria(
                    "CHECKPOINT!",
                    "HISTORIA: ¿Cómo terminamos aqui?",
                    "El grupo de refugiados fue dividido en distintos puntos seguros del bosque. Nos ordenaron mantener la calma, aunque nos advirtieron de un infectado mutante enorme capaz de comandar oleadas de infectados que atacan bases militares en grupo. Ese mutante es la prioridad máxima, en caso de verlo hay que eliminarlo para acabar con esta pesadilla.\n\nOBJETIVO: Acabar con el zombie mutante."
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
                    "HISTORIA: ¿Cómo salimos de esta?",
                    "Nos encontramos en el laboratorio donde todo empezó: un virus desatado que transforma a todo aquel que lo tenga una mordida zombie. Debemos escapar de esta instalación antes de que los infectados nos conviertan. Tenemos que acabar con todos, incluyendo a Billy, un infectado mutante enorme capaz de comandar oleadas de infectados.\n\nOBJETIVO: Acabar con las hordas y zombie mutante."
                );
            }
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Ciudad)
            {
                return new DatosHistoria(
                    "",
                    "HISTORIA: ¿Cómo seguimos?",
                    "Estamos ante un panorama aterrador: el virus se ha propagado por la ciudad. Nos ordenaron mantener la calma, aunque nos advirtieron de un infectado mutante enorme capaz de comandar oleadas de infectados que atacan bases militares en grupo. Ese mutante es la prioridad máxima, en caso de verlo hay que eliminarlo para acabar con esta pesadilla.\n\nOBJETIVO: Acabar con las hordas y zombie mutante."
                );
            }
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Cabana)
            {
                return new DatosHistoria(
                    "",
                    "HISTORIA: ¿Cómo terminamos aqui?",
                    "El grupo de refugiados fue dividido en distintos puntos seguros del bosque. Nos ordenaron mantener la calma, aunque nos advirtieron de un infectado mutante enorme capaz de comandar oleadas de infectados que atacan bases militares en grupo. Ese mutante es la prioridad máxima, en caso de verlo hay que eliminarlo para acabar con esta pesadilla.\n\nOBJETIVO: Acabar con las hordas y zombie mutante."
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
                    "HISTORIA: ¿Cómo salimos de esta?",
                    "Nos encontramos en el laboratorio donde todo empezó: un virus desatado que transforma a todo aquel que lo tenga una mordida zombie. Tenemos que aguantar lo maximo posible, acabar con hordas y mutantes.\n\nOBJETIVO: Acabar con las hordas y zombies mutantes."
                );
            }
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Ciudad)
            {
                return new DatosHistoria(
                    "",
                    "HISTORIA: ¿Cómo seguimos?",
                    "Estamos ante un panorama aterrador: el virus se ha propagado por la ciudad. Nos ordenaron mantener la calma y sobrevivir lo maximo posible, aunque nos advirtieron de un infectado mutante enorme capaz de comandar oleadas de infectados que atacan bases militares en grupo.\n\nOBJETIVO: Sobrevivir a los mutantes y hordas."
                );
            }
            if (esc == ConfiguracionPartidaSeleccionada.Escenario.Cabana)
            {
                return new DatosHistoria(
                    "",
                    "HISTORIA: ¿Cómo terminamos aqui?",
                    "El grupo de refugiados fue dividido en distintos puntos seguros del bosque. Nos ordenaron mantener la calma y sobrevivir lo mas que podamos, nos advirtieron de un infectado mutante enorme capaz de comandar oleadas de infectados que atacan bases militares en grupo.\n\nOBJETIVO: Sobrevivir lo maximo posible."
                );
            }
        }

        return new DatosHistoria(
            "",
            "HISTORIA: ¿Cómo salimos de esta?",
            "Nos encontramos en el laboratorio donde todo empezó: un virus desatado.\n\nOBJETIVO: Sobrevivir a las oleadas enemigas."
        );
    }

    public void ContinuarHistoria()
    {
        Debug.Log($"[StoryIntro] Botón presionado. Estado previo: historiaActiva={historiaActiva}, IsOwner={IsOwner}");

        // Desactivamos la bandera
        historiaActiva = false;

        // 1. Apagar panel de historia
        if (panelHistoria != null)
            panelHistoria.SetActive(false);

        // 2. Reactivar HUD
        if (hud != null)
            hud.SetActive(true);

        // 3. Reactivar movimiento, cámara y armas del jugador
        HabilitarJugador();

        // 4. Bloquear y ocultar cursor para jugar
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // 5. Iniciar la ronda si corresponde
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.StartRound(1);
        }

        Debug.Log("[StoryIntro] ¡Jugador desbloqueado con éxito!");
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
        }
    }
}