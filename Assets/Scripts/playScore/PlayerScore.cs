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

        NotificarResultadoCompraClientRpc(weaponId, alcanza, ParametrosParaDueno());
    }

    // Guarda la cura en el inventario del jugador (se usa después con Q / Z). Sin lugar no se cobra.
    [ServerRpc]
    public void ComprarCuracionServerRpc(string itemId, int costo, int puntosDeSalud)
    {
        PlayerHealth health = transform.root.GetComponentInChildren<PlayerHealth>();

        bool exito = health != null &&
                     ScoreNetwork.Value >= costo &&
                     health.GuardarCura(itemId, puntosDeSalud);

        if (exito)
            ScoreNetwork.Value -= costo;

        NotificarResultadoCompraClientRpc(itemId, exito, ParametrosParaDueno());
    }

    // Slot extra: compra única que sube la capacidad de cada slot del inventario.
    [ServerRpc]
    public void ComprarEspacioExtraServerRpc(string itemId, int costo)
    {
        PlayerHealth health = transform.root.GetComponentInChildren<PlayerHealth>();

        bool exito = health != null &&
                     ScoreNetwork.Value >= costo &&
                     health.ComprarEspacioExtra();

        if (exito)
            ScoreNetwork.Value -= costo;

        NotificarResultadoCompraClientRpc(itemId, exito, ParametrosParaDueno());
    }

    // Chaleco / casco: si no se tiene se compra (escudo lleno); si está dañado o roto se repara
    // por costoReparacion; si está lleno no se cobra nada.
    [ServerRpc]
    public void ComprarBlindajeServerRpc(string itemId, PiezaBlindaje pieza, int costoCompra, int costoReparacion, int escudo)
    {
        PlayerHealth health = transform.root.GetComponentInChildren<PlayerHealth>();
        bool exito = false;

        if (health != null && health.IsAlive)
        {
            if (!health.TieneBlindaje(pieza))
            {
                if (ScoreNetwork.Value >= costoCompra && health.EquiparBlindaje(pieza, escudo))
                {
                    ScoreNetwork.Value -= costoCompra;
                    exito = true;
                }
            }
            else if (health.NecesitaReparacion(pieza) && ScoreNetwork.Value >= costoReparacion)
            {
                if (health.RepararBlindaje(pieza))
                {
                    ScoreNetwork.Value -= costoReparacion;
                    exito = true;
                }
            }
        }

        NotificarResultadoCompraClientRpc(itemId, exito, ParametrosParaDueno());
    }

    private ClientRpcParams ParametrosParaDueno()
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };
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
