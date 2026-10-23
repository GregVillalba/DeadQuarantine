
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugMisionHistoria : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.f8Key.wasPressedThisFrame)
        {
            if (GestorHistoriaPartida.Instance != null)
            {
                GestorHistoriaPartida.Instance
                    .CompletarMisionLaboratorioParaPruebas();
            }
            else
            {
                Debug.LogWarning(
                    "[Historia Debug] No se encontró GestorHistoriaPartida."
                );
            }
        }
    }
}
