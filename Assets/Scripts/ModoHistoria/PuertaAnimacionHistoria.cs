
using UnityEngine;
using System.Collections;

public class PuertaAnimacionHistoria : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Tooltip("Colliders que tapan el hueco de la puerta mientras está cerrada (ej. el BoxCollider de " +
             "\"Tunnel Window wall001\"). Se desactivan al terminar de abrirse para poder cruzarla.")]
    [SerializeField] private Collider[] collidersBloqueoPaso;

    private bool animacionIniciada;
    private bool animacionFinalizada;
    private Coroutine comprobacionActual;

    private static readonly int EstadoAbrirPuerta =
        Animator.StringToHash("Base Layer.abrirPuerta");

    public bool AnimacionFinalizada => animacionFinalizada;

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

        if (!animator.HasState(0, EstadoAbrirPuerta))
        {
            Debug.LogError(
                "[Puerta] No existe el estado Base Layer.abrirPuerta.",
                this
            );
            return;
        }

        // Coloca la puerta en la posición inicial del clip.
        animator.enabled = true;
        animator.Play(EstadoAbrirPuerta, 0, 0f);
        animator.Update(0f);
        animator.speed = 0f;

        Debug.Log(
            "[Puerta] Animator preparado en el inicio del clip.",
            this
        );
    }

    public void AbrirPuerta()
    {
        Debug.Log("[Puerta] Solicitud de apertura recibida.", this);

        if (animator == null)
        {
            Debug.LogError("[Puerta] Animator sin asignar.", this);
            return;
        }

        if (!animator.HasState(0, EstadoAbrirPuerta))
        {
            Debug.LogError(
                "[Puerta] No se puede reproducir abrirPuerta.",
                this
            );
            return;
        }

        if (comprobacionActual != null)
            StopCoroutine(comprobacionActual);

        animacionIniciada = true;
        animacionFinalizada = false;

        animator.enabled = true;
        animator.speed = 1f;
        animator.Play(EstadoAbrirPuerta, 0, 0f);

        Debug.Log(
            $"[Puerta] Reproducción iniciada. " +
            $"Objeto: {animator.gameObject.name}, " +
            $"Animator habilitado: {animator.enabled}, " +
            $"Velocidad: {animator.speed}",
            this
        );

        comprobacionActual = StartCoroutine(ComprobarAnimacion());
    }

    private IEnumerator ComprobarAnimacion()
    {
        float tiempoTranscurrido = 0f;
        float tiempoUltimoLog = 0f;
        const float tiempoMaximo = 12f;

        // Permite que Unity actualice el estado del Animator.
        yield return null;

        while (tiempoTranscurrido < tiempoMaximo)
        {
            if (animator == null || !animator.enabled)
            {
                Debug.LogError(
                    "[Puerta] El Animator se deshabilitó durante la animación.",
                    this
                );
                yield break;
            }

            AnimatorStateInfo estado =
                animator.GetCurrentAnimatorStateInfo(0);

            bool estadoCorrecto = estado.IsName("abrirPuerta");

            if (estadoCorrecto)
            {
                if (tiempoTranscurrido - tiempoUltimoLog >= 0.5f)
                {
                    Debug.Log(
                        $"[Puerta] Animando. " +
                        $"Progreso: {estado.normalizedTime:P0}, " +
                        $"Velocidad: {animator.speed}",
                        this
                    );

                    tiempoUltimoLog = tiempoTranscurrido;
                }

                if (estado.normalizedTime >= 1f)
                {
                    animacionFinalizada = true;

                    // Mantiene la puerta en su pose final.
                    animator.speed = 0f;
                    LiberarPaso();

                    Debug.Log(
                        "[Puerta] Animación finalizada correctamente.",
                        this
                    );

                    comprobacionActual = null;
                    yield break;
                }
            }
            else if (tiempoTranscurrido - tiempoUltimoLog >= 0.5f)
            {
                Debug.LogWarning(
                    $"[Puerta] El estado actual no es abrirPuerta. " +
                    $"Estado: {estado.fullPathHash}, " +
                    $"Tiempo: {estado.normalizedTime:F2}",
                    this
                );

                tiempoUltimoLog = tiempoTranscurrido;
            }

            tiempoTranscurrido += Time.deltaTime;
            yield return null;
        }

        Debug.LogError(
            "[Puerta] La animación no llegó a su final en 12 segundos.",
            this
        );

        // Aunque la animación falle, el jugador no puede quedar encerrado.
        LiberarPaso();

        comprobacionActual = null;
    }

    private void LiberarPaso()
    {
        if (collidersBloqueoPaso == null || collidersBloqueoPaso.Length == 0)
        {
            Debug.LogWarning(
                "[Puerta] No hay colliders de bloqueo asignados: " +
                "si algo tapa el hueco de la puerta, el jugador no va a poder pasar.",
                this
            );
            return;
        }

        foreach (Collider colliderBloqueo in collidersBloqueoPaso)
        {
            if (colliderBloqueo != null)
                colliderBloqueo.enabled = false;
        }

        Debug.Log("[Puerta] Paso liberado.", this);
    }
}
