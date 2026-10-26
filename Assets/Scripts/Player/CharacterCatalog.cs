using System;
using UnityEngine;

public enum CharacterClass
{
    Soldier = 0,
    Tank = 1,
    Scout = 2
}

public enum CharacterGender
{
    Male = 0,
    Female = 1
}

/// <summary>
/// Catálogo de personajes. Se crea UNA vez: Assets > Create > Game > Character Catalog.
///
/// - Classes: las 3 clases con sus estadísticas (las comparten hombre y mujer).
/// - Variants: cada variante hombre/mujer tiene su propio "Nombre del personaje", modelo e icono.
///   El tipo visible del menú es fijo: EXPLORADOR / SOLDADO / TANQUE.
///   Movimiento, regeneración y velocidad de recarga usan 100 como referencia. La vida es un valor absoluto y
///   la estamina usa 100 como referencia del valor base configurado en PlayerMovement.
/// - Variants: los modelos (clase + género). El ORDEN define el número de personaje que se guarda y se sincroniza
///   por red (0 = soldado hombre, 1 = soldado mujer, ...). No cambies el orden una vez publicado el juego.
///   Una variante SIN Model Prefab se considera "no existe": el Tanque mujer se deja vacío y el Tanque queda sin
///   elección de género.
///
/// Si ya tenías el catálogo creado: clic derecho en el título del componente > "Aplicar estadísticas por defecto".
/// </summary>
[CreateAssetMenu(fileName = "CharacterCatalog", menuName = "Game/Character Catalog")]
public class CharacterCatalog : ScriptableObject
{
    [Serializable]
    public class ClassData
    {
        public CharacterClass type;

        // Se conserva por compatibilidad con catálogos ya creados.
        // Los nombres visibles de hombre/mujer ahora están en Variants.
        [HideInInspector]
        public string displayName;

        [InspectorName("Descripción")]
        [TextArea(2, 4)] public string description;
        [Tooltip("Icono de respaldo, solo si la variante no tiene el suyo (ver Variants > Icon).")]
        public Sprite icon;

        [Header("Estadísticas")]
        [Min(1)] public int maxHealth = 100;
        [Tooltip("Velocidad de movimiento en % (caminar, correr y agachado). 100 = Normal.")]
        [Min(1)] public float moveSpeedPercent = 100f;
        [Tooltip("Estamina máxima. 100 = Normal (equivale a la estamina del Inspector de PlayerMovement).")]
        [Min(1)] public float maxStamina = 100f;
        [Tooltip("Regeneración de estamina en %. 100 = Normal.")]
        [Min(1)] public float staminaRegenPercent = 100f;
        [Tooltip("Velocidad de recarga en %. MAYOR = recarga más rápida. 100 = Normal, 120 = 20% más rápido, 90 = 10% más lento.")]
        [Min(1)] public float reloadTimePercent = 100f;

        public float MoveMultiplier => moveSpeedPercent / 100f;
        public float StaminaMultiplier => maxStamina / 100f;
        public float StaminaRegenMultiplier => staminaRegenPercent / 100f;

        /// <summary>
        /// Multiplicador de VELOCIDAD de recarga.
        /// 1 = normal, 1.2 = 20% más rápido, 0.9 = 10% más lento.
        /// </summary>
        public float ReloadSpeedMultiplier => reloadTimePercent / 100f;

        // Alias conservado para no romper código viejo que todavía use ReloadMultiplier.
        public float ReloadMultiplier => ReloadSpeedMultiplier;
    }

    [Serializable]
    public class Variant
    {
        public CharacterClass type;
        public CharacterGender gender;

        [InspectorName("Nombre del personaje")]
        [Tooltip("Nombre propio de ESTA variante. Hombre y mujer pueden tener nombres diferentes.")]
        public string characterName;

        [Tooltip("Prefab del personaje (SM_Chr_...). De ahí se toma el cuerpo y los accesorios que estén ACTIVOS en el prefab. Vacío = esta variante no existe.")]
        public GameObject modelPrefab;

