using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Selector de personaje (estilo shooter): 3 tarjetas de clase con icono + nombre, pestañas Hombre / Mujer y panel de
/// información con estadísticas. Cada cambio se guarda al instante en CharacterSelection (PlayerPrefs), que es lo que
/// lee PlayerCharacter al aparecer el jugador en la partida (singleplayer y multiplayer). El modelo 3D lo muestra
/// CharacterSelectScreen leyendo CharacterSelection.
///
/// Cambiar de género conserva el tipo y viceversa. Si la clase no tiene los dos modelos (Tanque), las pestañas
/// Hombre / Mujer se apagan y no se puede elegir género.
/// El diseño se arma solo con Game > Construir UI de Selección de Personaje (ver CharacterSelectUIBuilder).
/// </summary>
public class CharacterSelectMenu : MonoBehaviour
{
    [Serializable]
    private class ClassSlot
    {
        public CharacterClass type;
        public Button button;               // la tarjeta (su Target Graphic es el fondo)
        public Image icon;                  // retrato de la variante elegida (Variant.icon)
        public TMP_Text tierLabel;          // LIGERO / NORMAL / PESADO
        public TMP_Text nameLabel;          // Explorador / Soldado / Tanque
        public GameObject selectedFrame;    // marca de "seleccionada"
    }

    [Serializable]
    private class StatRow
    {
        public Image fill;          // barra: si es Simple se recorta con anchors; si es Filled usa fillAmount
        public TMP_Text value;
    }

    [Header("Datos")]
    [SerializeField] private CharacterCatalog catalog;

    [Header("Tarjetas de clase (Scout, Soldier, Tank)")]
    [SerializeField] private ClassSlot[] classSlots;

    [Header("Variante")]
    [SerializeField] private Button maleButton;
    [SerializeField] private TMP_Text maleLabel;
    [SerializeField] private Button femaleButton;
    [SerializeField] private TMP_Text femaleLabel;

    [Header("Información")]
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Estadísticas")]
    [SerializeField] private StatRow health;
    [SerializeField] private StatRow moveSpeed;
    [SerializeField] private StatRow stamina;
    [SerializeField] private StatRow staminaRegen;
    [SerializeField] private StatRow reloadTime;

