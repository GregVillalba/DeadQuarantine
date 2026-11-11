using UnityEngine;

public class ScrollAndRecycle : MonoBehaviour
{
    [Header("Velocidad")]
    [SerializeField] private float speed = 6f;

    private DinoPlayer dino;
    private Renderer[] renderers;

    private void Start()
    {
        dino = FindFirstObjectByType<DinoPlayer>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void Update()
    {
        if (dino == null || !dino.isGameStarted || dino.isGameOver) return;

        transform.position += Vector3.left * speed * Time.deltaTime;
        if (renderers == null || renderers.Length == 0) return;

        Bounds ownBounds = GetVisualBounds();
        float leftEdge = Camera.main != null
            ? Camera.main.ViewportToWorldPoint(new Vector3(0f, 0f, 0f)).x
            : -10f;

        if (ownBounds.max.x >= leftEdge) return;

        float rightmost = ownBounds.max.x;
        foreach (ScrollAndRecycle other in FindObjectsByType<ScrollAndRecycle>(FindObjectsSortMode.None))
        {
            if (other == this || other == null || other.speed != speed ||
                other.renderers == null || other.renderers.Length == 0)
                continue;

            rightmost = Mathf.Max(rightmost, other.GetVisualBounds().max.x);
        }

        transform.position += Vector3.right * (rightmost - ownBounds.min.x);
    }

    private Bounds GetVisualBounds()
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
