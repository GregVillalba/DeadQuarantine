
using UnityEngine;

public class LectorTarjetaHistoria : MonoBehaviour
{
    [SerializeField] private GestorHistoriaPartida gestor;

    private void Awake()
    {
        if (gestor == null)
            gestor = GestorHistoriaPartida.Instance;
    }

    public void IntentarInteractuar()
    {
        if (gestor == null)
            gestor = GestorHistoriaPartida.Instance;

        if (gestor == null)
            return;

        gestor.IntentarAbrirPuertaRpc();
    }
}
