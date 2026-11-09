using Unity.Netcode;
public partial class PlayerRecursos
{
    [ServerRpc]
    public void ComprarPerkServerRpc(string id)
    {
        var health = GetComponent<PlayerHealth>();
        bool success = health && health.ComprarPerkServidor(id);
        NotificarResultadoCompraClientRpc(id, success, ParametrosParaDueno());
    }
}
