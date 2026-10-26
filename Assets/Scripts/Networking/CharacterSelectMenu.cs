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
        public TMP_Text tierLabel;          // EXPLORADOR / SOLDADO / TANQUE (tipo fijo)
        public TMP_Text nameLabel;          // Nombre propio editable desde CharacterCatalog
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

    [Header("Colores de modificadores")]
    [SerializeField] private Color statPositive = new Color(0.30f, 0.95f, 0.38f, 1f);
    [SerializeField] private Color statNegative = new Color(1.00f, 0.28f, 0.24f, 1f);
    [SerializeField] private Color statNeutral = Color.white;

    private CharacterClass currentClass = CharacterClass.Soldier;
    private CharacterGender currentGender = CharacterGender.Male;

    private float maxHealth = 1f, maxMove = 1f, maxStamina = 1f, maxRegen = 1f, maxReload = 1f;
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
            string variantName = catalog.GetCharacterName(slot.type, currentGender);

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
                slot.nameLabel.text = !string.IsNullOrWhiteSpace(variantName)
                    ? variantName.ToUpperInvariant()
                    : slot.type.ToString().ToUpperInvariant();
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
        if (nameText != null)
        {
            string selectedName = catalog.GetCharacterNameForVariant(index);
            nameText.text = !string.IsNullOrWhiteSpace(selectedName)
                ? selectedName.ToUpperInvariant()
                : currentClass.ToString().ToUpperInvariant();
        }
        if (descriptionText != null) descriptionText.text = data.description;

        // Valores absolutos: se muestran tal cual y la barra se compara contra la clase con mayor valor.
        SetStat(health, data.maxHealth / maxHealth, data.maxHealth.ToString("0"), statNeutral);
        SetStat(stamina, data.maxStamina / maxStamina, data.maxStamina.ToString("0"), statNeutral);

        // Porcentajes: 100% = valor base de referencia.
        // En pantalla mostramos el modificador respecto de 100:
        // 120% -> +20% (verde), 80% -> -20% (rojo), 100% -> 0% (neutral).
        SetPercentStat(moveSpeed, data.moveSpeedPercent, maxMove);
        SetPercentStat(staminaRegen, data.staminaRegenPercent, maxRegen);

        // IMPORTANTE: reloadTimePercent se interpreta visualmente como VELOCIDAD DE RECARGA.
        // Por lo tanto, un valor mayor significa que recarga MÁS RÁPIDO y debe tener una barra mayor.
        // Ejemplo: 120% -> +20% verde y una barra más llena que 100%.
        SetPercentStat(reloadTime, data.reloadTimePercent, maxReload);
    }

    // Tipo/clase fija del personaje. El nombre propio se toma de la Variant del CharacterCatalog.
    public static string TierName(CharacterClass type)
    {
        switch (type)
        {
            case CharacterClass.Scout: return "EXPLORADOR";
            case CharacterClass.Soldier: return "SOLDADO";
            case CharacterClass.Tank: return "TANQUE";
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

    private static void SetStat(StatRow row, float fraction, string text, Color valueColor)
    {
        if (row == null)
            return;

        float t = Mathf.Clamp01(fraction);

        if (row.fill != null)
        {
            // Sistema robusto: el valor de la estadística controla directamente
            // el ANCHO del RectTransform. Así no dependemos de Image.Type.Filled
            // ni de tener un Sprite asignado a la Image.
            row.fill.type = Image.Type.Simple;

            RectTransform rt = row.fill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(t, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        if (row.value != null)
        {
            row.value.text = text;
            row.value.color = valueColor;
        }
    }

    private void SetPercentStat(StatRow row, float percentValue, float maximum)
    {
        float fraction = maximum > 0f ? percentValue / maximum : 0f;
        float modifier = percentValue - 100f;

        string text;
        Color color;

        if (modifier > 0.01f)
        {
            text = "+" + modifier.ToString("0") + "%";
            color = statPositive;
        }
        else if (modifier < -0.01f)
        {
            text = modifier.ToString("0") + "%";
            color = statNegative;
        }
        else
        {
            text = "0%";
            color = statNeutral;
        }

        SetStat(row, fraction, text, color);
    }

    // Las barras se miden contra el MEJOR valor entre las 3 clases:
    // el mejor valor ocupa el 100% del ancho y los demás quedan proporcionados.
    // Ejemplo: velocidades 130 / 100 / 70 -> barras 100% / 77% / 54%.
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
            maxReload = Mathf.Max(maxReload, d.reloadTimePercent);
        }
    }
}