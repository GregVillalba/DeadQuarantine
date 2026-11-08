using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class DinoPlayer : MonoBehaviour
{
    [Header("Salto")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float checkRadius = 0.2f;

    [Header("UI / Inicio")]
    [Tooltip("Objeto que contiene SOLO el texto 'Presiona Espacio para comenzar'. No puede contener al Game Over.")]
    [SerializeField] private GameObject startInstructionsUI;

    [Header("UI / Game Over")]
    [Tooltip("Imagen de GAME OVER (RawImage_gameover).")]
    [SerializeField] private GameObject gameOverCanvas;
    [Tooltip("Imagen con componente Button para reiniciar. La secuencia al presionarla la maneja RestartUIRawImage; acá solo se muestra al perder.")]
    [SerializeField] private Button botonReiniciar;
    [Tooltip("Imagen 'presionado' (RawImage_again_presed). Acá solo se oculta al empezar; RestartUIRawImage la muestra al presionar.")]
    [SerializeField] private GameObject imagenReiniciando;

    [Header("Puntaje")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private int puntajeMetaPremio = 100;

    private int score = 0;
    private bool yaOtorgoPremio = false;

    [HideInInspector] public bool isGameStarted = false;
    [HideInInspector] public bool isGameOver = false;

    private Rigidbody2D rb;
    private bool isGrounded = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        rb.gravityScale = 4.2f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (GetComponent<Collider2D>() == null)
        {
            gameObject.AddComponent<BoxCollider2D>();
        }

        if (gameOverCanvas == null)
        {
            Debug.LogWarning("[DinoPlayer] 'Game Over Canvas' no está asignado: la pantalla de Game Over no se va a poder ocultar ni mostrar.", this);
        }

        if (botonReiniciar == null && gameOverCanvas != null)
        {
            botonReiniciar = gameOverCanvas.GetComponentInChildren<Button>(true);
        }

        if (startInstructionsUI != null) startInstructionsUI.SetActive(true);
        MostrarGameOver(false);
        if (imagenReiniciando != null) imagenReiniciando.SetActive(false);

        Debug.Log($"[DinoPlayer] Campos de UI -> Start: {NombreDe(startInstructionsUI)} | GameOver: {NombreDe(gameOverCanvas)} | Botón: {(botonReiniciar != null ? botonReiniciar.name : "(vacío)")} | Imagen reinicio: {NombreDe(imagenReiniciando)}", this);

        if (startInstructionsUI != null && gameOverCanvas != null &&
            gameOverCanvas.transform.IsChildOf(startInstructionsUI.transform))
        {
            Debug.LogError($"[DinoPlayer] 'Start Instructions UI' ({startInstructionsUI.name}) CONTIENE al Game Over ({gameOverCanvas.name}): al empezar se oculta junto con las instrucciones. Asigná en 'Start Instructions UI' solo el objeto con el texto 'Presiona espacio para comenzar'.", this);
        }

        ActualizarTextoPuntaje();
    }

    private void Start()
    {
        // Por si otro objeto de la escena dejó el Game Over visible al cargar
        if (!isGameOver)
        {
            MostrarGameOver(false);
        }
    }

    private void Update()
    {
        bool spacePressed = false;
        bool spaceReleased = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            spacePressed = Keyboard.current.spaceKey.wasPressedThisFrame;
            spaceReleased = Keyboard.current.spaceKey.wasReleasedThisFrame;
        }
#else
        spacePressed = Input.GetKeyDown(KeyCode.Space);
        spaceReleased = Input.GetKeyUp(KeyCode.Space);
#endif

        // Esc: salir del minijuego y volver al juego principal
        bool escPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            escPressed = Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame;
        }
#else
        escPressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E);
