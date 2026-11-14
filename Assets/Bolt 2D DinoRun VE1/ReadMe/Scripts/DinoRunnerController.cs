using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class DinoPlayer : MonoBehaviour
{
    public static DinoPlayer Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────
    // CONTADOR TOTAL DE CANTIMPLORAS EN LA SESIÓN DEL ARCADE
    // ─────────────────────────────────────────────────────────────
    private static int cantimplorasTotalesSesion = 0;

    [Header("Salto")]
    [SerializeField] private float jumpForce = 14f; // Fuerza directa para respuesta seca
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float checkRadius = 0.2f;

    [Header("UI / Inicio")]
    [Tooltip("Objeto que contiene SOLO el texto 'Presiona Espacio para comenzar'.")]
    [SerializeField] private GameObject startInstructionsUI;

    [Header("UI / Game Over")]
    [SerializeField] private GameObject gameOverCanvas;
    [SerializeField] private Button botonReiniciar;
    [SerializeField] private GameObject imagenReiniciando;

    [Header("Puntaje y Recompensas")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [Tooltip("Texto permanente ubicado debajo del puntaje que muestra las cantimploras ganadas.")]
    [SerializeField] private TextMeshProUGUI textoNotificacionPremio;

    private const int PUNTOS_POR_CANTIMPLORA = 200;

    public int score = 0;
    private int cantimplorasEntregadasEstaRonda = 0;

    [HideInInspector] public bool isGameStarted = false;
    [HideInInspector] public bool isGameOver = false;

    [Header("Hitbox de choque")]
    [Tooltip("Cuánto se achica el hitbox del dino por cada lado (0.2 = 20%). Más alto = más permisivo.")]
    [Range(0f, 0.4f)][SerializeField] private float dinoHitboxShrink = 0.2f;
    [Tooltip("Cuánto se achica el hitbox de cada cactus por cada lado.")]
    [Range(0f, 0.4f)][SerializeField] private float cactusHitboxShrink = 0.15f;

    private Collider2D dinoCollider;
    private Rigidbody2D rb;
    private bool isGrounded = true;

    private float groundYBase;

    private void Awake()
    {
        Instance = this;

        // Guarda la altura exacta donde lo dejaste en el editor como el límite del suelo
        groundYBase = transform.position.y;

        score = 0;
        cantimplorasEntregadasEstaRonda = 0;

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        // Inicia apoyado sin caer
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 4.2f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (GetComponent<Collider2D>() == null)
            gameObject.AddComponent<BoxCollider2D>();

        dinoCollider = GetComponent<Collider2D>();

        if (botonReiniciar == null && gameOverCanvas != null)
            botonReiniciar = gameOverCanvas.GetComponentInChildren<Button>(true);

        if (startInstructionsUI != null) startInstructionsUI.SetActive(true);
        MostrarGameOver(false);
        if (imagenReiniciando != null) imagenReiniciando.SetActive(false);

        if (textoNotificacionPremio != null)
        {
            textoNotificacionPremio.gameObject.SetActive(true);
            ActualizarTextoCantimploras();
        }

        ActualizarTextoPuntaje();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SalirDelMinijuego();
            return;
        }

        bool espacioPresionado = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool espacioSoltado = Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame;

        // INICIO
        if (!isGameStarted)
        {
            transform.position = new Vector3(transform.position.x, groundYBase, transform.position.z);

            if (espacioPresionado)
            {
                isGameStarted = true;
                rb.bodyType = RigidbodyType2D.Dynamic;

                if (startInstructionsUI != null)
                    startInstructionsUI.SetActive(false);

                EjecutarSalto();
                MinijuegoArcadeTrigger.InstanciaActiva?.RegistrarInicioDePartida();
            }
            return;
        }

        if (isGameOver) return;

        ChequearChoqueConCactus();
        if (isGameOver) return;

        // 1. FRENO DE PISO INQUEBRANTABLE:
        // Si el dinosaurio toca o baja de la altura inicial que tenía en el editor,
        // lo frena y lo apoya exactamente sobre la línea de tierra.
        if (transform.position.y <= groundYBase)
        {
            transform.position = new Vector3(transform.position.x, groundYBase, transform.position.z);
            if (rb.linearVelocity.y < 0f)
            {
                rb.linearVelocity = new Vector2(0f, 0f);
            }
            isGrounded = true;
        }

        // 2. DETECCIÓN DE SUELO PARA SALTAR:
        bool tocandoPiso = groundCheck != null &&
            Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
        bool enSuelo = isGrounded || tocandoPiso || Mathf.Abs(rb.linearVelocity.y) < 0.05f;

        // 3. SALTO INSTANTÁNEO
        if (espacioPresionado && enSuelo)
        {
            EjecutarSalto();
        }

        // 4. CORTE RÁPIDO AL SOLTAR ESPACIO
        if (espacioSoltado && rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y * 0.4f);
        }
    }

    private void EjecutarSalto()
    {
        rb.linearVelocity = new Vector2(0f, jumpForce);
        isGrounded = false;
    }

    private void Start()
    {
        Time.timeScale = 1f;
        Application.runInBackground = true;
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

    private void SalirDelMinijuego()
    {
        cantimplorasTotalesSesion = 0;
        score = 0;
        cantimplorasEntregadasEstaRonda = 0;
        ActualizarTextoCantimploras();

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
            scoreText.text = $"PUNTOS: {score:D5}";
    }

    private void ActualizarTextoCantimploras()
    {
        if (textoNotificacionPremio != null)
            textoNotificacionPremio.text = $"<color=#FF4B00>Cantimploras: +{cantimplorasTotalesSesion}</color>";
    }

    public void AddScore(int amount)
    {
        if (isGameOver) return;

        score += amount;
        ActualizarTextoPuntaje();
        VerificarEntregaCantimploras();
    }

    public void VerificarEntregaCantimploras()
    {
        int correspondientesEstaRonda = score / PUNTOS_POR_CANTIMPLORA;

        if (correspondientesEstaRonda > cantimplorasEntregadasEstaRonda)
        {
            int cantidadNuevas = correspondientesEstaRonda - cantimplorasEntregadasEstaRonda;

            for (int i = 0; i < cantidadNuevas; i++)
            {
                cantimplorasEntregadasEstaRonda++;
                cantimplorasTotalesSesion++;

                if (MinijuegoArcadeTrigger.InstanciaActiva != null)
                    MinijuegoArcadeTrigger.InstanciaActiva.DropearRecompensa();
                else
                {
                    var trigger = FindAnyObjectByType<MinijuegoArcadeTrigger>();
                    if (trigger != null) trigger.DropearRecompensa();
                }
            }

            ActualizarTextoCantimploras();
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

        Debug.Log($"[DinoPlayer] Ronda terminada con {score} puntos. Cantimploras acumuladas en sesión: {cantimplorasTotalesSesion}");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isGameStarted || isGameOver) return;

        if (other.GetComponentInParent<CactusMove>() != null) return;

        if (other.CompareTag("Obstacle"))
            TriggerGameOver();
    }

    private void ChequearChoqueConCactus()
    {
        if (dinoCollider == null) return;

        Rect hitboxDino = RectEncogido(dinoCollider.bounds, dinoHitboxShrink);

        for (int i = CactusMove.Activos.Count - 1; i >= 0; i--)
        {
            CactusMove cactus = CactusMove.Activos[i];
            if (cactus == null || cactus.ColliderCactus == null) continue;

            Rect hitboxCactus = RectEncogido(cactus.ColliderCactus.bounds, cactusHitboxShrink);
            if (hitboxDino.Overlaps(hitboxCactus))
            {
                TriggerGameOver();
                return;
            }
        }
    }

    private static Rect RectEncogido(Bounds b, float fraccion)
    {
        float w = b.size.x * (1f - 2f * fraccion);
        float h = b.size.y * (1f - 2f * fraccion);
        return new Rect(b.center.x - w * 0.5f, b.center.y - h * 0.5f, w, h);
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D c = dinoCollider != null ? dinoCollider : GetComponent<Collider2D>();
        if (c == null) return;

        Rect r = RectEncogido(c.bounds, dinoHitboxShrink);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(r.center, r.size);

        Gizmos.color = Color.yellow;
        foreach (CactusMove cactus in CactusMove.Activos)
        {
            if (cactus == null || cactus.ColliderCactus == null) continue;
            Rect rc = RectEncogido(cactus.ColliderCactus.bounds, cactusHitboxShrink);
            Gizmos.DrawWireCube(rc.center, rc.size);
        }
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
