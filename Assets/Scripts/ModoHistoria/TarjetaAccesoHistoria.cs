
using UnityEngine;
using Unity.Netcode;

public class TarjetaAccesoHistoria : NetworkBehaviour
{
    [SerializeField] private float distanciaRecogida = 3f;

    private bool recogida;

    public void IntentarRecoger()
    {
        if (!IsSpawned || recogida)
            return;

        SolicitarRecogerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SolicitarRecogerRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || recogida)
            return;

        GestorHistoriaPartida gestor = GestorHistoriaPartida.Instance;

        if (gestor == null)
        {
            Debug.LogWarning(
                "[Tarjeta] No se encontró GestorHistoriaPartida."
            );
            return;
        }

        if (gestor.EstadoMisionNetwork.Value != 1)
        {
            Debug.LogWarning(
                "[Tarjeta] No se puede recoger: la misión no está en BuscarTarjeta."
            );
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;

        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.ConnectedClients.TryGetValue(
                clientId, out NetworkClient cliente))
        {
            Debug.LogWarning(
                "[Tarjeta] No se encontró el cliente que intenta recogerla."
            );
            return;
        }

        if (cliente.PlayerObject == null)
        {
            Debug.LogWarning(
                "[Tarjeta] El cliente no tiene PlayerObject."
            );
            return;
        }

        float distancia = Vector3.Distance(
            cliente.PlayerObject.transform.position,
            transform.position
        );

        if (distancia > distanciaRecogida)
        {
            Debug.LogWarning(
                "[Tarjeta] El jugador está demasiado lejos. Distancia: " +
                distancia
            );
            return;
        }

        // El gestor confirma el registro antes de marcarla como recogida.
        if (gestor.RegistrarTarjeta(clientId, this))
        {
            recogida = true;

            Debug.Log(
                "[Tarjeta] Recogida confirmada para el cliente " + clientId
            );
        }
        else
        {
            Debug.LogWarning(
                "[Tarjeta] GestorHistoriaPartida rechazó el registro."
            );
        }
    }

    public void DespawnearTarjeta()
    {
        if (!IsServer || !IsSpawned)
        {
            Debug.LogWarning(
                "[Tarjeta] No se puede despawnear. IsServer=" + IsServer +
                " | IsSpawned=" + IsSpawned
            );
            return;
        }

        Debug.Log(
            "[Tarjeta] Ejecutando NetworkObject.Despawn(true)."
        );

        NetworkObject.Despawn(true);
    }
}
