using Unity.Netcode;

/// <summary>
/// Daño de obstáculos del escenario (alambre de púas). PlayerMovement del dueño detecta que lo toca
/// y lo manda al servidor, que lo aplica con TakeDamage (abatido / eliminado como cualquier daño).
/// </summary>
public partial class PlayerHealth
{
    /// <summary>Lo llama PlayerMovement del dueño al tocar un alambre de púas.</summary>
    public void ReportarDanoObstaculo(int dano, bool ignorarEscudo)
    {
        if (!IsOwner || dano <= 0 || !IsAlive)
            return;

        RecibirDanoObstaculoServerRpc(dano, ignorarEscudo);
    }

    [ServerRpc]
    private void RecibirDanoObstaculoServerRpc(int dano, bool ignorarEscudo)
    {
        if (dano > 0)
            TakeDamage(dano, ignorarEscudo);
    }
}
