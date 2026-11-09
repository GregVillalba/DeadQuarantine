using System;
using UnityEngine;
namespace DeadQuarantine.PvPvE
{
 public enum PvPvEWildcard : byte { None, ExtraHealing, ExtraGrenades, Overkill, PerkGreed, PrimaryGunfighter, SecondaryGunfighter, Perk1Greed, Perk2Greed }
 [CreateAssetMenu(menuName="Dead Quarantine/PvPvE/Wildcards")]
 public sealed class PvPvEWildcardCatalog : ScriptableObject
 {
  [Serializable] public sealed class Entry { public string id,name; [TextArea] public string description; public PvPvEWildcard kind; public Sprite icon; }
  public Entry[] entries=Array.Empty<Entry>();
  [Range(1,98)] public int extraHealingUnits=1,extraGrenadeUnits=1;
  public Entry Find(string id)=>string.IsNullOrEmpty(id)?null:Array.Find(entries,e=>e.id==id);
  public static uint Bit(PvPvEWildcard kind)=>kind>PvPvEWildcard.None&&kind<=PvPvEWildcard.Perk2Greed?1u<<((int)kind-1):0;
  public bool TryMask(string first,string second,out uint mask){
   mask=0;var a=Find(first);var b=Find(second);
   if((!string.IsNullOrEmpty(first)&&(a==null||Bit(a.kind)==0))||(!string.IsNullOrEmpty(second)&&(b==null||Bit(b.kind)==0))||a!=null&&b!=null&&a.kind==b.kind)return false;
   mask=Bit(a?.kind??PvPvEWildcard.None)|Bit(b?.kind??PvPvEWildcard.None);return true;
  }
 }
}
