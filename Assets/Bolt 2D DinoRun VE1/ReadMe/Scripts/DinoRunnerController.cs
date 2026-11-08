using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class DinoPlayer : MonoBehaviour
{
    public static DinoPlayer Instance { get; private set; }

    [Header("Salto")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float checkRadius = 0.2f;

    [Header("UI / Inicio")]
    [Tooltip("Objeto que contiene SOLO el texto 'Presiona Espacio para comenzar'.")]
    [SerializeField] private GameObject startInstructionsUI;

    [Header("UI / Game Over")]
    [Tooltip("Imagen de GAME OVER (RawImage_gameover).")]
    [SerializeField] private GameObject gameOverCanvas;
    [SerializeField] private Button botonReiniciar;
    [SerializeField] private GameObject imagenReiniciando;

    [Header("Puntaje y Recompensas")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private int puntosPorCantimplora = 100;
    [Tooltip("Velocidad con la que sube el puntaje por segundo si no usas obstáculos (ej: 10 pts/seg)")]
    [SerializeField] private float puntosPorSegundo = 0f;

    public int score = 0;
    private float scoreAcumuladoFloat = 0f;
    private int cantimplorasEntregadas = 0;

    [HideInInspector] public bool isGameStarted = false;
    [HideInInspector] public bool isGameOver = false;

    private Rigidbody2D rb;
    private bool isGrounded = false;

    private void Awake()
    {
        Instance = this;

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

        if (botonReiniciar == null && gameOverCanvas != null)
        {
            botonReiniciar = gameOverCanvas.GetComponentInChildren<Button>(true);
        }

        if (startInstructionsUI != null) startInstructionsUI.SetActive(true);
        MostrarGameOver(false);
        if (imagenReiniciando != null) imagenReiniciando.SetActive(false);

        ActualizarTextoPuntaje();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        Application.runInBackground = true;

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // -----------------------------------------------------------------
        // 1. SALIDA POR ESCAPE
        // -----------------------------------------------------------------
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SalirDelMinijuego();
            return;
        }

        // -----------------------------------------------------------------
        // 2. LECTURA DE TECLA ESPACIO
        // -----------------------------------------------------------------
        bool espacioPresionado = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool espacioSoltado = Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame;

        // 3. INICIO: El primer Espacio arranca el juego
        if (!isGameStarted)
        {
            if (espacioPresionado)
            {
                isGameStarted = true;
                if (startInstructionsUI != null)
                    startInstructionsUI.SetActive(false);

                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            }
            return;
        }

        if (isGameOver) return;

        // -----------------------------------------------------------------
        // 4. SUMA DE PUNTOS CONTINUA (Si el juego suma puntos con el tiempo)
        // -----------------------------------------------------------------
        if (puntosPorSegundo > 0f)
        {
            scoreAcumuladoFloat += puntosPorSegundo * Time.deltaTime;
            if ((int)scoreAcumuladoFloat > score)
            {
                score = (int)scoreAcumuladoFloat;
                ActualizarTextoPuntaje();
            }
        }

        // VERIFICAR DROPS EN CADA FRAME (Garantiza que no se pierda ningún múltiplo de 100)
        VerificarEntregaCantimploras();

        // -----------------------------------------------------------------
        // 5. CHEQUEO DE PISO Y SALTO
        // -----------------------------------------------------------------
        bool tocandoPiso = false;
        if (groundCheck != null)
        {
            tocandoPiso = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
        }

        bool enSuelo = isGrounded || tocandoPiso || Mathf.Abs(rb.linearVelocity.y) < 0.15f;

        if (espacioPresionado && enSuelo)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;
        }

        if (espacioSoltado && rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
        }
    }

    /// <summary>
    /// Suma puntos manualmente (por ejemplo al saltar cactus o desde ScrollAndRecycle).
    /// </summary>
    public void AddScore(int amount)
    {
        if (isGameOver) return;

        score += amount;
        scoreAcumuladoFloat = score;
        ActualizarTextoPuntaje();
        VerificarEntregaCantimploras();
    }

    /// <summary>
    /// Calcula cuántas cantimploras tocan según el puntaje total.
    /// Si tienes 800 puntos, arrojará 8 cantimploras.
    /// </summary>
    public void VerificarEntregaCantimploras()
    {
        if (puntosPorCantimplora <= 0) return;

        int totalQueCorresponden = score / puntosPorCantimplora;

        while (cantimplorasEntregadas < totalQueCorresponden)
        {
            cantimplorasEntregadas++;
            Debug.Log($"<color=cyan>[DinoPlayer] Entregando cantimplora #{cantimplorasEntregadas} (Score: {score})</color>");

            if (MinijuegoArcadeTrigger.InstanciaActiva != null)
            {
                MinijuegoArcadeTrigger.InstanciaActiva.DropearRecompensa();
            }
            else
            {
                var trigger = FindAnyObjectByType<MinijuegoArcadeTrigger>();
                if (trigger != null)
                {
                    trigger.DropearRecompensa();
                }
                else
                {
                    Debug.LogError("[DinoPlayer] No se encontró MinijuegoArcadeTrigger en ninguna escena activa.");
                }
            }
        }
    }

    private void SalirDelMinijuego()
    {
        Time.timeScale = 1f;

        if (MinijuegoArcadeTrigger.InstanciaActiva != null)
        {
            MinijuegoArcadeTrigger.InstanciaActiva.SalirDelMinijuego();
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        GameObject jugadorLocal = GameObject.FindGameObjectWithTag("Player");
        if (jugadorLocal != null)
        {
            jugadorLocal.SendMessage("SalirMinijuego", SendMessageOptions.DontRequireReceiver);
        }

        SceneManager.UnloadSceneAsync("DEMO");
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
        if (!isGameStarted || isGameOver) return;

        if (other.gameObject.name.Contains("Cactus") || other.CompareTag("Obstacle"))
        {
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

    private void MostrarGameOver(bool mostrar)
    {
        if (mostrar)
        {
            ActivarJerarquia(gameOverCanvas);
            ActivarJerarquia(botonReiniciar != null ? botonReiniciar.gameObject : null);

            BloquearCanvas(gameOverCanvas, true);
            if (botonReiniciar != null) botonReiniciar.gameObject.SetActive(true);
        }
        else
        {
            if (botonReiniciar != null) botonReiniciar.gameObject.SetActive(false);
            BloquearCanvas(gameOverCanvas, false);
        }
    }

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