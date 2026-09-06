using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerScore : NetworkBehaviour
{
    public NetworkVariable<int> ScoreNetwork = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<int> ZombiesEliminadosNetwork = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> DisparosRealizadosNetwork = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> DisparosAcertadosNetwork = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int PrecisionPorcentaje => DisparosRealizadosNetwork.Value > 0
        ? Mathf.RoundToInt(
            100f*DisparosAcertadosNetwork.Value / DisparosRealizadosNetwork.Value
        )
        : 0;

    public NetworkVariable<int> ReaparicionesNetwork = new NetworkVariable<int>(
    0,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);

public NetworkVariable<int> CaidasNetwork = new NetworkVariable<int>(
    0,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);

public void SumarCaida()
{
    if (!IsServer)
        return;

    CaidasNetwork.Value++;
}

public void SumarReaparicion()
{
    if (!IsServer)
        return;

    ReaparicionesNetwork.Value++;
}
    public event Action<string, bool> OnPurchaseResult;

    [ServerRpc]
    public void ComprarArmaServerRpc(string weaponId, int costo)
    {
        bool alcanza = ScoreNetwork.Value >= costo;

        if (alcanza)
        {
            ScoreNetwork.Value -= costo;
        }

        ClientRpcParams targetParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };

        NotificarResultadoCompraClientRpc(weaponId, alcanza, targetParams);
    }

    [ClientRpc]
    private void NotificarResultadoCompraClientRpc(string weaponId, bool exito, ClientRpcParams clientRpcParams = default)
    {
        OnPurchaseResult?.Invoke(weaponId, exito);
    }

    public void SumarPuntos(int cantidad)
    {
        if (!IsServer)
            return;

        ScoreNetwork.Value += cantidad;
    }

    public void SumarZombieEliminado()
    {
        if (!IsServer)
            return;

        ZombiesEliminadosNetwork.Value++;
    }


    [ServerRpc]
    public void RegistrarDisparoServerRpc()
    {
        DisparosRealizadosNetwork.Value++;
    }
    
    [ServerRpc]
    public void RegistrarImpactoServerRpc()
    {
        DisparosAcertadosNetwork.Value++;
        SumarPuntos(50);
    }


}
