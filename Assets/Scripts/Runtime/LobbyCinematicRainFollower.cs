using UnityEngine;

/// <summary>
/// Mantiene el FX de lluvia alrededor de la cámara del lobby.
/// </summary>
public class LobbyCinematicRainFollower : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset =
        new Vector3(0f, 8f, 7f);

    public void Configure(
        Transform newTarget)
    {
        target = newTarget;
        Snap();
    }

    private void OnEnable()
    {
        Snap();
    }

    private void LateUpdate()
    {
        Snap();
    }

    private void Snap()
    {
        if (target == null)
            return;

        transform.position =
            target.position + offset;
    }
}
