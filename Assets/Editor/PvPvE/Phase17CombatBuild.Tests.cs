#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DeadQuarantine.PvPvE.Networking;
using DeadQuarantine.Development;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static partial class Phase17CombatBuild {
 const string MapPath="Assets/PvPvE/Scenes/PvPvEMatch.unity";
 public static void BuildAll(){var globals=File.ReadAllBytes("Assets/DefaultNetworkPrefabs.asset");try{Install();Build();BuildLegacy();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);File.WriteAllBytes("Assets/DefaultNetworkPrefabs.asset",globals);}}
 public static void RebuildOnly(){var globals=File.ReadAllBytes("Assets/DefaultNetworkPrefabs.asset");try{Build();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);File.WriteAllBytes("Assets/DefaultNetworkPrefabs.asset",globals);}}
 static void BuildLegacy(){
  EditorSceneManager.OpenScene("Assets/Tests/PvPvE/Phase11/PerksValidation.unity");foreach(var old in UnityEngine.Object.FindObjectsByType<PvPvEPhase11PerksDriver>(FindObjectsInactive.Include))UnityEngine.Object.DestroyImmediate(old.gameObject);var go=new GameObject("Phase17 ordinary mode validation");go.AddComponent<PvPvEMatchmaking>();var driver=go.AddComponent<PvPvEPhase14InitialClassDriver>();driver.Settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");driver.PlayerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
  const string path="Assets/Tests/PvPvE/Phase17/OrdinaryValidation.unity";EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),path);string target=Path.Combine(Output,"Legacy/Ordinary.exe");Directory.CreateDirectory(Path.GetDirectoryName(target));string company=PlayerSettings.companyName,product=PlayerSettings.productName;try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase17 ordinary regression";var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{path},target=BuildTarget.StandaloneWindows64,locationPathName=target,options=BuildOptions.Development});if(build.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Ordinary validation build failed");}finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
 }
 static void Build(){
  var scene=EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)))if(b&&(b is NetworkBootstrap||b.GetType().Name=="CharacterRenameManager"||b.GetType().Name=="AchievementsManager"))b.enabled=false;
  new GameObject("Phase17 native validation").AddComponent<PvPvEPhase17CombatDriver>();Directory.CreateDirectory("Assets/Tests/PvPvE/Phase17");AssetDatabase.Refresh();const string menu="Assets/Tests/PvPvE/Phase17/CombatMenu.unity";EditorSceneManager.SaveScene(scene,menu);
  string target=Path.Combine(Output,"Game/Combat.exe");Directory.CreateDirectory(Path.GetDirectoryName(target));string company=PlayerSettings.companyName,product=PlayerSettings.productName;
  try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase17 deployment";var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{menu,MapPath},target=BuildTarget.StandaloneWindows64,locationPathName=target,options=BuildOptions.Development});if(build.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Phase17 build failed");}
  finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
 }
}
#endif