#endif
        if (escPressed)
        {
            if (MinijuegoArcadeTrigger.InstanciaActiva != null)
            {
                MinijuegoArcadeTrigger.InstanciaActiva.SalirDelMinijuego();
                
            }
            else
            {
                Debug.Log("[DinoPlayer] Esc: no hay un arcade activo. Si estás probando la escena DEMO sola, no hay a dónde volver; abrila desde la máquina con E.");
            }
        }

        // Inicio con el primer salto
        if (!isGameStarted)
        {
            if (spacePressed)
            {
                isGameStarted = true;
                if (startInstructionsUI != null)
                {
                    startInstructionsUI.SetActive(false);
                }
                rb.linearVelocity = new Vector2(0f, jumpForce);
            }
            return;
        }

        if (isGameOver) return;

        // Detección de suelo
        if (groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
        }
        else
        {
            if (Mathf.Abs(rb.linearVelocity.y) < 0.05f)
            {
                isGrounded = true;
            }
        }

        // Salto
        if (spacePressed && isGrounded)
        {
            rb.linearVelocity = new Vector2(0f, jumpForce);
            isGrounded = false;
        }

        // Salto variable al soltar la tecla
        if (spaceReleased && rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
        }
    }

    public void AddScore(int amount)
    {
        if (isGameOver) return;
        score += amount;
        ActualizarTextoPuntaje();

        // Al cruzar la meta de 100 puntos, avisar al arcade físico
        if (score >= puntajeMetaPremio && !yaOtorgoPremio)
        {
            yaOtorgoPremio = true;

            if (ArcadeDinoMachine.InstanciaActiva != null)
            {
                ArcadeDinoMachine.InstanciaActiva.DropearNafta();
            }
            else
            {
                Debug.LogWarning("[DinoPlayer] ArcadeDinoMachine.InstanciaActiva no encontrada en la escena activa.");
            }
        }
    }

    private void ActualizarTextoPuntaje()
    {
        if (scoreText != null)
        {
            scoreText.text = $"PUNTOS: {score:D5}";
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.name.Contains("Ground") || collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.name.Contains("Ground") || collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.name.Contains("Ground") || collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Antes de empezar (o ya perdido) los choques no cuentan
        if (!isGameStarted || isGameOver) return;

        if (other.gameObject.name.Contains("Cactus") || other.CompareTag("Obstacle"))
        {
            Debug.Log($"[DinoPlayer] Choque con '{other.gameObject.name}'");
            TriggerGameOver();
        }
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        MostrarGameOver(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[DinoPlayer] ¡GAME OVER!");
    }

    // ───────────────────────── UI ─────────────────────────

    // Al mostrar, el botón se activa y RestartUIRawImage (en el botón) se encarga de
    // mostrar el Game Over y manejar la secuencia al presionarlo.
    private void MostrarGameOver(bool mostrar)
    {
        if (mostrar)
        {
            // Si algún padre quedó desactivado, el Game Over no se vería aunque esté activo
            ActivarJerarquia(gameOverCanvas);
            ActivarJerarquia(botonReiniciar != null ? botonReiniciar.gameObject : null);

            BloquearCanvas(gameOverCanvas, true);
            if (botonReiniciar != null) botonReiniciar.gameObject.SetActive(true);

            if (gameOverCanvas != null)
            {
                Debug.Log($"[DinoPlayer] Mostrando Game Over: '{gameOverCanvas.name}' | activo en jerarquía={gameOverCanvas.activeInHierarchy} | escena='{gameOverCanvas.scene.name}'", gameOverCanvas);
            }
        }
        else
        {
            if (botonReiniciar != null) botonReiniciar.gameObject.SetActive(false);
            BloquearCanvas(gameOverCanvas, false);
        }
    }

    /// <summary>Activa el objeto y todos sus padres (y los Canvas desactivados), para que sea realmente visible.</summary>
    private static void ActivarJerarquia(GameObject objeto)
    {
        if (objeto == null) return;

        Transform t = objeto.transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);

            Canvas c = t.GetComponent<Canvas>();
            if (c != null && !c.enabled) c.enabled = true;

            t = t.parent;
        }
    }

    private static string NombreDe(GameObject objeto)
    {
        return objeto != null ? objeto.name : "(vacío)";
    }

    private void BloquearCanvas(GameObject canvasObj, bool activar)
    {
        if (canvasObj == null) return;
        canvasObj.SetActive(activar);

        Canvas c = canvasObj.GetComponent<Canvas>();
        if (c != null) c.enabled = activar;

        CanvasGroup cg = canvasObj.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = activar ? 1f : 0f;
            cg.interactable = activar;
            cg.blocksRaycasts = activar;
        }
    }
}