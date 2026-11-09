using UnityEngine;
using UnityEngine.Rendering.Universal;

public partial class WeaponAttachments
{
    Camera scopeCamera;
    RenderTexture scopeTexture;
    Material scopeLens;
    Weapon scopeWeapon;
    bool scopeRendering;
    public bool ScopeRendering => scopeRendering;
    public RenderTexture ScopeTexture => scopeTexture;

    void ClearScope()
    {
        if(scopeCamera){scopeCamera.enabled=false;scopeCamera.targetTexture=null;Destroy(scopeCamera.gameObject);}
        if(scopeTexture){scopeTexture.Release();Destroy(scopeTexture);}
        if(scopeLens)Destroy(scopeLens);
        scopeCamera=null;scopeTexture=null;scopeLens=null;scopeRendering=false;
    }
    Material BuildScopeLens(WeaponAttachmentCatalog.Entry entry)
    {
        if(scopeLens)return scopeLens;
        scopeLens=new Material(catalog.scopeLensMaterial);
        scopeLens.SetTexture("_BaseMap",Texture2D.blackTexture);
        scopeWeapon=GetComponent<Weapon>();
        // The original pack renders a narrow-field camera into the actual lens mesh.
        // Remote models and menu previews only use the opaque, unlit lens.
        if(remoteVisual||!scopeWeapon)return scopeLens;
        scopeTexture=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32){name="Scope "+stableWeaponId};scopeTexture.Create();
        var go=new GameObject("Scope lens camera");go.transform.SetParent(transform,false);scopeCamera=go.AddComponent<Camera>();
        scopeCamera.enabled=false;scopeCamera.fieldOfView=entry.lensFieldOfView;scopeCamera.aspect=1;
        scopeCamera.nearClipPlane=.03f;scopeCamera.farClipPlane=1000;scopeCamera.depth=-20;
        scopeCamera.targetTexture=scopeTexture;scopeCamera.allowHDR=false;scopeCamera.allowMSAA=false;
        var data=go.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.volumeLayerMask=0;
        return scopeLens;
    }
    void UpdateScope()
    {
        if(!scopeCamera||!scopeWeapon)return;
        var view=scopeWeapon.AimCamera;
        bool active=view&&scopeWeapon.isActiveAndEnabled&&scopeWeapon.IsAiming;
        scopeRendering=active;scopeCamera.enabled=false;
        scopeLens.SetTexture("_BaseMap",active?scopeTexture:Texture2D.blackTexture);
        if(!active)return;
        var optic=catalog.Find(ApprovedChoice.scope);if(optic!=null)scopeCamera.fieldOfView=optic.LensFieldOfView(scopeWeapon.HipFieldOfView);
        scopeCamera.transform.SetPositionAndRotation(view.transform.position,view.transform.rotation);
        scopeCamera.cullingMask=view.cullingMask&~(1<<30)&~(1<<31);
        scopeCamera.clearFlags=view.clearFlags;scopeCamera.backgroundColor=view.backgroundColor;
        scopeCamera.farClipPlane=view.farClipPlane;
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(scopeCamera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=scopeTexture});
    }
    void OnDisable(){scopeRendering=false;if(scopeCamera)scopeCamera.enabled=false;if(scopeLens)scopeLens.SetTexture("_BaseMap",Texture2D.blackTexture);}
    void OnDestroy(){ClearScope();}
}
