using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using DeadQuarantine.PvPvE.Networking;
namespace DeadQuarantine.PvPvE.UI
{
 // Owns preparation actions, countdown and failure; recovered loading art stays read-only.
 public sealed class PvPvEPreparationView:MonoBehaviour {
  PvPvEPreparationFlow flow;PvPvELoadingScreen loadingScreen;GameObject root,ownedInput;TMP_Text heading,details;GameObject leaveButton;bool disposed;
  public GameObject Panel=>root;
  void Awake(){flow=GetComponent<PvPvEPreparationFlow>();loadingScreen=GetComponent<PvPvELoadingScreen>();}
  void Update(){
   bool loading=loadingScreen&&loadingScreen.ShouldDisplay;
   bool show=flow&&(loading||flow.InPreparation&&flow.Stage.Value!=PvPvEPrepareStage.Playing&&flow.Stage.Value!=PvPvEPrepareStage.ReturningToLobby)&&!(PvPvEInitialClassView.Active&&PvPvEInitialClassView.Active.IsShowing)&&!PauseController.LocalPlayerPaused;
   if(show&&!root)Build();if(root)root.SetActive(show);if(!show)return;
   if(leaveButton)leaveButton.SetActive(flow.Stage.Value==PvPvEPrepareStage.Failed);
   EnsureInput();root.GetComponent<Image>().color=loading?Color.clear:flow.Stage.Value==PvPvEPrepareStage.Countdown?new Color(0,0,0,.18f):new Color(.008f,.018f,.022f,.98f);heading.gameObject.SetActive(!loading);details.gameObject.SetActive(!loading);
   bool countdown=flow.Stage.Value==PvPvEPrepareStage.Countdown;heading.font=PvPvEUIFactory.HeadingFont;heading.enableAutoSizing=!countdown;heading.fontSizeMax=countdown?96:34;heading.fontSize=countdown?96:34;heading.alignment=TextAlignmentOptions.Center;details.alignment=TextAlignmentOptions.Center;
   heading.rectTransform.anchorMin=countdown?new Vector2(.35f,.41f):new Vector2(.08f,.50f);heading.rectTransform.anchorMax=countdown?new Vector2(.65f,.59f):new Vector2(.92f,.67f);heading.rectTransform.offsetMin=heading.rectTransform.offsetMax=Vector2.zero;heading.color=Color.white;details.rectTransform.anchorMin=new(.08f,.30f);details.rectTransform.anchorMax=new(.92f,.40f);heading.text=flow.Status;details.text=countdown?"LA PARTIDA COMIENZA EN":flow.Stage.Value==PvPvEPrepareStage.Failed?"La partida no comenzó. Podés salir y buscar otra sala.":flow.Stage.Value==PvPvEPrepareStage.ReadyForPlacement?"Esperando inicio de la partida.":"Esperando la carga, el personaje y la clase de todos los participantes.";
   Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
  }
  void Build(){
   root=new GameObject("PvPvE preparation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=29990;
   var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1600,900);scale.matchWidthOrHeight=.5f;
   root.AddComponent<Image>().color=new Color(.008f,.018f,.022f,.98f);
   heading=Text("Status",new(.08f,.5f),new(.92f,.67f),34);details=Text("Details",new(.08f,.38f),new(.92f,.5f),22);
   var button=new GameObject("LeavePreparation",typeof(RectTransform),typeof(Image),typeof(Button));leaveButton=button;button.transform.SetParent(root.transform,false);var rect=(RectTransform)button.transform;rect.anchorMin=new(.7f,.08f);rect.anchorMax=new(.93f,.16f);rect.offsetMin=rect.offsetMax=Vector2.zero;button.GetComponent<Image>().color=new(.05f,.14f,.12f);button.GetComponent<Button>().onClick.AddListener(()=>{_ = PvPvEPreparationFlow.ExitToMenuAsync();});
   var label=new GameObject("Label",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();label.transform.SetParent(button.transform,false);label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;label.text="SALIR DE LA SALA";label.fontSize=22;label.alignment=TextAlignmentOptions.Center;PvPvEUIFactory.Polish(root.transform);
  }
  void EnsureInput(){
   if(!EventSystem.current&&!ownedInput){ownedInput=new GameObject("Preparation input",typeof(EventSystem),typeof(InputSystemUIInputModule));ownedInput.transform.SetParent(transform,false);ownedInput.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();}
  }
  TMP_Text Text(string name,Vector2 min,Vector2 max,int size){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(root.transform,false);var t=go.AddComponent<TextMeshProUGUI>();t.rectTransform.anchorMin=min;t.rectTransform.anchorMax=max;t.rectTransform.offsetMin=t.rectTransform.offsetMax=Vector2.zero;t.font=PvPvEUIFactory.BodyFont;t.fontSize=size;t.richText=false;t.color=PvPvEUIFactory.Accent;return t;}
  public void RestoreLobbyInput(){if(ownedInput){ownedInput.SetActive(false);Destroy(ownedInput);ownedInput=null;}}
  public void DisposeView(){if(disposed)return;disposed=true;if(root){root.SetActive(false);Destroy(root);}if(ownedInput)Destroy(ownedInput);}
  void OnDestroy()=>DisposeView();
 }
}
