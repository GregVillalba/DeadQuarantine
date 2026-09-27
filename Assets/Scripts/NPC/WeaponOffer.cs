using System;
using UnityEngine;

[Serializable]
public class WeaponOffer
{
    public string weaponId;
    public string weaponName;
    public int cost;
    public Sprite weaponIcon;

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
}