
using UnityEngine;

public class PuertaTraseraMansionHistoria : MonoBehaviour
{
    [Header("Animación")]
    [SerializeField] private Animator animatorPuerta;
    [SerializeField] private string nombreEstadoApertura = "Base Layer.Abrir";

    private int estadoAperturaHash;
    private bool puertaAbierta;

    private void Awake()
    {
        if (animatorPuerta == null)
            animatorPuerta = GetComponent<Animator>();

        if (animatorPuerta == null)
        {
            Debug.LogError(
                "[PuertaMansion] No se encontró el Animator.",
                this
            );
            return;
        }

        estadoAperturaHash =
            Animator.StringToHash(nombreEstadoApertura);

        if (!animatorPuerta.HasState(0, estadoAperturaHash))
        {
            Debug.LogError(
                "[PuertaMansion] No existe el estado: " +
                nombreEstadoApertura,
                this
            );
            return;
        }

        // Deja la puerta en el primer fotograma de la apertura.
        animatorPuerta.enabled = true;
        animatorPuerta.Play(estadoAperturaHash, 0, 0f);
        animatorPuerta.Update(0f);
        animatorPuerta.speed = 0f;

        Debug.Log(
            "[PuertaMansion] Puerta preparada y cerrada.",
            this
        );
    }

    public void AbrirPuerta()
    {
        if (puertaAbierta)
            return;

        if (animatorPuerta == null)
            return;

        if (!animatorPuerta.HasState(0, estadoAperturaHash))
        {
            Debug.LogError(
                "[PuertaMansion] No se puede reproducir la apertura.",
                this
            );
            return;
        }

        puertaAbierta = true;

        animatorPuerta.enabled = true;
        animatorPuerta.speed = 1f;
        animatorPuerta.Play(estadoAperturaHash, 0, 0f);

        Debug.Log(
            "[PuertaMansion] Animación de apertura iniciada.",
            this
        );
    }
}
