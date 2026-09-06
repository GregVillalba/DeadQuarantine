using UnityEngine;

public class FPSMonitor : MonoBehaviour
{
    [Header("Configuración del test")]
    [Tooltip("FPS mínimo aceptable según el criterio del test (CP-001)")]
    [SerializeField] private float fpsMinimoAceptable = 30f;

    [Tooltip("Si un solo frame tarda más que esto (en milisegundos), se considera congelamiento crítico")]
    [SerializeField] private float umbralCongelamientoMs = 100f;

    [Tooltip("Cada cuántos segundos se imprime el resumen por consola")]
    [SerializeField] private float intervaloReporte = 2f;

    private float tiempoAcumulado = 0f;
    private int framesAcumulados = 0;

    private float fpsMinimoRegistrado = float.MaxValue;
    private float fpsMaximoRegistrado = 0f;
    private int congelamientosDetectados = 0;

    void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        float fpsActual = 1f / deltaTime;
        float frameMs = deltaTime * 1000f;

        // Actualizar mínimos y máximos del período de test
        if (fpsActual < fpsMinimoRegistrado)
            fpsMinimoRegistrado = fpsActual;

        if (fpsActual > fpsMaximoRegistrado)
            fpsMaximoRegistrado = fpsActual;

        // Detectar congelamiento crítico (frame puntual muy largo)
        if (frameMs > umbralCongelamientoMs)
        {
            congelamientosDetectados++;
            //Debug.LogWarning(
            //    "[FPSMonitor] CONGELAMIENTO CRÍTICO detectado: " +
            //    frameMs.ToString("F1") + " ms en un solo frame (FPS instantáneo: " +
            //    fpsActual.ToString("F1") + ")"
           // );
        }

        // Acumular para el promedio del intervalo
        tiempoAcumulado += deltaTime;
        framesAcumulados++;

        if (tiempoAcumulado >= intervaloReporte)
        {
            float fpsPromedio = framesAcumulados / tiempoAcumulado;

            bool cumpleCriterio = fpsPromedio >= fpsMinimoAceptable;

            string estado = cumpleCriterio ? "OK" : "FALLA";

            //Debug.Log(
            //    "[FPSMonitor] FPS promedio: " + fpsPromedio.ToString("F1") +
            //    " | Mínimo registrado: " + fpsMinimoRegistrado.ToString("F1") +
            //    " | Máximo: " + fpsMaximoRegistrado.ToString("F1") +
            //    " | Congelamientos: " + congelamientosDetectados +
            //    " | CP-001: " + estado
           // );

            tiempoAcumulado = 0f;
            framesAcumulados = 0;
        }
    }

    // Podés llamar esto manualmente (por ejemplo, al terminar la horda)
    // para obtener el resultado final del test
    public void ImprimirResultadoFinal()
    {
        bool testAprobado = fpsMinimoRegistrado >= fpsMinimoAceptable && congelamientosDetectados == 0;

        //Debug.Log(
        //    "===== RESULTADO FINAL CP-001 =====\n" +
        //    "FPS mínimo durante la prueba: " + fpsMinimoRegistrado.ToString("F1") + "\n" +
         //   "FPS máximo durante la prueba: " + fpsMaximoRegistrado.ToString("F1") + "\n" +
        //    "Congelamientos críticos detectados: " + congelamientosDetectados + "\n" +
        //    "Resultado: " + (testAprobado ? "APROBADO ✔" : "FALLIDO ✘")
      //  );
    }
}