        [Tooltip("Icono (retrato) de ESTA variante: el de hombre y el de mujer son distintos. Si queda vacío se usa el Icon de la clase.")]
        public Sprite icon;
    }

    [SerializeField]
    private ClassData[] classes =
    {
        new ClassData
        {
            type = CharacterClass.Soldier, displayName = "Soldado",
            description = "Equilibrado. Buena vida y velocidad para cualquier situación.",
            maxHealth = 100, moveSpeedPercent = 100f, maxStamina = 100f, staminaRegenPercent = 100f, reloadTimePercent = 100f
        },
        new ClassData
        {
            type = CharacterClass.Tank, displayName = "Tanque",
            description = "Aguanta el doble de daño, pero se mueve más lento.",
            maxHealth = 200, moveSpeedPercent = 70f, maxStamina = 80f, staminaRegenPercent = 90f, reloadTimePercent = 120f
        },
        new ClassData
        {
            type = CharacterClass.Scout, displayName = "Explorador",
            description = "Rápido y ágil, pero frágil. Un error sale caro.",
            maxHealth = 80, moveSpeedPercent = 115f, maxStamina = 120f, staminaRegenPercent = 125f, reloadTimePercent = 90f
        }
    };

    [SerializeField]
    private Variant[] variants =
    {
        new Variant { type = CharacterClass.Soldier, gender = CharacterGender.Male,   characterName = "Mason" },
        new Variant { type = CharacterClass.Soldier, gender = CharacterGender.Female, characterName = "Reyes" },
        new Variant { type = CharacterClass.Tank,    gender = CharacterGender.Male,   characterName = "Knox" },
        new Variant { type = CharacterClass.Tank,    gender = CharacterGender.Female, characterName = "Mara" },
        new Variant { type = CharacterClass.Scout,   gender = CharacterGender.Male,   characterName = "Ryder" },
        new Variant { type = CharacterClass.Scout,   gender = CharacterGender.Female, characterName = "Vega" }
    };

    public int VariantCount => variants != null ? variants.Length : 0;

    public Variant GetVariant(int index)
    {
        return (variants != null && index >= 0 && index < variants.Length) ? variants[index] : null;
    }

    public ClassData GetClassData(CharacterClass type)
    {
        if (classes == null)
            return null;

        for (int i = 0; i < classes.Length; i++)
        {
            if (classes[i].type == type)
                return classes[i];
        }

        return null;
    }

    public ClassData GetClassDataForVariant(int index)
    {
        Variant variant = GetVariant(index);
        return variant != null ? GetClassData(variant.type) : null;
    }

    /// <summary>
    /// Nombre propio de la variante pedida. Si esa variante no tiene modelo,
    /// usa la variante jugable de la misma clase. Si todavía no configuraste
    /// el nombre nuevo, usa el antiguo displayName de la clase como respaldo.
    /// </summary>
    public string GetCharacterName(CharacterClass type, CharacterGender gender)
    {
        int index = FindPlayableVariantIndex(type, gender);
        return GetCharacterNameForVariant(index);
    }

    /// <summary>Nombre propio de una variante por índice.</summary>
    public string GetCharacterNameForVariant(int index)
    {
        Variant variant = GetVariant(index);

        if (variant == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(variant.characterName))
            return variant.characterName;

        // Compatibilidad con CharacterCatalog viejos.
        ClassData data = GetClassData(variant.type);

        if (data != null && !string.IsNullOrWhiteSpace(data.displayName))
            return data.displayName;

        return variant.type.ToString();
    }

    /// <summary>Número de personaje para una clase + género. -1 si no existe en la lista.</summary>
    public int FindVariantIndex(CharacterClass type, CharacterGender gender)
    {
        for (int i = 0; i < VariantCount; i++)
        {
            if (variants[i].type == type && variants[i].gender == gender)
                return i;
        }

        return -1;
    }

    /// <summary>Icono de una clase + género: el de la variante jugable; si no tiene, el icono de la clase.</summary>
    public Sprite GetIcon(CharacterClass type, CharacterGender gender)
    {
        Variant v = GetVariant(FindPlayableVariantIndex(type, gender));

        if (v != null && v.icon != null)
            return v.icon;

        ClassData data = GetClassData(type);
        return data != null ? data.icon : null;
    }

