#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DeadQuarantine.Development;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class PvPvEPhase5Build
{
 public static void Build()
 {
  PvPvEPhase5Tools.Install();
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NetworkBootstrap>(true)))b.enabled=false;
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(b=>b && b.GetType().Name=="CharacterRenameManager"))b.enabled=false;
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(b=>b && b.GetType().Name=="AchievementsManager"))UnityEngine.Object.DestroyImmediate(b);
  new GameObject("Phase5 validation driver").AddComponent<PvPvEPhase5SmokeDriver>();
  const string path="Assets/Tests/PvPvE/Phase5VotingValidation.unity";EditorSceneManager.SaveScene(scene,path);
  string output=Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE5_OUTPUT")??Path.GetFullPath("Temp/PvPvEPhase5");Directory.CreateDirectory(output+"/Build");
  string company=PlayerSettings.companyName,product=PlayerSettings.productName;
  try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase5 Voting Validation";
   var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{path},target=BuildTarget.StandaloneWindows64,locationPathName=output+"/Build/PvPvEPhase5.exe",options=BuildOptions.Development});
   if(r.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Voting validation build failed");
  }finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
 }
}
#endif
