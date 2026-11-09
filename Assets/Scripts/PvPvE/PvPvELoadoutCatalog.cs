using System;
using System.Linq;
using UnityEngine;
namespace DeadQuarantine.PvPvE
{
 [Serializable] public sealed class PvPvEClassDefinition {
  public string name="CLASE",primary,secondary,grenade,healing;
  public WeaponAttachmentChoice primaryAttachments,secondaryAttachments;
  public string perk1="",perk2="",perk1Extra="",perk2Extra="",wildcard1="",wildcard2="";
  public PvPvEClassDefinition Copy()=>JsonUtility.FromJson<PvPvEClassDefinition>(JsonUtility.ToJson(this));
 }
 [CreateAssetMenu(menuName="Dead Quarantine/PvPvE/Loadout catalog")]
 public sealed class PvPvELoadoutCatalog:ScriptableObject {
  [Serializable] public sealed class Gun {
   public enum Family { Fusil, Subfusil, Sniper, Escopeta, Pistola }
   public Family family;
   public string Category=>family switch{Family.Subfusil=>"SUBFUSIL",Family.Sniper=>"SNIPER",Family.Escopeta=>"ESCOPETA",Family.Pistola=>"PISTOLA",_=>"FUSIL"};
   public Sprite whiteBody,whiteMagazine,whiteDefaultSight;
   public string id,name;public bool secondary;public Sprite icon;public GameObject model;
   public int damage,magazine;public float range,fireInterval;
   // Derived from the existing Weapon/override clips by the mode installer; no second weapon implementation.
   public int pellets=1,burstCount=1;public float burstCooldown,falloffStart,falloffEnd,minDamageMultiplier=1,maxSpread=45;
   public float reloadSeconds=1.5f,emptyReloadSeconds=1.5f,shellOpenSeconds=.5f,shellSeconds=.5f;
   public float reloadFillSeconds,emptyReloadFillSeconds;
   public bool shellReload;
  }
  [Serializable] public sealed class Utility {
   public string id,name;public Sprite icon;
   [Min(0)] public int healing;
   [Min(1)] public int carryCount=1;
  }
  public Gun[] weapons=Array.Empty<Gun>();
  public Utility[] grenades=Array.Empty<Utility>(),healings=Array.Empty<Utility>();
  public WeaponAttachmentCatalog attachments;
  public PassivePerkCatalog perks;
  public PvPvEWildcardCatalog wildcards;
  public bool HasWildcard(PvPvEClassDefinition c,PvPvEWildcard kind)=>c!=null&&wildcards&&wildcards.TryMask(c.wildcard1,c.wildcard2,out var mask)&&(mask&PvPvEWildcardCatalog.Bit(kind))!=0;
  public int HealingCapacity(PvPvEClassDefinition c)=>string.IsNullOrEmpty(c?.healing)?0:Mathf.Clamp((FindHealing(c.healing)?.carryCount??0)+(HasWildcard(c,PvPvEWildcard.ExtraHealing)?wildcards.extraHealingUnits:0),1,99);
  public int GrenadeCapacity(PvPvEClassDefinition c)=>string.IsNullOrEmpty(c?.grenade)?0:Mathf.Clamp((Array.Find(grenades,g=>g.id==c.grenade)?.carryCount??0)+(HasWildcard(c,PvPvEWildcard.ExtraGrenades)?wildcards.extraGrenadeUnits:0),1,99);
  public bool AllowsSecondary(PvPvEClassDefinition c,Gun gun)=>gun!=null&&(gun.secondary||HasWildcard(c,PvPvEWildcard.Overkill))&&gun.id!=c.primary;
  public Gun Find(string id)=>Array.Find(weapons,w=>w.id==id);
  public Utility FindHealing(string id)=>Array.Find(healings,h=>h.id==id);
  public string HealingSummary(Utility healing)=>$"{healing.name}\n{healing.carryCount} UN. · {healing.healing} PV / UN.";
  public const int Pick10Limit=10;
  static int Pick(string id)=>string.IsNullOrEmpty(id)?0:1;
  static int AttachmentPoints(WeaponAttachmentChoice choice)=>Enum.GetValues(typeof(WeaponAttachmentKind)).Cast<WeaponAttachmentKind>().Sum(kind=>Pick(choice.Get(kind)));
  // Derive points from stable IDs, never trust or persist a client-provided total.
  public int Pick10Cost(PvPvEClassDefinition c)=>c==null?0:Pick(c.primary)+Pick(c.secondary)+Pick(c.grenade)+Pick(c.healing)+AttachmentPoints(c.primaryAttachments)+AttachmentPoints(c.secondaryAttachments)+Pick(c.perk1)+Pick(c.perk2)+Pick(c.perk1Extra)+Pick(c.perk2Extra)+Pick(c.wildcard1)+Pick(c.wildcard2);
  public bool Validate(PvPvEClassDefinition c,out string error){
   if(!ValidateEquipment(c,out error))return false;
   int points=Pick10Cost(c);if(points>Pick10Limit){error=$"PICK 10: {points}/{Pick10Limit}. Quitá {points-Pick10Limit} punto(s) para guardar.";return false;}
   return true;
  }
  // Used only to edit a pre-Pick-10 draft without discarding its existing choices.
  // Server application and persistent saves always call Validate, including the budget.
  public bool ValidateEquipment(PvPvEClassDefinition c,out string error){
   error="";
   if(c==null){error="La clase no existe.";return false;}
   if(string.IsNullOrWhiteSpace(c.name)||c.name.Length>32){error="Usá un nombre de 1 a 32 caracteres.";return false;}
   if(!wildcards||!wildcards.TryMask(c.wildcard1,c.wildcard2,out _)){error="Elegí hasta dos Wildcards distintas y existentes.";return false;}
   if(Find(c.primary) is not {secondary:false} || !AllowsSecondary(c,Find(c.secondary))){error="Elegí armas distintas. Dos primarias requieren Overkill.";return false;}
   if(!ValidAttachments(c.primary,c.primaryAttachments)||!ValidAttachments(c.secondary,c.secondaryAttachments)){error="Accesorio incompatible con el arma.";return false;}
   if(!string.IsNullOrEmpty(c.grenade)&&!grenades.Any(g=>g.id==c.grenade)||!string.IsNullOrEmpty(c.healing)&&!healings.Any(h=>h.id==c.healing)){error="Elegí utilidades existentes del juego.";return false;}
   var healing=FindHealing(c.healing);if(healing!=null&&(healing.healing<=0||healing.carryCount<1||healing.carryCount>99)){error="La curación no tiene una capacidad válida configurada.";return false;}
   if(HasWildcard(c,PvPvEWildcard.ExtraHealing)&&string.IsNullOrEmpty(c.healing)||HasWildcard(c,PvPvEWildcard.ExtraGrenades)&&string.IsNullOrEmpty(c.grenade)){error="Las cantidades extra requieren una utilidad equipada.";return false;}
   if(!TryPerkMask(c,out _)){error="Movilidad en Perk 1; Manejo en Perk 2. Los espacios extra requieren su Greed y perks distintas.";return false;}
   if(AttachmentCount(c.primaryAttachments)>AttachmentLimit(c,false)||AttachmentCount(c.secondaryAttachments)>AttachmentLimit(c,true)){error="Máximo dos accesorios por arma; el tercero requiere su Gunfighter.";return false;}
   if(wildcards.extraHealingUnits<1||wildcards.extraHealingUnits>98||wildcards.extraGrenadeUnits<1||wildcards.extraGrenadeUnits>98){error="Configuración de cantidades extra inválida.";return false;}
   if(grenades.Any(g=>g.id==c.grenade&&(g.carryCount<1||g.carryCount>99))){error="Capacidad de granadas inválida.";return false;}
   return true;
  }
  public static int AttachmentCount(WeaponAttachmentChoice c)=>Enum.GetValues(typeof(WeaponAttachmentKind)).Cast<WeaponAttachmentKind>().Count(k=>!string.IsNullOrEmpty(c.Get(k)));
  public int AttachmentLimit(PvPvEClassDefinition c,bool secondary)=>HasWildcard(c,secondary?PvPvEWildcard.SecondaryGunfighter:PvPvEWildcard.PrimaryGunfighter)?3:2;
  static WeaponAttachmentChoice TrimAttachments(WeaponAttachmentChoice c,int limit){foreach(WeaponAttachmentKind k in Enum.GetValues(typeof(WeaponAttachmentKind)))if(!string.IsNullOrEmpty(c.Get(k))&&--limit<0)c.Set(k,"");return c;}
  public static string GetPerk(PvPvEClassDefinition c,int slot)=>slot switch{0=>c.perk1,1=>c.perk2,2=>c.perk1Extra,_=>c.perk2Extra};
  public static void SetPerk(PvPvEClassDefinition c,int slot,string id){switch(slot){case 0:c.perk1=id;break;case 1:c.perk2=id;break;case 2:c.perk1Extra=id;break;case 3:c.perk2Extra=id;break;}}
  public bool TryPerkMask(PvPvEClassDefinition c,out uint mask){
   mask=0;if(!perks||c==null)return false;
   for(int i=0;i<4;i++){string id=GetPerk(c,i);if(string.IsNullOrEmpty(id))continue;var perk=perks.Find(id);if(perk==null||perk.CategoryForPvPvE!=(i%2==0?PerkCategory.Mobility:PerkCategory.Handling)||i==2&&!HasWildcard(c,PvPvEWildcard.Perk1Greed)||i==3&&!HasWildcard(c,PvPvEWildcard.Perk2Greed))return false;uint bit=PassivePerkCatalog.Bit(perk.perk);if(bit==0||(mask&bit)!=0)return false;mask|=bit;}
   return true;
  }
  public bool ValidAttachments(string gun,WeaponAttachmentChoice choice){
   if(attachments.Eligible(gun))return attachments.Validate(gun,choice);
   return Enum.GetValues(typeof(WeaponAttachmentKind)).Cast<WeaponAttachmentKind>().All(k=>string.IsNullOrEmpty(choice.Get(k)));
  }
  public PvPvEClassDefinition Default(int index)=>new(){name="CLASE "+(index+1),primary=weapons.First(w=>!w.secondary).id,secondary=weapons.First(w=>w.secondary).id,grenade=grenades[0].id,healing=healings[0].id};
  public PvPvEClassDefinition Repair(PvPvEClassDefinition value,int index){
   var c=value?.Copy()??Default(index);var fallback=Default(index);
   c.name=string.IsNullOrWhiteSpace(c.name)?fallback.name:c.name.Trim().Substring(0,Math.Min(32,c.name.Trim().Length));
   if(!wildcards||wildcards.Find(c.wildcard1)==null)c.wildcard1="";
   if(!wildcards||!wildcards.TryMask(c.wildcard1,c.wildcard2,out _))c.wildcard2="";
   if(Find(c.primary) is not {secondary:false})c.primary=fallback.primary;
   if(!AllowsSecondary(c,Find(c.secondary)))c.secondary=fallback.secondary;
   c.primaryAttachments=RepairAttachments(c.primary,c.primaryAttachments);c.secondaryAttachments=RepairAttachments(c.secondary,c.secondaryAttachments);
   if(!string.IsNullOrEmpty(c.grenade)&&!grenades.Any(g=>g.id==c.grenade))c.grenade=fallback.grenade;
   if(!string.IsNullOrEmpty(c.healing)&&!healings.Any(h=>h.id==c.healing))c.healing=fallback.healing;
   foreach(bool first in new[]{true,false}){var wc=wildcards.Find(first?c.wildcard1:c.wildcard2);if(wc!=null&&(wc.kind==PvPvEWildcard.ExtraHealing&&string.IsNullOrEmpty(c.healing)||wc.kind==PvPvEWildcard.ExtraGrenades&&string.IsNullOrEmpty(c.grenade))){if(first)c.wildcard1="";else c.wildcard2="";}}
   var candidates=new[]{c.perk1,c.perk2,c.perk1Extra,c.perk2Extra}.Where(id=>!string.IsNullOrEmpty(id)&&perks&&perks.Find(id)!=null).Distinct().ToArray();
   c.perk1=c.perk2=c.perk1Extra=c.perk2Extra="";
   foreach(var id in candidates){bool mobility=perks.Find(id).CategoryForPvPvE==PerkCategory.Mobility;if(mobility){if(c.perk1=="")c.perk1=id;else if(HasWildcard(c,PvPvEWildcard.Perk1Greed))c.perk1Extra=id;}else{if(c.perk2=="")c.perk2=id;else if(HasWildcard(c,PvPvEWildcard.Perk2Greed))c.perk2Extra=id;}}
   c.primaryAttachments=TrimAttachments(c.primaryAttachments,AttachmentLimit(c,false));c.secondaryAttachments=TrimAttachments(c.secondaryAttachments,AttachmentLimit(c,true));
   return c;
  }
  WeaponAttachmentChoice RepairAttachments(string gun,WeaponAttachmentChoice c){
   foreach(WeaponAttachmentKind kind in Enum.GetValues(typeof(WeaponAttachmentKind)))if(!attachments.Allows(gun,kind,c.Get(kind)))c.Set(kind,"");return c;
  }
 }
}
