
using System;
using System.Collections;
using UnityEngine;

public class FadeTransicionHistoria : MonoBehaviour
{
    public static FadeTransicionHistoria Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private CanvasGroup cortinaNegra;
    [SerializeField] private float duracionFade = 0.8f;

    private Coroutine transicionActual;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (cortinaNegra != null)
        {
            cortinaNegra.alpha = 0f;
            cortinaNegra.blocksRaycasts = false;
            cortinaNegra.interactable = false;
        }
    }

    public void EjecutarTransicion(Action accionDuranteNegro)
    {
        if (cortinaNegra == null)
        {
            Debug.LogError(
                "[FadeHistoria] No asignaste la cortina negra."
            );

            accionDuranteNegro?.Invoke();
            return;
        }

        if (transicionActual != null)
            return;

        transicionActual = StartCoroutine(
            Transicionar(accionDuranteNegro)
        );
    }

    private IEnumerator Transicionar(Action accionDuranteNegro)
    {
        cortinaNegra.blocksRaycasts = true;

        yield return CambiarAlpha(0f, 1f);

        accionDuranteNegro?.Invoke();

        yield return null;
        yield return CambiarAlpha(1f, 0f);

        cortinaNegra.blocksRaycasts = false;
        transicionActual = null;
    }

    private IEnumerator CambiarAlpha(float inicio, float destino)
    {
        float tiempo = 0f;

        while (tiempo < duracionFade)
        {
            tiempo += Time.unscaledDeltaTime;

            cortinaNegra.alpha = Mathf.Lerp(
                inicio,
                destino,
                Mathf.Clamp01(tiempo / duracionFade)
            );

            yield return null;
        }

        cortinaNegra.alpha = destino;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
