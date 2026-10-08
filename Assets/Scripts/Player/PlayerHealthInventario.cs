using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Un slot del inventario de curas: qué cura guarda y cuántas.</summary>
public struct SlotCura : INetworkSerializable, IEquatable<SlotCura>
{
    public FixedString64Bytes itemId;
    public int cantidad;
    public int curacion;

    public bool Vacio => cantidad <= 0;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref itemId);
        serializer.SerializeValue(ref cantidad);
        serializer.SerializeValue(ref curacion);
    }

    public bool Equals(SlotCura other)
    {
        return itemId.Equals(other.itemId) && cantidad == other.cantidad && curacion == other.curacion;
    }
}

/// <summary>
/// Inventario de curas del jugador (slot 1 al lado de la munición en el HUD; el slot 2 es de las granadas).
/// Las curas compradas en el mercader se guardan acá y se usan con UseSlot1 (Q por defecto).
/// Cada slot guarda un solo tipo de cura: la compra va al slot que ya tiene ese tipo (si hay lugar)
/// o al primer slot vacío. El "Slot extra" del mercader sube la capacidad de los slots de curas.
/// </summary>
public partial class PlayerHealth
{
    [Header("Inventario de curas")]
    [Tooltip("Cantidad de slots de curas. Hoy es 1: el slot 2 del HUD es de las granadas.")]
    [Min(1)] [SerializeField] private int cantidadSlotsCuras = 1;
    [Tooltip("Curas del mismo tipo que entran en un slot.")]
    [Min(1)] [SerializeField] private int capacidadSlot = 1;
    [Tooltip("Curas del mismo tipo que entran en un slot después de comprar el Slot extra.")]
    [Min(1)] [SerializeField] private int capacidadSlotConEspacioExtra = 2;

    // Se crea en Awake (antes del spawn de red), igual que las NetworkList de RoundManager.
    public NetworkList<SlotCura> SlotsCuras;

    public NetworkVariable<bool> EspacioExtraComprado =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>Cambió algún slot o la capacidad. Lo usan el HUD y la tienda.</summary>
    public event Action OnInventarioChanged;

    public int CantidadSlotsCuras => cantidadSlotsCuras;
    public int CapacidadPorSlot => EspacioExtraComprado.Value ? capacidadSlotConEspacioExtra : capacidadSlot;

    private PlayerControls controlesInventario;
    private InputAction accionSlot1;

    private void Awake()
    {
        SlotsCuras = new NetworkList<SlotCura>(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    }

    // ---------------------------------------------------------
    // Ciclo de vida (lo llama PlayerHealth en spawn / despawn)
    // ---------------------------------------------------------

    private void InicializarInventarioServidor()
    {
        SlotsCuras.Clear();

        for (int i = 0; i < cantidadSlotsCuras; i++)
            SlotsCuras.Add(default);

        EspacioExtraComprado.Value = false;
    }

    private void SuscribirInventario()
    {
        SlotsCuras.OnListChanged += InventarioListaChanged;
        EspacioExtraComprado.OnValueChanged += EspacioExtraChanged;

        if (IsOwner)
        {
            controlesInventario = new PlayerControls();
            ConfiguracionesJuego.CargarRebinds(controlesInventario.asset);

            accionSlot1 = controlesInventario.asset.FindAction("UseSlot1", false);
            accionSlot1?.Enable();
        }

        OnInventarioChanged?.Invoke();
    }

    private void DesuscribirInventario()
    {
        SlotsCuras.OnListChanged -= InventarioListaChanged;
        EspacioExtraComprado.OnValueChanged -= EspacioExtraChanged;

        if (controlesInventario != null)
        {
            controlesInventario.Disable();
            controlesInventario.Dispose();
            controlesInventario = null;
        }

        accionSlot1 = null;
    }

    private void InventarioListaChanged(NetworkListEvent<SlotCura> cambio)
    {
        OnInventarioChanged?.Invoke();
    }

    private void EspacioExtraChanged(bool anterior, bool actual)
    {
        OnInventarioChanged?.Invoke();
    }

    // ---------------------------------------------------------
    // Input (solo el dueño)
    // ---------------------------------------------------------

    private void LeerInputInventario()
    {
        if (!IsOwner || NPCShopUI.LocalShopOpen || PauseController.LocalPlayerPaused)
            return;

        if (accionSlot1 != null && accionSlot1.WasPressedThisFrame())
            IntentarUsarSlot(0);
    }

    private void IntentarUsarSlot(int indice)
    {
        // Con la vida llena (o abatido) no se gasta la cura.
        if (!PuedeCurarse || GetSlotCura(indice).Vacio)
            return;

        UsarSlotCuraServerRpc(indice);
    }

    // ---------------------------------------------------------
    // Consultas
    // ---------------------------------------------------------

    /// <summary>Tecla asignada al slot de curas (respeta la reasignación de teclas). Vacío si no hay.</summary>
    public string TeclaSlotCura => accionSlot1 != null ? accionSlot1.GetBindingDisplayString() : string.Empty;

    public SlotCura GetSlotCura(int indice)
    {
        if (SlotsCuras == null || indice < 0 || indice >= SlotsCuras.Count)
            return default;

        return SlotsCuras[indice];
    }

    /// <summary>true si hay lugar para guardar una cura de este tipo.</summary>
    public bool PuedeGuardarCura(string itemId)
    {
        return BuscarSlotPara(itemId) >= 0;
    }

    // Primero el slot que ya tiene ese tipo de cura (si le queda lugar); si no, el primer slot vacío.
    private int BuscarSlotPara(string itemId)
    {
        if (SlotsCuras == null)
            return -1;

        for (int i = 0; i < SlotsCuras.Count; i++)
        {
            SlotCura slot = SlotsCuras[i];

            if (!slot.Vacio && slot.itemId.ToString() == itemId && slot.cantidad < CapacidadPorSlot)
                return i;
        }

        for (int i = 0; i < SlotsCuras.Count; i++)
        {
            if (SlotsCuras[i].Vacio)
                return i;
        }

        return -1;
    }

    // ---------------------------------------------------------
    // Servidor
    // ---------------------------------------------------------

    /// <summary>Solo servidor. Guarda una cura comprada. false si no hay lugar.</summary>
    public bool GuardarCura(string itemId, int curacion)
    {
        if (!IsServer || curacion <= 0)
            return false;

        int indice = BuscarSlotPara(itemId);

        if (indice < 0)
            return false;

        SlotCura slot = SlotsCuras[indice];

        if (slot.Vacio)
        {
            slot.itemId = new FixedString64Bytes(itemId);
            slot.curacion = curacion;
            slot.cantidad = 0;
        }

        slot.cantidad++;
        SlotsCuras[indice] = slot;
        return true;
    }

    /// <summary>Solo servidor. Compra única del Slot extra. false si ya estaba comprado.</summary>
    public bool ComprarEspacioExtra()
    {
        if (!IsServer || EspacioExtraComprado.Value)
            return false;

        EspacioExtraComprado.Value = true;
        return true;
    }

    [ServerRpc]
    private void UsarSlotCuraServerRpc(int indice)
    {
        if (indice < 0 || indice >= SlotsCuras.Count || !PuedeCurarse)
            return;

        SlotCura slot = SlotsCuras[indice];

        if (slot.Vacio)
            return;

        Heal(slot.curacion);

        slot.cantidad--;
        SlotsCuras[indice] = slot.Vacio ? default : slot;
    }
}
