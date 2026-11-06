using UnityEngine;
using TMPro; // Si usás TextMeshPro. Si usás Text clásico, poné: using UnityEngine.UI;
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

    [Header("UI / Game Over & Inicio")]
    [Tooltip("Panel o Canvas con la imagen de 'Presiona Espacio' / Game Over")]
    [SerializeField] private GameObject startInstructionsUI;
    [SerializeField] private GameObject gameOverCanvas;

    [Header("Puntaje")]
    [SerializeField] private TextMeshProUGUI scoreText; // Arrastrá acá tu texto de puntaje (o Text si usás UI legacy)
    private int score = 0;

    [HideInInspector] public bool isGameStarted = false;
    [HideInInspector] public bool isGameOver = false;

    private Rigidbody2D rb;
    private bool isGrounded = false;

    void Awake()
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

        // Mostrar instrucciones iniciales y ocultar Game Over
        if (startInstructionsUI != null) startInstructionsUI.SetActive(true);
        if (gameOverCanvas != null) BloquearCanvas(gameOverCanvas, false);

        ActualizarTextoPuntaje();
    }

    void Update()
    {
        // 1. Lectura de tecla ESPACIO compatible con ambos Input Systems
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

        // 2. Si el juego aún no arrancó, el primer 'Espacio' lo inicia y ejecuta el primer salto
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

        // 3. Chequeo de suelo
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

        // 4. Salto durante la partida
        if (spacePressed && isGrounded)
        {
            rb.linearVelocity = new Vector2(0f, jumpForce);
            isGrounded = false;
        }

        // Salto variable al soltar antes
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
    }

    private void ActualizarTextoPuntaje()
    {
        if (scoreText != null)
        {
            scoreText.text = $"PUNTOS: {score:D5}"; // Formato con ceros a la izquierda: 00100, 00200
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

        BloquearCanvas(gameOverCanvas, true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("¡GAME OVER!");
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