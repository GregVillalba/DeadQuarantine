using UnityEngine;
using UnityEngine.Rendering.Universal;

public partial class WeaponAttachments
{
    Camera scopeCamera;
    RenderTexture scopeTexture;
    Material scopeLens;
    Weapon scopeWeapon;
    Transform aperture;
    Bounds apertureBounds;
    bool scopeRendering;
    internal bool HasMagnifiedLens => scopeCamera != null;
    public bool ScopeRendering => scopeRendering;
    public RenderTexture ScopeTexture => scopeTexture;

    void ClearScope()
    {
        if(scopeCamera){scopeCamera.enabled=false;scopeCamera.targetTexture=null;Destroy(scopeCamera.gameObject);}
        if(scopeTexture){scopeTexture.Release();Destroy(scopeTexture);}
        if(scopeLens)Destroy(scopeLens);
        scopeCamera=null;scopeTexture=null;scopeLens=null;scopeRendering=false;
        aperture=null;apertureBounds=default;
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
        scopeCamera.targetTexture=scopeTexture;scopeCamera.aspect=1;scopeCamera.allowHDR=false;scopeCamera.allowMSAA=false;
        var data=go.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.volumeLayerMask=0;
        return scopeLens;
    }
    void SetScopeAperture(Transform surface, Bounds bounds)
    {
        aperture=surface;
        apertureBounds=bounds;
    }
    float ScreenHeightOfAperture()
    {
        Camera camera=scopeWeapon.ViewModelCamera;
        if (!camera || !aperture) return 1f;

        float minY=float.PositiveInfinity, maxY=float.NegativeInfinity;
        Vector3 min=apertureBounds.min, max=apertureBounds.max;
        for (int i=0; i<8; i++)
        {
            var corner=new Vector3((i&1)==0?min.x:max.x,(i&2)==0?min.y:max.y,(i&4)==0?min.z:max.z);
            Vector3 viewport=camera.WorldToViewportPoint(aperture.TransformPoint(corner));
            if (viewport.z<=camera.nearClipPlane) continue;
            minY=Mathf.Min(minY,viewport.y);
            maxY=Mathf.Max(maxY,viewport.y);
        }
        return maxY>minY ? Mathf.Clamp(maxY-minY,.1f,1f) : 1f;
    }
    void UpdateScope()
    {
        if(!scopeCamera||!scopeWeapon)return;
        var view=scopeWeapon.AimCamera;
        bool active=view&&scopeWeapon.isActiveAndEnabled&&scopeWeapon.IsAiming;
        scopeRendering=active;scopeCamera.enabled=false;
        scopeLens.SetTexture("_BaseMap",active?scopeTexture:Texture2D.blackTexture);
        if(!active)return;
        var optic=catalog.Find(ApprovedChoice.scope);
        if(optic!=null)
        {
            // Preserve the catalog magnification after projecting the square render onto a smaller lens.
            float halfAngle=optic.LensFieldOfView(scopeWeapon.HipFieldOfView)*.5f*Mathf.Deg2Rad;
            scopeCamera.fieldOfView=Mathf.Max(1f,2f*Mathf.Atan(Mathf.Tan(halfAngle)*ScreenHeightOfAperture())*Mathf.Rad2Deg);
        }
        var aim = scopeWeapon.GetShotAimRay();
        scopeCamera.transform.SetPositionAndRotation(view.transform.position, Quaternion.LookRotation(aim.direction, view.transform.up));
        scopeCamera.cullingMask = view.cullingMask & ~(1 << gameObject.layer);
        scopeCamera.clearFlags=view.clearFlags;scopeCamera.backgroundColor=view.backgroundColor;
        scopeCamera.farClipPlane=view.farClipPlane;
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(scopeCamera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=scopeTexture});
    }
    void OnDisable(){scopeRendering=false;if(scopeCamera)scopeCamera.enabled=false;if(scopeLens)scopeLens.SetTexture("_BaseMap",Texture2D.blackTexture);}
    void OnDestroy(){ClearScope();}
}
