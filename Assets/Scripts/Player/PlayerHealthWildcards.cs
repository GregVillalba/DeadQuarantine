using System;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.Networking;
using Unity.Collections;
using Unity.Netcode;
public struct PvPvEClassEquipment : INetworkSerializable,IEquatable<PvPvEClassEquipment>
{
 public uint revision,mask; public FixedString64Bytes primary,secondary;
 public bool Active=>revision!=0;
 public void NetworkSerialize<T>(BufferSerializer<T> s) where T:IReaderWriter {s.SerializeValue(ref revision);s.SerializeValue(ref mask);s.SerializeValue(ref primary);s.SerializeValue(ref secondary);}
 public bool Equals(PvPvEClassEquipment o)=>revision==o.revision&&mask==o.mask&&primary.Equals(o.primary)&&secondary.Equals(o.secondary);
}
public partial class PlayerHealth
{
 public readonly NetworkVariable<PvPvEClassEquipment> EquipoDeClase=new(default,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
 public readonly NetworkVariable<SlotCura> GranadaPvPvE=new(default,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
 public bool GranadaPvPvEActiva=>GranadaPvPvE.Value.cantidad>0;
 public uint RevisionArmasAplicada {get;private set;}
 uint equipmentSequence;
 public bool ArmaPermitidaPorClase(string id)=>!EquipoDeClase.Value.Active||EquipoDeClase.Value.primary.ToString()==id||EquipoDeClase.Value.secondary.ToString()==id;
 // Trusted server entry point for later class selection/respawn. No client amount/mask RPC.
 public bool AplicarWildcardsDeClaseServidor(PvPvEClassDefinition c,bool refill=false)
 {
  if(!IsServer||!IsSpawned||!IsAlive||perksMatchEnded||!EsJugadorPvPvE)return false;
  var roster=PvPvETeamRoster.Active;var catalog=roster.Configuration.Loadouts;
  if(roster.Closing.Value||!catalog||catalog.perks!=perksCatalog||!catalog.Validate(c,out _)||!catalog.wildcards.TryMask(c.wildcard1,c.wildcard2,out var mask))return false;
  if(!AplicarCuracionDeClaseServidor(c,refill)||!AplicarPerksDeClaseServidor(c))return false;
  var grenade=new SlotCura{itemId=new FixedString64Bytes(c.grenade??""),cantidad=catalog.GrenadeCapacity(c)};
  if(!GranadaPvPvE.Value.Equals(grenade)||refill){GranadaPvPvE.Value=grenade;SlotGranada.Value=grenade;}
  var previous=EquipoDeClase.Value;
  if(!previous.Active||previous.mask!=mask||previous.primary.ToString()!=c.primary||previous.secondary.ToString()!=c.secondary){
   if(++equipmentSequence==0)equipmentSequence=1;
   EquipoDeClase.Value=new PvPvEClassEquipment{revision=equipmentSequence,mask=mask,primary=new FixedString64Bytes(c.primary),secondary=new FixedString64Bytes(c.secondary)};
  }
  return true;
 }
 void ActualizarArmasDeClase(){
  if(!IsOwner)return;var state=EquipoDeClase.Value;
  if(!state.Active){RevisionArmasAplicada=0;return;}
  if(RevisionArmasAplicada==state.revision)return;
  var switcher=GetComponentInChildren<WeaponSwitcher>(true);
  if(switcher&&switcher.ConfigurarEspaciosDeClase(state.primary.ToString(),state.secondary.ToString()))RevisionArmasAplicada=state.revision;
 }
 void LimpiarWildcardsServidor(){
  if(!IsServer||!EquipoDeClase.Value.Active)return;
  EquipoDeClase.Value=default;GranadaPvPvE.Value=default;SlotGranada.Value=default;CuracionPvPvE.Value=default;
  for(int i=0;i<SlotsCuras.Count;i++)SlotsCuras[i]=default;
 }
 void GranadaDeClaseChanged(SlotCura before,SlotCura after)=>OnInventarioChanged?.Invoke();
}
