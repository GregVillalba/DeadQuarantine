using System;
using UnityEngine;

public partial class WeaponAttachments
{
    [SerializeField] private WeaponAttachmentCatalog catalog;
    [SerializeField] private string stableWeaponId;
    public WeaponAttachmentCatalog Catalog=>catalog;
    public string StableWeaponId=>stableWeaponId;
    public WeaponAttachmentChoice ApprovedChoice {get;private set;}
    public bool HasRuntimeCatalog=>catalog&&catalog.Eligible(stableWeaponId);
    Sprite RuntimeHudIcon(WeaponAttachmentKind kind){
        var entry=catalog.Find(ApprovedChoice.Get(kind));
        if(entry==null)return null;
        return Array.Find(entry.weaponIcons,i=>i.weapon==stableWeaponId)?.icon;
    }
    readonly GameObject[] mounted=new GameObject[4];
    Transform defaultOptical, runtimeOptical;
    LineRenderer laserBeam;
    Transform laserOrigin;
    public bool HasFlashlight=>HasRuntimeCatalog&&catalog.Find(ApprovedChoice.utility)?.flashlight==true;
    public bool HasLaser=>HasRuntimeCatalog&&catalog.Find(ApprovedChoice.utility)?.laser==true;
    public Vector3 UtilityOrigin=>laserOrigin?laserOrigin.position:transform.position;
    public Vector3 UtilityDirection {get{var physical=Find(transform,"root");return physical?-physical.up:transform.forward;}}
    Flashlight utilityControl;
    bool remoteVisual;
    static readonly WeaponAttachmentKind[] kinds=(WeaponAttachmentKind[])Enum.GetValues(typeof(WeaponAttachmentKind));

