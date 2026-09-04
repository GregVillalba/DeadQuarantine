using Unity.Netcode;

public class PlayerScore : NetworkBehaviour
{
    public NetworkVariable<int> ScoreNetwork = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public void SumarPuntos(int cantidad)
    {
        if (!IsServer)
            return;

        ScoreNetwork.Value += cantidad;
    }
}
