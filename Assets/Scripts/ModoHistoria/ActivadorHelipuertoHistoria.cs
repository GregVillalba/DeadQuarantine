
using UnityEngine;

public class ActivadorHelipuertoHistoria : MonoBehaviour
{
    private bool activado;

    private void OnTriggerEnter(Collider other)
    {
        if (activado)
            return;

        Transform jugador = other.transform.root;

        if (!jugador.CompareTag("Player"))
            return;

        GestorMansionHistoria gestor =
            GestorMansionHistoria.Instance;

        if (gestor == null)
        {
            Debug.LogError(
                "[Helipuerto] No se encontró GestorMansionHistoria."
            );
            return;
        }

        if (gestor.EstadoActual !=
            GestorMansionHistoria.EstadoMision.IrAlHelipuerto)
        {
            return;
        }

        activado = true;

        Debug.Log(
            "[Helipuerto] Jugador llegó al punto de extracción."
        );

        gestor.RegistrarLlegadaAlHelipuerto();
    }
}
