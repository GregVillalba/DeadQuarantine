
using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    // Versión 2: guardado dividido por modos de juego.
    public int version = 2;

    public HistoriaSaveData historia;
    public SupervivenciaSaveData supervivencia;
}

// =====================================================
// GUARDADO DEL MODO HISTORIA
// =====================================================

[Serializable]
public class HistoriaSaveData
{
    // 0 = Ninguno, 1 = SafeZone1, 2 = SafeZone2.
    public int checkpoint;

    public int dificultad;

    public DatosJugadorSaveData jugador = new DatosJugadorSaveData();

    public string fechaGuardado;
}

// =====================================================
// GUARDADO DEL MODO SUPERVIVENCIA
// =====================================================

[Serializable]
public class SupervivenciaSaveData
{
    public int escenario;
    public int dificultad;

    public int rondaActual;
    public bool partidaPerdida;

    public DatosJugadorSaveData jugador = new DatosJugadorSaveData();

    public string fechaGuardado;
}

// =====================================================
// DATOS COMUNES DEL JUGADOR
// =====================================================

[Serializable]
public class DatosJugadorSaveData
{
    // PUNTUACIÓN Y ESTADÍSTICAS

    public int puntuacion;
    public int zombiesEliminados;
    public int disparosRealizados;
    public int disparosAcertados;
    public int reapariciones;
    public int caidas;
    public int headshotsRealizados;

    // ESTADO DEL JUGADOR

    public float salud;
    public int vidas;
    public bool jugadorVivo;
    public bool jugadorEspectando;

    // BLINDAJE

    public int blindaje;

    public int chalecoEscudo;
    public int chalecoEscudoMax;

    public int cascoEscudo;
    public int cascoEscudoMax;

    // INVENTARIO DE CURACIONES

    public List<SlotCuraSaveData> slotsCuras =
        new List<SlotCuraSaveData>();

    // INVENTARIO DE GRANADAS

    public SlotCuraSaveData slotGranada =
        new SlotCuraSaveData();

    // ESPACIOS EXTRA

    public int espaciosExtraCuras;
    public int espaciosExtraGranadas;

    // ARMAS E INVENTARIO

    public List<ArmaSaveData> armas =
        new List<ArmaSaveData>();

    public string armaEquipada;

    // CAMPOS DE COMPATIBILIDAD CON EL SISTEMA ACTUAL.
    // Los revisaremos al adaptar PlayerHealth y el inventario.

    public int cantidadCuraciones;
    public int cantidadGranadas;
    public int espaciosExtra;

    // RECURSOS DEL JUGADOR

    public int pilaDeLatas;
    public int cantimplora;
    public int bidon;
}

// =====================================================
// INFORMACIÓN DE ARMAS
// =====================================================

[Serializable]
public class ArmaSaveData
{
    public string identificador;

    // 0 = Primary, 1 = Secondary.
    public int casillero;

    public int municionCargador;
    public int municionReserva;

    public int mira = -1;
    public int cargador = -1;
    public int laser = -1;
    public int empunadura = -1;
    public int boca = -1;
}

// =====================================================
// INFORMACIÓN DE SLOTS
// =====================================================

[Serializable]
public class SlotCuraSaveData
{
    public string itemId;
    public int cantidad;
    public int curacion;
}
