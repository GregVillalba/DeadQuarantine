using System.Collections.Generic;
using UnityEngine;

public class CactusMove : MonoBehaviour
{
    // Registro de cactus activos: el dino lo usa para calcular el choque
    public static readonly List<CactusMove> Activos = new List<CactusMove>();

    [SerializeField] private float speed = 6f;
    [SerializeField] private float destroyX = -12f;

    public Collider2D ColliderCactus { get; private set; }

    private DinoPlayer dino;
    private Collider2D colliderDino;
    private bool scored = false;

    // El spawner define la velocidad para que todos los cactus sean consistentes
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }

    void Awake()
    {
        ColliderCactus = GetComponentInChildren<Collider2D>();

        // El prefab también trae este componente para reciclar elementos del escenario.
        // En los cactus debe quedar un único controlador de movimiento y puntaje.
        ScrollAndRecycle reciclador = GetComponent<ScrollAndRecycle>();
        if (reciclador != null)
            reciclador.enabled = false;
    }

    void OnEnable()
    {
        Activos.Add(this);
    }

    void OnDisable()
    {
        Activos.Remove(this);
    }

    void Start()
    {
        dino = Object.FindFirstObjectByType<DinoPlayer>();
        if (dino != null)
            colliderDino = dino.GetComponent<Collider2D>();
    }

    void Update()
    {
        // No se mueve si el juego no empezó o si ya perdiste
        if (dino == null || !dino.isGameStarted || dino.isGameOver) return;

        float xAnterior = transform.position.x;

        // Desplazamiento
        transform.position += Vector3.left * speed * Time.deltaTime;

        float xDino = colliderDino != null ? colliderDino.bounds.center.x : dino.transform.position.x;
        if (!scored && xAnterior >= xDino && transform.position.x < xDino)
        {
            scored = true;
            dino.AddScore(100);
        }

        // Destrucción al salir de pantalla
        if (transform.position.x <= destroyX)
        {
            Destroy(gameObject);
        }
    }
}
