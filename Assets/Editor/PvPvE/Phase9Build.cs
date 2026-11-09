#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DeadQuarantine.Development;
using DeadQuarantine.PvPvE.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class Phase9Build {
 public static void BuildAll(){Phase9ClassesIntegration.Install();CompleteWeaponsIntegrationBuilder.RepairAnimationsPhase9();BuildClasses();Phase9WeaponsValidation.Build();}
 public static void BuildValidationOnly(){BuildClasses();Phase9WeaponsValidation.Build();}
 public static void OpenProductionMenu(){EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);}
 public static void BuildClasses(){
  var scene=EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
  var editor=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PvPvEClassEditor>(true)).Single();
  if(!editor.entryButton||!editor.catalog||!editor.font||!editor.container)throw new InvalidOperationException("Missing class editor references");
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NetworkBootstrap>(true)))b.enabled=false;
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(b=>b&&(b.GetType().Name=="CharacterRenameManager"||b.GetType().Name=="AchievementsManager")))b.enabled=false;
  new GameObject("Phase9 classes validation driver").AddComponent<PvPvEPhase9SmokeDriver>();
  Directory.CreateDirectory("Assets/Tests/PvPvE/Phase9");AssetDatabase.Refresh();
  const string target="Assets/Tests/PvPvE/Phase9/ClassesValidation.unity";EditorSceneManager.SaveScene(scene,target);
  string output=Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE9_OUTPUT");Directory.CreateDirectory(output+"/ClassesBuild");
  string company=PlayerSettings.companyName,product=PlayerSettings.productName;
  try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase9 Classes Validation";
   var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{target},target=BuildTarget.StandaloneWindows64,locationPathName=output+"/ClassesBuild/Classes.exe",options=BuildOptions.Development});
   if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Class validation build failed");
  }finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
 }
}
#endif
