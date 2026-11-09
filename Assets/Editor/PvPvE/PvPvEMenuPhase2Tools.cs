#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DeadQuarantine.PvPvE.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PvPvEMenuPhase2Tools
{
    public const string ScenePath="Assets/Scenes/PantallasUI.unity";
    const string EntryPath="Canvas/elegirModoPanel/TODO/player2/Iniciar_juego (2)";
    const string RootName="PvPvEModeNavigation";
    static readonly Color Accent=new Color32(0,255,102,255);
    static TMP_FontAsset font;
    static string Output=>Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE2_OUTPUT")??Path.GetFullPath("Temp/PvPvEPhase2");

    public static void Install()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var canvas=scene.GetRootGameObjects().First(g=>g.name=="Canvas" && g.transform.Find("menuPrincipalPanel")).GetComponent<Canvas>();
        var entry=canvas.transform.Find(EntryPath.Substring("Canvas/".Length)).GetComponent<Button>();
        var loader=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SceneLoader>(true)).First();
        var old=canvas.transform.Find(RootName);
        if(!old && (entry.onClick.GetPersistentEventCount()!=1 || entry.onClick.GetPersistentMethodName(0)!="LoadNetworkLobby"))
            throw new InvalidOperationException("The multiplayer entry changed; preserve its listeners before adapting this installer.");
        if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
        font=entry.GetComponentInChildren<TMP_Text>(true).font;
        var root=Rect(RootName,canvas.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        var controller=root.gameObject.AddComponent<PvPvEModeMenu>();
        var modes=Panel("MultiplayerModesPanel",root);
        Label("Eyebrow",modes,"DEAD QUARANTINE  /  MULTIJUGADOR",new Vector2(.07f,.85f),new Vector2(.92f,.91f),23,Accent);
        Label("Title",modes,"ELEGÍ TU MODALIDAD",new Vector2(.07f,.75f),new Vector2(.92f,.85f),48,Color.white);
        Label("Subtitle",modes,"Jugá en equipo o enfrentate a otros supervivientes.",new Vector2(.07f,.69f),new Vector2(.9f,.75f),24,Dim());
        var coop=Card("CooperativeButton",modes,"COOPERATIVO","Historia · Ronda 5 · Supervivencia",.47f,.64f);
        var pvpve=Card("PvPvEButton",modes,"PvPvE TEAM DEATHMATCH","Dos equipos. Zombies hostiles. Hasta 5 contra 5.",.26f,.43f);
        var back=Action("BackButton",modes,"VOLVER",new Vector2(.07f,.09f),new Vector2(.27f,.17f));
        Label("SideTitle",modes,"SOBREVIVIR ES SOLO EL PRINCIPIO.",new Vector2(.67f,.48f),new Vector2(.93f,.66f),30,Accent);
        Label("SideDescription",modes,"Conservá a tus aliados cerca.\n\nElegí cómo enfrentar la cuarentena.",new Vector2(.67f,.27f),new Vector2(.93f,.47f),24,Dim());

        var page=Panel("PvPvEEntryPanel",root);
        Label("Eyebrow",page,"MULTIJUGADOR  /  PvPvE",new Vector2(.07f,.85f),new Vector2(.93f,.91f),23,Accent);
        Label("Title",page,"TEAM DEATHMATCH",new Vector2(.07f,.74f),new Vector2(.93f,.85f),52,Color.white);
        Label("Description",page,"Enfrentá al equipo rival mientras los zombies acechan a todos.",new Vector2(.07f,.65f),new Vector2(.91f,.73f),26,Dim());
        Rule(page,"Teams","HASTA 5 VS 5","Equipos equilibrados",.07f,.32f);
        Rule(page,"Kills","50 BAJAS PvP","Los zombies no suman al objetivo",.37f,.64f);
        Rule(page,"Time","10 MINUTOS","Empate: muerte súbita",.69f,.94f);
        var search=Action("SearchButton",page,"BUSCAR PARTIDA",new Vector2(.07f,.22f),new Vector2(.43f,.32f));search.interactable=false;
        Label("Availability",page,"PRÓXIMAMENTE",new Vector2(.07f,.16f),new Vector2(.43f,.21f),20,Accent);
        Label("SearchStatus",page,"La búsqueda de partidas todavía no está disponible.",new Vector2(.46f,.2f),new Vector2(.94f,.32f),23,Dim());
        var pvpveBack=Action("BackButton",page,"VOLVER",new Vector2(.07f,.05f),new Vector2(.27f,.13f));
        var so=new SerializedObject(controller);
        Assign(so,"previousPanel",canvas.transform.Find("elegirModoPanel").gameObject);
        Assign(so,"modeSelectionPanel",modes.gameObject);Assign(so,"pvpvePanel",page.gameObject);
        Assign(so,"cooperativeButton",coop);Assign(so,"pvpveBackButton",pvpveBack);Assign(so,"previousMultiplayerButton",entry);so.ApplyModifiedPropertiesWithoutUndo();
        while(entry.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(entry.onClick,0);
        UnityEventTools.AddPersistentListener(entry.onClick,controller.OpenModes);
        UnityEventTools.AddPersistentListener(coop.onClick,loader.LoadNetworkLobby);
        UnityEventTools.AddPersistentListener(pvpve.onClick,controller.OpenPvPvE);
        UnityEventTools.AddPersistentListener(back.onClick,controller.BackToPrevious);
        UnityEventTools.AddPersistentListener(pvpveBack.onClick,controller.BackToModes);
        modes.gameObject.SetActive(false);page.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Menu scene did not save");
        ValidateScene();
    }
    public static void ValidateScene()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var controllers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PvPvEModeMenu>(true)).ToArray();
        if(controllers.Length!=1)throw new Exception("Expected exactly one mode menu");
        var c=controllers[0];var so=new SerializedObject(c);
        foreach(string field in new[]{"previousPanel","modeSelectionPanel","pvpvePanel","cooperativeButton","pvpveBackButton","previousMultiplayerButton"})
            if(!so.FindProperty(field).objectReferenceValue)throw new Exception("Unassigned menu reference: "+field);
        var entry=(Button)so.FindProperty("previousMultiplayerButton").objectReferenceValue;
        if(entry.onClick.GetPersistentEventCount()!=1 || entry.onClick.GetPersistentMethodName(0)!="OpenModes")throw new Exception("Multiplayer entry is not wired once to OpenModes");
        foreach(var button in c.GetComponentsInChildren<Button>(true)) {
            if(button.name=="SearchButton") {if(button.interactable||button.onClick.GetPersistentEventCount()!=0)throw new Exception("Search must remain unavailable until real networking is implemented");continue;}
            if(button.onClick.GetPersistentEventCount()!=1||!button.onClick.GetPersistentTarget(0))throw new Exception("Missing/duplicate callback: "+button.name);
        }
        var coop=(Button)so.FindProperty("cooperativeButton").objectReferenceValue;
        if(!(coop.onClick.GetPersistentTarget(0) is SceneLoader) || coop.onClick.GetPersistentMethodName(0)!="LoadNetworkLobby")throw new Exception("Existing lobby navigation was replaced");
        if(c.ModeSelectionPanel.activeSelf||c.PvPvEPanel.activeSelf)throw new Exception("New panels must start closed");
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"References.txt"),"PASS: one menu; assigned Inspector references; one persistent callback per navigation button; original SceneLoader route preserved; real search disabled; both new panels initially closed.");
    }
    static void Assign(SerializedObject so,string field,UnityEngine.Object value)=>so.FindProperty(field).objectReferenceValue=value;
    static Color Dim()=>new Color32(165,187,190,255);
    static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=offsetMin;r.offsetMax=offsetMax;return r;
    }
    static RectTransform Panel(string name,Transform root)
    {
        var r=Rect(name,root,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);r.gameObject.AddComponent<Image>().color=new Color(.012f,.025f,.026f,.94f);return r;
    }
    static TMP_Text Label(string name,Transform root,string value,Vector2 min,Vector2 max,int size,Color color)
    {
        var rect=Rect(name,root,min,max,Vector2.zero,Vector2.zero);var t=rect.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.MidlineLeft;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;return t;
    }
    static Button Action(string name,Transform root,string text,Vector2 min,Vector2 max)
    {
        var r=Rect(name,root,min,max,Vector2.zero,Vector2.zero);var image=r.gameObject.AddComponent<Image>();image.color=Color.white;
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;
        var colors=b.colors;colors.normalColor=new Color(.025f,.10f,.08f,1);colors.highlightedColor=new Color(.04f,.23f,.12f,1);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.02f,.38f,.14f,1);colors.disabledColor=new Color(.07f,.09f,.085f,1);b.colors=colors;
        var label=Label("Label",r,text,new Vector2(.06f,.08f),new Vector2(.94f,.92f),26,Accent);
        return b;
    }
    static Button Card(string name,Transform root,string title,string description,float low,float high)
    {
        var b=Action(name,root,"",new Vector2(.07f,low),new Vector2(.61f,high));
        UnityEngine.Object.DestroyImmediate(b.transform.Find("Label").gameObject);
        var strip=Rect("Accent",b.transform,Vector2.zero,new Vector2(.006f,1),Vector2.zero,Vector2.zero);strip.gameObject.AddComponent<Image>().color=Accent;
        Label("Title",b.transform,title,new Vector2(.05f,.47f),new Vector2(.95f,.91f),32,Color.white);
        Label("Description",b.transform,description,new Vector2(.05f,.12f),new Vector2(.95f,.49f),22,Dim());return b;
    }
    static void Rule(Transform page,string name,string title,string description,float left,float right)
    {
        var r=Rect(name,page,new Vector2(left,.42f),new Vector2(right,.59f),Vector2.zero,Vector2.zero);r.gameObject.AddComponent<Image>().color=new Color(.025f,.09f,.07f,1);
        Label("Title",r,title,new Vector2(.07f,.45f),new Vector2(.94f,.9f),32,Accent);
        Label("Description",r,description,new Vector2(.07f,.08f),new Vector2(.94f,.48f),20,Dim());
    }
}
#endif
