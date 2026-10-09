using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Un slot del inventario de utilidades: qué objeto guarda y cuántos (curas y granadas).</summary>
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
/// Inventario de utilidades del jugador (al lado de la munición en el HUD):
/// - Slot de curas (tecla Q, UseSlot1).
/// - Slot de granadas (tecla G, lo tira GrenadeThrow).
/// El jugador empieza con los dos slots vacíos y con capacidad 1 por slot.
/// Cada slot guarda un solo tipo de objeto: la compra va al slot que ya tiene ese tipo (si hay lugar)
/// o al slot vacío. Cada slot tiene su propio "Slot extra" en el mercader: se compra por separado y
/// varias veces (por defecto 1 + 2 compras = 3 por slot).
/// </summary>
public partial class PlayerHealth
{
    [Header("Inventario de utilidades")]
    [Tooltip("Cantidad de slots de curas. Hoy es 1: el slot 2 del HUD es de las granadas.")]
    [Min(1)] [SerializeField] private int cantidadSlotsCuras = 1;
    [Tooltip("Objetos del mismo tipo que entran en un slot al empezar (sin ningún Slot extra).")]
    [Min(1)] [SerializeField] private int capacidadSlot = 1;
    [Tooltip("Cuánto sube la capacidad de cada slot por cada Slot extra comprado.")]
    [Min(1)] [SerializeField] private int incrementoPorEspacioExtra = 1;
    [Tooltip("Cuántas veces se puede comprar el Slot extra de CADA slot (curas y granadas por separado). Con capacidad 1 y 2 compras: 3 por slot.")]
    [Min(0)] [SerializeField] private int maxEspaciosExtra = 2;

    // Se crea en Awake (antes del spawn de red), igual que las NetworkList de RoundManager.
    public NetworkList<SlotCura> SlotsCuras;

    /// <summary>Slot G: qué granada lleva el jugador y cuántas. Vacío = no puede tirar granadas.</summary>
    public NetworkVariable<SlotCura> SlotGranada =
        new NetworkVariable<SlotCura>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>Cuántos Slot extra se compraron para el slot de curas (0 hasta maxEspaciosExtra).</summary>
    public NetworkVariable<int> EspaciosExtraCuras =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>Cuántos Slot extra se compraron para el slot de granadas (0 hasta maxEspaciosExtra).</summary>
    public NetworkVariable<int> EspaciosExtraGranadas =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>Cambió algún slot o la capacidad. Lo usan el HUD y la tienda.</summary>
    public event Action OnInventarioChanged;

    public int CantidadSlotsCuras => cantidadSlotsCuras;
    /// <summary>Capacidad actual del slot de curas (Q).</summary>
    public int CapacidadCuras => capacidadSlot + EspaciosExtraCuras.Value * incrementoPorEspacioExtra;

    /// <summary>Capacidad actual del slot de granadas (G).</summary>
    public int CapacidadGranadas => capacidadSlot + EspaciosExtraGranadas.Value * incrementoPorEspacioExtra;

    /// <summary>Cuántos Slot extra se compraron para ese slot.</summary>
    public int EspaciosExtraDe(SlotInventarioExtra slot)
    {
        return slot == SlotInventarioExtra.Granadas ? EspaciosExtraGranadas.Value : EspaciosExtraCuras.Value;
    }

    /// <summary>true si ya no se pueden comprar más Slot extra para ese slot.</summary>
    public bool EspaciosExtraAlMaximo(SlotInventarioExtra slot)
    {
        return EspaciosExtraDe(slot) >= maxEspaciosExtra;
    }

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

        SlotGranada.Value = default;
        EspaciosExtraCuras.Value = 0;
        EspaciosExtraGranadas.Value = 0;
    }

    private void SuscribirInventario()
    {
        SlotsCuras.OnListChanged += InventarioListaChanged;
        SlotGranada.OnValueChanged += SlotGranadaChanged;
        EspaciosExtraCuras.OnValueChanged += EspaciosExtraChanged;
        EspaciosExtraGranadas.OnValueChanged += EspaciosExtraChanged;

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
        SlotGranada.OnValueChanged -= SlotGranadaChanged;
        EspaciosExtraCuras.OnValueChanged -= EspaciosExtraChanged;
        EspaciosExtraGranadas.OnValueChanged -= EspaciosExtraChanged;

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

    private void SlotGranadaChanged(SlotCura anterior, SlotCura actual)
    {
        OnInventarioChanged?.Invoke();
    }

    private void EspaciosExtraChanged(int anterior, int actual)
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

    /// <summary>true si hay lugar en el slot G para una granada de este tipo.</summary>
    public bool PuedeGuardarGranada(string itemId)
    {
        SlotCura slot = SlotGranada.Value;

        if (slot.Vacio)
            return true;

        return slot.itemId.ToString() == itemId && slot.cantidad < CapacidadGranadas;
    }

    // Primero el slot que ya tiene ese tipo de cura (si le queda lugar); si no, el primer slot vacío.
    private int BuscarSlotPara(string itemId)
    {
        if (SlotsCuras == null)
            return -1;

        for (int i = 0; i < SlotsCuras.Count; i++)
        {
            SlotCura slot = SlotsCuras[i];

            if (!slot.Vacio && slot.itemId.ToString() == itemId && slot.cantidad < CapacidadCuras)
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

    /// <summary>Solo servidor. Guarda una granada comprada en el slot G. false si no hay lugar.</summary>
    public bool GuardarGranada(string itemId)
    {
        if (!IsServer || string.IsNullOrEmpty(itemId) || !PuedeGuardarGranada(itemId))
            return false;

        SlotCura slot = SlotGranada.Value;

        if (slot.Vacio)
        {
            slot.itemId = new FixedString64Bytes(itemId);
            slot.curacion = 0;
            slot.cantidad = 0;
        }

        slot.cantidad++;
        SlotGranada.Value = slot;
        return true;
    }

    /// <summary>Solo servidor. Compra del Slot extra de ese slot (curas o granadas). false si ya está al máximo.</summary>
    public bool ComprarEspacioExtra(SlotInventarioExtra slot)
    {
        if (!IsServer || EspaciosExtraAlMaximo(slot))
            return false;

        if (slot == SlotInventarioExtra.Granadas)
            EspaciosExtraGranadas.Value++;
        else
            EspaciosExtraCuras.Value++;

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

    /// <summary>
    /// Lo llama GrenadeThrow del dueño al soltar la granada. El servidor valida que quede una en el slot G,
    /// crea el proyectil y recién ahí la descuenta.
    /// </summary>
    [ServerRpc]
    public void SolicitarLanzamientoGranadaServerRpc(Vector3 posicion, Vector3 velocidad)
    {
        if (!IsAlive)
            return;

        SlotCura slot = SlotGranada.Value;

        if (slot.Vacio)
            return;

        GrenadeThrow lanzador = transform.root.GetComponentInChildren<GrenadeThrow>(true);

        if (lanzador == null)
        {
            Debug.LogWarning("[PlayerHealth] No se encontró GrenadeThrow en el jugador.");
            return;
        }

        if (!lanzador.CrearProyectilServidor(slot.itemId.ToString(), posicion, velocidad))
            return;

        slot.cantidad--;
        SlotGranada.Value = slot.Vacio ? default : slot;
    }
}