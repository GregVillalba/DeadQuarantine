using System;
using System.Collections;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed class NetworkWeaponAttachments : NetworkBehaviour
{
    public WeaponAttachmentCatalog catalog;
    public struct Row : INetworkSerializable,IEquatable<Row> {
        public int weapon;
        public FixedString64Bytes scope,support,utility,muzzle;
        public WeaponAttachmentChoice Choice=>new(){scope=scope.ToString(),support=support.ToString(),utility=utility.ToString(),muzzle=muzzle.ToString()};
        public Row(int index,WeaponAttachmentChoice c){weapon=index;scope=c.Get(WeaponAttachmentKind.Scope);support=c.Get(WeaponAttachmentKind.Support);utility=c.Get(WeaponAttachmentKind.Utility);muzzle=c.Get(WeaponAttachmentKind.Muzzle);}
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T:IReaderWriter{
            s.SerializeValue(ref weapon);s.SerializeValue(ref scope);s.SerializeValue(ref support);s.SerializeValue(ref utility);s.SerializeValue(ref muzzle);
        }
        public bool Equals(Row other)=>weapon==other.weapon&&scope.Equals(other.scope)&&support.Equals(other.support)&&utility.Equals(other.utility)&&muzzle.Equals(other.muzzle);
    }
    NetworkList<Row> approved;
    NetworkWeaponPresentation presentation;
    GameObject lastVisible;
    Row lastRow;
    [Min(16)] public int requestsPerSecond=64;
    double requestWindow;int requestCount;
    public int RejectedRequests {get;private set;}
    public event Action<string,WeaponAttachmentChoice> Changed;
    public event Action<ulong,string,bool> ShotVisibility;
    void Awake(){approved=new NetworkList<Row>();presentation=GetComponent<NetworkWeaponPresentation>();}
    public override void OnNetworkSpawn(){
        approved.OnListChanged+=ListChanged;
        ResetVisuals();
        if(IsServer){approved.Clear();for(int i=0;i<presentation.Weapons.Length;i++)if(catalog.Eligible(presentation.Weapons[i].Id))approved.Add(new Row(i,default));}
        ApplyAll();
    }
    void ResetVisuals(){lastVisible=null;if(!presentation)return;foreach(var definition in presentation.Weapons)if(definition.Source)definition.Source.GetComponent<WeaponAttachments>()?.ResetRuntimeAttachments();}
    public override void OnNetworkDespawn(){approved.OnListChanged-=ListChanged;ResetVisuals();base.OnNetworkDespawn();}
    public bool Request(string weaponId,WeaponAttachmentChoice choice){
        if(!IsSpawned||!IsOwner||!catalog.Validate(weaponId,choice))return false;
        int index=Array.FindIndex(presentation.Weapons,d=>d.Id==weaponId);if(index<0)return false;
        RequestServerRpc(new Row(index,choice));return true;
    }
    [ServerRpc] void RequestServerRpc(Row row){
        if(DeadQuarantine.PvPvE.Networking.PvPvEPreparationFlow.OwnsStagingPlayer(NetworkObject)){RejectedRequests++;return;}
        if(row.weapon<0||row.weapon>=presentation.Weapons.Length||!catalog.Validate(presentation.Weapons[row.weapon].Id,row.Choice)) {RejectedRequests++;return;}
        var source=presentation.Weapons[row.weapon].Source;
        if(source.IsReloading||source.IsChambering||(presentation.Switcher&&(presentation.Switcher.IsHolstering||presentation.Switcher.IsUnholstering))){RejectedRequests++;return;}
        // Bound hostile request traffic, while still allowing the initial per-weapon restore batch.
        double now=NetworkManager.ServerTime.Time;
        if(now>=requestWindow){requestWindow=now+1;requestCount=0;}
        if(++requestCount>requestsPerSecond){RejectedRequests++;return;}
        for(int i=0;i<approved.Count;i++)if(approved[i].weapon==row.weapon){approved[i]=row;return;}
        RejectedRequests++;
    }
    void ListChanged(NetworkListEvent<Row> change){
        ApplyAll();
    }
    // Only the trusted deployment coordinator supplies class attachments in PvPvE.
    public bool ApplyClassServer(DeadQuarantine.PvPvE.PvPvEClassDefinition c){
        if(!IsServer||!IsSpawned||!DeadQuarantine.PvPvE.Networking.PvPvEPreparationFlow.OwnsStagingPlayer(NetworkObject)||!DeadQuarantine.PvPvE.Networking.PvPvETeamRoster.Active.Configuration.Loadouts.Validate(c,out _))return false;
        for(int i=0;i<approved.Count;i++){var row=approved[i];string id=presentation.Weapons[row.weapon].Id;approved[i]=new Row(row.weapon,id==c.primary?c.primaryAttachments:id==c.secondary?c.secondaryAttachments:default);}
        return true;
    }
    // The existing merchant charges its server-side offer; clients only send the offer ID.
    public bool BuyFlashlightServer(string offerId){
        var health=GetComponent<PlayerHealth>();var light=GetComponent<Flashlight>();var resources=GetComponent<PlayerRecursos>();
        if(!IsServer||!IsSpawned||!health||!health.IsAlive||!light||!resources||DeadQuarantine.PvPvE.Networking.PvPvEPreparationFlow.OwnsStagingPlayer(NetworkObject))return false;
        int index=IsOwner&&presentation.Switcher?presentation.Switcher.CurrentWeaponIndex:presentation.EquippedIndex;
        if(index<0||index>=presentation.Weapons.Length)return false;
        var choice=GetChoice(index);var current=catalog.Find(choice.utility);if(light.Purchased&&current?.flashlight==true)return false;
        string utility=current?.flashlight==true?current.id:current?.laser==true?"Laser_Flashlight_01":"Flashlight_01";
        if(!catalog.Allows(presentation.Weapons[index].Id,WeaponAttachmentKind.Utility,utility))return false;
        float distance=health.PerksCatalog?health.PerksCatalog.merchantDistance:3.5f;
        var vendor=FindObjectsByType<NPCWeaponVendor>(FindObjectsSortMode.None).FirstOrDefault(v=>v.PuedeComerciar&&Vector3.Distance(v.transform.position,health.transform.position)<=distance&&v.Offers.Any(o=>o.esLinterna&&o.weaponId==offerId&&o.disponible&&DisponibilidadTienda.SeMuestraEnPartidaActual(o)));
        if(!vendor)return false;var offer=vendor.Offers.First(o=>o.esLinterna&&o.weaponId==offerId);
        int row=-1;for(int i=0;i<approved.Count;i++)if(approved[i].weapon==index){row=i;break;}if(row<0)return false;
        if(!light.Purchased&&!resources.GastarRecursos(offer.Precio))return false;
        light.GrantPurchaseServer();choice.utility=utility;approved[row]=new Row(index,choice);return true;
    }
    public WeaponAttachmentChoice GetChoice(int weapon){foreach(var r in approved)if(r.weapon==weapon)return r.Choice;return default;}
    void ApplyAll(){
        if(!presentation||!catalog)return;
        foreach(var row in approved){
            if(row.weapon<0||row.weapon>=presentation.Weapons.Length)continue;
            var d=presentation.Weapons[row.weapon];var attachments=d.Source.GetComponent<WeaponAttachments>();
            if(attachments){attachments.ApplyApproved(row.Choice);Changed?.Invoke(d.Id,row.Choice);}

        }
        RefreshVisibleWeapon();
    }
    void LateUpdate(){if(IsSpawned)RefreshVisibleWeapon();}
    public void RefreshVisibleWeapon(){
        if(!presentation||!presentation.VisibleWeapon)return;
        int index=presentation.EquippedIndex;
        // On the owning peer the presentation NetworkVariable is one round-trip behind the equipped model.
        if(IsOwner&&presentation.Switcher)index=presentation.Switcher.CurrentWeaponIndex;
        if(index<0||index>=presentation.Weapons.Length||!catalog.Eligible(presentation.Weapons[index].Id))return;
        Row row=new(index,GetChoice(index));
        if(lastVisible==presentation.VisibleWeapon&&lastRow.Equals(row))return;
        lastVisible=presentation.VisibleWeapon;lastRow=row;
        var attachments=lastVisible.GetComponent<WeaponAttachments>();if(!attachments)attachments=lastVisible.AddComponent<WeaponAttachments>();
        attachments.ConfigureRuntime(catalog,presentation.Weapons[index].Id,true);attachments.ApplyApproved(row.Choice);
    }
    public void PublishShotVisibility(int weapon){
        DeadQuarantine.PvPvE.Networking.PvPvETeamDeployment.Active?.CancelProtectionServer(OwnerClientId);
        if(weapon<0||weapon>=presentation.Weapons.Length)return;
        var d=presentation.Weapons[weapon];var a=d.Source.GetComponent<WeaponAttachments>();
        ShotVisibility?.Invoke(OwnerClientId,d.Id,a==null||!a.Suppressed);
    }
    [Serializable] sealed class Saved {public int version=1;public string weapon;public WeaponAttachmentChoice choice;}
    public static string Serialize(string weapon,WeaponAttachmentChoice choice)=>JsonUtility.ToJson(new Saved{weapon=weapon,choice=choice});
    public static bool Deserialize(string json,string weapon,out WeaponAttachmentChoice choice){
        choice=default;try{var data=JsonUtility.FromJson<Saved>(json);if(data==null||data.version!=1||data.weapon!=weapon)return false;choice=data.choice;return true;}catch(ArgumentException){return false;}
    }
    public static void Save(string weapon,WeaponAttachmentChoice choice){PlayerPrefs.SetString("DQ.Attachments.v1."+weapon,Serialize(weapon,choice));PlayerPrefs.Save();}
    public static bool TryLoad(string weapon,out WeaponAttachmentChoice choice)=>Deserialize(PlayerPrefs.GetString("DQ.Attachments.v1."+weapon,""),weapon,out choice);
}
