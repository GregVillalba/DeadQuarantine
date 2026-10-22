
using UnityEngine;

public class PuertaAnimacionHistoria : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private bool animacionIniciada;
    private static readonly int EstadoAbrirPuerta =
        Animator.StringToHash("abrirPuerta");

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError(
                "[Puerta] No se encontró un Animator.",
                this
            );
            return;
        }

        // Mantiene el estado inicial sin avanzar la animación.
        animator.speed = 0f;
    }

    public void AbrirPuerta()
    {
        if (animator == null || animacionIniciada)
            return;

        if (!animator.HasState(0, EstadoAbrirPuerta))
        {
            Debug.LogError(
                "[Puerta] No existe el estado AbrirPuerta en el Animator.",
                this
            );
            return;
        }

        animacionIniciada = true;
        animator.speed = 1f;
        animator.Play(EstadoAbrirPuerta, 0, 0f);
        animator.Update(0f);

        Debug.Log(
            "[Puerta] Animación de apertura iniciada.",
            this
        );
    }
}
