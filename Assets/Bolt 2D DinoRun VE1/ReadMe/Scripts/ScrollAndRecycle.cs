using UnityEngine;

public class ScrollAndRecycle : MonoBehaviour
{
    [Header("Velocidad")]
    [SerializeField] private float speed = 6f;

    [Header("Límites de Reciclado en X")]
    [SerializeField] private float endX = -10f;   // Posición donde desaparece por la izquierda
    [SerializeField] private float startX = 10f;  // Posición donde reaparece por la derecha

    private DinoPlayer dino;

    void Start()
    {
        dino = Object.FindFirstObjectByType<DinoPlayer>();
    }

    void Update()
    {
        // Detenido si el juego no empezó o si terminó
        if (dino == null || !dino.isGameStarted || dino.isGameOver) return;

        // Mover hacia la izquierda
        transform.position += Vector3.left * speed * Time.deltaTime;

        // Si superó el borde izquierdo, reaparece en startX a la derecha
        if (transform.position.x <= endX)
        {
            Vector3 pos = transform.position;
            pos.x = startX;
            transform.position = pos;
        }
    }
}