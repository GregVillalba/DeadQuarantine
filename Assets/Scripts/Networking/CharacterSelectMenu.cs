using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Selector de personaje de UN slot del lobby (el del jugador local): tipo (Ligero / Normal / Pesado),
/// variante (Hombre / Mujer) y estadísticas. Cada cambio se guarda al instante en CharacterSelection (PlayerPrefs),
/// que es lo que lee PlayerCharacter al aparecer el jugador en la partida. El modelo 3D y la publicación a los demás
/// jugadores los hacen LobbyPlayerSlot y el lobby leyendo CharacterSelection.
///
/// Cambiar de género conserva el tipo y viceversa: siempre se resuelve con catalog.FindVariantIndex(tipo, género).
/// </summary>
public class CharacterSelectMenu : MonoBehaviour
{
    [Serializable]
    private class ClassSlot
    {
        public CharacterClass type;
        public Button button;
        public TMP_Text label;
    }

    [Serializable]
    private class StatRow
    {
        public Image fill;          // Image con Type = Filled, Fill Method = Horizontal
        public TMP_Text value;
    }

    [Header("Datos")]
    [SerializeField] private CharacterCatalog catalog;

    [Header("Tipo (poné los 3 botones: Scout, Soldier, Tank)")]
    [SerializeField] private ClassSlot[] classSlots;

    [Header("Variante")]
    [SerializeField] private Button maleButton;
    [SerializeField] private Button femaleButton;

    [Header("Información (opcionales)")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Estadísticas")]
    [SerializeField] private StatRow health;
    [SerializeField] private StatRow walkSpeed;
    [SerializeField] private StatRow sprintSpeed;
    [SerializeField] private StatRow crouchSpeed;

    [Header("Colores")]
    [SerializeField] private Color selectedColor = new Color(0.2f, 1f, 0.4f, 1f);
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.55f);

    private CharacterClass currentClass = CharacterClass.Soldier;
    private CharacterGender currentGender = CharacterGender.Male;

    private float maxHealth = 1f, maxWalk = 1f, maxSprint = 1f, maxCrouch = 1f;
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
        CharacterCatalog.Variant saved = catalog.GetVariant(CharacterSelection.SelectedIndex);

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
        currentGender = gender;
        Apply();
    }

    private void Apply()
    {
        int index = catalog.FindVariantIndex(currentClass, currentGender);

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
        CharacterCatalog.ClassData data = catalog.GetClassDataForVariant(index);

        for (int i = 0; i < classSlots.Length; i++)
        {
            bool selected = classSlots[i].type == currentClass;
            SetSelected(classSlots[i].button, classSlots[i].label, selected);

            if (classSlots[i].label != null)
                classSlots[i].label.text = TierName(classSlots[i].type);
        }

        SetSelected(maleButton, null, currentGender == CharacterGender.Male);
        SetSelected(femaleButton, null, currentGender == CharacterGender.Female);

        if (data == null)
            return;

        if (nameText != null) nameText.text = data.displayName;
        if (descriptionText != null) descriptionText.text = data.description;

        SetStat(health, data.maxHealth, maxHealth, data.maxHealth.ToString());
        SetStat(walkSpeed, data.walkSpeed, maxWalk, data.walkSpeed.ToString("0.#"));
        SetStat(sprintSpeed, data.sprintSpeed, maxSprint, data.sprintSpeed.ToString("0.#"));
        SetStat(crouchSpeed, data.crouchSpeed, maxCrouch, data.crouchSpeed.ToString("0.#"));
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

    private void SetSelected(Button button, TMP_Text label, bool selected)
    {
        Color c = selected ? selectedColor : normalColor;

        if (button != null && button.targetGraphic != null)
            button.targetGraphic.color = c;

        if (label != null)
            label.color = c;
    }

    private static void SetStat(StatRow row, float value, float max, string text)
    {
        if (row == null)
            return;

        if (row.fill != null)
            row.fill.fillAmount = max > 0f ? Mathf.Clamp01(value / max) : 0f;

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
            maxWalk = Mathf.Max(maxWalk, d.walkSpeed);
            maxSprint = Mathf.Max(maxSprint, d.sprintSpeed);
            maxCrouch = Mathf.Max(maxCrouch, d.crouchSpeed);
        }
    }
}