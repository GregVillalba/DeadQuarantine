using DeadQuarantine.PvPvE.Networking;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DeadQuarantine.PvPvE.UI {
 public sealed class PvPvEDeploymentView:MonoBehaviour {
  PvPvETeamDeployment deployment;GameObject root,input;TMP_Text heading,message;RectTransform choices;bool started;float startUntil;Button pauseClassButton;PauseController pauseOwner;TMP_Text pauseClassLabel;Image backdrop;string classProfile="local";
  public GameObject Panel=>root;
  public bool Choosing{get;private set;}
  public string Feedback{get;private set;}="";
  void Awake(){deployment=GetComponent<PvPvETeamDeployment>();deployment.ClassReply+=Reply;}
  void Reply(bool accepted,string text){Feedback=text;if(accepted)Choosing=false;}
  void Update(){
   bool active=deployment&&deployment.IsSpawned&&PvPvEPreparationFlow.Active&&PvPvEPreparationFlow.Active.Stage.Value==PvPvEPrepareStage.Playing;
   if(!active){started=false;Choosing=false;Feedback="";}
   if(active&&!started){started=true;startUntil=Time.unscaledTime+1;}
   bool start=active&&Time.unscaledTime<startUntil;
   bool dead=active&&deployment.TryGet(deployment.NetworkManager.LocalClientId,out var row)&&row.Life==PvPvELifeStage.Dead;
   var player=active?deployment.NetworkManager.LocalClient?.PlayerObject:null;
   var pauseController=player?player.GetComponentInChildren<PauseController>(true):null;
   if(active&&pauseController&&pauseOwner!=pauseController)InstallPauseAction(pauseController);
   bool pause=active&&PauseController.LocalPlayerPaused&&pauseController&&pauseController.IsPauseHomeVisible;
   if(!pause)Choosing=false;
   if(pauseClassLabel)pauseClassLabel.text=Choosing?"VOLVER A PAUSA":"CAMBIAR CLASE"+(string.IsNullOrEmpty(Feedback)?"":"\nSe aplicará en el próximo respawn");
   bool show=active&&(!PauseController.LocalPlayerPaused||pause)&&(dead||pause&&Choosing||start||PvPvETeamDeployment.LocalBlocked);if(show&&!root)Build();if(root)root.SetActive(show);if(!show)return;
   if(backdrop){PvPvEUIFactory.Place(backdrop.rectTransform,dead?new(.28f,.35f):new(.50f,.18f),dead?new(.72f,.69f):new(.98f,.88f));backdrop.gameObject.SetActive(dead||pause);}
   PvPvEUIFactory.Place(heading.rectTransform,dead?new(.32f,.53f):new(.54f,.76f),dead?new(.68f,.64f):new(.94f,.85f));
   PvPvEUIFactory.Place(message.rectTransform,dead?new(.32f,.39f):new(.54f,.65f),dead?new(.68f,.53f):new(.94f,.75f));
   heading.font=PvPvEUIFactory.HeadingFont;heading.fontSize=dead?44:28;message.font=PvPvEUIFactory.BodyFont;
   heading.alignment=dead?TextAlignmentOptions.Center:TextAlignmentOptions.MidlineLeft;message.alignment=heading.alignment;
   if(start&&!dead&&!pause){PvPvEUIFactory.Place(heading.rectTransform,new(.35f,.41f),new(.65f,.59f));heading.alignment=TextAlignmentOptions.Center;heading.fontSize=96;heading.color=Color.white;if(backdrop){PvPvEUIFactory.Place(backdrop.rectTransform,new(.35f,.41f),new(.65f,.59f));backdrop.color=new Color(0,0,0,.65f);backdrop.gameObject.SetActive(true);}}
   heading.text=dead?"HAS MUERTO":pause?"CLASE PARA LA PRÓXIMA VIDA":start?"START":"PREPARANDO EQUIPAMIENTO";
   message.text=dead&&deployment.TryGet(deployment.NetworkManager.LocalClientId,out row)?"REAPARECIENDO EN "+Mathf.Max(0,Mathf.CeilToInt((float)(row.Due-deployment.NetworkManager.ServerTime.Time)))+"\n"+Feedback:Feedback;
   choices.gameObject.SetActive(pause);foreach(Transform child in choices)if(child.name.StartsWith("Class "))child.gameObject.SetActive(Choosing);
   if(pause){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
  }
  void InstallPauseAction(PauseController owner){
   if(pauseClassButton)Destroy(pauseClassButton.gameObject);pauseOwner=owner;if(!owner.PauseHomePanel)return;
   var sample=owner.PauseHomePanel.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.gameObject.activeSelf&&b.GetComponentInChildren<TMP_Text>(true));if(!sample)return;
   var clone=Instantiate(sample.gameObject,sample.transform.parent);clone.name="PvPvE_ChangeClass";pauseClassButton=clone.GetComponent<Button>();pauseClassButton.onClick=new Button.ButtonClickedEvent();pauseClassButton.onClick.AddListener(()=>GetComponent<PvPvEInitialClassView>()?.OpenClassChange());pauseClassLabel=clone.GetComponentInChildren<TMP_Text>(true);pauseClassLabel.text="CAMBIAR CLASE";PvPvEUIFactory.StyleButton(pauseClassButton);
   owner.RefreshPausePresentation(pauseClassButton);

  }
  #if UNITY_EDITOR || DEVELOPMENT_BUILD
  public void UseValidationProfile(string profile)=>classProfile=profile;
  #endif
  public void Choose(int index){
   var store=new PvPvEClassStore(deployment.Settings.Loadouts,classProfile);var c=store.Get(index);
   if(!deployment.Settings.Loadouts.Validate(c,out var error)){Feedback=error;return;}
   deployment.ChangeClassServerRpc(index,JsonUtility.ToJson(c),deployment.GetComponent<PvPvEInitialClassSelection>().Epoch.Value);
  }
  void Build(){
   root=new GameObject("PvPvE life and class",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=29995;var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new(1600,900);
   heading=Label("Title",new(.61f,.8f),new(.97f,.91f),28);message=Label("Message",new(.61f,.61f),new(.97f,.8f),20);
   choices=new GameObject("Class options",typeof(RectTransform)).GetComponent<RectTransform>();choices.SetParent(root.transform,false);choices.anchorMin=new(.54f,.23f);choices.anchorMax=new(.94f,.63f);choices.offsetMin=choices.offsetMax=Vector2.zero;
   var bg=new GameObject("LifeBackdrop",typeof(RectTransform),typeof(Image));bg.transform.SetParent(root.transform,false);bg.transform.SetAsFirstSibling();backdrop=bg.GetComponent<Image>();backdrop.color=PvPvEUIFactory.PanelColor;backdrop.raycastTarget=false;
   var store=new PvPvEClassStore(deployment.Settings.Loadouts,classProfile);
   for(int i=0;i<5;i++){int index=i;var c=store.Get(i);Button("Class "+i,c.name+" · "+deployment.Settings.Loadouts.Find(c.primary)?.name+" / "+deployment.Settings.Loadouts.Find(c.secondary)?.name,i,()=>Choose(index));}
   if(!EventSystem.current){input=new GameObject("PvPvE life input",typeof(EventSystem),typeof(InputSystemUIInputModule));input.transform.SetParent(transform,false);input.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();}
  }
  void Button(string name,string text,int index,UnityEngine.Events.UnityAction action){
   var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(choices,false);var rt=(RectTransform)go.transform;rt.anchorMin=new(0,1-(index+1)*.15f);rt.anchorMax=new(1,1-index*.15f);rt.offsetMin=new(0,3);rt.offsetMax=new(0,-3);go.GetComponent<Image>().color=new(.025f,.10f,.11f,.96f);go.GetComponent<Button>().onClick.AddListener(action);PvPvEUIFactory.StyleButton(go.GetComponent<Button>());
   var t=new GameObject("Label",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();t.transform.SetParent(go.transform,false);t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;t.rectTransform.offsetMin=new(10,0);t.rectTransform.offsetMax=new(-10,0);t.font=PvPvEUIFactory.BodyFont;t.fontSize=18;t.text=text;t.richText=false;t.alignment=TextAlignmentOptions.MidlineLeft;
  }
  TMP_Text Label(string name,Vector2 min,Vector2 max,int size){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(root.transform,false);var t=go.AddComponent<TextMeshProUGUI>();t.rectTransform.anchorMin=min;t.rectTransform.anchorMax=max;t.rectTransform.offsetMin=t.rectTransform.offsetMax=Vector2.zero;t.fontSize=size;t.color=PvPvEUIFactory.Accent;t.richText=false;return t;}
  public void DisposeView(){if(pauseClassButton)Destroy(pauseClassButton.gameObject);if(root){root.SetActive(false);Destroy(root);}if(input)Destroy(input);if(deployment)deployment.ClassReply-=Reply;}
  void OnDestroy()=>DisposeView();
 }
}
