using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Compatibilidad con dropdowns gráficos anteriores.
/// El menú V3 usa su propio dropdown pendiente + botón APLICAR.
/// </summary>
[RequireComponent(typeof(TMP_Dropdown))]
public sealed class GraphicsQualityDropdown : MonoBehaviour
{
    private TMP_Dropdown dropdown;

    private void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();
    }

    private void OnEnable()
    {
        dropdown.onValueChanged.RemoveListener(
            OnValueChanged
        );

        dropdown.ClearOptions();

        dropdown.AddOptions(
            new List<string>(
                GraphicsSettingsManager.PresetNames
            )
        );

        dropdown.SetValueWithoutNotify(
            GraphicsQuality.Current
        );

        dropdown.RefreshShownValue();

        dropdown.onValueChanged.AddListener(
            OnValueChanged
        );
    }

    private void OnDisable()
    {
        dropdown.onValueChanged.RemoveListener(
            OnValueChanged
        );
    }

    private void OnValueChanged(int index)
    {
        // Solo para UIs antiguas que todavía usen este componente.
        // En el menú V3 el controlador maneja cambios pendientes.
        GraphicsQuality.Set(index);
    }
}
