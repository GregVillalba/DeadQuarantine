using UnityEngine;

public class InfiniteGround : MonoBehaviour
{
    [Header("Velocidad de Desplazamiento")]
    [SerializeField] private float speed = 6f;

    private float spriteWidth;
    private DinoPlayer dino;

    void Start()
    {
        dino = Object.FindFirstObjectByType<DinoPlayer>();

        // Obtiene el ancho visual exacto del sprite en unidades de Unity
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            spriteWidth = sr.bounds.size.x;
        }
        else
        {
            spriteWidth = 10f; // Valor por defecto si no detecta SpriteRenderer
        }
    }

    void Update()
    {
        // Se detiene si perdiste
        if (dino != null && dino.isGameOver) return;

        // Mover hacia la izquierda
        transform.position += Vector3.left * speed * Time.deltaTime;

        // Si el objeto ya salió completamente por la izquierda
        if (transform.position.x <= -spriteWidth)
        {
            // Se teletransporta justo después del siguiente bloque (2 veces el ancho si usas 2 segmentos)
            Vector3 offset = new Vector3(spriteWidth * 2f, 0f, 0f);
            transform.position += offset;
        }
    }
}
