using System;
using UnityEngine;

/// <summary>Sección de la tienda en la que aparece una oferta.</summary>
public enum CategoriaTienda
{
    Armas,
    Utilidades,
    Consumibles,
    Equipamiento
}

/// <summary>Pieza de blindaje que da escudo. Ninguna = la oferta no es blindaje.</summary>
public enum PiezaBlindaje
{
    Ninguna,
    Chaleco,
    Casco
}

[Serializable]
public class WeaponOffer
{
    public string weaponId;
    public string weaponName;
    public int cost;
    public Sprite weaponIcon;

    [Tooltip("Imagen grande del modelo para la tarjeta de la tienda (se genera con Tools > Tienda > Generar imagen de arma). Si queda vacía se usa Weapon Icon.")]
    public Sprite imagenTarjeta;

    [Tooltip("Sección de la tienda en la que se muestra esta oferta.")]
    public CategoriaTienda categoria = CategoriaTienda.Armas;

    [Tooltip("Desmarcalo para mostrar la oferta en la tienda sin que todavía se pueda comprar (ej: arma en desarrollo).")]
    public bool disponible = true;

    [Header("En qué partidas aparece")]
    [Tooltip("Dificultades en las que la oferta aparece en la tienda. En las demás directamente no se muestra.")]
    public DificultadesTienda dificultades = DisponibilidadTienda.TodasLasDificultades;

    [Tooltip("Modos de juego en los que la oferta aparece en la tienda. En los demás directamente no se muestra.")]
    public ModosTienda modos = DisponibilidadTienda.TodosLosModos;

    [Header("Descripción y estadísticas (panel de detalle)")]
    [TextArea(2, 4)]
    public string descripcion;
    public int dano;
    public float alcance;

    [Tooltip("Marcalo para armas arrojadizas (granadas, etc). En esos casos no se muestran Cargador ni Cadencia en el panel de detalle.")]
    public bool esArrojadiza;

    public int capacidadCargador;
    public float cadencia;

    [Tooltip("Marcalo para objetos de un solo uso (frascos de vida, munición, etc). Se pueden comprar varias veces.")]
    public bool esConsumible;

    [Tooltip("Vida que recupera al comprarlo. Si es mayor a 0, la compra cura al jugador en vez de desbloquear un arma.")]
    public int puntosDeSalud;

    public bool esMunicion;
    
    [Tooltip("Puntos de escudo que da la pieza de blindaje (chaleco / casco).")]
    public int armadura;
    public bool esEquipamiento;

    [Tooltip("Chaleco o Casco: al comprarlo da 'Armadura' puntos de escudo. Si el escudo de la pieza baja, " +
             "se puede reparar en la tienda por un porcentaje del costo (ver NPCWeaponVendor).")]
    public PiezaBlindaje piezaBlindaje = PiezaBlindaje.Ninguna;

    [Tooltip("Slot extra: compra única que sube la capacidad de cada slot del inventario de curas.")]
    public bool esEspacioInventario;

    

    public bool esBarril;

    /// <summary>La compra cura al jugador en vez de desbloquear un arma.</summary>
    public bool EsCuracion => puntosDeSalud > 0;

    /// <summary>La compra equipa (o repara) un chaleco / casco en vez de desbloquear un arma.</summary>
    public bool EsBlindaje => piezaBlindaje != PiezaBlindaje.Ninguna && armadura > 0;

    public bool SeMuestraEnDificultad(DifficultyLevel nivel)
    {
        return (dificultades & DisponibilidadTienda.AFlag(nivel)) != 0;
    }

    // Sin modo elegido (escena abierta directo desde el editor) se muestra siempre.
    public bool SeMuestraEnModo(ConfiguracionPartidaSeleccionada.ModoJuego modo)
    {
        ModosTienda flag = DisponibilidadTienda.AFlag(modo);
        return flag == 0 || (modos & flag) != 0;
    }
}
