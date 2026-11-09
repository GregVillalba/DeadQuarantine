using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace DeadQuarantine.PvPvE.UI
{
 public sealed class PvPvEClassEditor:MonoBehaviour {
  public PvPvELoadoutCatalog catalog;
  public RectTransform container;
  public TMP_FontAsset font;
  public Button entryButton;
  GameObject panel,picker;RectTransform options;TMP_Text heading,status,pickerTitle,previewTitle,stats;
  TMP_InputField nameInput;Button[] classButtons;Button primaryCard,secondaryCard,grenadeCard,healingCard;Button[,] attachmentCards;
  Button[] perkCards,wildcardCards;
  TMP_Text pick10Counter;Image[] pick10Pips;
  readonly Dictionary<int,PvPvEClassDefinition> legacyDrafts=new();
  PvPvEWeaponPreview preview,characterPreview,pickerPreview;TMP_Text pickerStats;Image pickerDetailIcon;bool pickerEquipment;PvPvEClassStore store;int selected;bool secondaryFocus;
  readonly Color accent=PvPvEUIFactory.Accent,tile=PvPvEUIFactory.Surface;
  public bool IsOpen=>panel&&panel.activeSelf;
  int escapeFrame=-1;
  public bool ConsumedEscapeThisFrame=>escapeFrame==Time.frameCount;
  public int SelectedClass=>selected;
  public PvPvEClassStore Store=>store;
  public PvPvEClassDefinition Current=>legacyDrafts.TryGetValue(selected,out var draft)?draft.Copy():store.Get(selected);
  public int PointsUsed=>catalog.Pick10Cost(Current);
  public bool PreviewReady=>preview&&preview.Ready;
  public bool CharacterPreviewReady=>false;
  public GameObject Panel=>panel;
  void Awake(){font=PvPvEUIFactory.BodyFont?PvPvEUIFactory.BodyFont:font;store=new PvPvEClassStore(catalog);Build();panel.SetActive(false);}
  void Update(){if(!IsOpen)return;if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){escapeFrame=Time.frameCount;if(picker.activeSelf)picker.SetActive(false);else Close();}}
  public void Open(){
   var lobby=GetComponent<PvPvELobbyView>();if(!lobby||!lobby.IsShowing)return;
   lobby.CloseCharacters();panel.SetActive(true);panel.transform.SetAsLastSibling();picker.SetActive(false);Refresh();
   EventSystem.current?.SetSelectedGameObject(classButtons[selected].gameObject);
  }
  public void Close(){if(!panel)return;if(picker)picker.SetActive(false);panel.SetActive(false);if(entryButton&&entryButton.IsActive())EventSystem.current?.SetSelectedGameObject(entryButton.gameObject);}
  public void SelectClass(int index){if(index<0||index>=5)return;selected=index;picker.SetActive(false);Refresh();}
  public bool Rename(string value){var c=Current;c.name=value.Trim();return Commit(c);}
  public bool ChooseWeapon(bool secondary,string id){
   var gun=catalog.Find(id);if(gun==null||(secondary?!catalog.AllowsSecondary(Current,gun):gun.secondary))return Fail("Arma incompatible con ese espacio.");
   var c=Current;if(secondary){c.secondary=id;c.secondaryAttachments=default;}else{c.primary=id;c.primaryAttachments=default;}
   secondaryFocus=secondary;return Commit(c);
  }
  public bool ChooseAttachment(bool secondary,WeaponAttachmentKind kind,string id){
   var c=Current;var choice=secondary?c.secondaryAttachments:c.primaryAttachments;choice.Set(kind,id);
   if(secondary)c.secondaryAttachments=choice;else c.primaryAttachments=choice;secondaryFocus=secondary;return Commit(c);
  }
  public bool ChooseUtility(bool healing,string id){var c=Current;if(healing)c.healing=id;else c.grenade=id;return Commit(c);}
  public bool ChoosePerk(int slot,string id){if(slot<0||slot>3)return false;var c=Current;PvPvELoadoutCatalog.SetPerk(c,slot,id);return Commit(c);}
  public bool ChooseWildcard(int slot,string id){if(slot<0||slot>1)return false;var c=Current;if(slot==0)c.wildcard1=id;else c.wildcard2=id;return Commit(c);}
  bool CanChoose(PvPvEClassDefinition value)=>catalog.ValidateEquipment(value,out _)&&(catalog.Pick10Cost(value)<=PvPvELoadoutCatalog.Pick10Limit||(PointsUsed>PvPvELoadoutCatalog.Pick10Limit&&catalog.Pick10Cost(value)<=PointsUsed));
  bool Commit(PvPvEClassDefinition value){
   if(!catalog.ValidateEquipment(value,out var error))return Fail(error);
   if(catalog.Pick10Cost(value)>PvPvELoadoutCatalog.Pick10Limit){
    if(PointsUsed<=PvPvELoadoutCatalog.Pick10Limit||catalog.Pick10Cost(value)>PointsUsed){catalog.Validate(value,out error);return Fail(error);}
    legacyDrafts[selected]=value.Copy();picker.SetActive(false);Refresh();return true;
   }
   if(!store.Save(selected,value,out error))return Fail(error);
   legacyDrafts.Remove(selected);picker.SetActive(false);Refresh();status.text="GUARDADO";status.color=accent;return true;
  }
  bool Fail(string error){status.text=error;status.color=new Color(1,.55f,.4f);return false;}
  void Refresh(){
   var c=Current;int points=catalog.Pick10Cost(c);
   pick10Counter.text=$"PICK 10   {points}/{PvPvELoadoutCatalog.Pick10Limit}";pick10Counter.color=points>10?new Color(1,.55f,.4f):points==10?new Color(1,.8f,.35f):accent;
   for(int i=0;i<pick10Pips.Length;i++)pick10Pips[i].color=i<points?pick10Counter.color:tile;
   nameInput.SetTextWithoutNotify(c.name);heading.text="CREAR UNA CLASE  /  "+(selected+1).ToString("00");
   for(int i=0;i<5;i++){var label=classButtons[i].GetComponentInChildren<TMP_Text>();label.text="Clase personalizada "+(i+1);classButtons[i].targetGraphic.color=i==selected?new Color(.29f,.21f,.10f):tile;}
   SetCard(primaryCard,"PRIMARIA",catalog.Find(c.primary).name+"\n"+AttachmentLabels(c.primaryAttachments),catalog.Find(c.primary).icon);
   SetCard(secondaryCard,"SECUNDARIA",catalog.Find(c.secondary).name+"\n"+AttachmentLabels(c.secondaryAttachments),catalog.Find(c.secondary).icon);
   var grenade=Array.Find(catalog.grenades,g=>g.id==c.grenade);var heal=Array.Find(catalog.healings,g=>g.id==c.healing);
   SetCard(grenadeCard,"GRANADA",(grenade==null?"NINGUNA":grenade.name+"\n"+catalog.GrenadeCapacity(c)+" UN."),grenade?.icon);SetCard(healingCard,"CURACIÓN",(heal==null?"NINGUNA":heal.name+"\n"+catalog.HealingCapacity(c)+" UN. · "+heal.healing+" PV / UN."),heal?.icon);
   for(int i=0;i<4;i++){var perk=catalog.perks.Find(PvPvELoadoutCatalog.GetPerk(c,i));bool extra=i>=2;perkCards[i].gameObject.SetActive(!extra||catalog.HasWildcard(c,i==2?PvPvEWildcard.Perk1Greed:PvPvEWildcard.Perk2Greed));SetCard(perkCards[i],i%2==0?"MOVILIDAD":"MANEJO",perk?.NameForPvPvE??"NINGUNO",perk?.IconForPvPvE);}
   for(int weapon=0;weapon<2;weapon++){var selection=weapon==0?c.primaryAttachments:c.secondaryAttachments;var selectedItems=AttachmentItems(selection);int limit=catalog.AttachmentLimit(c,weapon==1);for(int slot=0;slot<3;slot++){var card=attachmentCards[weapon,slot];card.gameObject.SetActive(slot<limit);var entry=slot<selectedItems.Length?catalog.attachments.Find(selection.Get(selectedItems[slot])):null;SetCard(card,"ACCESORIO "+(slot+1),entry?.displayName??"NINGUNO",entry?.icon);card.interactable=catalog.attachments.Eligible(weapon==0?c.primary:c.secondary);}}
   for(int i=0;i<2;i++){var wc=catalog.wildcards.Find(i==0?c.wildcard1:c.wildcard2);SetCard(wildcardCards[i],"WILDCARD "+(i+1),wc?.name??"NINGUNA",wc?.icon);}
   var gun=catalog.Find(secondaryFocus?c.secondary:c.primary);var choice=secondaryFocus?c.secondaryAttachments:c.primaryAttachments;

   previewTitle.text="CLASE PERSONALIZADA "+(selected+1)+"\n"+gun.name;
   preview.Show(gun,catalog.attachments,choice);

   DrawWeaponStats(stats,gun,choice,false);
   status.text=points>PvPvELoadoutCatalog.Pick10Limit?$"CLASE ANTERIOR: {points}/10. Quitá {points-10} punto(s) para guardar. Se conserva el original.":"Los cambios se guardan automáticamente.";status.color=points>10?new Color(1,.55f,.4f):new Color(.65f,.75f,.72f);
  }
  void Weapons(bool secondary){
   secondaryFocus=secondary;OpenPicker(secondary?"ELEGIR SECUNDARIA":"ELEGIR PRIMARIA",true);
   foreach(var gun in catalog.weapons.Where(w=>secondary?catalog.AllowsSecondary(Current,w):!w.secondary&&w.id!=Current.secondary)){
    var capture=gun;var candidate=Current;if(secondary){candidate.secondary=gun.id;candidate.secondaryAttachments=default;}else{candidate.primary=gun.id;candidate.primaryAttachments=default;}
    Option(gun.id,gun.name,gun.icon,()=>ChooseWeapon(secondary,capture.id),candidate);
   }
  }
  void Utilities(bool healing){OpenPicker(healing?"ELEGIR CURACIÓN":"ELEGIR GRANADA");var empty=Current;if(healing)empty.healing="";else empty.grenade="";Option("utility_none","NINGUNA",null,()=>ChooseUtility(healing,""),empty);foreach(var u in healing?catalog.healings:catalog.grenades){var capture=u;var candidate=Current;if(healing)candidate.healing=u.id;else candidate.grenade=u.id;Option(u.id,healing?catalog.HealingSummary(u):u.name,u.icon,()=>ChooseUtility(healing,capture.id),candidate);}}
  void Perks(int slot){
   bool mobility=slot%2==0;OpenPicker(mobility?"PERK 1 / MOVILIDAD":"PERK 2 / MANEJO");options.GetComponent<GridLayoutGroup>().cellSize=new Vector2(220,220);
   var empty=Current;PvPvELoadoutCatalog.SetPerk(empty,slot,"");Option("perk_none","NINGUNO",null,()=>ChoosePerk(slot,""),empty);
   foreach(var entry in catalog.perks.entries.Where(e=>e.CategoryForPvPvE==(mobility?PerkCategory.Mobility:PerkCategory.Handling))){var capture=entry;var candidate=Current;PvPvELoadoutCatalog.SetPerk(candidate,slot,entry.id);Option(entry.id,entry.NameForPvPvE+"\n"+entry.description,entry.IconForPvPvE,()=>ChoosePerk(slot,capture.id),candidate);var button=options.GetChild(options.childCount-1);button.Find("Category").GetComponent<TMP_Text>().text=mobility?"MOVILIDAD":"MANEJO";var label=button.Find("Name").GetComponent<TMP_Text>();label.fontSize=14;label.rectTransform.anchorMax=new(.96f,.49f);((RectTransform)button.Find("Icon")).anchorMin=new(.06f,.51f);}
  }
  void Wildcards(int slot){
   OpenPicker("WILDCARD "+(slot+1)+"  /  MÁXIMO DOS DISTINTAS");options.GetComponent<GridLayoutGroup>().cellSize=new Vector2(220,220);
   var empty=Current;if(slot==0)empty.wildcard1="";else empty.wildcard2="";Option("wildcard_none","NINGUNA",null,()=>ChooseWildcard(slot,""),empty);
   var c=Current;var other=catalog.wildcards.Find(slot==0?c.wildcard2:c.wildcard1);
   foreach(var entry in catalog.wildcards.entries){var capture=entry;var candidate=Current;if(slot==0)candidate.wildcard1=entry.id;else candidate.wildcard2=entry.id;Option(entry.id,entry.name+"\n"+entry.description,entry.icon,()=>ChooseWildcard(slot,capture.id),candidate);
    var b=options.GetChild(options.childCount-1).GetComponent<Button>();b.interactable&=other==null||other.kind!=entry.kind;
    var label=b.transform.Find("Name").GetComponent<TMP_Text>();label.fontSize=14;label.rectTransform.anchorMax=new Vector2(.96f,.58f);((RectTransform)b.transform.Find("Icon")).anchorMin=new Vector2(.06f,.6f);
   }
  }
  static WeaponAttachmentKind[] AttachmentItems(WeaponAttachmentChoice choice)=>Enum.GetValues(typeof(WeaponAttachmentKind)).Cast<WeaponAttachmentKind>().Where(k=>!string.IsNullOrEmpty(choice.Get(k))).ToArray();
  PvPvEClassDefinition SlotCandidate(bool secondary,int slot,string id){
   var c=Current;var choice=secondary?c.secondaryAttachments:c.primaryAttachments;var items=AttachmentItems(choice);
   if(slot<items.Length)choice.Set(items[slot],"");var entry=catalog.attachments.Find(id);
   if(entry!=null){if(!string.IsNullOrEmpty(choice.Get(entry.kind)))return null;choice.Set(entry.kind,id);}
   if(secondary)c.secondaryAttachments=choice;else c.primaryAttachments=choice;return c;
  }
  public bool ChooseAttachmentSlot(bool secondary,int slot,string id){var c=SlotCandidate(secondary,slot,id);return c!=null?Commit(c):Fail("Ese montaje ya está ocupado por otro accesorio.");}
  void Attachments(bool secondary,int slot){
   secondaryFocus=secondary;var c=Current;string id=secondary?c.secondary:c.primary;OpenPicker("ACCESORIO "+(slot+1)+" / "+catalog.Find(id).name,true);
   Option("none","NINGUNO",null,()=>ChooseAttachmentSlot(secondary,slot,""),SlotCandidate(secondary,slot,""));
   foreach(var entry in catalog.attachments.entries.Where(e=>catalog.attachments.Allows(id,e.kind,e.id))){var capture=entry;var candidate=SlotCandidate(secondary,slot,entry.id);Option(entry.id,entry.displayName,entry.icon,()=>ChooseAttachmentSlot(secondary,slot,capture.id),candidate);if(candidate==null)options.GetChild(options.childCount-1).GetComponent<Button>().interactable=false;}
  }
  void OpenPicker(string title,bool equipment=false){
   options.GetComponent<GridLayoutGroup>().cellSize=new Vector2(220,160);
   foreach(Transform child in options){child.gameObject.SetActive(false);Destroy(child.gameObject);}pickerTitle.text=title;pickerEquipment=equipment;ClearWeaponStats(pickerStats);picker.SetActive(true);pickerPreview.gameObject.SetActive(pickerEquipment);pickerDetailIcon.gameObject.SetActive(!pickerEquipment);pickerDetailIcon.sprite=null;pickerStats.text=title;ShowCandidate(Current);
   Canvas.ForceUpdateCanvases();var viewport=(RectTransform)options.parent;options.GetComponent<GridLayoutGroup>().constraintCount=Mathf.Clamp(Mathf.FloorToInt((viewport.rect.width-4)/232),1,6);
   viewport.GetComponent<ScrollRect>().verticalNormalizedPosition=1;
  }
  void Option(string id,string name,Sprite icon,Action action,PvPvEClassDefinition candidate=null){
   var card=Card("Choose_"+id,options,Vector2.zero,Vector2.one,"",name,icon);card.onClick.AddListener(()=>{action();if(candidate!=null&&pickerEquipment){picker.SetActive(true);pickerPreview.gameObject.SetActive(true);ShowCandidate(Current);card.Select();}});
   if(candidate!=null){card.interactable=CanChoose(candidate);card.transform.Find("Category").GetComponent<TMP_Text>().text=catalog.Find(id)?.Category??$"CLASE  {catalog.Pick10Cost(candidate)}/10";
    if(!card.interactable)card.transform.Find("Name").GetComponent<TMP_Text>().alpha=.45f;
   }
   if(candidate!=null){var events=card.gameObject.AddComponent<EventTrigger>();foreach(var type in new[]{EventTriggerType.PointerEnter,EventTriggerType.Select}){var entry=new EventTrigger.Entry{eventID=type};entry.callback.AddListener(_=>{ShowCandidate(candidate);if(!pickerEquipment){pickerDetailIcon.sprite=icon;pickerDetailIcon.enabled=icon;ClearWeaponStats(pickerStats);pickerStats.text=name;}});events.triggers.Add(entry);}}
   if(!pickerEquipment&&pickerDetailIcon.sprite==null&&icon){pickerDetailIcon.sprite=icon;pickerDetailIcon.enabled=true;pickerStats.text=name;}
   if(name.Contains("\n"))ExpandHealingLabel(card);
   var layout=card.gameObject.AddComponent<LayoutElement>();layout.preferredWidth=220;layout.preferredHeight=160;
  }
  string AttachmentLabels(WeaponAttachmentChoice choice){var names=Enum.GetValues(typeof(WeaponAttachmentKind)).Cast<WeaponAttachmentKind>().Select(k=>catalog.attachments.Find(choice.Get(k))?.displayName).Where(n=>!string.IsNullOrEmpty(n));return names.Any()?string.Join(" · ",names):"SIN ACCESORIOS";}
  void ShowCandidate(PvPvEClassDefinition candidate){
   if(!pickerEquipment)return;
   var gun=catalog.Find(secondaryFocus?candidate.secondary:candidate.primary);if(gun==null)return;
   pickerPreview.Show(gun,catalog.attachments,secondaryFocus?candidate.secondaryAttachments:candidate.primaryAttachments);
   DrawWeaponStats(pickerStats,gun,secondaryFocus?candidate.secondaryAttachments:candidate.primaryAttachments,true);

  }
  void ClearWeaponStats(TMP_Text target){var bars=target.transform.Find("WeaponStatBars");if(bars)bars.gameObject.SetActive(false);target.enabled=true;}
  void DrawWeaponStats(TMP_Text target,PvPvELoadoutCatalog.Gun gun,WeaponAttachmentChoice choice,bool vertical){
   target.enabled=vertical;target.text=vertical?gun.name.ToUpperInvariant():"";
   var group=target.transform.Find("WeaponStatBars") as RectTransform;
   if(!group)group=Rect("WeaponStatBars",target.transform,Vector2.zero,new(1,vertical?.82f:1));group.gameObject.SetActive(true);
   float damage=1,spread=1,recoil=1;
   foreach(WeaponAttachmentKind k in Enum.GetValues(typeof(WeaponAttachmentKind))){var a=catalog.attachments.Find(choice.Get(k));if(a!=null){damage*=a.damage;spread*=a.spread;recoil*=a.recoil;}}
   float reach=catalog.attachments.Modifier(choice,a=>a.range);recoil*=catalog.attachments.Modifier(choice,a=>a.verticalRecoil);
   string[] names={"DAÑO","ALCANCE","CADENCIA","CARGADOR","DISPERSIÓN REL.","RETROCESO REL."};
   float[] values={gun.damage*damage,gun.range*reach,60/Mathf.Max(.01f,gun.fireInterval),gun.magazine,spread*100,recoil*100};
   float[] maximum={catalog.weapons.Max(g=>g.damage)*1.25f,catalog.weapons.Max(g=>g.range),catalog.weapons.Max(g=>60/Mathf.Max(.01f,g.fireInterval)),catalog.weapons.Max(g=>g.magazine),150,150};
   string[] units={""," m","/min","","%","%"};
   for(int i=0;i<names.Length;i++){
    var row=group.Find("Stat"+i) as RectTransform;float x=vertical?0:(i%2)*.51f;float top=1-(vertical?i*.165f:(i/2)*.333f);
    if(!row){row=Rect("Stat"+i,group,new(x,top-(vertical?.16f:.32f)),new(x+(vertical?1:.48f),top));
     var label=Text("Label",row,"",new(0,.32f),new(1,1),vertical?15:12,Color.white);
     var track=Rect("Track",row,new(0,.08f),new(1,.24f));track.gameObject.AddComponent<Image>().color=new Color(.17f,.2f,.2f);
     Rect("Fill",track,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>().color=new Color(.2f,.86f,.43f);
    }
    row.Find("Label").GetComponent<TMP_Text>().text=names[i]+"  "+values[i].ToString("0.#")+units[i];
    var fill=(RectTransform)row.Find("Track/Fill");fill.anchorMax=new(Mathf.Clamp01(values[i]/Mathf.Max(.01f,maximum[i])),1);
   }
  }
  static string KindName(WeaponAttachmentKind kind)=>kind switch{WeaponAttachmentKind.Scope=>"MIRA",WeaponAttachmentKind.Support=>"SOPORTE",WeaponAttachmentKind.Utility=>"LÁSER / LINTERNA",_=>"BOCA"};
  void Build(){
   var root=Rect("CreateAClass",container,Vector2.zero,Vector2.one);panel=root.gameObject;root.gameObject.AddComponent<Image>().color=new Color(.012f,.022f,.025f,1f);
   heading=Text("Title",root,"CREAR UNA CLASE",new(.035f,.9f),new(.58f,.98f),31,Color.white);
   pick10Counter=Text("Pick10Counter",root,"PICK 10",new(.61f,.93f),new(.82f,.985f),24,accent);
   pick10Pips=new Image[10];for(int i=0;i<10;i++){pick10Pips[i]=Rect("Pick10Point_"+i,root,new(.61f+i*.021f,.912f),new(.627f+i*.021f,.922f)).gameObject.AddComponent<Image>();pick10Pips[i].raycastTarget=false;}
   var back=Button("CloseClasses",root,"VOLVER",new(.84f,.91f),new(.97f,.97f));back.onClick.AddListener(Close);
   Text("ClassesTitle",root,"CLASES PERSONALIZADAS",new(.025f,.8f),new(.18f,.87f),16,accent);
   classButtons=new Button[5];for(int i=0;i<5;i++){int index=i;float top=.78f-i*.105f;classButtons[i]=Button("Class_"+i,root,"CLASE "+(i+1),new(.025f,top-.085f),new(.18f,top));classButtons[i].onClick.AddListener(()=>SelectClass(index));}
   var inputRect=Rect("ClassName",root,new(.21f,.83f),new(.61f,.88f));inputRect.gameObject.AddComponent<Image>().color=tile;
   nameInput=inputRect.gameObject.AddComponent<TMP_InputField>();nameInput.characterLimit=32;
   var nameText=Text("Value",inputRect,"",new(.035f,.05f),new(.965f,.95f),23,Color.white);nameInput.textViewport=inputRect;nameInput.textComponent=(TextMeshProUGUI)nameText;nameInput.fontAsset=font;nameInput.onEndEdit.AddListener(value=>Rename(value));
   status=Text("SaveStatus",root,"",new(.64f,.83f),new(.97f,.88f),17,accent);
   primaryCard=Card("Primary",root,new(.21f,.61f),new(.61f,.81f),"PRIMARIA","",null);primaryCard.onClick.AddListener(()=>Weapons(false));
   secondaryCard=Card("Secondary",root,new(.21f,.28f),new(.61f,.48f),"SECUNDARIA","",null);secondaryCard.onClick.AddListener(()=>Weapons(true));
   attachmentCards=new Button[2,3];for(int weapon=0;weapon<2;weapon++)for(int slot=0;slot<3;slot++){int w=weapon,index=slot;float left=.21f+slot*.135f;float bottom=weapon==0?.50f:.17f;attachmentCards[weapon,slot]=Card("Attachment_"+weapon+"_"+slot,root,new(left,bottom),new(left+.125f,bottom+.10f),"ACCESORIO "+(slot+1),"NINGUNO",null);attachmentCards[weapon,slot].onClick.AddListener(()=>Attachments(w==1,index));}
   previewTitle=Text("WeaponTitle",root,"",new(.64f,.76f),new(.97f,.81f),20,Color.white);
   var frame=Rect("PreviewFrame",root,new(.64f,.64f),new(.97f,.76f));var image=Rect("WeaponPreview",frame,Vector2.zero,Vector2.one).gameObject.AddComponent<RawImage>();image.raycastTarget=false;var imageAspect=image.gameObject.AddComponent<AspectRatioFitter>();imageAspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;imageAspect.aspectRatio=1000f/600;preview=image.gameObject.AddComponent<PvPvEWeaponPreview>();
   stats=Text("Stats",root,"",new(.64f,.55f),new(.97f,.64f),16,PvPvEUIFactory.Muted);
   perkCards=new Button[4];for(int i=0;i<4;i++){int slot=i;float left=i<2?.64f:.815f;float bottom=i%2==0?.40f:.27f;perkCards[i]=Card("Perk_"+i,root,new(left,bottom),new(left+.155f,bottom+.115f),i%2==0?"MOVILIDAD":"MANEJO","NINGUNO",null);perkCards[i].onClick.AddListener(()=>Perks(slot));}
   grenadeCard=Card("Grenade",root,new(.64f,.145f),new(.97f,.255f),"GRANADA","",null);grenadeCard.onClick.AddListener(()=>Utilities(false));
   healingCard=Card("Healing",root,new(.64f,.025f),new(.97f,.135f),"CURACIÓN","",null);healingCard.onClick.AddListener(()=>Utilities(true));ExpandHealingLabel(healingCard);ExpandHealingLabel(grenadeCard);
   wildcardCards=new Button[2];for(int i=0;i<2;i++){int slot=i;float left=.21f+i*.205f;wildcardCards[i]=Card("Wildcard_"+i,root,new(left,.025f),new(left+.195f,.15f),"WILDCARD "+(i+1),"NINGUNA",null);wildcardCards[i].onClick.AddListener(()=>Wildcards(slot));var label=wildcardCards[i].transform.Find("Name").GetComponent<TMP_Text>();label.fontSize=15;label.rectTransform.anchorMax=new(.67f,.65f);PvPvEUIFactory.Place((RectTransform)wildcardCards[i].transform.Find("Icon"),new(.70f,.18f),new(.97f,.72f));}
   var reset=Button("ResetClass",root,"RESTABLECER ESTA CLASE",new(.025f,.025f),new(.18f,.10f));reset.onClick.AddListener(()=>{store.Reset(selected);legacyDrafts.Remove(selected);Refresh();});
   var pick=Rect("ItemPicker",root,new(.2f,.005f),new(.98f,.815f));picker=pick.gameObject;pick.gameObject.AddComponent<Image>().color=new Color(.018f,.035f,.04f,1f);
   pickerTitle=Text("PickerTitle",pick,"",new(.025f,.87f),new(.8f,.99f),26,Color.white);
   Button("ClosePicker",pick,"VOLVER",new(.82f,.89f),new(.97f,.97f)).onClick.AddListener(()=>picker.SetActive(false));
   var hero=Rect("PickerModelFrame",pick,new(.34f,.44f),new(.98f,.87f));var heroImage=Rect("PickerModel",hero,Vector2.zero,Vector2.one).gameObject.AddComponent<RawImage>();heroImage.raycastTarget=false;
   var heroFit=heroImage.gameObject.AddComponent<AspectRatioFitter>();heroFit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;heroFit.aspectRatio=1000f/600;pickerPreview=heroImage.gameObject.AddComponent<PvPvEWeaponPreview>();
   pickerDetailIcon=Rect("PickerPerkIcon",pick,new(.43f,.48f),new(.87f,.85f)).gameObject.AddComponent<Image>();pickerDetailIcon.preserveAspect=true;pickerDetailIcon.raycastTarget=false;
   pickerStats=Text("SelectionDetails",pick,"",new(.03f,.46f),new(.33f,.83f),23,Color.white);
   var viewport=Rect("Viewport",pick,new(.02f,.025f),new(.965f,.42f));viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
   options=Rect("Cards",viewport,Vector2.up,Vector2.one);options.pivot=new Vector2(.5f,1);options.sizeDelta=new Vector2(0,0);
   var grid=options.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(220,160);grid.spacing=new Vector2(12,12);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=4;grid.padding=new RectOffset(8,8,8,8);
   var fit=options.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
   var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=options;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
   PvPvEUIFactory.Polish(root,true);picker.SetActive(false);
  }
  Button Card(string name,Transform parent,Vector2 min,Vector2 max,string kind,string value,Sprite icon){
   var button=Button(name,parent,"",min,max);Text("Category",button.transform,kind,new(.04f,.77f),new(.96f,.97f),14,accent);
   var image=Rect("Icon",button.transform,new(.06f,.32f),new(.94f,.77f)).gameObject.AddComponent<Image>();image.preserveAspect=true;image.raycastTarget=false;
   Text("Name",button.transform,value,new(.04f,.025f),new(.96f,.29f),18,Color.white);SetCard(button,kind,value,icon);return button;
  }
  void SetCard(Button button,string kind,string value,Sprite icon){
   button.transform.Find("Category").GetComponent<TMP_Text>().text=kind;button.transform.Find("Name").GetComponent<TMP_Text>().text=value;
   var image=button.transform.Find("Icon").GetComponent<Image>();image.sprite=icon;image.enabled=icon;if(kind.StartsWith("WILDCARD"))image.material=Resources.Load<Material>("PvPvE/WhiteIcon");
  }
  static void ExpandHealingLabel(Button card){var label=card.transform.Find("Name").GetComponent<TMP_Text>();label.fontSize=16;label.rectTransform.anchorMax=new Vector2(.96f,.4f);((RectTransform)card.transform.Find("Icon")).anchorMin=new Vector2(.06f,.41f);}
  RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
  TMP_Text Text(string name,Transform parent,string value,Vector2 min,Vector2 max,int size,Color color){var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.richText=false;t.raycastTarget=false;t.enableAutoSizing=true;t.fontSizeMax=size;t.fontSizeMin=Mathf.Max(12,size*.78f);t.overflowMode=TextOverflowModes.Ellipsis;return t;}
  Button Button(string name,Transform parent,string value,Vector2 min,Vector2 max){var r=Rect(name,parent,min,max);var image=r.gameObject.AddComponent<Image>();image.color=tile;var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;PvPvEUIFactory.StyleButton(b);if(!string.IsNullOrEmpty(value)){var t=Text("Label",r,value,new(.04f,0),new(.96f,1),19,Color.white);t.alignment=TextAlignmentOptions.Center;}return b;}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  public void UseValidationProfile(string profile){legacyDrafts.Clear();store=new PvPvEClassStore(catalog,profile);if(IsOpen)Refresh();}
#endif
 }
}
