using System;
using UnityEngine;

namespace DeadQuarantine.PvPvE
{
    public enum PassivePerk : byte { None, Gloves, Boots, Kneepads, Holster, Canteen }
    public enum PerkCategory : byte { Handling, Mobility, Endurance }
    [CreateAssetMenu(menuName = "Dead Quarantine/Passive perks")]
    public sealed class PassivePerkCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string id, name;
            [TextArea] public string description;
            public PassivePerk perk;
            public PerkCategory category;
            public Sprite icon;
            public Sprite pvpveIcon;
            public string NameForPvPvE => perk==PassivePerk.Canteen?"Bebida energética":name;
            public PerkCategory CategoryForPvPvE => perk==PassivePerk.Canteen?PerkCategory.Mobility:category;
            public Sprite IconForPvPvE => pvpveIcon ? pvpveIcon : icon;
            [Min(0)] public int cans, water, fuel;
            public PrecioRecursos Price => new(cans, water, fuel);
        }
        public Entry[] entries = Array.Empty<Entry>();
        [Min(1)] public float reloadSpeed = 1.5f;
        [Range(.01f, 1)] public float sprintDrain = .7f;
        [Range(.01f, 1)] public float switchDuration = .8f;
        [Min(1)] public float staminaRegen = 1.15f;
        [Min(.1f)] public float slideDuration = .65f, slideCost = 1f, slideCooldown = 1.25f;
        [Min(1)] public float slideSpeed = 11;
        [Min(.1f)] public float merchantDistance = 4;
        public Entry Find(string id) => string.IsNullOrEmpty(id) ? null : Array.Find(entries, e => e.id == id);
        public Entry Find(PassivePerk perk) => Array.Find(entries, e => e.perk == perk);
        public static uint Bit(PassivePerk perk) => perk > PassivePerk.None && perk <= PassivePerk.Canteen ? 1u << ((int)perk - 1) : 0;
        public bool TryClassMask(string first, string second, out uint mask, bool allowSameCategory = false)
        {
            mask = 0;
            var a = Find(first); var b = Find(second);
            if ((!string.IsNullOrEmpty(first) && a == null) || (!string.IsNullOrEmpty(second) && b == null)) return false;
            if (a != null && b != null && (a.perk == b.perk || (!allowSameCategory && a.category == b.category))) return false;
            mask = Bit(a?.perk ?? PassivePerk.None) | Bit(b?.perk ?? PassivePerk.None);
            return true;
        }
        public static string CategoryName(PerkCategory c) => c == PerkCategory.Handling ? "MANEJO" : c == PerkCategory.Mobility ? "MOVILIDAD" : "RESISTENCIA";
    }
}
