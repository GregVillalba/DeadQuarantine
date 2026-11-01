
using UnityEngine;

public class ActivadorSalidaCiudadHistoria : MonoBehaviour
{
    private bool activado;

    private void OnTriggerEnter(Collider other)
    {
        if (activado)
            return;

        Transform jugador = other.transform.root;

        if (!jugador.CompareTag("Player"))
            return;

        GestorHistoriaPartida gestor =
            GestorHistoriaPartida.Instance;

        if (gestor == null)
        {
            Debug.LogError(
                "[Salida Ciudad] No se encontró GestorHistoriaPartida."
            );
            return;
        }

        if (!gestor.EsperandoSalidaCiudad)
            return;

        activado = true;

        Debug.Log(
            "[Salida Ciudad] Jugador llegó al camino de salida."
        );

        gestor.IntentarCompletarSalidaCiudad();
    }
}