    /// <summary>¿Esta clase tiene modelo hombre Y mujer? Si no (ej: Tanque), no se puede elegir género.</summary>
    public bool HasGenderChoice(CharacterClass type)
    {
        bool male = false;
        bool female = false;

        for (int i = 0; i < VariantCount; i++)
        {
            if (variants[i].type != type || variants[i].modelPrefab == null)
                continue;

            if (variants[i].gender == CharacterGender.Male) male = true;
            else female = true;
        }

        return male && female;
    }

    /// <summary>
    /// Número de personaje a usar para clase + género: el pedido si tiene modelo; si no, la otra variante de la clase
    /// que sí lo tenga (el Tanque siempre resuelve a su único modelo).
    /// </summary>
    public int FindPlayableVariantIndex(CharacterClass type, CharacterGender gender)
    {
        int wanted = FindVariantIndex(type, gender);

        if (wanted >= 0 && variants[wanted].modelPrefab != null)
            return wanted;

        for (int i = 0; i < VariantCount; i++)
        {
            if (variants[i].type == type && variants[i].modelPrefab != null)
                return i;
        }

        return wanted;
    }

    /// <summary>Si el número apunta a una variante sin modelo (ej: un 3 viejo guardado), devuelve la jugable de su clase.</summary>
    public int ResolveIndex(int index)
    {
        Variant v = GetVariant(index);

        if (v == null || v.modelPrefab != null)
            return index;

        for (int i = 0; i < VariantCount; i++)
        {
            if (variants[i].type == v.type && variants[i].modelPrefab != null)
                return i;
        }

        return index;
    }

    /// <summary>
    /// Completa SOLO los nombres vacíos de las variantes.
    /// No pisa nombres que ya hayas personalizado.
    /// </summary>
    [ContextMenu("Aplicar nombres por defecto a variantes")]
    private void ApplyDefaultVariantNames()
    {
        if (variants == null)
            return;

        for (int i = 0; i < variants.Length; i++)
        {
            Variant v = variants[i];

            if (v == null || !string.IsNullOrWhiteSpace(v.characterName))
                continue;

            if (v.type == CharacterClass.Soldier && v.gender == CharacterGender.Male)
                v.characterName = "Mason";
            else if (v.type == CharacterClass.Soldier && v.gender == CharacterGender.Female)
                v.characterName = "Reyes";
            else if (v.type == CharacterClass.Tank && v.gender == CharacterGender.Male)
                v.characterName = "Knox";
            else if (v.type == CharacterClass.Tank && v.gender == CharacterGender.Female)
                v.characterName = "Mara";
            else if (v.type == CharacterClass.Scout && v.gender == CharacterGender.Male)
                v.characterName = "Ryder";
            else if (v.type == CharacterClass.Scout && v.gender == CharacterGender.Female)
                v.characterName = "Vega";
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>Pone las estadísticas del diseño en las 3 clases sin tocar iconos, nombres ni modelos.</summary>
    [ContextMenu("Aplicar estadísticas por defecto")]
    private void ApplyDefaultStats()
    {
        if (classes == null)
            return;

        for (int i = 0; i < classes.Length; i++)
        {
            ClassData c = classes[i];

            switch (c.type)
            {
                case CharacterClass.Soldier:
                    c.maxHealth = 100; c.moveSpeedPercent = 100f; c.maxStamina = 100f; c.staminaRegenPercent = 100f; c.reloadTimePercent = 100f;
                    break;
                case CharacterClass.Tank:
                    c.maxHealth = 200; c.moveSpeedPercent = 70f; c.maxStamina = 80f; c.staminaRegenPercent = 90f; c.reloadTimePercent = 120f;
                    break;
                case CharacterClass.Scout:
                    c.maxHealth = 80; c.moveSpeedPercent = 115f; c.maxStamina = 120f; c.staminaRegenPercent = 125f; c.reloadTimePercent = 90f;
                    break;
            }
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}