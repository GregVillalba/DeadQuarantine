
using UnityEngine;

public class PuertaAnimacionHistoria : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private bool animacionIniciada;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError(
                "[Puerta] No se encontró un Animator en " + gameObject.name,
                this
            );
            return;
        }

        Debug.Log(
            "[Puerta] Animator encontrado en " + gameObject.name,
            this
        );

        animator.speed = 1f;
        animator.Play("Cerrada", 0, 0f);
        animator.Update(0f);
    }

    public void AbrirPuerta()
    {
        Debug.Log("[Puerta] Se recibió la orden de abrir.", this);

        if (animator == null)
        {
            Debug.LogError("[Puerta] El Animator es nulo.", this);
            return;
        }

        if (animacionIniciada)
        {
            Debug.LogWarning("[Puerta] La animación ya fue iniciada.", this);
            return;
        }

        int estado = Animator.StringToHash("AbrirPuerta");

        if (!animator.HasState(0, estado))
        {
            Debug.LogError(
                "[Puerta] No existe el estado AbrirPuerta en el Animator.",
                this
            );
            return;
        }

        animacionIniciada = true;
        animator.speed = 1f;
        animator.Play(estado, 0, 0f);

        Debug.Log("[Puerta] Animación iniciada correctamente.", this);
    }
}
