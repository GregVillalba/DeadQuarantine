
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

        if (gestor == null ||
            gestor.EstadoMisionNetwork.Value != 1)
            return;

        ulong clientId = rpcParams.Receive.SenderClientId;

        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.ConnectedClients.TryGetValue(
                clientId, out NetworkClient cliente))
            return;

        if (cliente.PlayerObject == null)
            return;

        float distancia = Vector3.Distance(
            cliente.PlayerObject.transform.position,
            transform.position
        );

        if (distancia > distanciaRecogida)
            return;

        recogida = true;
        gestor.RegistrarTarjeta(clientId, this);
    }

    public void DespawnearTarjeta()
    {
        if (!IsServer || !IsSpawned)
            return;

        NetworkObject.Despawn(true);
    }
}
