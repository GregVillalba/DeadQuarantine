using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Pone este componente en un TMP_Dropdown del menú de opciones. Se llena solo con los presets de calidad
/// (los niveles de Project Settings > Quality), muestra el actual y lo cambia al elegir otro.
/// </summary>
[RequireComponent(typeof(TMP_Dropdown))]
public class GraphicsQualityDropdown : MonoBehaviour
{
    private TMP_Dropdown dropdown;

    private void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();
    }

    private void OnEnable()
    {
        dropdown.onValueChanged.RemoveListener(OnValueChanged);

        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(QualitySettings.names));
        dropdown.SetValueWithoutNotify(GraphicsQuality.Current);
        dropdown.RefreshShownValue();

        dropdown.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnDisable()
    {
        dropdown.onValueChanged.RemoveListener(OnValueChanged);
    }

    private void OnValueChanged(int index)
    {
        GraphicsQuality.Set(index);
    }
}