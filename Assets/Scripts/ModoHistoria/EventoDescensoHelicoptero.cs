
using UnityEngine;

public class EventoDescensoHelicoptero : MonoBehaviour
{
    public void DescensoTerminado()
    {
        GestorMansionHistoria gestor =
            GestorMansionHistoria.Instance;

        if (gestor == null)
        {
            Debug.LogError(
                "[Helipuerto] No se encontró GestorMansionHistoria al terminar el descenso."
            );
            return;
        }

        gestor.RegistrarFinDescensoHelicoptero();
    }
}
