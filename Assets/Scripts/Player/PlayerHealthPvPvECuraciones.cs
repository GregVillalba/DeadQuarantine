using System;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.Networking;
using Unity.Collections;
using Unity.Netcode;

/// <summary>Selected class supply. Zero capacity preserves the original inventory rules.</summary>
public struct PvPvEHealingSupply : INetworkSerializable, IEquatable<PvPvEHealingSupply>
{
    public FixedString64Bytes itemId;
    public int capacity, healing;
    public bool Active => capacity > 0 && healing > 0 && itemId.Length > 0;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref itemId);
        serializer.SerializeValue(ref capacity);
        serializer.SerializeValue(ref healing);
    }
    public bool Equals(PvPvEHealingSupply other) => itemId.Equals(other.itemId) && capacity == other.capacity && healing == other.healing;
}

public partial class PlayerHealth
{
    public readonly NetworkVariable<PvPvEHealingSupply> CuracionPvPvE = new(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public bool CuracionPvPvEActiva => CuracionPvPvE.Value.Active;
    public string TipoCuracionPvPvE => CuracionPvPvE.Value.itemId.ToString();

    /// <summary>
    /// Server integration point for initial class selection / respawn in their later phases.
    /// Clients never supply amounts, capacities or healing through an RPC.
    /// Reapplying the same class without refill preserves its remaining supply.
    /// </summary>
    public bool AplicarCuracionDeClaseServidor(PvPvEClassDefinition selectedClass, bool refill = false)
    {
        if (!IsServer || !IsSpawned) return false;
        var roster = PvPvETeamRoster.Active;
        if (!roster || !roster.IsSpawned || roster.NetworkManager != NetworkManager ||
            roster.Closing.Value || !roster.Contains(OwnerClientId)) return false;
        var catalog = roster.Configuration.Loadouts;
        if (!catalog || !catalog.Validate(selectedClass, out _)) return false;
        if(string.IsNullOrEmpty(selectedClass.healing)){
            CuracionPvPvE.Value=default;
            for(int i=0;i<SlotsCuras.Count;i++)SlotsCuras[i]=default;
            return true;
        }
        var entry = catalog.FindHealing(selectedClass.healing);
        if (entry == null || entry.healing <= 0 || entry.carryCount <= 0 || entry.carryCount > 99) return false;
        var supply = new PvPvEHealingSupply { itemId = new FixedString64Bytes(entry.id), capacity = catalog.HealingCapacity(selectedClass), healing = entry.healing };
        bool changed = !CuracionPvPvE.Value.Equals(supply);
        if (!changed && !refill) return true;
        // Validate everything before replacing the old inventory. Only the chosen type survives.
        CuracionPvPvE.Value = supply;
        for (int i = 0; i < SlotsCuras.Count; i++) SlotsCuras[i] = default;
        if (SlotsCuras.Count == 0) SlotsCuras.Add(default);
        SlotsCuras[0] = new SlotCura { itemId = supply.itemId, cantidad = supply.capacity, curacion = supply.healing };
        return true;
    }

    private void CuracionDeClaseChanged(PvPvEHealingSupply previous, PvPvEHealingSupply current) => OnInventarioChanged?.Invoke();
}
