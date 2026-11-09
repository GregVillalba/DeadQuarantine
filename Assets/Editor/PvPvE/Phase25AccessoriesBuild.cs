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
public static class Phase25AccessoriesBuild {
 static string Output=>Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE25_OUTPUT");
 const string MapPath="Assets/PvPvE/Scenes/PvPvEMatch.unity";
 public static void BuildBothPrepared(){Phase25WeaponsValidation.Build();BuildAll();}
 public static void BuildBoth(){Phase25WeaponsValidation.RepairAndBuild();BuildAll();}
 public static void BuildAll(){var globals=File.ReadAllBytes("Assets/DefaultNetworkPrefabs.asset");try{Build();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);File.WriteAllBytes("Assets/DefaultNetworkPrefabs.asset",globals);}}
 static void Build(){
  var scene=EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)))if(b&&(b is NetworkBootstrap||b.GetType().Name=="CharacterRenameManager"||b.GetType().Name=="AchievementsManager"))b.enabled=false;
  new GameObject("Phase24 native validation").AddComponent<PvPvEPhase25AccessoriesDriver>();Directory.CreateDirectory("Assets/Tests/PvPvE/Phase25/Mode");AssetDatabase.Refresh();const string menu="Assets/Tests/PvPvE/Phase25/Mode/ReviewMenu.unity";EditorSceneManager.SaveScene(scene,menu);
  string target=Path.Combine(Output,"Mode/Accessories.exe");Directory.CreateDirectory(Path.GetDirectoryName(target));string company=PlayerSettings.companyName,product=PlayerSettings.productName;
  try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase25 accessories";var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{menu,MapPath},target=BuildTarget.StandaloneWindows64,locationPathName=target,options=BuildOptions.Development});if(build.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Phase24 build failed");}
  finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
 }
}
#endif
