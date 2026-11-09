#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DeadQuarantine.Development;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class PvPvEPhase4Build
{
 public static void Build()
 {
  PvPvEPhase4Tools.PrepareStage();PvPvEPhase4Tools.Validate();
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NetworkBootstrap>(true)))b.enabled=false;
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true).Where(b=>b && b.GetType().Name=="CharacterRenameManager")))b.enabled=false;
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(b=>b && b.GetType().Name=="AchievementsManager"))UnityEngine.Object.DestroyImmediate(b);
  new GameObject("Phase4 validation driver").AddComponent<PvPvEPhase4SmokeDriver>();
  const string scenePath="Assets/Tests/PvPvE/Phase4LobbyValidation.unity";EditorSceneManager.SaveScene(scene,scenePath);
  string output=Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE4_OUTPUT")??Path.GetFullPath("Temp/PvPvEPhase4");Directory.CreateDirectory(output+"/Build");
  string company=PlayerSettings.companyName,product=PlayerSettings.productName;
  try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase4 Lobby Validation";
   var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scenePath},target=BuildTarget.StandaloneWindows64,locationPathName=output+"/Build/PvPvEPhase4.exe",options=BuildOptions.Development});
   if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Phase4 validation build failed");
  }finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
 }
}
#endif
