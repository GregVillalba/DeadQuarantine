using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace DeadQuarantine.PvPvE {
 public static class PvPvEUIFactory {
  public static readonly Color PanelColor=new(.035f,.044f,.052f,.97f),Accent=new(.96f,.68f,.28f);
  static TMP_FontAsset bodyFont,headingFont;
  public static TMP_FontAsset BodyFont=>bodyFont?bodyFont:(bodyFont=Resources.Load<TMP_FontAsset>("PvPvE/BodyFont"));
  public static TMP_FontAsset HeadingFont=>headingFont?headingFont:(headingFont=Resources.Load<TMP_FontAsset>("PvPvE/HeadingFont"));
  public static readonly Color Surface=new(.055f,.069f,.08f,.98f),Muted=new(.66f,.72f,.75f);
  public static void Place(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
  public static void StyleButton(Button b){
   if(!b)return;var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new(1.6f,1.6f,1.6f);colors.selectedColor=new(2.4f,1.7f,.9f);colors.pressedColor=new(3f,2.1f,1f);colors.disabledColor=new(.55f,.55f,.55f,.65f);colors.fadeDuration=.12f;b.colors=colors;
   if(b.targetGraphic)b.targetGraphic.color=Surface;
   var outline=b.GetComponent<Outline>();if(!outline)outline=b.gameObject.AddComponent<Outline>();outline.effectColor=new(.43f,.49f,.51f,.23f);outline.effectDistance=new(1,-1);outline.useGraphicAlpha=false;
  }
  public static void Polish(Transform root,bool page=false){
   foreach(var button in root.GetComponentsInChildren<Button>(true))StyleButton(button);
   foreach(var text in root.GetComponentsInChildren<TMP_Text>(true)){
    text.raycastTarget=false;text.characterSpacing=.3f;
    bool title=text.name is "Title" or "PickerTitle" or "ClassesTitle" or "WeaponTitle" or "Eyebrow";
    if(BodyFont)text.font=title&&HeadingFont?HeadingFont:BodyFont;
    if(text.name=="Title")text.fontSize=Mathf.Max(38,text.fontSize);if(text.name=="PickerTitle")text.fontSize=32;
    if(text.GetComponentInParent<Button>(true)&&text.name!="Category")text.color=Color.white;
    if(text.name is "Title" or "PickerTitle" or "ClassesTitle" or "WeaponTitle")text.fontStyle=FontStyles.Bold;
    text.enableAutoSizing=true;text.fontSizeMax=text.fontSize;text.fontSizeMin=Mathf.Max(12,text.fontSize*.78f);text.overflowMode=TextOverflowModes.Ellipsis;
   }
   if(!page||root.Find("DQ_HeaderRule"))return;
   var line=new GameObject("DQ_HeaderRule",typeof(RectTransform),typeof(Image));line.transform.SetParent(root,false);var rect=(RectTransform)line.transform;Place(rect,new(.025f,.892f),new(.975f,.895f));line.GetComponent<Image>().color=Accent;line.GetComponent<Image>().raycastTarget=false;
  }
  public static void Line(Transform parent,float x,float y,float width,Color color){var line=Rect(parent,"Rule",x,y,width,2).gameObject.AddComponent<Image>();line.color=color;line.raycastTarget=false;}
        public static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(width, height); return rt;
        }
        public static RectTransform Panel(Transform parent, string name, float x, float y, float width, float height, float alpha = .92f)
        {
            var rt = Rect(parent, name, x, y, width, height);
            var image = rt.gameObject.AddComponent<Image>(); var color = PanelColor; color.a = alpha; image.color = color;
            image.raycastTarget=false;return rt;
        }
        public static TextMeshProUGUI Text(Transform parent, string name, string value, float x, float y, float width, float height, TMP_FontAsset font, int size = 23)
        {
            var rt = Rect(parent, name, x, y, width, height);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>(); text.font = BodyFont ? BodyFont : font ? font : TMP_Settings.defaultFontAsset;
            text.text = value; text.fontSize = size; text.color = new Color(.86f, .9f, .91f); text.richText = false;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false;
            return text;
        }

  public static Canvas Canvas(GameObject owner,string name,int order=500){var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(owner.transform,false);var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=order;canvas.pixelPerfect=true;var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new(1920,1080);scaler.matchWidthOrHeight=.5f;return canvas;}
 }
}