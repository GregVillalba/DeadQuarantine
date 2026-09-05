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
    }
}