    public void ConfigureRuntime(WeaponAttachmentCatalog source,string weapon,bool remote=false) {
        catalog=source;stableWeaponId=weapon;remoteVisual=remote;
    }
    public bool ApplyApproved(WeaponAttachmentChoice choice) {
        if(!HasRuntimeCatalog||!catalog.Validate(stableWeaponId,choice))return false;
        if(defaultOptical==null)defaultOptical=Find(transform,"DQ_OpticalSight")??GetComponent<Weapon>()?.AttachmentDefaultSight;
        foreach(var k in kinds){
            int index=(int)k;var entry=catalog.Find(choice.Get(k));
            if(ApprovedChoice.Get(k)==choice.Get(k)&&((entry==null)||mounted[index]))continue;
            if(mounted[index]){mounted[index].SetActive(false);Destroy(mounted[index]);mounted[index]=null;}
            if(k==WeaponAttachmentKind.Scope){runtimeOptical=null;ClearScope();}
            if(entry==null)continue;
            var socket=Find(transform,entry.socket);if(!socket)return false;
            var go=Instantiate(entry.model,socket,false);go.name="DQ_ATT_"+entry.id;mounted[index]=go;
            SetLayer(go.transform,gameObject.layer);
            ApplyOwnerVisibility(go);
            // Imported authored mount origin: never use a mesh's bounding centre as its attachment anchor.
            go.transform.localPosition=entry.MountPosition(stableWeaponId);go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
            if(k==WeaponAttachmentKind.Scope){
                var physical=Find(transform,"root");var b=BoundsIn(go,physical);Vector3 optical=new(b.center.x,b.max.y,Mathf.Lerp(b.min.z,b.max.z,entry.opticalHeight));
                foreach(var renderer in go.GetComponentsInChildren<MeshRenderer>(true)){
                    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var materials=renderer.sharedMaterials;
                    for(int m=0;m<materials.Length;m++){
                        string name=materials[m]?materials[m].name.ToLowerInvariant():"";
                        if(entry.magnifiedLens&&catalog.scopeLensMaterial&&(name=="render"||name.Contains("sight_render"))){materials[m]=BuildScopeLens(entry);}
                        else if(name.StartsWith("sight")||name.Contains("sight_unlit")){
                            var sb=mesh.GetSubMesh(m).bounds;
                            optical=physical.InverseTransformPoint(renderer.transform.TransformPoint(sb.center));
                            materials[m]=entry.magnifiedLens&&catalog.scopeReticleMaterial?catalog.scopeReticleMaterial:catalog.reticleMaterial;
                        }else if(name=="render"||name.StartsWith("layer")||name.Contains("transparent"))materials[m]=catalog.transparentLens;
                    }renderer.sharedMaterials=materials;
                }
                runtimeOptical=new GameObject("DQ_OpticalSight").transform;runtimeOptical.SetParent(go.transform,false);
                runtimeOptical.position=physical.TransformPoint(optical);
            }
            if(k==WeaponAttachmentKind.Utility)BuildUtility(go,entry);
        }
        ApprovedChoice=choice;
        var standard=Find(transform,"DQ_DefaultScope");if(standard)standard.gameObject.SetActive(string.IsNullOrEmpty(choice.scope));
        if(!remoteVisual)GetComponent<Weapon>()?.SetAttachmentSight(runtimeOptical?runtimeOptical:defaultOptical);
        Changed?.Invoke();return true;
    }
    public float DamageMultiplier=>Multiplier(e=>e.damage);
    public float SpreadMultiplier=>Multiplier(e=>e.spread);
    public float RecoilMultiplier=>Multiplier(e=>e.recoil);
    public Vector2 RecoilAxes=>new(Multiplier(e=>e.verticalRecoil),Multiplier(e=>e.horizontalRecoil));
    public float RangeMultiplier=>Permanent(e=>e.range);
    public float AdsDurationMultiplier=>Permanent(e=>e.adsDuration);
    public float MobilityMultiplier=>Permanent(e=>e.mobility);
    public float AdsMovementMultiplier=>Permanent(e=>e.adsMovement);
    public float AdsSwayMultiplier=>Multiplier(e=>e.adsSway);
    public float IncomingShakeMultiplier=>Permanent(e=>e.incomingShake);
    float Permanent(Func<WeaponAttachmentCatalog.Entry,float> get)=>HasRuntimeCatalog?catalog.Modifier(ApprovedChoice,get):1;
    public bool Suppressed=>catalog&&catalog.Find(ApprovedChoice.muzzle)?.suppressed==true;
    public AudioClip ShotSound=>Suppressed?catalog.Find(ApprovedChoice.muzzle).shotSound:null;
    public float AimFov(float fallback)=>catalog?.Find(ApprovedChoice.scope)?.aimFov??fallback;
    float Multiplier(Func<WeaponAttachmentCatalog.Entry,float> get){
        if(!HasRuntimeCatalog)return 1;
        float value=1;
        foreach(var kind in kinds){var e=catalog.Find(ApprovedChoice.Get(kind));if(e!=null&&(!e.requiresBracing||IsBraced))value*=get(e);}
        return value;
    }
    public bool IsBraced {
        get {var movement=GetComponentInParent<PlayerMovement>();var cc=GetComponentInParent<CharacterController>();
            return GetComponent<Weapon>() is {IsAiming:true}&&movement&&cc&&cc.isGrounded&&movement.IsCrouching&&new Vector2(cc.velocity.x,cc.velocity.z).sqrMagnitude<.04f;}
    }
    void BuildUtility(GameObject go,WeaponAttachmentCatalog.Entry entry){
        laserBeam=null;
        var physical=Find(transform,"root");var bounds=BoundsIn(go,physical);
        laserOrigin=new GameObject("DQ_LaserEmitter").transform;laserOrigin.SetParent(go.transform,false);
        laserOrigin.position=physical.TransformPoint(new Vector3(bounds.center.x,bounds.min.y-.005f,bounds.center.z));
        // Light comes from the player's existing Flashlight component, never from a second beam.
        if(entry.laser){laserBeam=go.AddComponent<LineRenderer>();laserBeam.positionCount=2;laserBeam.startWidth=.002f;laserBeam.endWidth=.002f;
            laserBeam.sharedMaterial=catalog.laserMaterial;laserBeam.startColor=laserBeam.endColor=Color.red;laserBeam.useWorldSpace=true;
            laserBeam.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;laserBeam.receiveShadows=false;}
    }
    bool HideThirdPersonForOwner {
        get {var p=remoteVisual?GetComponentInParent<NetworkWeaponPresentation>():null;return p&&p.IsOwner&&!p.ShowLocalBodyForTesting;}
    }
    void ApplyOwnerVisibility(GameObject model){
        if(!remoteVisual)return;
        bool hide=HideThirdPersonForOwner;
        foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=hide;
    }
    void LateUpdate(){
        UpdateScope();
        if(remoteVisual){foreach(var model in mounted)if(model)ApplyOwnerVisibility(model);}
        bool hidden=HideThirdPersonForOwner;
        if(!utilityControl)utilityControl=GetComponentInParent<Flashlight>();
        bool active=!hidden&&utilityControl&&utilityControl.UtilityEnabled;
        if(laserBeam)laserBeam.enabled=active;
        if(!active||!laserBeam||!laserOrigin)return;
        var physical=Find(transform,"root");Vector3 forward=physical?-physical.up:transform.forward;
        Vector3 origin=laserOrigin.position;
        if(laserBeam){Vector3 end=origin+forward*35;
            int mask=~((1<<gameObject.layer)|(1<<LayerMask.NameToLayer("Ignore Raycast")));
            if(Physics.Raycast(origin,forward,out var hit,35,mask,QueryTriggerInteraction.Ignore))end=hit.point;
            laserBeam.SetPosition(0,origin);laserBeam.SetPosition(1,end);}
    }
    public static Transform Find(Transform root,string name)=>Array.Find(root.GetComponentsInChildren<Transform>(true),t=>t.name==name);
    static void SetLayer(Transform t,int layer){t.gameObject.layer=layer;foreach(Transform child in t)SetLayer(child,layer);}
    public static Bounds BoundsIn(GameObject go,Transform relative){
        var bounds=new Bounds();bool first=true;
        foreach(var r in go.GetComponentsInChildren<Renderer>(true)){
            var mf=r.GetComponent<MeshFilter>();var sk=r as SkinnedMeshRenderer;var mesh=mf?mf.sharedMesh:sk?sk.sharedMesh:null;if(!mesh)continue;
            var b=mesh.bounds;
            for(int i=0;i<8;i++){var p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                p=relative.InverseTransformPoint(r.transform.TransformPoint(p));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
        }return bounds;
    }
}
