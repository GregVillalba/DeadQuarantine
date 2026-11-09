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

public static class PvPvEPhase3Tools
{
 public const string SettingsPath="Assets/PvPvE/Settings/PvPvENetworkSettings.asset";
 public static void Install()
 {
  Directory.CreateDirectory("Assets/PvPvE/Settings");Directory.CreateDirectory("Assets/PvPvE/Prefabs");AssetDatabase.Refresh();
  var config=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>(SettingsPath);if(!config){config=ScriptableObject.CreateInstance<PvPvENetworkSettings>();AssetDatabase.CreateAsset(config,SettingsPath);}
  var roster=new GameObject("PvPvE Team Roster");roster.AddComponent<NetworkObject>();var team=roster.AddComponent<PvPvETeamRoster>();var tso=new SerializedObject(team);tso.FindProperty("settings").objectReferenceValue=config;tso.ApplyModifiedPropertiesWithoutUndo();
  config.RosterPrefab=PrefabUtility.SaveAsPrefabAsset(roster,"Assets/PvPvE/Prefabs/PvPvETeamRoster.prefab");UnityEngine.Object.DestroyImmediate(roster);EditorUtility.SetDirty(config);
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/PantallasUI.unity");
  var menu=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PvPvEModeMenu>(true)).Single();
  var service=menu.GetComponent<PvPvEMatchmaking>()??menu.gameObject.AddComponent<PvPvEMatchmaking>();var so=new SerializedObject(service);Set(so,"settings",config);so.ApplyModifiedPropertiesWithoutUndo();
  var menuSettings=new SerializedObject(menu);Set(menuSettings,"matchmaking",service);menuSettings.ApplyModifiedPropertiesWithoutUndo();
  var page=menu.PvPvEPanel;var old=page.transform.Find("RoomRosterPanel");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
  var panel=Rect("RoomRosterPanel",page.transform,new Vector2(.07f,.36f),new Vector2(.94f,.63f));panel.gameObject.AddComponent<Image>().color=new Color(.02f,.08f,.06f,1);
  var template=page.transform.Find("SearchStatus").GetComponent<TMP_Text>();
  Text("AlphaTitle",panel,template,"ALPHA",new Vector2(.025f,.78f),new Vector2(.47f,.98f),28);
  Text("BravoTitle",panel,template,"BRAVO",new Vector2(.53f,.78f),new Vector2(.98f,.98f),28);
  var alpha=Text("AlphaPlayers",panel,template,"",new Vector2(.025f,.02f),new Vector2(.47f,.77f),23);
  var bravo=Text("BravoPlayers",panel,template,"",new Vector2(.53f,.02f),new Vector2(.98f,.77f),23);alpha.richText=false;bravo.richText=false;
  var view=page.GetComponent<PvPvEConnectionPanel>()??page.AddComponent<PvPvEConnectionPanel>();var viewSettings=new SerializedObject(view);
  Set(viewSettings,"matchmaking",service);var search=page.transform.Find("SearchButton").GetComponent<Button>();Set(viewSettings,"searchButton",search);
  Set(viewSettings,"buttonLabel",search.GetComponentInChildren<TMP_Text>(true));Set(viewSettings,"status",template);Set(viewSettings,"availability",page.transform.Find("Availability").GetComponent<TMP_Text>());Set(viewSettings,"alpha",alpha);Set(viewSettings,"bravo",bravo);Set(viewSettings,"rosterPanel",panel.gameObject);
  var rules=viewSettings.FindProperty("rules");rules.arraySize=3;int index=0;foreach(string name in new[]{"Teams","Kills","Time"})rules.GetArrayElementAtIndex(index++).objectReferenceValue=page.transform.Find(name).gameObject;viewSettings.ApplyModifiedPropertiesWithoutUndo();
  search.interactable=true;while(search.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(search.onClick,0);UnityEventTools.AddPersistentListener(search.onClick,view.SearchOrLeave);
  page.transform.Find("Availability").GetComponent<TMP_Text>().text="RELAY · EQUIPOS AUTOMÁTICOS";template.text="Encontrá una sala automáticamente o esperá a otros jugadores.";
  panel.gameObject.SetActive(false);EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Cannot save menu");AssetDatabase.SaveAssets();
 }
 static void Set(SerializedObject so,string field,UnityEngine.Object value)=>so.FindProperty(field).objectReferenceValue=value;
 static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;return r;}
 static TMP_Text Text(string name,Transform parent,TMP_Text source,string value,Vector2 min,Vector2 max,float size){var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=source.font;t.color=source.color;t.fontSize=size;t.text=value;t.raycastTarget=false;t.alignment=TextAlignmentOptions.TopLeft;return t;}
}
#endif
