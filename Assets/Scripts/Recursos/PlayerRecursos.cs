using System;
using Unity.Netcode;

/// <summary>
/// Inventario de recursos del jugador (Pila de latas, Cantimplora y Bidón) y compras en la tienda
/// del mercader pagadas con esos recursos. Cada jugador tiene el suyo. Lo escribe solo el servidor;
/// todos lo pueden leer.
/// </summary>
public class PlayerRecursos : NetworkBehaviour
{
    public NetworkVariable<int> PilaDeLatasNetwork = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> CantimploraNetwork = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> BidonNetwork = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public event Action<string, bool> OnPurchaseResult;

    /// <summary>Variable de red de un recurso. Sirve para escuchar sus cambios (OnValueChanged).</summary>
    public NetworkVariable<int> RecursoNetwork(TipoRecurso tipo)
    {
        switch (tipo)
        {
            case TipoRecurso.Cantimplora: return CantimploraNetwork;
            case TipoRecurso.Bidon: return BidonNetwork;
            default: return PilaDeLatasNetwork;
        }
    }

    public int Cantidad(TipoRecurso tipo)
    {
        return RecursoNetwork(tipo).Value;
    }

    /// <summary>true si el jugador tiene al menos lo que pide el precio de cada recurso.</summary>
    public bool Alcanza(PrecioRecursos precio)
    {
        foreach (TipoRecurso tipo in Enum.GetValues(typeof(TipoRecurso)))
        {
            if (Cantidad(tipo) < precio.Cantidad(tipo))
                return false;
        }

        return true;
    }

    public void SumarRecurso(TipoRecurso tipo, int cantidad)
    {
        if (!IsServer || cantidad <= 0)
            return;

        RecursoNetwork(tipo).Value += cantidad;
    }

    // Descuenta todo el precio o nada: sin recursos suficientes de alguno no se toca ninguno.
    public bool GastarRecursos(PrecioRecursos precio)
    {
        if (!IsServer || !Alcanza(precio))
            return false;

        foreach (TipoRecurso tipo in Enum.GetValues(typeof(TipoRecurso)))
        {
            int cantidad = precio.Cantidad(tipo);

            if (cantidad > 0)
                RecursoNetwork(tipo).Value -= cantidad;
        }

        return true;
    }

    // =========================================================
    // COMPRAS EN LA TIENDA DEL MERCADER
    // =========================================================

    // Armas, munición y barril: el arma se desbloquea (o la munición se restaura) del lado del cliente al confirmarse.
    [ServerRpc]
    public void ComprarArmaServerRpc(string weaponId, PrecioRecursos precio)
    {
        bool exito = GastarRecursos(precio);

        NotificarResultadoCompraClientRpc(weaponId, exito, ParametrosParaDueno());
    }

    // Guarda la cura en el inventario del jugador (se usa después con Q / Z). Sin lugar no se cobra.
    [ServerRpc]
    public void ComprarCuracionServerRpc(string itemId, PrecioRecursos precio, int puntosDeSalud)
    {
        PlayerHealth health = transform.root.GetComponentInChildren<PlayerHealth>();

        bool exito = health != null &&
                     Alcanza(precio) &&
                     health.GuardarCura(itemId, puntosDeSalud);

        if (exito)
            GastarRecursos(precio);

        NotificarResultadoCompraClientRpc(itemId, exito, ParametrosParaDueno());
    }

    // Guarda la granada en el slot G del jugador. Sin lugar (slot lleno o de otro tipo) no se cobra.
    [ServerRpc]
    public void ComprarGranadaServerRpc(string itemId, PrecioRecursos precio)
    {
        PlayerHealth health = transform.root.GetComponentInChildren<PlayerHealth>();

        bool exito = health != null &&
                     Alcanza(precio) &&
                     health.GuardarGranada(itemId);

        if (exito)
            GastarRecursos(precio);

        NotificarResultadoCompraClientRpc(itemId, exito, ParametrosParaDueno());
    }

    // Slot extra: sube la capacidad de un slot (curas o granadas). Se compra por separado para cada uno,
    // varias veces (hasta el máximo de PlayerHealth).
    [ServerRpc]
    public void ComprarEspacioExtraServerRpc(string itemId, PrecioRecursos precio, SlotInventarioExtra slot)
    {
        PlayerHealth health = transform.root.GetComponentInChildren<PlayerHealth>();

        bool exito = health != null &&
                     Alcanza(precio) &&
                     health.ComprarEspacioExtra(slot);

        if (exito)
            GastarRecursos(precio);

        NotificarResultadoCompraClientRpc(itemId, exito, ParametrosParaDueno());
    }

    // Chaleco / casco: si no se tiene se compra (escudo lleno); si está dañado o roto se repara
    // por precioReparacion; si está lleno no se cobra nada.
    [ServerRpc]
    public void ComprarBlindajeServerRpc(string itemId, PiezaBlindaje pieza, PrecioRecursos precioCompra, PrecioRecursos precioReparacion, int escudo)
    {
        PlayerHealth health = transform.root.GetComponentInChildren<PlayerHealth>();
        bool exito = false;

        if (health != null && health.IsAlive)
        {
            if (!health.TieneBlindaje(pieza))
            {
                if (Alcanza(precioCompra) && health.EquiparBlindaje(pieza, escudo))
                {
                    GastarRecursos(precioCompra);
                    exito = true;
                }
            }
            else if (health.NecesitaReparacion(pieza) && Alcanza(precioReparacion))
            {
                if (health.RepararBlindaje(pieza))
                {
                    GastarRecursos(precioReparacion);
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
}
