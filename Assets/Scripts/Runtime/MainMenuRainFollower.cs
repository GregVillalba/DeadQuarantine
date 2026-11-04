using UnityEngine;

/// <summary>
/// Mantiene el FX de lluvia centrado alrededor de la cámara del menú.
/// </summary>
public class MainMenuRainFollower : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 8f, 0f);
    [SerializeField] private bool followY;

    public void Configure(Transform newTarget)
    {
        target = newTarget;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 p = target.position;

        if (!followY)
            p.y = transform.position.y - offset.y;

        transform.position =
            new Vector3(
                p.x + offset.x,
                followY ? target.position.y + offset.y : transform.position.y,
                p.z + offset.z
            );
    }
}
