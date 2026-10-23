using UnityEngine;
using Unity.Netcode;

public class RadioHistoriaCiudad : NetworkBehaviour
{
    private bool recogida;

    public void IntentarRecoger()
    {
        if (!IsSpawned || recogida)
            return;

        SolicitarRecogerRadioRpc();
    }

    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Everyone
    )]
    private void SolicitarRecogerRadioRpc(
        RpcParams rpcParams = default)
    {
        if (!IsServer || recogida)
            return;

        GestorHistoriaPartida gestor =
            GestorHistoriaPartida.Instance;

        if (gestor == null ||
            gestor.EstadoMisionNetwork.Value != 4)
            return;

        ulong clientId = rpcParams.Receive.SenderClientId;

        gestor.RegistrarRadio(clientId, this);

        if (gestor.EstadoMisionNetwork.Value != 5)
            return;

        recogida = true;

        Debug.Log(
            "[Radio] Recogida confirmada. Activando detector para cliente " +
            clientId
        );

        // Se usa el gestor, no la radio que acaba de despawnearse.
        gestor.ActivarDetectorSenal(clientId);
    }

    public void DespawnearRadio()
    {
        if (!IsServer || !IsSpawned)
            return;

        NetworkObject.Despawn(true);
    }
}