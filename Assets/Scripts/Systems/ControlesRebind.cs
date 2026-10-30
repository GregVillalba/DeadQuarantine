using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Rebind temporal de controles.
/// Los cambios se hacen sobre una copia de PlayerControls y recién se guardan
/// cuando ConfiguracionMenuV3Controller llama Commit().
/// </summary>
[DisallowMultipleComponent]
public sealed class ControlesRebind : MonoBehaviour
{
    private static readonly (string goName, string action, string part)[] Bindings =
    {
        ("RebindMoveUp", "Move", "up"),
        ("RebindMoveDown", "Move", "down"),
        ("RebindMoveLeft", "Move", "left"),
        ("RebindMoveRight", "Move", "right"),
        ("RebindJump", "Jump", null),
        ("RebindSprint", "Sprint", null),
        ("RebindInteractuar", "Interact", null),
        ("RebindAgacharse", "Crouch", null),
        ("RebindDisparar", "Fire", null),
        ("RebindApuntar", "Aim", null),
        ("RebindRecargar", "Reload", null),
        ("RebindArmaPrimaria", "SelectPrimary", null),
        ("RebindArmaSecundaria", "SelectSecondary", null),
        ("RebindGolpe", "Melee", null),
        ("RebindSlot1", "UseSlot1", null),
        ("RebindGranada", "Grenade", null),
        ("RebindSaltarCompras", "SkipShop", null),
    };

    private readonly List<Row> rows = new();
    private PlayerControls controls;
    private InputActionRebindingExtensions.RebindingOperation operation;
    private InputAction rebindingAction;
    private bool mapWasEnabled;
    private TMP_Text aviso;

    private sealed class Row
    {
        public Button button;
        public TMP_Text valueText;
        public string action;
        public string part;
    }

    public bool HasPendingChanges { get; private set; }
    public bool IsRebinding => operation != null;

    private void Awake()
    {
        EnsureReady();
    }

    private void OnDestroy()
    {
        CancelActiveRebind();

        if (controls != null)
        {
            controls.Dispose();
            controls = null;
        }
    }

    public void ReloadSaved()
    {
        EnsureReady();
        CancelActiveRebind();

        controls.asset.RemoveAllBindingOverrides();
        ConfiguracionesJuego.CargarRebinds(controls.asset);

        HasPendingChanges = false;
        UpdateRows();
        SetMessage("");
    }

    public void ResetPendingToDefault()
    {
        EnsureReady();
        CancelActiveRebind();

        controls.asset.RemoveAllBindingOverrides();

        HasPendingChanges = true;
        UpdateRows();
        SetMessage("CONTROLES PREPARADOS PARA RESTABLECER // PULSÁ APLICAR");
    }

    public void Commit()
    {
        EnsureReady();
        CancelActiveRebind();

        ConfiguracionesJuego.GuardarRebinds(
            controls.asset
        );

        HasPendingChanges = false;
        SetMessage("CONTROLES APLICADOS");
    }

    private void EnsureReady()
    {
        if (controls != null)
            return;

        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);

        aviso = FindByName<TMP_Text>("Aviso");

        rows.Clear();

        foreach (var binding in Bindings)
        {
            Button button =
                FindByName<Button>(
                    binding.goName
                );

            if (button == null)
                continue;

            TMP_Text value =
                FindChildByName<TMP_Text>(
                    button.transform,
                    "Valor"
                );

            if (value == null)
                value = button.GetComponentInChildren<TMP_Text>(true);

            Row row = new Row
            {
                button = button,
                valueText = value,
                action = binding.action,
                part = binding.part
            };

            rows.Add(row);

            Row captured = row;
            button.onClick.AddListener(
                () => BeginRebind(captured)
            );
        }

        UpdateRows();
    }

    private void BeginRebind(Row row)
    {
        if (controls == null || row == null)
            return;

        InputAction action =
            controls.asset.FindAction(
                row.action,
                false
            );

        if (action == null)
            return;

        int bindingIndex =
            FindBindingIndex(
                action,
                row.part
            );

        if (bindingIndex < 0)
            return;

        CancelActiveRebind();

        InputActionMap map = action.actionMap;
        mapWasEnabled = map != null && map.enabled;
        rebindingAction = action;

        if (mapWasEnabled)
            map.Disable();

        foreach (Row r in rows)
            r.button.interactable = false;

        if (row.valueText != null)
            row.valueText.text = "PRESIONÁ UNA TECLA...";

        SetMessage("ESCUCHANDO ENTRADA // ESC CANCELA");

        operation =
            action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithTimeout(10f)
                .OnCancel(_ => FinishRebind(false))
                .OnComplete(_ => FinishRebind(true));

        operation.Start();
    }

    private void FinishRebind(bool changed)
    {
        InputActionRebindingExtensions.RebindingOperation finished =
            operation;

        operation = null;

        RestoreMap();

        if (finished != null)
            finished.Dispose();

        foreach (Row r in rows)
            r.button.interactable = true;

        if (changed)
        {
            HasPendingChanges = true;
            SetMessage("CAMBIO PENDIENTE // PULSÁ APLICAR");
        }
        else
        {
            SetMessage("");
        }

        UpdateRows();
    }

    private void CancelActiveRebind()
    {
        if (operation == null)
            return;

        InputActionRebindingExtensions.RebindingOperation op =
            operation;

        operation = null;
        RestoreMap();
        op.Dispose();

        foreach (Row r in rows)
            r.button.interactable = true;

        UpdateRows();
    }

    private void RestoreMap()
    {
        if (rebindingAction != null &&
            mapWasEnabled)
        {
            rebindingAction.actionMap.Enable();
        }

        rebindingAction = null;
        mapWasEnabled = false;
    }

    private void UpdateRows()
    {
        if (controls == null)
            return;

        foreach (Row row in rows)
        {
            if (row.valueText == null)
                continue;

            InputAction action =
                controls.asset.FindAction(
                    row.action,
                    false
                );

            if (action == null)
            {
                row.valueText.text = "-";
                continue;
            }

            int index =
                FindBindingIndex(
                    action,
                    row.part
                );

            row.valueText.text =
                index >= 0
                    ? action.GetBindingDisplayString(index)
                        .ToUpperInvariant()
                    : "-";
        }
    }

    private static int FindBindingIndex(
        InputAction action,
        string part)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding =
                action.bindings[i];

            if (!string.IsNullOrEmpty(part))
            {
                if (!binding.isPartOfComposite)
                    continue;

                if (!string.Equals(
                        binding.name,
                        part,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }
            else
            {
                if (binding.isComposite ||
                    binding.isPartOfComposite)
                {
                    continue;
                }
            }

            if (string.IsNullOrEmpty(binding.path))
                continue;

            bool keyboard =
                binding.path.StartsWith(
                    "<Keyboard>",
                    StringComparison.Ordinal
                );

            bool mouse =
                binding.path.StartsWith(
                    "<Mouse>",
                    StringComparison.Ordinal
                );

            if (keyboard || mouse)
                return i;
        }

        return -1;
    }

    private void SetMessage(string message)
    {
        if (aviso != null)
            aviso.text = message;
    }

    private T FindByName<T>(string name)
        where T : Component
    {
        T[] components =
            GetComponentsInChildren<T>(true);

        foreach (T component in components)
        {
            if (component.gameObject.name == name)
                return component;
        }

        return null;
    }

    private static T FindChildByName<T>(
        Transform root,
        string name)
        where T : Component
    {
        T[] components =
            root.GetComponentsInChildren<T>(true);

        foreach (T component in components)
        {
            if (component.gameObject.name == name)
                return component;
        }

        return null;
    }
}
