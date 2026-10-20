using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Precio de una oferta en recursos, en la tarjeta de la tienda del mercader:
/// un icono y una cantidad por recurso. Lo completa NPCShopUI.
/// </summary>
public class PrecioRecursosUI : MonoBehaviour
{
    [Serializable]
    private class PrecioRecurso
    {
        public TipoRecurso tipo;
        public Image icono;
        public TextMeshProUGUI cantidadText;
    }

    [Tooltip("Un elemento por recurso del precio.")]
    [SerializeField] private List<PrecioRecurso> precios = new List<PrecioRecurso>();

    [Tooltip("Tinte del icono y la cantidad de los recursos que la oferta no pide (precio 0).")]
    [SerializeField] private Color colorNoPide = new Color(1f, 1f, 1f, 0.3f);

    /// <summary>
    /// Muestra cuánto pide la oferta de un recurso. colorCantidad se usa solo si lo pide
    /// (ej: verde si al jugador le alcanza, rojo si no).
    /// </summary>
    public void MostrarPrecio(TipoRecurso tipo, int precio, Color colorCantidad)
    {
        bool pide = precio > 0;

        foreach (PrecioRecurso recurso in precios)
        {
            if (recurso == null || recurso.tipo != tipo)
                continue;

            if (recurso.cantidadText != null)
            {
                recurso.cantidadText.text = Mathf.Max(0, precio).ToString();
                recurso.cantidadText.color = pide ? colorCantidad : colorNoPide;
            }

            if (recurso.icono != null)
                recurso.icono.color = pide ? Color.white : colorNoPide;
        }
    }
}
