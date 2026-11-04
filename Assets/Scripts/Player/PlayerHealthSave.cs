using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerHealth
{
    
/// <summary>
/// Copia el estado de vida, blindaje e inventario al objeto de guardado.
/// Debe ejecutarse en el servidor.
/// </summary>
public void GuardarDatosJugador(DatosJugadorSaveData datos)
{
    if (!IsServer || datos == null || !IsSpawned)
        return;

    // VIDA Y VIDAS
    datos.salud = CurrentHealth.Value;
    datos.vidas = Lives.Value;

    // En un checkpoint seguro el jugador debe continuar vivo.
    datos.jugadorVivo = State.Value == PlayerState.Alive;
    datos.jugadorEspectando = false;

    // BLINDAJE TOTAL Y POR PIEZA
    datos.blindaje = Armor.Value;

    datos.chalecoEscudo = VestShield.Value;
    datos.chalecoEscudoMax = VestShieldMax.Value;

    datos.cascoEscudo = HelmetShield.Value;
    datos.cascoEscudoMax = HelmetShieldMax.Value;

    // INVENTARIO DE CURACIONES
    datos.slotsCuras = new List<SlotCuraSaveData>();

    if (SlotsCuras != null)
    {
        for (int i = 0; i < SlotsCuras.Count; i++)
        {
            SlotCura slot = SlotsCuras[i];

            datos.slotsCuras.Add(new SlotCuraSaveData
            {
                itemId = slot.itemId.ToString(),
                cantidad = slot.cantidad,
                curacion = slot.curacion
            });
        }
    }

    // INVENTARIO DE GRANADAS
    SlotCura granada = SlotGranada.Value;

    datos.slotGranada = new SlotCuraSaveData
    {
        itemId = granada.itemId.ToString(),
        cantidad = granada.cantidad,
        curacion = granada.curacion
    };

    // ESPACIOS EXTRA
    datos.espaciosExtraCuras = EspaciosExtraCuras.Value;
    datos.espaciosExtraGranadas = EspaciosExtraGranadas.Value;
}

/// <summary>
/// Restaura el estado de vida, blindaje e inventario desde un guardado.
/// Debe ejecutarse en el servidor, después de OnNetworkSpawn.
/// </summary>
public void RestaurarDatosJugador(DatosJugadorSaveData datos)
{
    if (!IsServer || datos == null || !IsSpawned)
        return;

    // Detener cualquier temporizador de abatido anterior.
    if (downedCoroutine != null)
    {
        StopCoroutine(downedCoroutine);
        downedCoroutine = null;
    }

    // VIDA Y VIDAS
    CurrentHealth.Value = Mathf.Clamp(
        Mathf.RoundToInt(datos.salud),
        1,
        maxHealth
    );

    Lives.Value = Mathf.Max(0, datos.vidas);

    // En un checkpoint siempre se recupera en estado normal.
    State.Value = PlayerState.Alive;
    DownedTimeRemaining.Value = 0f;

    lastDamageTime = Time.time;
    nextRegenerationTime = Time.time + RegenDelay;

    // BLINDAJE
    int maximoChaleco = Mathf.Max(0, datos.chalecoEscudoMax);
    int maximoCasco = Mathf.Max(0, datos.cascoEscudoMax);

    VestShieldMax.Value = maximoChaleco;
    VestShield.Value = Mathf.Clamp(
        datos.chalecoEscudo,
        0,
        maximoChaleco
    );

    HelmetShieldMax.Value = maximoCasco;
    HelmetShield.Value = Mathf.Clamp(
        datos.cascoEscudo,
        0,
        maximoCasco
    );

    Armor.Value = VestShield.Value + HelmetShield.Value;

    // INVENTARIO DE CURACIONES
    SlotsCuras.Clear();

    for (int i = 0; i < cantidadSlotsCuras; i++)
    {
        SlotCura slot = default;

        if (datos.slotsCuras != null &&
            i < datos.slotsCuras.Count)
        {
            SlotCuraSaveData guardado = datos.slotsCuras[i];

            if (guardado != null &&
                !string.IsNullOrEmpty(guardado.itemId) &&
                guardado.cantidad > 0)
            {
                slot.itemId = new Unity.Collections.FixedString64Bytes(
                    guardado.itemId
                );

                slot.cantidad = guardado.cantidad;
                slot.curacion = guardado.curacion;
            }
        }

        SlotsCuras.Add(slot);
    }

    // INVENTARIO DE GRANADAS
    SlotCuraSaveData granadaGuardada = datos.slotGranada;

    if (granadaGuardada != null &&
        !string.IsNullOrEmpty(granadaGuardada.itemId) &&
        granadaGuardada.cantidad > 0)
    {
        SlotGranada.Value = new SlotCura
        {
            itemId = new Unity.Collections.FixedString64Bytes(
                granadaGuardada.itemId
            ),
            cantidad = granadaGuardada.cantidad,
            curacion = granadaGuardada.curacion
        };
    }
    else
    {
        SlotGranada.Value = default;
    }

    // ESPACIOS EXTRA
    EspaciosExtraCuras.Value = Mathf.Clamp(
        datos.espaciosExtraCuras,
        0,
        maxEspaciosExtra
    );

    EspaciosExtraGranadas.Value = Mathf.Clamp(
        datos.espaciosExtraGranadas,
        0,
        maxEspaciosExtra
    );

    Debug.Log(
        "[PlayerHealth] Datos del jugador restaurados. " +
        "Vida: " + CurrentHealth.Value +
        " | Vidas: " + Lives.Value +
        " | Blindaje: " + Armor.Value
    );

    OnInventarioChanged?.Invoke();
    OnBlindajeChanged?.Invoke();
}

}