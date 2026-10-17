using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra cuántos recursos de cada tipo tiene el jugador (un círculo por recurso: icono y cantidad abajo).
/// Se usa en la tienda del mercader y en el HUD (Recursos_HUD). Se actualiza sola cuando cambia una cantidad.
/// </summary>
public class RecursosTiendaUI : MonoBehaviour
{
    [Serializable]
    private class RecursoTienda
    {
        public TipoRecurso tipo;
        public Image icono;
        public TextMeshProUGUI cantidadText;
    }

    [Tooltip("Un elemento por círculo de recurso.")]
    [SerializeField] private List<RecursoTienda> recursos = new List<RecursoTienda>();

    [Tooltip("Opcional. Si queda vacío se busca el PlayerRecursos del jugador al que pertenece esta UI.")]
    [SerializeField] private PlayerRecursos playerRecursos;

    [SerializeField] private Color colorIconoConRecursos = Color.white;
    [Tooltip("Tinte del icono cuando el jugador no tiene ninguna unidad de ese recurso.")]
    [SerializeField] private Color colorIconoSinRecursos = new Color(0.45f, 0.45f, 0.45f, 1f);

    private bool suscripto;

    private void OnEnable()
    {
        if (playerRecursos == null)
            playerRecursos = transform.root.GetComponentInChildren<PlayerRecursos>(true);

        Suscribirse();
        ActualizarRecursos();
    }

    private void OnDisable()
    {
        Desuscribirse();
    }

    private void Suscribirse()
    {
        if (suscripto || playerRecursos == null)
            return;

        foreach (TipoRecurso tipo in Enum.GetValues(typeof(TipoRecurso)))
            playerRecursos.RecursoNetwork(tipo).OnValueChanged += OnRecursoCambiado;

        suscripto = true;
    }

    private void Desuscribirse()
    {
        if (!suscripto || playerRecursos == null)
            return;

        foreach (TipoRecurso tipo in Enum.GetValues(typeof(TipoRecurso)))
            playerRecursos.RecursoNetwork(tipo).OnValueChanged -= OnRecursoCambiado;

        suscripto = false;
    }

    private void OnRecursoCambiado(int previousValue, int currentValue)
    {
        ActualizarRecursos();
    }

    private void ActualizarRecursos()
    {
        foreach (RecursoTienda recurso in recursos)
        {
            if (recurso == null)
                continue;

            int cantidad = playerRecursos != null ? playerRecursos.Cantidad(recurso.tipo) : 0;

            if (recurso.cantidadText != null)
                recurso.cantidadText.text = cantidad.ToString();

            if (recurso.icono != null)
                recurso.icono.color = cantidad > 0 ? colorIconoConRecursos : colorIconoSinRecursos;
        }
    }
}
