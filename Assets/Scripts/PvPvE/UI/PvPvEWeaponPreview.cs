using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
namespace DeadQuarantine.PvPvE.UI
{
 public sealed class PvPvEWeaponPreview:MonoBehaviour,IDragHandler {
  Camera cameraView;GameObject holder;RenderTexture texture;Coroutine building;Vector3 previewCenter,previewUp;LobbyCinematicPlayerDisplay character;
  readonly System.Collections.Generic.List<Material> ownedMaterials=new();
  static int nextStage;static readonly System.Collections.Generic.Stack<int> availableStages=new();int stage=-1;
  Vector3 StageOrigin=>new(30000+stage*100,1000,0);
  public bool Ready {get;private set;}
  public LobbyCinematicPlayerDisplay Character=>character;
  void OnEnable(){
   stage=availableStages.Count>0?availableStages.Pop():nextStage++;
   var go=new GameObject("Class weapon preview camera");go.transform.position=StageOrigin;cameraView=go.AddComponent<Camera>();
   cameraView.enabled=false;cameraView.clearFlags=CameraClearFlags.SolidColor;cameraView.backgroundColor=Color.clear;cameraView.cullingMask=1<<30;
   cameraView.useOcclusionCulling=false;cameraView.allowHDR=false;cameraView.allowMSAA=false;
   var data=cameraView.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.volumeLayerMask=0;data.renderShadows=false;
   cameraView.nearClipPlane=.01f;cameraView.farClipPlane=20;cameraView.fieldOfView=32;
   texture=new RenderTexture(1000,600,24,RenderTextureFormat.ARGB32);cameraView.targetTexture=texture;GetComponent<RawImage>().texture=texture;
  }
  public void Show(PvPvELoadoutCatalog.Gun gun,WeaponAttachmentCatalog catalog,WeaponAttachmentChoice choice){
   if(building!=null)StopCoroutine(building);if(holder){holder.SetActive(false);Destroy(holder);}Ready=false;
   building=StartCoroutine(Build(gun,catalog,choice));
  }
  public void ShowCharacter(LobbyCinematicPlayerDisplay actor){
   character=actor;cameraView.enabled=false;cameraView.cullingMask=1<<31;
   cameraView.targetTexture=null;texture.Release();Destroy(texture);texture=new RenderTexture(400,650,24,RenderTextureFormat.ARGB32);
   cameraView.targetTexture=texture;GetComponent<RawImage>().texture=texture;
  }
  IEnumerator Build(PvPvELoadoutCatalog.Gun gun,WeaponAttachmentCatalog catalog,WeaponAttachmentChoice choice){
   holder=new GameObject("Class preview visual");holder.SetActive(false);holder.transform.position=StageOrigin;
   foreach(var material in ownedMaterials)if(material)Destroy(material);ownedMaterials.Clear();
   var model=Instantiate(gun.model,holder.transform,false);model.name="Preview_"+gun.id;model.SetActive(true);
   foreach(var animator in model.GetComponentsInChildren<Animator>(true))animator.enabled=false;
   foreach(var lod in model.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);
   foreach(var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))Destroy(behaviour);
   foreach(var collider in model.GetComponentsInChildren<Collider>(true))Destroy(collider);
   foreach(var body in model.GetComponentsInChildren<Rigidbody>(true))Destroy(body);
   foreach(var particles in model.GetComponentsInChildren<ParticleSystem>(true)){particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=particles.main;main.playOnAwake=false;var renderer=particles.GetComponent<ParticleSystemRenderer>();if(renderer)renderer.enabled=false;}
   yield return null;
   if(catalog.Eligible(gun.id)){var a=model.AddComponent<WeaponAttachments>();a.ConfigureRuntime(catalog,gun.id,true);a.ApplyApproved(choice);a.enabled=false;}
   foreach(var light in model.GetComponentsInChildren<Light>(true))light.enabled=false;
   foreach(var line in model.GetComponentsInChildren<LineRenderer>(true))line.enabled=false;
   SetLayer(model.transform);foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=false;
   // Menu models must remain visible with every quality preset and each map's ambient light.
   // Copy albedo into UI-only materials; never alter a gameplay renderer/material or global lighting.
   var template=Resources.Load<Material>("PvPvE/ModelPreview");
   if(template)foreach(var renderer in model.GetComponentsInChildren<Renderer>(true)){
    if(renderer is ParticleSystemRenderer||renderer is LineRenderer)continue;
    var materials=renderer.sharedMaterials;
    for(int i=0;i<materials.Length;i++){var source=materials[i];if(!source)continue;var mat=new Material(template);mat.name="UI "+source.name;
     if(source.HasProperty("_BaseMap")){mat.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));mat.SetTextureScale("_BaseMap",source.GetTextureScale("_BaseMap"));mat.SetTextureOffset("_BaseMap",source.GetTextureOffset("_BaseMap"));}
     else if(source.HasProperty("_MainTex"))mat.SetTexture("_BaseMap",source.GetTexture("_MainTex"));
     mat.SetColor("_BaseColor",source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.HasProperty("_Color")?source.GetColor("_Color"):Color.white);materials[i]=mat;ownedMaterials.Add(mat);
    }renderer.sharedMaterials=materials;
   }
   holder.SetActive(true);yield return null;
   Frame();cameraView.enabled=false;Ready=true;building=null;
  }
  void Frame(){
   var renderers=holder.GetComponentsInChildren<Renderer>().WhereVisible();if(renderers.Length==0)return;
   Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
   var physical=WeaponAttachments.Find(holder.transform,"root");Vector3 up=physical?physical.forward:Vector3.up;
   previewCenter=b.center;previewUp=up;
   Vector3 direction=physical?(physical.right+physical.up*.2f+physical.forward*.22f).normalized:new Vector3(1,.2f,1).normalized;
   var rotation=Quaternion.LookRotation(-direction,up);var inverse=Quaternion.Inverse(rotation);
   float halfHeight=0,halfWidth=0,halfDepth=0;
   for(int i=0;i<8;i++){var corner=new Vector3((i&1)==0?-b.extents.x:b.extents.x,(i&2)==0?-b.extents.y:b.extents.y,(i&4)==0?-b.extents.z:b.extents.z);var local=inverse*corner;halfWidth=Mathf.Max(halfWidth,Mathf.Abs(local.x));halfHeight=Mathf.Max(halfHeight,Mathf.Abs(local.y));halfDepth=Mathf.Max(halfDepth,Mathf.Abs(local.z));}
   float tangent=Mathf.Tan(cameraView.fieldOfView*.5f*Mathf.Deg2Rad);
   float distance=Mathf.Max(.15f,Mathf.Max(halfHeight/tangent,halfWidth/(tangent*texture.width/texture.height))*1.12f+halfDepth);
   cameraView.transform.SetPositionAndRotation(b.center+direction*distance,Quaternion.LookRotation(-direction,up));
   var light=cameraView.GetComponent<Light>();if(!light)light=cameraView.gameObject.AddComponent<Light>();light.type=LightType.Spot;light.range=10;light.spotAngle=75;light.intensity=4;light.cullingMask=1<<30;light.shadows=LightShadows.None;
  }
  void FitWeaponPreview(){
   if(character||!Ready||!cameraView||!texture)return;
   var frame=transform.parent as RectTransform;if(!frame||frame.rect.height<1)return;
   float aspect=Mathf.Clamp(frame.rect.width/frame.rect.height,.5f,8);
   if(Mathf.Abs((float)texture.width/texture.height-aspect)<.025f)return;
   cameraView.targetTexture=null;texture.Release();Destroy(texture);texture=new RenderTexture(1000,Mathf.Clamp(Mathf.RoundToInt(1000/aspect),125,2000),24,RenderTextureFormat.ARGB32);texture.Create();cameraView.targetTexture=texture;cameraView.aspect=(float)texture.width/texture.height;GetComponent<RawImage>().texture=texture;
   var fitter=GetComponent<AspectRatioFitter>();if(fitter)fitter.aspectRatio=aspect;Frame();
  }
  void LateUpdate(){
   FitWeaponPreview();
   if(character&&cameraView&&texture){
    Ready=character.IsVisible&&character.TryGetHeadWorldPosition(out _);if(!Ready)return;
    var anchor=character.Anchor;var center=anchor.position+Vector3.up*.95f;var direction=anchor.forward;
    cameraView.fieldOfView=38;cameraView.transform.SetPositionAndRotation(center+direction*2.85f,Quaternion.LookRotation(-direction,Vector3.up));
    var nodes=anchor.GetComponentsInChildren<Transform>(true);var layers=new int[nodes.Length];
    try{for(int i=0;i<nodes.Length;i++){layers[i]=nodes[i].gameObject.layer;nodes[i].gameObject.layer=31;}RenderPipeline.SubmitRenderRequest(cameraView,new RenderPipeline.StandardRequest{destination=texture});}
    finally{for(int i=0;i<nodes.Length;i++)if(nodes[i])nodes[i].gameObject.layer=layers[i];}
   }else if(Ready&&cameraView&&texture)RenderPipeline.SubmitRenderRequest(cameraView,new RenderPipeline.StandardRequest{destination=texture});
  }
  public void OnDrag(PointerEventData e){if(!Ready||!holder)return;holder.transform.RotateAround(previewCenter,previewUp,e.delta.x*.3f);}
  static void SetLayer(Transform t){t.gameObject.layer=30;foreach(Transform c in t)SetLayer(c);}
  void OnDisable(){foreach(var material in ownedMaterials)if(material)Destroy(material);ownedMaterials.Clear();if(stage>=0){availableStages.Push(stage);stage=-1;}if(building!=null)StopCoroutine(building);building=null;Ready=false;character=null;if(holder){holder.SetActive(false);Destroy(holder);}if(cameraView){cameraView.enabled=false;cameraView.targetTexture=null;Destroy(cameraView.gameObject);}GetComponent<RawImage>().texture=null;if(texture){texture.Release();Destroy(texture);}holder=null;cameraView=null;texture=null;}
 }
 static class PreviewRenderers {
  public static Renderer[] WhereVisible(this Renderer[] source)=>System.Array.FindAll(source,r=>r.enabled&&r is not LineRenderer&&r is not ParticleSystemRenderer);
 }
}
