public partial class WeaponSwitcher
{
 // Reuse the existing two inventory positions and normal holster/unholster sequence.
 // Ammo, animations, poses and weapon assets are unchanged.
 public bool ConfigurarEspaciosDeClase(string primary,string secondary){
  if(!IsInitialized||isSwitching)return false;
  int a=FindSlotIndex(primary),b=FindSlotIndex(secondary);
  if(a<0||b<0||a==b)return false;
  queuedUnlocks.Clear();queuedEquip.Clear();inventory[0]=a;inventory[1]=b;
  InventoryChanged?.Invoke();if(currentIndex!=a&&currentIndex!=b)RequestEquip(a);
  return true;
 }
 bool PermitidaEnClase(int index){var health=GetComponentInParent<PlayerHealth>();return !health||health.ArmaPermitidaPorClase(slots[index].weaponId);}
}
