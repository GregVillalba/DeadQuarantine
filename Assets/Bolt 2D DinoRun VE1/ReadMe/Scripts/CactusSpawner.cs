using UnityEngine;

public class CactusSpawner : MonoBehaviour
{
    [Header("Prefabs de Cactus")]
    [SerializeField] private GameObject[] cactusPrefabs;

    [Header("Velocidad (la controla el spawner)")]
    [SerializeField] private float cactusSpeed = 6f;

    [Header("Separación entre cactus (unidades de mundo)")]
    [Tooltip("Distancia mínima entre un cactus y el siguiente. Tiene que ser mayor que lo que recorrés en un salto.")]
    [SerializeField] private float minGap = 12f;
    [SerializeField] private float maxGap = 19f;

    [Header("Punto de Aparición")]
    [SerializeField] private float spawnX = 12f;
    [SerializeField] private float groundY = -1.8f; // Ajustá este valor a la altura de tu suelo

    [Header("Gracia inicial")]
    [SerializeField] private float initialGraceTime = 1.0f;

    private float distanceTraveled;
    private float nextGap;
    private float graceTimer;
    private DinoPlayer dino;

    void Start()
    {
        dino = Object.FindFirstObjectByType<DinoPlayer>();
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

        // Cuánto "avanzó el mundo" desde el último cactus
        distanceTraveled += cactusSpeed * Time.deltaTime;

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
        moveScript.SetSpeed(cactusSpeed);
    }

    void SetNextGap()
    {
        nextGap = Random.Range(minGap, maxGap);
    }
}