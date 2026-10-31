using UnityEngine;
using System.Diagnostics;

public class RastreadorDestruccion : MonoBehaviour
{
    private void OnDisable()
    {
        // Si el objeto o la escena se destruyen, OnDisable se ejecuta en el instante exacto de la orden
        StackTrace trace = new StackTrace(true);
        UnityEngine.Debug.LogWarning($"[RASTREO] {gameObject.name} se desactivó o comenzó su destrucción desde:\n{trace}");
    }

    private void OnDestroy()
    {
        UnityEngine.Debug.LogError($"[ALERTA FINAL] ¡{gameObject.name} DESTRUIDO!");
    }
}