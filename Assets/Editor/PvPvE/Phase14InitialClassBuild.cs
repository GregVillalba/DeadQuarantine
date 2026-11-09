#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.Networking;
using DeadQuarantine.Development;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class Phase14InitialClassBuild
{
 static string Output=>Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE14_OUTPUT");
 public static void Install(){
  var loadouts=AssetDatabase.LoadAssetAtPath<PvPvELoadoutCatalog>("Assets/PvPvE/Data/LoadoutCatalog.asset");
  var settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");settings.ProtocolVersion=Math.Max(settings.ProtocolVersion,5014);EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
  const string rosterPath="Assets/PvPvE/Prefabs/PvPvETeamRoster.prefab";
  string text=File.ReadAllText(rosterPath);string guid=AssetDatabase.AssetPathToGUID("Assets/Scripts/PvPvE/Networking/PvPvEInitialClassSelection.cs");
  if(!text.Contains("guid: "+guid)){
   const string id="5140000000000000014";if(text.Contains("&"+id))throw new Exception("Roster component ID collision");
   text=text.Replace("  - component: {fileID: 5001497527439948455}","  - component: {fileID: 5001497527439948455}\n  - component: {fileID: "+id+"}");
   text+="\n--- !u!114 &"+id+"\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 4958710834160195841}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: "+guid+", type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n";
   File.WriteAllText(rosterPath,text);AssetDatabase.ImportAsset(rosterPath,ImportAssetOptions.ForceUpdate);
  }
  if(!AssetDatabase.LoadAssetAtPath<GameObject>(rosterPath).GetComponent<PvPvEInitialClassSelection>())throw new Exception("Initial class selector missing from roster");
  for(int i=0;i<5;i++)if(loadouts.Pick10Cost(loadouts.Default(i))!=4||!loadouts.Validate(loadouts.Default(i),out _))throw new Exception("Invalid default Pick 10 class");
 }
 public static void BuildAll(){Install();try{BuildGame();BuildMenu();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);}}
 public static void BuildGameAndRestoreMenu(){try{BuildGame();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);}}
 public static void BuildGame(){
  EditorSceneManager.OpenScene("Assets/Tests/PvPvE/Phase11/PerksValidation.unity");
  foreach(var old in UnityEngine.Object.FindObjectsByType<PvPvEPhase11PerksDriver>(FindObjectsInactive.Include))UnityEngine.Object.DestroyImmediate(old.gameObject);
  var go=new GameObject("Phase14 native validation");go.AddComponent<PvPvEMatchmaking>();var driver=go.AddComponent<PvPvEPhase14InitialClassDriver>();
  driver.Settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");driver.PlayerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
  var grenade=driver.PlayerPrefab.GetComponentInChildren<GrenadeThrow>(true);var so=new SerializedObject(grenade);var types=so.FindProperty("tipos");
  driver.GrenadePrefabs=Enumerable.Range(0,types.arraySize).Select(i=>types.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue as GameObject).Append(so.FindProperty("grenadePrefab").objectReferenceValue as GameObject).Where(g=>g).Distinct().ToArray();
  SaveBuild("InitialClassValidation","Game/InitialClass.exe");
 }
 public static void BuildMenu(){
  var scene=EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)))if(b&&(b is NetworkBootstrap||b.GetType().Name=="CharacterRenameManager"||b.GetType().Name=="AchievementsManager"))b.enabled=false;
  new GameObject("Phase14 class validation").AddComponent<PvPvEPhase9SmokeDriver>();SaveBuild("InitialClassMenu","Menu/Classes.exe");
 }
 static void SaveBuild(string name,string relative){
  Directory.CreateDirectory("Assets/Tests/PvPvE/Phase14");AssetDatabase.Refresh();string scene="Assets/Tests/PvPvE/Phase14/"+name+".unity";EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),scene);
  string target=Path.Combine(Output,relative);Directory.CreateDirectory(Path.GetDirectoryName(target));string company=PlayerSettings.companyName,product=PlayerSettings.productName;
  try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase14 "+name;var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scene},target=BuildTarget.StandaloneWindows64,locationPathName=target,options=BuildOptions.Development});if(build.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Phase14 build failed");}
  finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
 }
}
#endif
