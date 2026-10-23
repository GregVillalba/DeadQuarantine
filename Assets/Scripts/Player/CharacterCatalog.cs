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
/// - Classes: las 3 clases con sus estadísticas (las comparten hombre y mujer). El lobby las muestra tal cual.
/// - Variants: los 6 modelos (clase + género). El ORDEN define el número de personaje que se guarda y se sincroniza
///   por red (0 = soldado hombre, 1 = soldado mujer, ...). No cambies el orden una vez publicado el juego.
/// </summary>
[CreateAssetMenu(fileName = "CharacterCatalog", menuName = "Game/Character Catalog")]
public class CharacterCatalog : ScriptableObject
{
    [Serializable]
    public class ClassData
    {
        public CharacterClass type;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("Estadísticas")]
        [Min(1)] public int maxHealth = 100;
        [Min(0.1f)] public float walkSpeed = 5f;
        [Min(0.1f)] public float sprintSpeed = 7.5f;
        [Min(0.1f)] public float crouchSpeed = 2.5f;
    }

    [Serializable]
    public class Variant
    {
        public CharacterClass type;
        public CharacterGender gender;

        [Tooltip("Prefab del personaje (SM_Chr_...). De ahí se toma el cuerpo y los accesorios que estén ACTIVOS en el prefab.")]
        public GameObject modelPrefab;
    }

    [SerializeField]
    private ClassData[] classes =
    {
        // El soldado usa los valores actuales de tu Player (moveSpeed 5, sprintSpeed 7.5, crouchSpeed 2.5).
        new ClassData
        {
            type = CharacterClass.Soldier, displayName = "Soldado",
            description = "Equilibrado. Buena vida y velocidad para cualquier situación.",
            maxHealth = 100, walkSpeed = 5f, sprintSpeed = 7.5f, crouchSpeed = 2.5f
        },
        // Tanque: el doble de vida, 20% más lento.
        new ClassData
        {
            type = CharacterClass.Tank, displayName = "Tanque",
            description = "Aguanta el doble de daño, pero se mueve más lento.",
            maxHealth = 200, walkSpeed = 4f, sprintSpeed = 6f, crouchSpeed = 2f
        },
        // Explorador: 20% más rápido, vida reducida.
        new ClassData
        {
            type = CharacterClass.Scout, displayName = "Explorador",
            description = "Rápido y ágil, pero frágil. Un error sale caro.",
            maxHealth = 80, walkSpeed = 6f, sprintSpeed = 9f, crouchSpeed = 3f
        }
    };

    [SerializeField]
    private Variant[] variants =
    {
        new Variant { type = CharacterClass.Soldier, gender = CharacterGender.Male },
        new Variant { type = CharacterClass.Soldier, gender = CharacterGender.Female },
        new Variant { type = CharacterClass.Tank,    gender = CharacterGender.Male },
        new Variant { type = CharacterClass.Tank,    gender = CharacterGender.Female },
        new Variant { type = CharacterClass.Scout,   gender = CharacterGender.Male },
        new Variant { type = CharacterClass.Scout,   gender = CharacterGender.Female }
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

    /// <summary>Número de personaje para una clase + género (para el lobby). -1 si no existe.</summary>
    public int FindVariantIndex(CharacterClass type, CharacterGender gender)
    {
        for (int i = 0; i < VariantCount; i++)
        {
            if (variants[i].type == type && variants[i].gender == gender)
                return i;
        }

        return -1;
    }
}
