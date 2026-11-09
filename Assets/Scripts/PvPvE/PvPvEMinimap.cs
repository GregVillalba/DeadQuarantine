using System.Collections.Generic;
using System.Linq;
using DeadQuarantine.PvPvE.Networking;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
namespace DeadQuarantine.PvPvE {
 // Projection, cropped UVs and per-player markers recovered from the previous minimap.
 // Uses baked navigation textures, not a runtime camera/RenderTexture.
 public sealed class PvPvEMinimap:MonoBehaviour {
  public static PvPvEMinimap Active{get;private set;}
  public static readonly Color AllyColor=new(.2f,.65f,1),EnemyColor=new(1,.2f,.18f);
  readonly Dictionary<ulong,Image> markers=new();
  PvPvECombat combat;PvPvETeamRoster roster;GameObject canvasRoot,panel;RectTransform viewport;RawImage terrain;TMP_Text title;
  Sprite circle,arrow;Texture2D circleTexture,arrowTexture;float nextRefresh;readonly Vector2 size=new(344,176);
  public GameObject Panel=>panel;public RawImage Terrain=>terrain;public bool Shown=>panel&&panel.activeSelf;
  public int VisibleMarkers=>markers.Values.Count(m=>m&&m.gameObject.activeSelf);
  public void Initialize(PvPvECombat source){
   Active=this;combat=source;roster=source.GetComponent<PvPvETeamRoster>();
   circle=Icon(false,out circleTexture);arrow=Icon(true,out arrowTexture);
   canvasRoot=new GameObject("PvPvE minimap",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));canvasRoot.transform.SetParent(transform,false);
   var canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=150;canvas.pixelPerfect=true;
   var scale=canvasRoot.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new(1600,900);scale.matchWidthOrHeight=.5f;
   panel=Rect(canvasRoot.transform,"Minimap",new(.025f,1),new(0,-24),new(360,216)).gameObject;
   Image(panel.transform,"Frame",new(360,216),new(0,0),new(.05f,.09f,.105f,.97f));
   Image(panel.transform,"Accent",new(360,2),new(0,0),PvPvEUIFactory.Accent);
   title=Text(panel.transform,"Map",new(240,22),new(10,-192),16);title.alignment=TextAlignmentOptions.MidlineLeft;
   var north=Text(panel.transform,"North",new(30,20),new(165,-3),15);north.text="N";north.alignment=TextAlignmentOptions.Center;
   var range=Text(panel.transform,"Range",new(90,22),new(260,-192),16);range.text=Mathf.Max(10,source.Settings.MinimapRange).ToString("0")+" m";range.alignment=TextAlignmentOptions.MidlineRight;
   viewport=Rect(panel.transform,"RadarViewport",new(0,1),new(8,-20),size);viewport.gameObject.AddComponent<RectMask2D>();
   terrain=Rect(viewport,"WalkableMap",new(0,1),Vector2.zero,size).gameObject.AddComponent<RawImage>();terrain.raycastTarget=false;
   panel.SetActive(false);
  }
  static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 dimensions){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.pivot=new(0,1);if(anchor.x==.5f)r.pivot=new(.5f,1);r.sizeDelta=dimensions;r.anchoredPosition=position;return r;}
  static Image Image(Transform parent,string name,Vector2 dimensions,Vector2 position,Color color){var i=Rect(parent,name,new(0,1),position,dimensions).gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;return i;}
  static TMP_Text Text(Transform parent,string name,Vector2 dimensions,Vector2 position,int fontSize){var t=Rect(parent,name,new(0,1),position,dimensions).gameObject.AddComponent<TextMeshProUGUI>();t.font=PvPvEUIFactory.BodyFont;t.fontSize=fontSize;t.color=new(.77f,.85f,.87f);t.raycastTarget=false;t.richText=false;return t;}
  static Sprite Icon(bool triangle,out Texture2D texture){
   const int n=64;texture=new Texture2D(n,n,TextureFormat.RGBA32,false);texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;var pixels=new Color32[n*n];
   for(int y=0;y<n;y++)for(int x=0;x<n;x++){float u=(x+.5f)/n*2-1,v=(y+.5f)/n*2-1;bool inside=triangle?v>-.72f&&v<.88f&&Mathf.Abs(u)<(.88f-v)*.48f:u*u+v*v<.72f*.72f;pixels[y*n+x]=new(255,255,255,(byte)(inside?255:0));}
   texture.SetPixels32(pixels);texture.Apply(false,true);return Sprite.Create(texture,new Rect(0,0,n,n),new(.5f,.5f),64,0,SpriteMeshType.FullRect);
  }
  // Shared north-up projection. Offscreen directions use the same scale on both axes.
  public static Vector2 Project(Vector2 delta,float range,Vector2 viewportSize,float margin,out bool edge){
   range=Mathf.Max(10,range);var point=delta*(viewportSize.x/range);var half=viewportSize*.5f-Vector2.one*margin;
   float scale=Mathf.Max(Mathf.Abs(point.x)/Mathf.Max(1,half.x),Mathf.Abs(point.y)/Mathf.Max(1,half.y));edge=scale>1;return edge?point/scale:point;
  }
  public static Rect MapUV(Vector2 center,Vector2 min,Vector2 max,float range,Vector2 dimensions){var bounds=max-min;float height=range*dimensions.y/dimensions.x;if(bounds.x<=0||bounds.y<=0)return new Rect(0,0,1,1);return new Rect((center.x-range*.5f-min.x)/bounds.x,(center.y-height*.5f-min.y)/bounds.y,range/bounds.x,height/bounds.y);}
  public bool EnemyVisible(PlayerHealth target){var local=combat.NetworkManager.LocalClient?.PlayerObject;var health=local?local.GetComponent<PlayerHealth>():null;return health&&target&&roster.TryGetMember(health.OwnerClientId,out var a)&&roster.TryGetMember(target.OwnerClientId,out var b)&&a.Team!=b.Team&&combat.IsRadarRevealed(target);}
  public bool TryMarker(ulong id,out Vector2 position,out bool edge,out Color color){position=default;edge=false;color=default;if(!markers.TryGetValue(id,out var image)||!image||!image.gameObject.activeSelf)return false;position=image.rectTransform.anchoredPosition;edge=image.sprite==arrow&&id!=combat.NetworkManager.LocalClientId;color=image.color;return true;}
  void Update(){
   if(!combat||!combat.IsSpawned||!combat.NetworkManager){if(panel)panel.SetActive(false);return;}
   if(Time.unscaledTime<nextRefresh||!panel)return;nextRefresh=Time.unscaledTime+.05f;
   var obj=combat.NetworkManager.LocalClient?.PlayerObject;var local=obj?obj.GetComponent<PlayerHealth>():null;
   var flow=combat.GetComponent<PvPvEPreparationFlow>();var map=System.Array.Find(combat.Settings.Maps,m=>m.Id==flow.Map.Value);
   bool show=!PauseController.LocalPlayerPaused&&combat.IsPlaying&&combat.Alive(local)&&map!=null&&map.Minimap&&roster.TryGetMember(local.OwnerClientId,out _);panel.SetActive(show);if(!show){foreach(var marker in markers.Values)marker.gameObject.SetActive(false);return;}
   roster.TryGetMember(local.OwnerClientId,out var owner);terrain.texture=map.Minimap;title.text=map.Name.ToUpperInvariant();float range=Mathf.Max(10,combat.Settings.MinimapRange);var center=new Vector2(local.transform.position.x,local.transform.position.z);terrain.uvRect=MapUV(center,map.MinimapMin,map.MinimapMax,range,size);
   foreach(var marker in markers.Values)marker.gameObject.SetActive(false);
   foreach(var player in combat.NetworkManager.SpawnManager.SpawnedObjectsList){
    if(!player.TryGetComponent<PlayerHealth>(out var target)||!combat.Alive(target)||!roster.TryGetMember(target.OwnerClientId,out var member))continue;
    bool ally=member.Team==owner.Team;if(!ally&&!EnemyVisible(target))continue;
    var point=Project(new Vector2(target.transform.position.x,target.transform.position.z)-center,range,size,10,out bool edge);
    if(!markers.TryGetValue(target.OwnerClientId,out var marker)){marker=Image(viewport,"Player_"+target.OwnerClientId,new(10,10),Vector2.zero,AllyColor);marker.rectTransform.anchorMin=marker.rectTransform.anchorMax=new(.5f,.5f);marker.rectTransform.pivot=new(.5f,.5f);var outline=marker.gameObject.AddComponent<Outline>();outline.effectColor=new(0,0,0,.85f);outline.effectDistance=new(1,-1);markers[target.OwnerClientId]=marker;}
    bool self=target.OwnerClientId==owner.ClientId;marker.sprite=self||edge?arrow:circle;marker.color=ally?AllyColor:EnemyColor;marker.rectTransform.sizeDelta=Vector2.one*(self?16:edge?15:10);marker.rectTransform.anchoredPosition=point;
    marker.rectTransform.localRotation=Quaternion.Euler(0,0,self?-target.transform.eulerAngles.y:edge?Mathf.Atan2(point.y,point.x)*Mathf.Rad2Deg-90:0);marker.gameObject.SetActive(true);
   }
   foreach(var id in markers.Keys.ToArray())if(!roster.Contains(id)){Destroy(markers[id].gameObject);markers.Remove(id);}
  }
  void OnDestroy(){if(canvasRoot)Destroy(canvasRoot);if(circle)Destroy(circle);if(arrow)Destroy(arrow);if(circleTexture)Destroy(circleTexture);if(arrowTexture)Destroy(arrowTexture);if(Active==this)Active=null;}
 }
}