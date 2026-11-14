using UnityEngine;

public class CactusSpawner : MonoBehaviour
{
    [Header("Prefabs de Cactus")]
    [SerializeField] private GameObject[] cactusPrefabs;

    [Header("Velocidad (la controla el spawner)")]
    [SerializeField] private float cactusSpeed = 6f;
    [SerializeField, Min(0f)] private float velocidadMaxima = 9.5f;
    [SerializeField, Min(1f)] private float segundosParaVelocidadMaxima = 60f;

    [Header("Separación entre cactus (unidades de mundo)")]
    [Tooltip("Separación inicial. Tiene que permitir pasar el primer obstáculo con comodidad.")]
    [SerializeField] private float minGap = 14f;
    [SerializeField] private float maxGap = 19f;
    [Tooltip("Separación al alcanzar la dificultad máxima.")]
    [SerializeField, Min(1f)] private float minGapFinal = 12f;
    [SerializeField, Min(1f)] private float maxGapFinal = 16f;

    [Header("Punto de Aparición")]
    [SerializeField] private float spawnX = 12f;
    [SerializeField] private float groundY = -1.8f; // Ajustá este valor a la altura de tu suelo

    [Header("Gracia inicial")]
    [SerializeField] private float initialGraceTime = 1.0f;

    private float distanceTraveled;
    private float nextGap;
    private float graceTimer;
    private float tiempoDePartida;
    private float velocidadActual;
    private DinoPlayer dino;

    void Start()
    {
        dino = Object.FindFirstObjectByType<DinoPlayer>();
        velocidadActual = cactusSpeed;
        SetNextGap();
        // Arranca "lleno" para que el primer cactus salga tras la gracia inicial
        distanceTraveled = nextGap;
        graceTimer = initialGraceTime;
    }

    void Update()
    {
        if (dino == null || !dino.isGameStarted || dino.isGameOver) return;

        if (graceTimer > 0f)
        {
            graceTimer -= Time.deltaTime;
            return;
        }

        tiempoDePartida += Time.deltaTime;
        float progreso = Mathf.Clamp01(tiempoDePartida / segundosParaVelocidadMaxima);
        // Curva suave: el arranque conserva un ritmo amable y la presión crece durante la partida.
        float dificultad = Mathf.SmoothStep(0f, 1f, progreso);
        velocidadActual = Mathf.Lerp(cactusSpeed, velocidadMaxima, dificultad);

        // Los obstáculos que ya están en pantalla mantienen el mismo ritmo para que
        // los cactus nuevos no alcancen ni se amontonen con los anteriores.
        for (int i = CactusMove.Activos.Count - 1; i >= 0; i--)
        {
            CactusMove cactus = CactusMove.Activos[i];
            if (cactus != null)
                cactus.SetSpeed(velocidadActual);
        }

        // Cuánto "avanzó el mundo" desde el último cactus
        distanceTraveled += velocidadActual * Time.deltaTime;

        if (distanceTraveled >= nextGap)
        {
            SpawnCactus();
            distanceTraveled = 0f;
            SetNextGap();
        }
    }

    void SpawnCactus()
    {
        if (cactusPrefabs == null || cactusPrefabs.Length == 0) return;

        int randomIndex = Random.Range(0, cactusPrefabs.Length);
        Vector3 spawnPos = new Vector3(spawnX, groundY, 0f);

        GameObject cactus = Instantiate(cactusPrefabs[randomIndex], spawnPos, Quaternion.identity);

        CactusMove moveScript = cactus.GetComponent<CactusMove>();
        if (moveScript == null)
        {
            moveScript = cactus.AddComponent<CactusMove>();
        }

        // Misma velocidad para todos: evita que uno alcance a otro
        moveScript.SetSpeed(velocidadActual);
    }

    void SetNextGap()
    {
        float progreso = Mathf.Clamp01(tiempoDePartida / segundosParaVelocidadMaxima);
        float dificultad = Mathf.SmoothStep(0f, 1f, progreso);
        float gapMinimo = Mathf.Lerp(minGap, minGapFinal, dificultad);
        float gapMaximo = Mathf.Lerp(maxGap, maxGapFinal, dificultad);
        nextGap = Random.Range(gapMinimo, Mathf.Max(gapMinimo, gapMaximo));
    }
}
