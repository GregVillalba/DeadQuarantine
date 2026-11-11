using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
public class DinoWorldCoverage : MonoBehaviour
{
    [SerializeField] private Color skyFallback = new Color(0.22f, 0.66f, 0.96f, 1f);
    [SerializeField, Min(0f)] private float groundScrollSpeed = 6f;

    private Camera gameCamera;
    private Transform groundStrip;
    private float groundTileWidth;
    private float stripStartX;
    private DinoPlayer dino;

    private void Start()
    {
        gameCamera = GetComponent<Camera>();
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = skyFallback;
        dino = FindFirstObjectByType<DinoPlayer>();
        BuildGroundStrip();
    }

    private void Update()
    {
        if (groundStrip == null || dino == null || !dino.isGameStarted || dino.isGameOver)
            return;

        Vector3 position = groundStrip.position;
        position.x -= groundScrollSpeed * Time.deltaTime;
        if (position.x <= stripStartX - groundTileWidth)
            position.x += groundTileWidth;
        groundStrip.position = position;
    }

    private void BuildGroundStrip()
    {
        SpriteRenderer source = null;
        foreach (SpriteRenderer candidate in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == gameObject.scene && candidate.sprite != null &&
                candidate.sprite.name == "Ground")
            {
                source = candidate;
                break;
            }
        }

        if (source == null)
        {
            Debug.LogError("[DinoWorldCoverage] No se encontró el sprite Ground en la escena.", this);
            return;
        }

        groundTileWidth = source.sprite.bounds.size.x;
        float tileHeight = source.sprite.bounds.size.y;
        BoxCollider2D sourceCollider = source.GetComponent<BoxCollider2D>();
        float colliderHeight = sourceCollider != null ? sourceCollider.bounds.size.y : tileHeight;
        // Conserva el centro visual original; el centro del collider puede tener offset.
        float groundCenterY = source.transform.position.y;
        float groundZ = source.transform.position.z;
        int sortingLayer = source.sortingLayerID;
        int sortingOrder = source.sortingOrder;
        Sprite groundSprite = source.sprite;

        foreach (SpriteRenderer oldTile in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (oldTile.gameObject.scene != gameObject.scene || oldTile.sprite == null ||
                oldTile.sprite.name != "Ground") continue;

            oldTile.enabled = false;
            ScrollAndRecycle oldScroller = oldTile.GetComponent<ScrollAndRecycle>();
            if (oldScroller != null) oldScroller.enabled = false;
            Collider2D oldCollider = oldTile.GetComponent<Collider2D>();
            if (oldCollider != null) oldCollider.enabled = false;
        }

        float stripWidth = gameCamera.orthographicSize * 2f * gameCamera.aspect + groundTileWidth * 2f;
        GameObject strip = new GameObject("Ground Continuous");
        SceneManager.MoveGameObjectToScene(strip, gameObject.scene);
        groundStrip = strip.transform;
        groundStrip.position = new Vector3(gameCamera.transform.position.x, groundCenterY, groundZ);
        stripStartX = groundStrip.position.x;

        SpriteRenderer renderer = strip.AddComponent<SpriteRenderer>();
        renderer.sprite = groundSprite;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.size = new Vector2(stripWidth, tileHeight);
        renderer.sortingLayerID = sortingLayer;
        renderer.sortingOrder = sortingOrder;

        BoxCollider2D collider = strip.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(stripWidth, colliderHeight);
        collider.offset = Vector2.zero;

    }
}
