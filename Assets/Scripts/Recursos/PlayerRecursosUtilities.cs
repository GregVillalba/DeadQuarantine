using Unity.Netcode;
public partial class PlayerRecursos {
 [ServerRpc] public void ComprarLinternaServerRpc(string id){
  var equipment=GetComponent<NetworkWeaponAttachments>();bool success=equipment&&equipment.BuyFlashlightServer(id);
  NotificarResultadoCompraClientRpc(id,success,ParametrosParaDueno());
 }
}
