using TMPro;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DeadQuarantine.PvPvE.Networking;
using DeadQuarantine.PvPvE.UI;
using static DeadQuarantine.PvPvE.PvPvEUIFactory;
namespace DeadQuarantine.PvPvE {
 // Recovered preview/shade/mode/map/status layout; current NGO flow remains the authority.
 public sealed class PvPvELoadingScreen:MonoBehaviour {
  PvPvEPreparationFlow flow;PvPvEMapVoting voting;PvPvETeamRoster roster;Canvas canvas;
  static PvPvENetworkSettings originalSettings;static PvPvELoadingScreen originalScreen;
  bool original;string originalTarget,originalOrigin;float originalProgress;PvPvEMapOption originalMap;TextMeshProUGUI modeLabel;
  public static void ConfigureOriginal(PvPvENetworkSettings settings)=>originalSettings=settings;
  public static void BeginOriginal(string scene){
   if(PvPvETeamRoster.Active)return;
   if(originalScreen){originalScreen.originalTarget=scene;return;}
   var settings=originalSettings;var matchmaking=PvPvEMatchmaking.Active;if(matchmaking)settings=matchmaking.Settings;
   var cfg=ConfiguracionPartidaSeleccionada.Instancia;if(!settings||cfg==null)return;
   var go=new GameObject("Original mode loading");DontDestroyOnLoad(go);var screen=go.AddComponent<PvPvELoadingScreen>();screen.original=true;screen.originalTarget=scene;screen.originalOrigin=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
   screen.originalMap=System.Array.Find(settings.Maps,m=>m.Id==cfg.escenarioSeleccionado);if(screen.originalMap==null){Destroy(go);return;}
   originalScreen=screen;screen.Build();screen.modeLabel.text=cfg.modoSeleccionado switch{ConfiguracionPartidaSeleccionada.ModoJuego.Historia=>"HISTORIA",ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5=>"RONDA 5",_=>"SUPERVIVENCIA"};screen.mapName.text=screen.originalMap.Name;
  }
  public static void OriginalProgress(float progress){if(originalScreen)originalScreen.originalProgress=Mathf.Clamp01(progress);}
  public static void CancelOriginal(){if(originalScreen){Destroy(originalScreen.gameObject);originalScreen=null;}}
  void UpdateOriginal(){
   var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
   if(scene!=originalOrigin&&scene!=originalTarget){CancelOriginal();return;}
   var manager=Unity.Netcode.NetworkManager.Singleton;var player=manager&&manager.LocalClient!=null?manager.LocalClient.PlayerObject:null;
   var spawn=player?player.GetComponent<MultiplayerPlayerSpawnAssigner>():null;
   // Story cinematics legitimately disable the FP camera. Loading ends at initialization/spawn;
   // the existing intro/Ready systems retain ownership of gameplay controls.
   if(scene==originalTarget&&player&&player.IsSpawned&&(!spawn||spawn.IsAtSpawn.Value)){CancelOriginal();return;}
   var views=originalMap.LoadingViews;preview.texture=views!=null&&views.Length>0?views[(int)(Time.unscaledTime/5)%views.Length]:originalMap.Preview;
   preview.color=preview.texture?Color.white:PanelColor;
   status.text=originalProgress<.9f?"CARGANDO ESCENARIO · "+Mathf.RoundToInt(originalProgress*100)+"%":"PREPARANDO JUGADORES";
   FitPreview();
  }
  Camera loadingCamera;RawImage preview;TextMeshProUGUI mapName,status;bool disposed;
  public GameObject Panel=>canvas?canvas.gameObject:null;
  public bool IsShowing=>canvas&&canvas.gameObject.activeInHierarchy;
  public string MapText=>mapName?mapName.text:"";
  public string StatusText=>status?status.text:"";
  public Texture Preview=>preview?preview.texture:null;
  public bool ShouldDisplay=>!disposed&&flow&&flow.IsSpawned&&voting.SelectedMap!=null&&!roster.Closing.Value&&!PauseController.LocalPlayerPaused&&
   !(PvPvEInitialClassView.Active&&PvPvEInitialClassView.Active.IsShowing)&&
   (flow.Stage.Value==PvPvEPrepareStage.Lobby?voting.Phase.Value==PvPvEVotePhase.VoteResult:
    flow.Stage.Value is PvPvEPrepareStage.LoadingMap or PvPvEPrepareStage.SelectingClasses or PvPvEPrepareStage.ReadyForPlacement or PvPvEPrepareStage.PlacingTeams);
  void Awake(){flow=GetComponent<PvPvEPreparationFlow>();voting=GetComponent<PvPvEMapVoting>();roster=GetComponent<PvPvETeamRoster>();}
  void Update(){
   if(original){UpdateOriginal();return;}
   bool show=ShouldDisplay;if(show&&!canvas)Build();if(!canvas)return;canvas.gameObject.SetActive(show);if(loadingCamera)loadingCamera.enabled=show;if(!show)return;
   var map=voting.SelectedMap;var photos=map.LoadingViews;var photo=photos!=null&&photos.Length>0?photos[(int)(Time.unscaledTime/5)%photos.Length]:map.Preview;preview.texture=photo;preview.color=photo?Color.white:PanelColor;mapName.text=map.Name;
   status.text=flow.Stage.Value==PvPvEPrepareStage.Lobby?"LA PARTIDA COMIENZA EN "+Mathf.Max(0,Mathf.CeilToInt((float)voting.Remaining)):flow.Status;
   // Cover the screen without stretching the photograph, including 16:10 and ultrawide.
   FitPreview();
  }
  void FitPreview(){
   float screen=((RectTransform)canvas.transform).rect.width/((RectTransform)canvas.transform).rect.height;
   float image=preview.texture?(float)preview.texture.width/preview.texture.height:screen;
   preview.uvRect=screen>image?new Rect(0,(1-image/screen)*.5f,1,image/screen):new Rect((1-screen/image)*.5f,0,screen/image,1);
  }
  void Build(){
   canvas=Canvas(gameObject,"PvPvELoading",29980);var root=(RectTransform)canvas.transform;
   preview=Rect(root,"MapPreview",0,0,1920,1080).gameObject.AddComponent<RawImage>();Stretch(preview.rectTransform);preview.raycastTarget=false;
   var shade=Panel(root,"Shade",0,0,1920,1080,.32f);Stretch(shade);
   loadingCamera=new GameObject("Loading camera fallback").AddComponent<Camera>();loadingCamera.transform.SetParent(transform,false);loadingCamera.cullingMask=0;loadingCamera.clearFlags=CameraClearFlags.SolidColor;loadingCamera.backgroundColor=Color.black;loadingCamera.depth=-1000;
   var content=new GameObject("MapInformation",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();content.SetParent(root,false);Place(content,new(.025f,.06f),new(.945f,.34f));content.GetComponent<Image>().color=new Color(.015f,.02f,.025f,.90f);content.GetComponent<Image>().raycastTarget=false;
   var rule=new GameObject("Rule",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();rule.SetParent(content,false);rule.anchorMin=new(0,1);rule.anchorMax=Vector2.one;rule.sizeDelta=new(0,2);rule.anchoredPosition=Vector2.zero;rule.GetComponent<Image>().color=Accent;
   var mode=Text(content,"Mode","PvPvE TEAM DEATHMATCH",0,0,1,1,null,26);Place(mode.rectTransform,new(.018f,.75f),new(.97f,.95f));mode.color=Accent;modeLabel=mode;
   mapName=Text(content,"MapName","",0,0,1,1,null,72);Place(mapName.rectTransform,new(.018f,.36f),new(.97f,.77f));mapName.font=HeadingFont;
   status=Text(content,"State","",0,0,1,1,null,24);Place(status.rectTransform,new(.018f,.06f),new(.72f,.32f));

  }
  static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
  public void DisposeView(){if(disposed)return;disposed=true;if(loadingCamera)Destroy(loadingCamera.gameObject);if(canvas){canvas.gameObject.SetActive(false);Destroy(canvas.gameObject);}}
  void OnDestroy(){if(originalScreen==this)originalScreen=null;DisposeView();}
 }
}
