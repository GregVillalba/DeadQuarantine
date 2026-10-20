using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CarruselImagenes : MonoBehaviour
{
    [SerializeField] Image imagenUI;          // el Image donde se ve el laboratorio
    [SerializeField] Sprite[] sprites;        // las fotos del laboratorio
    [SerializeField] float segundosPorImagen = 3f;
    [SerializeField] float duracionFade = 0.5f;
    [SerializeField] Color colorFade = Color.black;

    Coroutine rutina;

    void OnEnable()
    {
        Iniciar(sprites);
    }

    // Llamalo al elegir/hover otro escenario (Ciudad, Mansión...) con sus propias imágenes
    public void Iniciar(Sprite[] nuevos)
    {
        sprites = nuevos;
        if (rutina != null) StopCoroutine(rutina);
        if (sprites == null || sprites.Length == 0) return;
        rutina = StartCoroutine(Ciclo());
    }

    IEnumerator Ciclo()
    {
        int i = 0;
        imagenUI.sprite = sprites[0];
        Aplicar(1f);

        while (sprites.Length > 1)
        {
            yield return new WaitForSecondsRealtime(segundosPorImagen);
            yield return Fade(1f, 0f);
            i = (i + 1) % sprites.Length;
            imagenUI.sprite = sprites[i];
            yield return Fade(0f, 1f);
        }
    }

    IEnumerator Fade(float desde, float hasta)
    {
        for (float t = 0; t < duracionFade; t += Time.unscaledDeltaTime)
        {
            Aplicar(Mathf.Lerp(desde, hasta, t / duracionFade));
            yield return null;
        }
        Aplicar(hasta);
    }

    // 1 = imagen normal, 0 = totalmente teñida con colorFade
    void Aplicar(float v)
    {
        imagenUI.color = Color.Lerp(colorFade, Color.white, v);
    }
}
