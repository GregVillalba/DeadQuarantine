using UnityEngine;

public class CactusMove : MonoBehaviour
{
    [SerializeField] private float speed = 6f;
    [SerializeField] private float destroyX = -12f;

    private DinoPlayer dino;
    private bool scored = false;

    void Start()
    {
        dino = Object.FindFirstObjectByType<DinoPlayer>();
    }

    void Update()
    {
        // No se mueve si el juego no empezó o si ya perdiste
        if (dino == null || !dino.isGameStarted || dino.isGameOver) return;

        // Desplazamiento
        transform.position += Vector3.left * speed * Time.deltaTime;

        // Detección de salto exitoso: cuando el cactus pasa por detrás del jugador en X
        if (!scored && transform.position.x < dino.transform.position.x)
        {
            scored = true;
            dino.AddScore(100);
        }

        // Destrucción al salir de pantalla
        if (transform.position.x <= destroyX)
        {
            Destroy(gameObject);
        }
    }
}