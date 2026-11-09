using System;
using System.Collections.Generic;
using System.Linq;
using DeadQuarantine.PvPvE.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static DeadQuarantine.PvPvE.PvPvEUIFactory;
namespace DeadQuarantine.PvPvE {
 // Recovered match-only view: table cells, ordering, local summary and feed layout.
 // Existing class/lobby/minimap/life HUD and confirmed-score feedback keep their own owners.
 public sealed class PvPvEView:MonoBehaviour {
  public static PvPvEView Active{get;private set;}
  PvPvECombat combat;PvPvETeamRoster roster;PvPvEPreparationFlow flow;Canvas canvas;
  RectTransform scoreboard,header,feedRoot;TextMeshProUGUI matchHud,localStats,resultTitle;
  readonly TextMeshProUGUI[] teamTitles=new TextMeshProUGUI[2];
  readonly TextMeshProUGUI[,,] scoreCells=new TextMeshProUGUI[2,5,5];readonly Image[,] scoreRows=new Image[2,5];
  ImageWeapon[] feedWeapons;Image[] headshots;
  TextMeshProUGUI[,] feedCells;PlayerControls controls;InputAction scoreboardAction;bool ownsAction;float refreshAt;
  public GameObject CanvasRoot=>canvas?canvas.gameObject:null;
  public bool ScoreboardVisible=>scoreboard&&scoreboard.gameObject.activeSelf;
  public string MatchText=>matchHud?matchHud.text:"";public string LocalSummary=>localStats?localStats.text:"";
  public int VisibleFeedCount{get;private set;}
  public void Initialize(PvPvECombat source){
   Active=this;combat=source;roster=source.GetComponent<PvPvETeamRoster>();flow=source.GetComponent<PvPvEPreparationFlow>();
   canvas=Canvas(gameObject,"PvPvE Match HUD",500);BuildMatch(canvas.transform);
   controls=new PlayerControls();ConfiguracionesJuego.CargarRebinds(controls.asset);scoreboardAction=controls.asset.FindAction("Scoreboard",false);ownsAction=scoreboardAction==null;if(ownsAction)scoreboardAction=new InputAction("Scoreboard",InputActionType.Button,"<Keyboard>/tab");scoreboardAction.Enable();
   canvas.gameObject.SetActive(false);
  }
  void BuildMatch(Transform parent){
   header=Panel(parent,"MatchHUD",0,290,660,98);header.anchorMin=header.anchorMax=new(.5f,1);header.pivot=new(.5f,1);header.anchoredPosition=new(0,-28);
   matchHud=Text(header,"Scores","",18,9,624,80,null,25);matchHud.alignment=TextAlignmentOptions.Center;
   feedRoot=Rect(parent,"KillFeed",0,25,550,260);feedRoot.anchorMin=feedRoot.anchorMax=new(1,1);feedRoot.pivot=new(1,1);feedRoot.anchoredPosition=new(-30,-28);
   feedCells=new TextMeshProUGUI[Mathf.Clamp(combat.Settings.KillFeedRows,1,10),3];
   for(int row=0;row<feedCells.GetLength(0);row++){var line=Panel(feedRoot,"Death "+row,0,row*33,550,30,.62f);float[] x={8,197,365};float[] widths={185,164,177};for(int c=0;c<3;c++){feedCells[row,c]=Text(line,"Cell "+c,"",x[c],2,widths[c],26,null,c==1?16:20);feedCells[row,c].textWrappingMode=TextWrappingModes.NoWrap;feedCells[row,c].alignment=c==2?TextAlignmentOptions.MidlineRight:TextAlignmentOptions.MidlineLeft;}}
   feedWeapons=new ImageWeapon[feedCells.GetLength(0)];headshots=new Image[feedWeapons.Length];
   for(int row=0;row<feedWeapons.Length;row++){
    var line=feedCells[row,0].transform.parent;
    feedWeapons[row]=Rect(line,"WeaponIcon",197,2,120,26).gameObject.AddComponent<ImageWeapon>();
    var image=Rect(line,"Headshot",325,1,28,28).gameObject.AddComponent<Image>();image.sprite=Resources.Load<Sprite>("PvPvE/Headshot");image.preserveAspect=true;image.raycastTarget=false;image.material=Resources.Load<Material>("PvPvE/HeadshotIcon");headshots[row]=image;
   }
   var help=Text(parent,"Help","TAB  ESTADÍSTICAS    /    ESC  PAUSA",0,0,720,28,null,16);help.rectTransform.anchorMin=help.rectTransform.anchorMax=new(.5f,0);help.rectTransform.pivot=new(.5f,0);help.rectTransform.anchoredPosition=new(0,12);help.alignment=TextAlignmentOptions.Center;
   scoreboard=Panel(parent,"Scoreboard",0,0,1850,850,.97f);scoreboard.anchorMin=scoreboard.anchorMax=new(.5f,.5f);scoreboard.pivot=new(.5f,.5f);scoreboard.anchoredPosition=Vector2.zero;
   resultTitle=Text(scoreboard,"Title","ESTADÍSTICAS DE PARTIDA",40,15,1770,96,null,32);resultTitle.alignment=TextAlignmentOptions.Center;
   for(int team=0;team<2;team++){
    float x=30+team*910;Color color=TeamColor(team==0?PvPvETeam.Alpha:PvPvETeam.Bravo);
    var table=Panel(scoreboard,"Team"+team,x,115,880,590,.65f);Line(table,0,0,880,color);
    teamTitles[team]=Text(table,"TeamScore","",20,20,840,54,null,38);teamTitles[team].color=color;
    float[] columns={20,350,480,610,740};string[] labels={"JUGADOR","PUNTOS","KILLS","MUERTES","ASIST."};
    for(int c=0;c<5;c++)Text(table,"Header"+c,labels[c],columns[c],95,c==0?300:130,30,null,17).color=Color.gray;
    for(int row=0;row<5;row++){
     var line=Panel(table,"Row"+row,8,145+row*82,864,75,.5f);scoreRows[team,row]=line.GetComponent<Image>();
     for(int c=0;c<5;c++){scoreCells[team,row,c]=Text(line,"Cell"+c,"",columns[c]-8,22,c==0?300:130,42,null,23);scoreCells[team,row,c].textWrappingMode=TextWrappingModes.NoWrap;}
    }
   }
   Line(scoreboard,30,740,1790,new(.2f,.27f,.24f));localStats=Text(scoreboard,"LocalSummary","",50,773,1750,50,null,24);PvPvEUIFactory.Polish(scoreboard);scoreboard.gameObject.SetActive(false);
  }
  public static PvPvECombatStats[] Sorted(IEnumerable<PvPvECombatStats> stats,PvPvETeam team)=>stats.Where(p=>p.Team==team).OrderByDescending(p=>p.Points).ThenByDescending(p=>p.Kills).ThenByDescending(p=>p.Assists).ThenBy(p=>p.ClientId).ToArray();
  public static string Clock(double seconds){int left=Math.Max(0,(int)Math.Ceiling(seconds));return(left/60).ToString("00")+":"+(left%60).ToString("00");}
  public static Color TeamColor(PvPvETeam team)=>team==PvPvETeam.Alpha?new(.25f,.65f,1):team==PvPvETeam.Bravo?new(.96f,.35f,.3f):new(.8f,.84f,.85f);
  public string Cell(int team,int row,int column)=>scoreCells[team,row,column].text;
  public int VisibleRows(int team)=>Enumerable.Range(0,5).Count(i=>scoreRows[team,i].gameObject.activeSelf);
  public ulong[] DisplayedIds(PvPvETeam team)=>Sorted(Enumerable.Range(0,combat.Stats.Count).Select(i=>combat.Stats[i]).Where(s=>roster.Contains(s.ClientId)),team).Select(s=>s.ClientId).ToArray();
  public string FeedLine(int row)=>string.Join(" ",Enumerable.Range(0,3).Select(c=>feedCells[row,c].text));
  void Update(){
   if(!canvas||!combat||!combat.IsSpawned||!combat.NetworkManager)return;
   bool active=(flow.Stage.Value is PvPvEPrepareStage.Playing or PvPvEPrepareStage.ReturningToLobby)&&!roster.Closing.Value&&!PauseController.LocalPlayerPaused;canvas.gameObject.SetActive(active);if(!active){scoreboard.gameObject.SetActive(false);return;}
   bool show=(combat.ScoreLocked.Value||scoreboardAction!=null&&scoreboardAction.IsPressed())&&!PauseController.LocalPlayerPaused&&!NPCShopUI.LocalShopOpen&&!(UI.PvPvEInitialClassView.Active&&UI.PvPvEInitialClassView.Active.IsShowing);scoreboard.gameObject.SetActive(show);header.gameObject.SetActive(!show);canvas.sortingOrder=show?30000:500;
   var screen=(RectTransform)canvas.transform;float fit=Mathf.Min(1,screen.rect.width*.96f/1850,screen.rect.height*.84f/850);scoreboard.localScale=Vector3.one*fit;
   if(Time.unscaledTime<refreshAt)return;refreshAt=Time.unscaledTime+.1f;RefreshMatch();
  }
  void RefreshMatch(){
   matchHud.text="ALPHA "+combat.AlphaKills.Value+"  —  "+combat.BravoKills.Value+" BRAVO\nOBJETIVO "+combat.Settings.KillLimit+"  ·  "+Clock(combat.RemainingSeconds);
   if(combat.Resolution.Value==PvPvEMatchResolution.Overtime)matchHud.text="ALPHA "+combat.AlphaKills.Value+"  —  "+combat.BravoKills.Value+" BRAVO\nOVERTIME · MUERTE SÚBITA";
   if(combat.ScoreLocked.Value){combat.TryStats(combat.NetworkManager.LocalClientId,out var local);string outcome=combat.Winner.Value==PvPvETeam.None?"EMPATE":local.Team==combat.Winner.Value?"VICTORIA":"DERROTA";resultTitle.fontSize=42;resultTitle.fontSizeMax=42;resultTitle.font=HeadingFont;resultTitle.text=outcome+" · "+combat.ResultReason.Value+"\n"+(combat.Resolution.Value==PvPvEMatchResolution.ReturningToLobby?"VOLVIENDO AL LOBBY…":"LOBBY EN "+Math.Max(0,Math.Ceiling(combat.ResultsDeadline.Value-combat.NetworkManager.ServerTime.Time)));}
   else resultTitle.text="ESTADÍSTICAS DE PARTIDA";
   UpdateScoreboard();double now=combat.NetworkManager.ServerTime.Time;var feed=Enumerable.Range(0,combat.KillFeed.Count).Select(i=>combat.KillFeed[i]).Where(e=>e.Expires>now).OrderByDescending(e=>e.Sequence).Take(feedCells.GetLength(0)).ToArray();VisibleFeedCount=feed.Length;
   for(int row=0;row<feedCells.GetLength(0);row++){
    feedCells[row,0].transform.parent.gameObject.SetActive(row<feed.Length);if(row>=feed.Length)continue;var e=feed[row];
    feedCells[row,0].text=e.KillerName.ToString();feedCells[row,0].color=TeamColor(e.KillerTeam);var gun=combat.Settings.Loadouts.weapons.FirstOrDefault(g=>g.id==e.Weapon.ToString()||g.name==e.Weapon.ToString());
    feedWeapons[row].gameObject.SetActive(gun!=null);if(gun!=null)feedWeapons[row].BindCatalog(gun,combat.Settings.Loadouts.attachments,default);
    headshots[row].gameObject.SetActive(e.Headshot);feedCells[row,1].text=gun==null?e.Weapon.ToString():"";feedCells[row,2].text=e.VictimName.ToString();feedCells[row,2].color=TeamColor(e.VictimTeam);
   }
  }
  void UpdateScoreboard(){
   if(!ScoreboardVisible)return;
   for(int team=0;team<2;team++){
    var which=team==0?PvPvETeam.Alpha:PvPvETeam.Bravo;var people=Sorted(Enumerable.Range(0,combat.Stats.Count).Select(i=>combat.Stats[i]).Where(s=>roster.Contains(s.ClientId)),which);teamTitles[team].text=which.ToString().ToUpperInvariant()+"   "+(team==0?combat.AlphaKills.Value:combat.BravoKills.Value);
    for(int row=0;row<5;row++){
     scoreRows[team,row].gameObject.SetActive(row<people.Length);if(row>=people.Length)continue;var p=people[row];bool own=p.ClientId==combat.NetworkManager.LocalClientId;roster.TryGetMember(p.ClientId,out var member);
     scoreRows[team,row].color=own?new(.27f,.21f,.12f,.96f):new(.055f,.069f,.08f,.80f);string[] values={member.Name.ToString(),p.Points.ToString(),p.Kills.ToString(),p.Deaths.ToString(),p.Assists.ToString()};for(int c=0;c<5;c++)scoreCells[team,row,c].text=values[c];
    }
   }
   if(combat.TryStats(combat.NetworkManager.LocalClientId,out var local)){roster.TryGetMember(local.ClientId,out var member);localStats.text=member.Name+"    PUNTOS "+local.Points+"    KILLS "+local.Kills+"    MUERTES "+local.Deaths+"    ASISTENCIAS "+local.Assists;}
  }
  void OnEnable(){scoreboardAction?.Enable();}
  void OnDisable(){scoreboardAction?.Disable();if(scoreboard)scoreboard.gameObject.SetActive(false);}
  void OnDestroy(){scoreboardAction?.Disable();if(ownsAction)scoreboardAction?.Dispose();controls?.Dispose();if(canvas)Destroy(canvas.gameObject);if(Active==this)Active=null;}
 }
}