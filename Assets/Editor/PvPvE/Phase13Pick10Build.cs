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
public static class Phase13Pick10Build
{
 static string Output=>Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE13_OUTPUT");
 public static void Install(){
  var loadouts=AssetDatabase.LoadAssetAtPath<PvPvELoadoutCatalog>("Assets/PvPvE/Data/LoadoutCatalog.asset");
  var settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");settings.ProtocolVersion=Math.Max(settings.ProtocolVersion,5013);EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
  for(int i=0;i<5;i++)if(loadouts.Pick10Cost(loadouts.Default(i))!=4||!loadouts.Validate(loadouts.Default(i),out _))throw new Exception("Invalid default Pick 10 class");
 }
 public static void BuildAll(){Install();try{BuildGame();BuildMenu();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);}}
 public static void BuildGameAndRestoreMenu(){try{BuildGame();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);}}
 public static void BuildGame(){
  EditorSceneManager.OpenScene("Assets/Tests/PvPvE/Phase11/PerksValidation.unity");
  foreach(var old in UnityEngine.Object.FindObjectsByType<PvPvEPhase11PerksDriver>(FindObjectsInactive.Include))UnityEngine.Object.DestroyImmediate(old.gameObject);
  var go=new GameObject("Phase13 native validation");go.AddComponent<PvPvEMatchmaking>();var driver=go.AddComponent<PvPvEPhase12WildcardsDriver>();
  driver.Settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");driver.PlayerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
  var grenade=driver.PlayerPrefab.GetComponentInChildren<GrenadeThrow>(true);var so=new SerializedObject(grenade);var types=so.FindProperty("tipos");
  driver.GrenadePrefabs=Enumerable.Range(0,types.arraySize).Select(i=>types.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue as GameObject).Append(so.FindProperty("grenadePrefab").objectReferenceValue as GameObject).Where(g=>g).Distinct().ToArray();
  SaveBuild("Pick10Validation","Game/Pick10.exe");
 }
 public static void BuildMenu(){
  var scene=EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)))if(b&&(b is NetworkBootstrap||b.GetType().Name=="CharacterRenameManager"||b.GetType().Name=="AchievementsManager"))b.enabled=false;
  new GameObject("Phase13 class validation").AddComponent<PvPvEPhase9SmokeDriver>();SaveBuild("Pick10Menu","Menu/Classes.exe");
 }
 static void SaveBuild(string name,string relative){
  Directory.CreateDirectory("Assets/Tests/PvPvE/Phase13");AssetDatabase.Refresh();string scene="Assets/Tests/PvPvE/Phase13/"+name+".unity";EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),scene);
  string target=Path.Combine(Output,relative);Directory.CreateDirectory(Path.GetDirectoryName(target));string company=PlayerSettings.companyName,product=PlayerSettings.productName;
  try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase13 "+name;var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scene},target=BuildTarget.StandaloneWindows64,locationPathName=target,options=BuildOptions.Development});if(build.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Phase13 build failed");}
  finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
 }
}
#endif
