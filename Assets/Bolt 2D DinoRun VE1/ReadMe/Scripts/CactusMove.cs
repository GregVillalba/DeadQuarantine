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
    private bool scored = false;

    // El spawner define la velocidad para que todos los cactus sean consistentes
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }

    void Awake()
    {
        ColliderCactus = GetComponentInChildren<Collider2D>();
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
    }

    void Update()
    {
        // No se mueve si el juego no empezó o si ya perdiste
        if (dino == null || !dino.isGameStarted || dino.isGameOver) return;

        // Desplazamiento
        transform.position += Vector3.left * speed * Time.deltaTime;

        // Detección de salto exitoso: cuando el cactus pasa por detrás del jugador en X
        if (!scored && transform.position.x < dino.transform.position.x)
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