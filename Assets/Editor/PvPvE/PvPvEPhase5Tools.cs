#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DeadQuarantine.PvPvE.Networking;
using DeadQuarantine.PvPvE.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class PvPvEPhase5Tools
{
 public static void Install()
 {
  var config=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>(PvPvEPhase3Tools.SettingsPath);
  config.ProtocolVersion=5001;config.MapVoteSeconds=15;config.PostVoteSeconds=10;
  config.Maps=new[]{
   Map(ConfiguracionPartidaSeleccionada.Escenario.Laboratorio,"LABORATORIO","Laboratorio_juego.jpg"),
   Map(ConfiguracionPartidaSeleccionada.Escenario.Ciudad,"CIUDAD","Ciudad_juego.jpg"),
   Map(ConfiguracionPartidaSeleccionada.Escenario.Cabana,"MANSIÓN DEL BOSQUE","Assets/Easter/fondoMansion.jpg")};
  if(!config.HasValidMaps)throw new Exception("Missing map IDs/previews");EditorUtility.SetDirty(config);
  string path=AssetDatabase.GetAssetPath(config.RosterPrefab);var root=PrefabUtility.LoadPrefabContents(path);
  try{var vote=root.GetComponent<PvPvEMapVoting>()??root.AddComponent<PvPvEMapVoting>();var so=new SerializedObject(vote);so.FindProperty("settings").objectReferenceValue=config;so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
  var scene=EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
  var menu=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PvPvEModeMenu>(true)).Single();
  var panel=menu.transform.Find("PvPvELobby/LobbyPanel");var old=panel.Find("MapVotingPanel");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
  var font=panel.Find("Title").GetComponent<TMP_Text>().font;
  var area=Rect("MapVotingPanel",panel,Vector2.zero,Vector2.one);var view=area.gameObject.AddComponent<PvPvEMapVotingView>();
  var headline=Text("VoteStatus",area,"ESPERANDO UN JUGADOR POR EQUIPO",new Vector2(.27f,.72f),new Vector2(.73f,.81f),font,22);
  var hint=Text("Hint",area,"La votación comienza al completar ambos equipos.",new Vector2(.35f,.12f),new Vector2(.66f,.165f),font,16);
  var cards=new Button[3];var counts=new TMP_Text[3];
  for(int i=0;i<3;i++){
   float left=.27f+i*.157f;var r=Rect("Map"+i,area,new Vector2(left,.17f),new Vector2(left+.146f,.33f));
   var image=r.gameObject.AddComponent<Image>();image.color=new Color(.015f,.04f,.035f,.96f);cards[i]=r.gameObject.AddComponent<Button>();cards[i].targetGraphic=image;
   Text("Name",r,config.Maps[i].Name,new Vector2(.03f,.73f),new Vector2(.97f,.98f),font,19);
   var frame=Rect("PreviewFrame",r,new Vector2(.03f,.22f),new Vector2(.97f,.73f));
   var preview=Rect("Preview",frame,Vector2.zero,Vector2.one);var raw=preview.gameObject.AddComponent<RawImage>();raw.texture=config.Maps[i].Preview;raw.raycastTarget=false;var fit=preview.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=(float)raw.texture.width/raw.texture.height;
   counts[i]=Text("Votes",r,"0 VOTOS",new Vector2(.03f,.015f),new Vector2(.97f,.22f),font,17);
  }
  UnityEventTools.AddPersistentListener(cards[0].onClick,view.Vote0);UnityEventTools.AddPersistentListener(cards[1].onClick,view.Vote1);UnityEventTools.AddPersistentListener(cards[2].onClick,view.Vote2);
  var settings=new SerializedObject(view);Set(settings,"matchmaking",menu.GetComponent<PvPvEMatchmaking>());Set(settings,"headline",headline);Set(settings,"hint",hint);
  var buttons=settings.FindProperty("cards");var labels=settings.FindProperty("counts");buttons.arraySize=labels.arraySize=3;for(int i=0;i<3;i++){buttons.GetArrayElementAtIndex(i).objectReferenceValue=cards[i];labels.GetArrayElementAtIndex(i).objectReferenceValue=counts[i];}settings.ApplyModifiedPropertiesWithoutUndo();
  EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save voting UI");AssetDatabase.SaveAssets();Validate();
 }
 static PvPvEMapOption Map(ConfiguracionPartidaSeleccionada.Escenario id,string name,string file)=>new(){Id=id,Name=name,Preview=AssetDatabase.LoadAssetAtPath<Texture2D>(file.StartsWith("Assets/")?file:"Assets/Figma/ImageFills/"+file)};
 public static void Validate()
 {
  var config=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>(PvPvEPhase3Tools.SettingsPath);if(!config.HasValidMaps || !config.RosterPrefab.GetComponent<PvPvEMapVoting>())throw new Exception("Voting configuration missing");
  var scene=EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PvPvEMapVotingView>(true)).Single();var so=new SerializedObject(view);
  foreach(string field in new[]{"matchmaking","headline","hint"})if(!so.FindProperty(field).objectReferenceValue)throw new Exception("Missing voting reference: "+field);
  foreach(var button in view.GetComponentsInChildren<Button>(true))if(button.onClick.GetPersistentEventCount()!=1 || button.onClick.GetPersistentTarget(0)!=view)throw new Exception("Missing voting callback");
  string output=Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE5_OUTPUT")??"Documentation/PvPvE/Verificacion/Fase5";Directory.CreateDirectory(output);File.WriteAllText(output+"/References.txt","PASS: three existing map IDs and previews, voting NetworkBehaviour on roster, serialized UI references and callbacks.");
 }
 static void Set(SerializedObject so,string name,UnityEngine.Object obj)=>so.FindProperty(name).objectReferenceValue=obj;
 static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
 static TMP_Text Text(string name,Transform parent,string value,Vector2 min,Vector2 max,TMP_FontAsset font,float size){var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.color=new Color(.55f,.95f,.75f);t.text=value;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;return t;}
}
#endif
