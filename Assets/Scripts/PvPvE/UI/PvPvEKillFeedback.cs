using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeadQuarantine.PvPvE.Networking;
namespace DeadQuarantine.PvPvE.UI {
 public sealed class PvPvEKillFeedback:MonoBehaviour {
  PvPvECombat combat;GameObject root;TMP_Text totalText,details;Image headIcon;AudioSource sound;float until,began;double total,from,displayed;
  public double AccumulatedPoints=>total;public GameObject Panel=>root;public string Message=>totalText?totalText.text+" "+details.text:"";
  void Awake(){combat=GetComponent<PvPvECombat>();if(combat){combat.LocalElimination+=Elimination;combat.LocalAssist+=Assist;}}
  void Build(){
   root=new GameObject("PvPvE confirmed kill",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=29980;var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new(1600,900);scaler.matchWidthOrHeight=.5f;
   totalText=Label("Total",new(54,42),new(400,38),28);details=Label("Bonuses",new(54,-4),new(430,96),20);
   headIcon=new GameObject("Headshot",typeof(RectTransform),typeof(Image)).GetComponent<Image>();headIcon.transform.SetParent(root.transform,false);var r=headIcon.rectTransform;r.anchorMin=r.anchorMax=new(.5f,.5f);r.pivot=new(1,.5f);r.sizeDelta=new(28,28);r.anchoredPosition=new(48,-4);headIcon.sprite=Resources.Load<Sprite>("PvPvE/Headshot");headIcon.material=Resources.Load<Material>("PvPvE/HeadshotIcon");headIcon.preserveAspect=true;headIcon.raycastTarget=false;sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;
  }
  TMP_Text Label(string name,Vector2 position,Vector2 size,int fontSize){var t=new GameObject(name,typeof(RectTransform)).AddComponent<TextMeshProUGUI>();t.transform.SetParent(root.transform,false);var r=t.rectTransform;r.anchorMin=r.anchorMax=new(.5f,.5f);r.pivot=new(0,.5f);r.anchoredPosition=position;r.sizeDelta=size;t.font=PvPvEUIFactory.HeadingFont;t.fontSize=fontSize;t.alignment=TextAlignmentOptions.MidlineLeft;t.color=PvPvEUIFactory.Accent;t.raycastTarget=false;t.richText=false;return t;}
  void Elimination(double basis,double head,double far,bool zombie){
   Show(basis+head+far);details.text="+"+basis.ToString("0.##")+ (zombie?" ZOMBIE":" ELIMINACIÓN")+(head>0?"\n+"+head.ToString("0.##")+" HEADSHOT":"")+(far>0?"\n+"+far.ToString("0.##")+" LONGSHOT":"");headIcon.gameObject.SetActive(head>0);headIcon.rectTransform.anchoredPosition=new(48,head>0&&far>0?-4:-18);
  }
  public void ShowLegacy(int points,string reason,bool head){
   bool keepHead=head||reason=="ELIMINACIÓN"&&Time.unscaledTime<until&&headIcon&&headIcon.gameObject.activeSelf;
   Show(points);details.text=reason;headIcon.gameObject.SetActive(keepHead);
  }
  void Assist(int points){Show(points);details.text="ASISTENCIA";headIcon.gameObject.SetActive(false);}
  void Show(double points){
   if(!root)Build();if(Time.unscaledTime>=until){total=0;displayed=0;}from=displayed;total+=System.Math.Max(0,points);began=Time.unscaledTime;until=began+2.5f;root.SetActive(true);
   var manager=combat?combat.NetworkManager:Unity.Netcode.NetworkManager.Singleton;var player=manager?manager.LocalClient?.PlayerObject:null;var weapon=player?player.GetComponentInChildren<WeaponSwitcher>(true)?.CurrentWeapon:null;if(weapon)sound.outputAudioMixerGroup=weapon.PresentationMixer;if(combat&&combat.Settings.KillSound)sound.PlayOneShot(combat.Settings.KillSound);
  }
  void Update(){if(!root)return;float now=Time.unscaledTime;root.SetActive(now<until&&!PauseController.LocalPlayerPaused&&(!PvPvEView.Active||!PvPvEView.Active.ScoreboardVisible));if(!root.activeSelf)return;
   float t=Mathf.Clamp01((now-began)/.28f);displayed=from+(total-from)*(1-System.Math.Pow(1-t,3));totalText.text="+"+(t>=1?total:System.Math.Round(displayed)).ToString("0.##");float pop=Mathf.Sin(Mathf.Clamp01((now-began)/.26f)*Mathf.PI);totalText.rectTransform.localScale=Vector3.one*(1+.12f*pop);float alpha=Mathf.Clamp01((until-now)/.35f);totalText.alpha=details.alpha=alpha;headIcon.color=new Color(1,1,1,alpha);
  }
  void OnDestroy(){if(combat){combat.LocalElimination-=Elimination;combat.LocalAssist-=Assist;}if(root)Destroy(root);if(sound)Destroy(sound);}
 }
}
