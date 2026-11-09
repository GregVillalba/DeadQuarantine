#if UNITY_EDITOR
using System;
using System.IO;
using DeadQuarantine.PvPvE.Networking;
using DeadQuarantine.Development;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class PvPvEPhase3Build
{
 public static void Build()
 {
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var go=new GameObject("Phase3 validation");go.AddComponent<PvPvEMatchmaking>();var driver=go.AddComponent<PvPvEPhase3SmokeDriver>();driver.Settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>(PvPvEPhase3Tools.SettingsPath);
  Directory.CreateDirectory("Assets/Tests/PvPvE");AssetDatabase.Refresh();string path="Assets/Tests/PvPvE/Phase3NetworkValidation.unity";EditorSceneManager.SaveScene(scene,path);
  string output=Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE3_OUTPUT")??Path.GetFullPath("Temp/PvPvEPhase3");Directory.CreateDirectory(output+"/Build");
  string company=PlayerSettings.companyName,product=PlayerSettings.productName;
  try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase3 Network Validation";
   var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{path},target=BuildTarget.StandaloneWindows64,locationPathName=output+"/Build/PvPvEPhase3.exe",options=BuildOptions.Development});
   if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Network validation build failed");
  }finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
 }
}
#endif
