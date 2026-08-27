using System.Collections;
using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class BulletTrail : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float speed = 300f;
    [SerializeField] private float destroyDelay = 0.2f;

    public void Init(Vector3 targetPoint)
    {
        StartCoroutine(MoveToTarget(targetPoint));
    }

    private IEnumerator MoveToTarget(Vector3 targetPoint)
    {
        Vector3 startPoint = transform.position;
        float distance = Vector3.Distance(startPoint, targetPoint);
        float duration = distance / speed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPoint, targetPoint, elapsed / duration);
            yield return null;
        }

        transform.position = targetPoint;

        Destroy(gameObject, destroyDelay);
    }
}