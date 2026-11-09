#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DeadQuarantine.PvPvE.Networking;
using DeadQuarantine.PvPvE.UI;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class PvPvEPhase4Tools
{
 public const string StagePath="Assets/PvPvE/Prefabs/PvPvELobbyStage.prefab";
 public const string LobbyPath="Assets/PvPvE/Prefabs/PvPvELobbyUI.prefab";
 static TMP_FontAsset font;
 public static void Install()
 {
  PrepareStage();
  var scene=EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
  var menu=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PvPvEModeMenu>(true)).Single();
  font=menu.PvPvEPanel.GetComponentInChildren<TMP_Text>(true).font;
  var old=menu.transform.Find("PvPvELobby");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
  var oldView=menu.GetComponent<PvPvELobbyView>();if(oldView)UnityEngine.Object.DestroyImmediate(oldView);
  var view=menu.gameObject.AddComponent<PvPvELobbyView>();
  var container=Rect("PvPvELobby",menu.transform,Vector2.zero,Vector2.one);
  var panel=Rect("LobbyPanel",container,Vector2.zero,Vector2.one);
  Label("Title",panel,"PvPvE  /  TEAM DEATHMATCH",new Vector2(.04f,.88f),new Vector2(.96f,.96f),35,Color.white);
  var status=Label("Status",panel,"",new Vector2(.04f,.82f),new Vector2(.96f,.88f),21,new Color(.4f,.9f,.7f));
  var alpha=Team("Alpha",panel,.03f,.26f);var bravo=Team("Bravo",panel,.74f,.97f);
  var characters=Button("CharacterButton",panel,"ELEGIR PERSONAJE",new Vector2(.04f,.05f),new Vector2(.34f,.14f));
  var leave=Button("LeaveButton",panel,"SALIR DE LA SALA",new Vector2(.69f,.05f),new Vector2(.96f,.14f));
  var popup=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CharacterSelect.prefab"),container);
  popup.name="CharacterPopup";popup.SetActive(false);
  foreach(var canvas in popup.GetComponentsInChildren<Canvas>(true)){canvas.overrideSorting=true;canvas.sortingOrder=100;}
  var screen=popup.GetComponentInChildren<CharacterSelectScreen>(true);var cs=new SerializedObject(screen);
  cs.FindProperty("sceneOnConfirm").stringValue="";cs.FindProperty("toggleStageRendering").boolValue=true;
  foreach(string field in new[]{"onConfirm","onBack"})cs.FindProperty(field).FindPropertyRelative("m_PersistentCalls.m_Calls").ClearArray();cs.ApplyModifiedPropertiesWithoutUndo();
  // CharacterSelectScreen owns the existing prefab's confirm/back buttons. Only their destination changes.
  foreach(string field in new[]{"onConfirm","onBack"}){
   var ev=(UnityEngine.Events.UnityEvent)typeof(CharacterSelectScreen).GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(screen);
   UnityEventTools.AddPersistentListener(ev,view.CloseCharacters);
  }
  EditorUtility.SetDirty(screen);
  var so=new SerializedObject(view);
  Set(so,"matchmaking",menu.GetComponent<PvPvEMatchmaking>());Set(so,"menu",menu);Set(so,"panel",panel.gameObject);Set(so,"characterPopup",popup);Set(so,"stagePrefab",AssetDatabase.LoadAssetAtPath<GameObject>(StagePath));
  Set(so,"alpha",alpha);Set(so,"bravo",bravo);Set(so,"status",status);Set(so,"characterButton",characters);Set(so,"leaveButton",leave);so.ApplyModifiedPropertiesWithoutUndo();
  UnityEventTools.AddPersistentListener(characters.onClick,view.OpenCharacters);UnityEventTools.AddPersistentListener(leave.onClick,view.Leave);
  panel.gameObject.SetActive(false);
  EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Cannot save phase 4 lobby");AssetDatabase.SaveAssets();Validate();
 }
 public static void PrepareStage()
 {
  var source=EditorSceneManager.OpenScene("Assets/Scenes/NetworkLobby.unity");
  var original=source.GetRootGameObjects().Single(g=>g.name=="LobbyCinematicWorld_Multiplayer");
  var player=(GameObject)new SerializedObject(original.GetComponent<LobbyCinematicPlayerDisplay>()).FindProperty("playerPrefab").objectReferenceValue;
  var stage=UnityEngine.Object.Instantiate(original);stage.name="PvPvELobbyStage";stage.SetActive(false);
  try{
   foreach(var presenter in stage.GetComponents<LobbyCinematicPartyPresenter>())UnityEngine.Object.DestroyImmediate(presenter);
   foreach(var actor in stage.GetComponents<LobbyCinematicPlayerDisplay>()){UnityEngine.Object.DestroyImmediate(actor.Anchor.gameObject);UnityEngine.Object.DestroyImmediate(actor);}
   foreach(var behaviour in stage.GetComponentsInChildren<MonoBehaviour>(true).Reverse())if(behaviour && !behaviour.enabled)UnityEngine.Object.DestroyImmediate(behaviour);
   foreach(var net in stage.GetComponentsInChildren<NetworkObject>(true))UnityEngine.Object.DestroyImmediate(net);
   foreach(var agent in stage.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))UnityEngine.Object.DestroyImmediate(agent);
   // Remove these whole props, preserving every terrain/road renderer from the source.
   foreach(var item in stage.GetComponentsInChildren<Transform>(true))
    if(item.name.StartsWith("SM_Env_Fountain",StringComparison.OrdinalIgnoreCase)||item.name.StartsWith("SM_Env_Well",StringComparison.OrdinalIgnoreCase))item.gameObject.SetActive(false);
   for(int i=0;i<10;i++){
    var anchor=new GameObject("LobbyPlayerAnchor_"+(i+1));anchor.transform.SetParent(stage.transform,false);anchor.SetActive(false);
    var actor=stage.AddComponent<LobbyCinematicPlayerDisplay>();actor.Configure(player,anchor.transform,"Handgun");
    var so=new SerializedObject(actor);so.FindProperty("logDiagnostics").boolValue=false;so.FindProperty("visualBodyOnly").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
   }
   PrefabUtility.SaveAsPrefabAsset(stage,StagePath);
  }finally{UnityEngine.Object.DestroyImmediate(stage);}
  AssetDatabase.ImportAsset(StagePath,ImportAssetOptions.ForceUpdate);
 }
 public static void Validate()
 {
  var scene=EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
  var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PvPvELobbyView>(true)).Single();var so=new SerializedObject(view);
  foreach(string field in new[]{"matchmaking","menu","panel","characterPopup","stagePrefab","alpha","bravo","status","characterButton","leaveButton"})if(!so.FindProperty(field).objectReferenceValue)throw new Exception("Missing lobby reference: "+field);
  var stage=AssetDatabase.LoadAssetAtPath<GameObject>(StagePath);if(stage.GetComponents<LobbyCinematicPlayerDisplay>().Length!=10 || stage.GetComponent<LobbyCinematicPartyPresenter>() || stage.GetComponentsInChildren<NetworkObject>(true).Length!=0)throw new Exception("Invalid stage actors or gameplay networking");
  var config=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>(PvPvEPhase3Tools.SettingsPath);
  if(!config.Characters || Enumerable.Range(0,config.Characters.VariantCount).Count(config.IsValidCharacter)!=5)throw new Exception("Original character catalog reference missing");
  var popup=so.FindProperty("characterPopup").objectReferenceValue as GameObject;
  if(!popup.GetComponentInChildren<CharacterSelectMenu>(true) || !popup.GetComponentInChildren<CharacterPreviewStage>(true))throw new Exception("Original selector/preview missing");
  string output=Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE4_OUTPUT")??"Documentation/PvPvE/Verificacion/Fase4";Directory.CreateDirectory(output);File.WriteAllText(output+"/References.txt","PASS: ten existing actor displays; visual-only existing stage; original catalog and selector; all lobby references assigned; cooperative lobby untouched.");
 }
 static void Set(SerializedObject so,string field,UnityEngine.Object value)=>so.FindProperty(field).objectReferenceValue=value;
 static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;return r;}
 static TMP_Text Label(string name,Transform parent,string value,Vector2 min,Vector2 max,int size,Color color){var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.color=color;t.text=value;t.raycastTarget=false;return t;}
 static TMP_Text Team(string name,Transform parent,float min,float max){var r=Rect(name+"Panel",parent,new Vector2(min,.23f),new Vector2(max,.78f));r.gameObject.AddComponent<Image>().color=new Color(.012f,.03f,.027f,.88f);var text=Label(name+"Players",r,"",new Vector2(.06f,.02f),new Vector2(.95f,.97f),24,Color.white);text.textWrappingMode=TextWrappingModes.NoWrap;return text;}
 static Button Button(string name,Transform parent,string value,Vector2 min,Vector2 max){var r=Rect(name,parent,min,max);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.02f,.14f,.09f,.97f);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;var t=Label("Label",r,value,new Vector2(.04f,0),new Vector2(.96f,1),24,new Color(.4f,1,.65f));t.alignment=TextAlignmentOptions.Center;return b;}
}
#endif