    [Header("Colores")]
    [SerializeField] private Color accentColor = new Color(0.25f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color textNormal = new Color(0.78f, 0.82f, 0.82f, 1f);
    [SerializeField] private Color textDim = new Color(0.40f, 0.43f, 0.43f, 1f);
    [SerializeField] private Color textOnAccent = new Color(0.04f, 0.06f, 0.07f, 1f);
    [SerializeField] private Color cardNormal = new Color(0.05f, 0.07f, 0.08f, 0.85f);
    [SerializeField] private Color cardSelected = new Color(0.09f, 0.15f, 0.12f, 0.95f);
    [SerializeField] private Color tabNormal = new Color(0.10f, 0.12f, 0.13f, 0.90f);
    [SerializeField] private Color tabDisabled = new Color(0.07f, 0.08f, 0.09f, 0.60f);

    private CharacterClass currentClass = CharacterClass.Soldier;
    private CharacterGender currentGender = CharacterGender.Male;

    private float maxHealth = 1f, maxMove = 1f, maxStamina = 1f, maxRegen = 1f, minReload = 1000f;
    private bool wired;

    private void Wire()
    {
        if (wired)
            return;

        wired = true;

        for (int i = 0; i < classSlots.Length; i++)
        {
            CharacterClass type = classSlots[i].type;

            if (classSlots[i].button != null)
                classSlots[i].button.onClick.AddListener(() => SelectClass(type));
        }

        if (maleButton != null)
            maleButton.onClick.AddListener(() => SelectGender(CharacterGender.Male));

        if (femaleButton != null)
            femaleButton.onClick.AddListener(() => SelectGender(CharacterGender.Female));

        ComputeStatMaximums();
    }

    private void OnEnable()
    {
        Wire();

        // Retoma la selección guardada (C.A. 7: se conserva entre sesiones y escenas).
        CharacterCatalog.Variant saved = catalog.GetVariant(catalog.ResolveIndex(CharacterSelection.SelectedIndex));

        if (saved != null)
        {
            currentClass = saved.type;
            currentGender = saved.gender;
        }

        Apply();
    }

    public void SelectClass(CharacterClass type)
    {
        currentClass = type;
        Apply();
    }

    public void SelectGender(CharacterGender gender)
    {
        // Sin variantes (Tanque) no se puede elegir.
        if (!catalog.HasGenderChoice(currentClass))
            return;

        currentGender = gender;
        Apply();
    }

    private void Apply()
    {
        // Si la clase no tiene el género pedido (Tanque mujer), resuelve a su único modelo sin perder el género recordado.
        int index = catalog.FindPlayableVariantIndex(currentClass, currentGender);

        if (index < 0)
        {
            Debug.LogWarning("[CharacterSelectMenu] No hay variante para " + currentClass + " / " + currentGender);
            return;
        }

        if (CharacterSelection.SelectedIndex != index)
            CharacterSelection.SelectedIndex = index;

        Refresh(index);
    }

    private void Refresh(int index)
    {
        // Tarjetas
        for (int i = 0; i < classSlots.Length; i++)
        {
            ClassSlot slot = classSlots[i];
            bool selected = slot.type == currentClass;
            CharacterCatalog.ClassData cd = catalog.GetClassData(slot.type);

            if (slot.button != null && slot.button.targetGraphic != null)
                slot.button.targetGraphic.color = selected ? cardSelected : cardNormal;

            if (slot.selectedFrame != null)
                slot.selectedFrame.SetActive(selected);

            if (slot.tierLabel != null)
            {
                slot.tierLabel.text = TierName(slot.type);
                slot.tierLabel.color = selected ? accentColor : textNormal;
            }

            if (slot.nameLabel != null)
            {
                slot.nameLabel.text = cd != null ? cd.displayName.ToUpperInvariant() : slot.type.ToString().ToUpperInvariant();
                slot.nameLabel.color = selected ? Color.white : textNormal;
            }

            if (slot.icon != null)
            {
                // Cada variante (hombre / mujer) tiene su propio icono: la tarjeta muestra el del género elegido.
                Sprite sprite = catalog.GetIcon(slot.type, currentGender);
                slot.icon.sprite = sprite;
                slot.icon.enabled = sprite != null;
            }
        }

        // Pestañas Hombre / Mujer (apagadas si la clase no tiene los dos modelos)
        bool genderChoice = catalog.HasGenderChoice(currentClass);

        SetTab(maleButton, maleLabel, genderChoice, currentGender == CharacterGender.Male);
        SetTab(femaleButton, femaleLabel, genderChoice, currentGender == CharacterGender.Female);

        // Panel de información
        CharacterCatalog.ClassData data = catalog.GetClassDataForVariant(index);

        if (data == null)
            return;

        if (tierText != null) tierText.text = TierName(currentClass);
        if (nameText != null) nameText.text = data.displayName.ToUpperInvariant();
        if (descriptionText != null) descriptionText.text = data.description;

        SetStat(health, data.maxHealth / maxHealth, data.maxHealth.ToString());
        SetStat(moveSpeed, data.moveSpeedPercent / maxMove, data.moveSpeedPercent.ToString("0") + "%");
        SetStat(stamina, data.maxStamina / maxStamina, data.maxStamina.ToString("0"));
        SetStat(staminaRegen, data.staminaRegenPercent / maxRegen, data.staminaRegenPercent.ToString("0") + "%");
        // En la recarga menos es mejor: la barra se llena más cuanto más rápida es.
        SetStat(reloadTime, data.reloadTimePercent > 0f ? minReload / data.reloadTimePercent : 0f, data.reloadTimePercent.ToString("0") + "%");
    }

    // Nombres de la US (C.A. 3). El nombre propio de la clase (Soldado, Tanque...) sale del catálogo.
    public static string TierName(CharacterClass type)
    {
        switch (type)
        {
            case CharacterClass.Scout: return "LIGERO";
            case CharacterClass.Soldier: return "NORMAL";
            case CharacterClass.Tank: return "PESADO";
            default: return type.ToString().ToUpperInvariant();
        }
    }

    private void SetTab(Button button, TMP_Text label, bool enabledChoice, bool selected)
    {
        if (button != null)
        {
            button.interactable = enabledChoice;

            if (button.targetGraphic != null)
                button.targetGraphic.color = !enabledChoice ? tabDisabled : (selected ? accentColor : tabNormal);
        }

        if (label != null)
            label.color = !enabledChoice ? textDim : (selected ? textOnAccent : textNormal);
    }

    private static void SetStat(StatRow row, float fraction, string text)
    {
        if (row == null)
            return;

        float t = Mathf.Clamp01(fraction);

        if (row.fill != null)
        {
            if (row.fill.type == Image.Type.Filled)
            {
                row.fill.fillAmount = t;
            }
            else
            {
                RectTransform rt = row.fill.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(t, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        if (row.value != null)
            row.value.text = text;
    }

    // Las barras se miden contra el mejor valor entre las 3 clases.
    private void ComputeStatMaximums()
    {
        foreach (CharacterClass type in Enum.GetValues(typeof(CharacterClass)))
        {
            CharacterCatalog.ClassData d = catalog.GetClassData(type);

            if (d == null)
                continue;

            maxHealth = Mathf.Max(maxHealth, d.maxHealth);
            maxMove = Mathf.Max(maxMove, d.moveSpeedPercent);
            maxStamina = Mathf.Max(maxStamina, d.maxStamina);
            maxRegen = Mathf.Max(maxRegen, d.staminaRegenPercent);
            minReload = Mathf.Min(minReload, Mathf.Max(1f, d.reloadTimePercent));
        }
    }
}