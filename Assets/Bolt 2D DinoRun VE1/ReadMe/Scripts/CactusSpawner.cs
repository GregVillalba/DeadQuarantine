using UnityEngine;

public class CactusSpawner : MonoBehaviour
{
    [Header("Prefabs de Cactus")]
    [SerializeField] private GameObject[] cactusPrefabs;

    [Header("Tiempos de Generación (Segundos)")]
    [SerializeField] private float minSpawnTime = 1.8f;
    [SerializeField] private float maxSpawnTime = 3.5f;

    [Header("Punto de Aparición")]
    [SerializeField] private float spawnX = 12f;
    [SerializeField] private float groundY = -1.8f; // Ajustá este valor a la altura de tu suelo

    private float timer;
    private float nextSpawnTime;
    private DinoPlayer dino;

    void Start()
    {
        dino = Object.FindFirstObjectByType<DinoPlayer>();
        SetNextSpawnTime();

        // Dar 2 segundos de gracia al inicio antes de tirar el primer obstáculo
        timer = -1.0f;
    }

    void Update()
    {
        if (dino == null || !dino.isGameStarted || dino.isGameOver) return;

        timer += Time.deltaTime;
        if (timer >= nextSpawnTime)
        {
            SpawnCactus();
            timer = 0f;
            SetNextSpawnTime();
        }
    }

    void SpawnCactus()
    {
        if (cactusPrefabs == null || cactusPrefabs.Length == 0) return;

        int randomIndex = Random.Range(0, cactusPrefabs.Length);
        Vector3 spawnPos = new Vector3(spawnX, groundY, 0f);

        GameObject cactus = Instantiate(cactusPrefabs[randomIndex], spawnPos, Quaternion.identity);

        // Asegurar que tenga el script de movimiento
        CactusMove moveScript = cactus.GetComponent<CactusMove>();
        if (moveScript == null)
        {
            moveScript = cactus.AddComponent<CactusMove>();
        }
    }

    void SetNextSpawnTime()
    {
        nextSpawnTime = Random.Range(minSpawnTime, maxSpawnTime);
    }
}