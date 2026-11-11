using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class DinoSpriteAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] runFrames = new Sprite[4];
    [SerializeField] private Sprite jumpFrame;
    [SerializeField] private Sprite dieFrame;
    [SerializeField, Min(1f)] private float framesPerSecond = 10f;

    private SpriteRenderer spriteRenderer;
    private DinoPlayer player;
    private Rigidbody2D body;
    private float frameTimer;
    private int frameIndex;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        player = GetComponent<DinoPlayer>();
        body = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (player != null && player.isGameOver)
        {
            SetSprite(dieFrame);
            return;
        }

        if (player == null || !player.isGameStarted)
        {
            SetSprite(FirstRunFrame());
            return;
        }

        if (body != null && Mathf.Abs(body.linearVelocity.y) > 0.15f)
        {
            // La hoja no trae un cuadro de salto aparte; conserva una pose de carrera.
            SetSprite(FirstRunFrame());
            return;
        }

        if (runFrames == null || runFrames.Length == 0) return;

        frameTimer += Time.deltaTime;
        if (frameTimer >= 1f / framesPerSecond)
        {
            frameTimer = 0f;
            frameIndex = (frameIndex + 1) % runFrames.Length;
            SetSprite(runFrames[frameIndex]);
        }
    }

    private Sprite FirstRunFrame() => runFrames != null && runFrames.Length > 0 ? runFrames[0] : null;

    private void SetSprite(Sprite sprite)
    {
        if (sprite != null && spriteRenderer.sprite != sprite)
            spriteRenderer.sprite = sprite;
    }
}
