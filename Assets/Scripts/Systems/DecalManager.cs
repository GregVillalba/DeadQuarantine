using UnityEngine;
using System.Collections.Generic;

public class DecalManager : MonoBehaviour
{
    public static DecalManager Instance { get; private set; }

    [SerializeField] private GameObject bulletHolePrefab;
    [SerializeField] private int maxDecals = 40;
    [SerializeField] private float surfaceOffset = 0.01f;

    private readonly Queue<GameObject> activeDecals = new Queue<GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    public void SpawnBulletHole(Vector3 position, Vector3 normal)
    {
        if (bulletHolePrefab == null) return;

        Vector3 spawnPosition = position + normal * surfaceOffset;
        Quaternion spawnRotation = Quaternion.LookRotation(-normal);

        GameObject decal = Instantiate(bulletHolePrefab, spawnPosition, spawnRotation);
        activeDecals.Enqueue(decal);

        if (activeDecals.Count > maxDecals)
        {
            GameObject oldest = activeDecals.Dequeue();
            if (oldest != null) Destroy(oldest);
        }
    }
}