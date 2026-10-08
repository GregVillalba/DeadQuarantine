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
    
    public int armadura;
    public bool esEquipamiento;

    

    public bool esBarril;

    /// <summary>La compra cura al jugador en vez de desbloquear un arma.</summary>
    public bool EsCuracion => puntosDeSalud > 0;
}
