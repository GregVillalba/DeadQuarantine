using System;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using DeadQuarantine.PvPvE.Networking;
namespace DeadQuarantine.PvPvE.UI
{
 public sealed class PvPvEInitialClassView:MonoBehaviour {
  public static PvPvEInitialClassView Active {get;private set;}
  public static bool BlocksLocalGameplay=>(Active&&(Active.changingLife||Active.selector&&Active.selector.ShouldShowLocal))||PvPvEPreparationFlow.LocalControlsLocked;
  PvPvEInitialClassSelection selector;PvPvEClassStore store;GameObject root;Canvas canvas;TMP_FontAsset font;
  Button[] classes;TMP_Text title,points,status,primaryLabel,secondaryLabel,primaryAttachments,secondaryAttachments;
  TMP_Text[] summary;Button confirm,reset;PvPvEWeaponPreview primaryPreview,secondaryPreview;bool changingLife;PvPvETeamDeployment deployment;TMP_Text personalized;
  int selected;uint observedEpoch,request;bool pending,disposed,shown;float pendingSince;string notice;string renderedSignature;
  CursorLockMode oldLock;bool oldVisible;GameObject ownedEventSystem;
  readonly Color tile=new(.035f,.065f,.07f,.97f),accent=PvPvEUIFactory.Accent;
  public GameObject Panel=>root;public bool IsShowing=>root&&root.activeInHierarchy;
  public bool Pending=>pending;public int SelectedIndex=>selected;public PvPvEClassStore Store=>store;
  public bool PreviewsReady=>primaryPreview&&primaryPreview.Ready&&secondaryPreview&&secondaryPreview.Ready;
  public PvPvEClassDefinition DisplayedClass {get;private set;}
  void Awake(){selector=GetComponent<PvPvEInitialClassSelection>();Active=this;selector.Changed+=OnChanged;selector.Reply+=OnReply;deployment=GetComponent<PvPvETeamDeployment>();if(deployment)deployment.ClassReply+=OnClassChangeReply;}
  void Update(){
   if(matchEditor&&matchEditor.EditingInMatch)matchEditor.HandleEscape();
   if(changingLife&&!PauseController.LocalPlayerPaused){if(matchEditor)matchEditor.CancelMatchEditing();changingLife=false;}
   bool desired=changingLife||selector&&selector.ShouldShowLocal;
   if(desired&&root==null)Build();
   if(desired!=shown){shown=desired;if(desired){oldLock=Cursor.lockState;oldVisible=Cursor.visible;store.Load();Refresh();}else RestoreCursor();}
   if(root&&root.activeSelf!=(desired&&(changingLife||!PauseController.LocalPlayerPaused))){root.SetActive(desired&&(changingLife||!PauseController.LocalPlayerPaused));renderedSignature=null;if(root.activeSelf)Refresh();}
   if(pending&&Time.unscaledTime-pendingSince>8){pending=false;notice="No llegó la confirmación. Podés reintentar.";Refresh();}
  }
  void LateUpdate(){if(IsShowing){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
  void OnChanged(){if(!root)return;if(observedEpoch!=selector.Epoch.Value){observedEpoch=selector.Epoch.Value;pending=false;notice="";store.Load();}Refresh();}
  void OnReply(uint epoch,uint id,bool accepted,string error){if(!pending||id!=request||epoch!=selector.Epoch.Value)return;if(!accepted){pending=false;notice=error;}Refresh();}
  PvPvEClassEditor matchEditor;
  public void OpenClassEditor(){
   OpenClassChange();if(!changingLife)return;
   matchEditor=FindFirstObjectByType<PvPvEClassEditor>(FindObjectsInactive.Include);
   if(!matchEditor)return;
   root.SetActive(true);
   matchEditor.OpenForNextRespawn(root.transform,selected,()=>{if(disposed||!changingLife)return;store.Load();Choose(matchEditor.SelectedClass);Confirm();});
  }
  public void OpenClassChange(){
   if(!deployment||!deployment.IsSpawned||!PauseController.LocalPlayerPaused)return;
   changingLife=true;pending=false;notice="La nueva clase se aplicará en el próximo respawn.";
   if(!root)Build();store.Load();Refresh();
  }
  void OnClassChangeReply(bool accepted,string error){if(!changingLife)return;pending=false;notice=error;if(accepted){changingLife=false;if(root)root.SetActive(false);}else Refresh();}
  public void Choose(int index){if(index<0||index>=5||pending||!changingLife&&selector.TryGet(selector.NetworkManager.LocalClientId,out _))return;selected=index;notice="";Refresh();}
  public void Confirm(){
   if(!root||pending||!changingLife&&(!selector.CanSelect(selector.NetworkManager.LocalClientId)||selector.TryGet(selector.NetworkManager.LocalClientId,out _)))return;
   var definition=store.Get(selected);if(!selector.Catalog.Validate(definition,out string error)){notice=error;Refresh();return;}
   pending=true;pendingSince=Time.unscaledTime;notice="";request++;Refresh();if(changingLife){deployment.ChangeClassServerRpc(selected,JsonUtility.ToJson(definition),selector.Epoch.Value);return;}selector.ConfirmServerRpc(selected,JsonUtility.ToJson(definition),selector.Epoch.Value,request);
  }
  void Refresh(){
   if(!root||disposed)return;var catalog=selector.Catalog;bool accepted=selector.TryGet(selector.NetworkManager.LocalClientId,out var approved)&&!changingLife;
   if(accepted){pending=false;selected=approved.ClassIndex;DisplayedClass=JsonUtility.FromJson<PvPvEClassDefinition>(approved.DefinitionJson.ToString());}else DisplayedClass=store.Get(selected);
   var c=DisplayedClass;bool valid=catalog.Validate(c,out string error);
   title.text=accepted?"CLASE INICIAL CONFIRMADA":"SELECCIONAR CLASE";
   var close=root.transform.Find("CloseClassChange");if(close)close.gameObject.SetActive(changingLife);
   personalized.text="CLASE PERSONALIZADA "+(selected+1);points.text=$"PICK 10  {catalog.Pick10Cost(c)}/10";points.color=valid?accent:new Color(1,.55f,.4f);
   for(int i=0;i<5;i++){var definition=store.Get(i);var a=catalog.Find(definition.primary);var b=catalog.Find(definition.secondary);
    classes[i].GetComponentInChildren<TMP_Text>().text="Clase personalizada "+(i+1);
    classes[i].targetGraphic.color=i==selected?new Color(.29f,.21f,.10f):tile;classes[i].interactable=!accepted&&!pending;
   }
   primaryLabel.text="PRIMARIA · "+catalog.Find(c.primary).name;secondaryLabel.text="SECUNDARIA · "+catalog.Find(c.secondary).name;
   string signature=c.primary+JsonUtility.ToJson(c.primaryAttachments)+c.secondary+JsonUtility.ToJson(c.secondaryAttachments);
   if(root.activeInHierarchy&&signature!=renderedSignature){renderedSignature=signature;primaryPreview.Show(catalog.Find(c.primary),catalog.attachments,c.primaryAttachments);secondaryPreview.Show(catalog.Find(c.secondary),catalog.attachments,c.secondaryAttachments);}
   primaryAttachments.text=AttachmentSummary(c.primaryAttachments);secondaryAttachments.text=AttachmentSummary(c.secondaryAttachments);
   var grenade=Array.Find(catalog.grenades,g=>g.id==c.grenade);var healing=catalog.FindHealing(c.healing);
   SetSummary(0,"GRANADA",(grenade==null?"NINGUNA":grenade.name+"\n"+catalog.GrenadeCapacity(c)+" UN."),grenade?.icon);
   SetSummary(1,"CURACIÓN",(healing==null?"NINGUNA":healing.name+$"\n{catalog.HealingCapacity(c)} UN. · {healing.healing} PV"),healing?.icon);
   SetSummary(2,"PERKS",string.Join(" · ",Enumerable.Range(0,4).Select(i=>catalog.perks.Find(PvPvELoadoutCatalog.GetPerk(c,i))?.NameForPvPvE).Where(n=>n!=null)),catalog.perks.Find(c.perk1)?.IconForPvPvE);
   SetSummary(3,"WILDCARDS",(catalog.wildcards.Find(c.wildcard1)?.name??"Ninguna")+"\n"+(catalog.wildcards.Find(c.wildcard2)?.name??"Ninguna"),catalog.wildcards.Find(c.wildcard1)?.icon);
   confirm.interactable=valid&&!accepted&&!pending&&(changingLife||selector.CanSelect(selector.NetworkManager.LocalClientId));
   confirm.GetComponentInChildren<TMP_Text>().text=accepted?"CLASE CONFIRMADA":pending?"CONFIRMANDO…":"CONFIRMAR CLASE";
   reset.gameObject.SetActive(!valid);reset.interactable=!valid&&!accepted&&!pending;
   status.text=accepted?(PvPvEPreparationFlow.Active?PvPvEPreparationFlow.Active.Status:"Clase aprobada por el servidor. Esperando la preparación de la partida."):!string.IsNullOrEmpty(notice)?notice:!valid?error:"Elegí una de tus cinco clases guardadas y confirmá.";
  }
  string AttachmentSummary(WeaponAttachmentChoice choice)=>string.Join(" · ",Enum.GetValues(typeof(WeaponAttachmentKind)).Cast<WeaponAttachmentKind>().Select(kind=>(kind switch{WeaponAttachmentKind.Scope=>"Mira",WeaponAttachmentKind.Support=>"Soporte",WeaponAttachmentKind.Utility=>"Láser / linterna",_=>"Boca"})+": "+(selector.Catalog.attachments.Find(choice.Get(kind))?.displayName??"Ninguno")));
  void SetSummary(int i,string category,string value,Sprite icon){summary[i].text=category+"\n"+value;var image=summary[i].transform.parent.Find("Icon").GetComponent<Image>();image.sprite=icon;image.enabled=icon;}
  void Build(){
   store??=new PvPvEClassStore(selector.Catalog);font=PvPvEUIFactory.BodyFont?PvPvEUIFactory.BodyFont:TMP_Settings.defaultFontAsset;observedEpoch=selector.Epoch.Value;
   root=new GameObject("PvPvE Initial Class",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
   canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
   var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
   root.AddComponent<Image>().color=new Color(.008f,.018f,.022f,.98f);
   if(!EventSystem.current){ownedEventSystem=new GameObject("Initial class input",typeof(EventSystem),typeof(InputSystemUIInputModule));ownedEventSystem.transform.SetParent(transform,false);ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();}
   title=Text("Title",root.transform,"SELECCIONAR CLASE",new(.03f,.89f),new(.96f,.97f),32,Color.white);
   Text("Hint",root.transform,"EQUIPAMIENTO INICIAL",new(.03f,.825f),new(.23f,.87f),17,accent);
   points=Text("Pick10",root.transform,"",new(.27f,.8f),new(.65f,.88f),26,accent);
   classes=new Button[5];for(int i=0;i<5;i++){int index=i;float top=.78f-i*.14f;classes[i]=Button("SavedClass_"+i,"",new(.03f,top-.12f),new(.24f,top));var label=classes[i].GetComponentInChildren<TMP_Text>();label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=20;classes[i].onClick.AddListener(()=>Choose(index));}
   personalized=Text("PersonalizedClass",root.transform,"",new(.69f,.8f),new(.97f,.88f),24,accent);
   primaryPreview=Preview("PrimaryPreview",new(.27f,.56f),new(.97f,.78f));
   secondaryPreview=Preview("SecondaryPreview",new(.27f,.30f),new(.97f,.47f));
   primaryLabel=Text("Primary",root.transform,"",new(.27f,.52f),new(.65f,.56f),20,Color.white);secondaryLabel=Text("Secondary",root.transform,"",new(.27f,.26f),new(.65f,.30f),20,Color.white);
   primaryAttachments=Text("PrimaryAttachments",root.transform,"",new(.27f,.47f),new(.65f,.52f),15,PvPvEUIFactory.Muted);secondaryAttachments=Text("SecondaryAttachments",root.transform,"",new(.27f,.215f),new(.65f,.26f),15,PvPvEUIFactory.Muted);
   summary=new TMP_Text[4];for(int i=0;i<4;i++){float left=.27f+i*.177f;var card=Rect("Summary_"+i,root.transform,new(left,.105f),new(left+.163f,.205f));card.gameObject.AddComponent<Image>().color=tile;
    var icon=Rect("Icon",card,new(.74f,.54f),new(.98f,.97f)).gameObject.AddComponent<Image>();icon.preserveAspect=true;icon.raycastTarget=false;icon.material=Resources.Load<Material>("PvPvE/WhiteIcon");
    summary[i]=Text("Details",card,"",new(.04f,.03f),new(.96f,.93f),17,Color.white);
   }
   status=Text("Status",root.transform,"",new(.27f,.008f),new(.97f,.035f),14,accent);
   reset=Button("ResetInvalidClass","RESTABLECER CLASE INVÁLIDA",new(.27f,.045f),new(.61f,.095f));reset.onClick.AddListener(()=>{if(!pending&&!selector.TryGet(selector.NetworkManager.LocalClientId,out _)){store.Reset(selected);notice="";Refresh();}});
   confirm=Button("ConfirmInitialClass","CONFIRMAR CLASE",new(.66f,.045f),new(.97f,.095f));confirm.onClick.AddListener(Confirm);
   var back=Button("CloseClassChange","VOLVER A PAUSA",new(.77f,.90f),new(.97f,.97f));back.onClick.AddListener(()=>{if(changingLife&&!pending){changingLife=false;root.SetActive(false);}});back.gameObject.AddComponent<CanvasGroup>();
   PvPvEUIFactory.Polish(root.transform,true);root.SetActive(false);Refresh();
  }
  ImageWeapon WhiteIcon(string name,Vector2 min,Vector2 max){var frame=Rect(name,root.transform,min,max);frame.gameObject.AddComponent<Image>().color=PvPvEUIFactory.Surface;frame.gameObject.AddComponent<RectMask2D>();var icon=Rect("Composition",frame,Vector2.zero,Vector2.one);return icon.gameObject.AddComponent<ImageWeapon>();}
  PvPvEWeaponPreview Preview(string name,Vector2 min,Vector2 max){var frame=Rect(name+"Frame",root.transform,min,max);frame.gameObject.AddComponent<Image>().color=PvPvEUIFactory.Surface;var r=Rect(name,frame,Vector2.zero,Vector2.one);var image=r.gameObject.AddComponent<RawImage>();image.raycastTarget=false;var aspect=r.gameObject.AddComponent<AspectRatioFitter>();aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=1000f/600;return r.gameObject.AddComponent<PvPvEWeaponPreview>();}
  RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
  TMP_Text Text(string name,Transform parent,string value,Vector2 min,Vector2 max,int size,Color color){var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.richText=false;t.textWrappingMode=TextWrappingModes.Normal;t.raycastTarget=false;return t;}
  Button Button(string name,string value,Vector2 min,Vector2 max){var r=Rect(name,root.transform,min,max);var image=r.gameObject.AddComponent<Image>();image.color=tile;var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;Text("Label",r,value,new(.04f,.02f),new(.96f,.98f),20,Color.white);return button;}
  void RestoreCursor(){if(shown)return;if(PauseController.LocalPlayerPaused||!selector||!selector.IsSpawned){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}else{Cursor.lockState=oldLock;Cursor.visible=oldVisible;}}
  public void DisposeView(){if(disposed)return;disposed=true;if(matchEditor)matchEditor.CancelMatchEditing();selector.Changed-=OnChanged;selector.Reply-=OnReply;if(deployment)deployment.ClassReply-=OnClassChangeReply;if(shown){shown=false;RestoreCursor();}if(root){root.SetActive(false);Destroy(root);}if(ownedEventSystem)Destroy(ownedEventSystem);if(Active==this)Active=null;}
  void OnDestroy()=>DisposeView();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  public void UseValidationProfile(string profile){store=new PvPvEClassStore(selector.Catalog,profile);if(root)Refresh();}
  public void RefreshValidationClasses(){store.Load();if(root)Refresh();}
#endif
 }
}
