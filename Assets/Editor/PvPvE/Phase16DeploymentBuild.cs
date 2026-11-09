#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DeadQuarantine.PvPvE.Networking;
using DeadQuarantine.Development;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class Phase16DeploymentBuild {
 const string MapPath="Assets/PvPvE/Scenes/PvPvEMatch.unity";
 static string Output=>Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE16_OUTPUT");
 public static void BuildAll(){
  byte[] globals=File.ReadAllBytes("Assets/DefaultNetworkPrefabs.asset");
  try{Install();ConfigureSpawns();Build();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);File.WriteAllBytes("Assets/DefaultNetworkPrefabs.asset",globals);}
 }
 public static void RebuildOnly(){byte[] globals=File.ReadAllBytes("Assets/DefaultNetworkPrefabs.asset");try{Build();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);File.WriteAllBytes("Assets/DefaultNetworkPrefabs.asset",globals);}}
 public static void FinalValidationBuild(){RebuildOnly();try{BuildLegacy();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);}}
 static void BuildLegacy(){
  EditorSceneManager.OpenScene("Assets/Tests/PvPvE/Phase11/PerksValidation.unity");foreach(var old in UnityEngine.Object.FindObjectsByType<PvPvEPhase11PerksDriver>(FindObjectsInactive.Include))UnityEngine.Object.DestroyImmediate(old.gameObject);var go=new GameObject("Phase16 ordinary mode validation");go.AddComponent<PvPvEMatchmaking>();var driver=go.AddComponent<PvPvEPhase14InitialClassDriver>();driver.Settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");driver.PlayerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
  const string path="Assets/Tests/PvPvE/Phase16/OrdinaryValidation.unity";EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),path);string target=Path.Combine(Output,"Legacy/Ordinary.exe");Directory.CreateDirectory(Path.GetDirectoryName(target));string company=PlayerSettings.companyName,product=PlayerSettings.productName;try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase16 ordinary regression";var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{path},target=BuildTarget.StandaloneWindows64,locationPathName=target,options=BuildOptions.Development});if(build.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Ordinary validation build failed");}finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
 }
 static void Install(){
  var settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");settings.ProtocolVersion=5016;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
  const string path="Assets/PvPvE/Prefabs/PvPvETeamRoster.prefab";string text=File.ReadAllText(path);string guid=AssetDatabase.AssetPathToGUID("Assets/Scripts/PvPvE/Networking/PvPvETeamDeployment.cs");
  if(!text.Contains("guid: "+guid)){
   const string id="5160000000000000016";text=text.Replace("  - component: {fileID: 5150000000000000015}","  - component: {fileID: 5150000000000000015}\n  - component: {fileID: "+id+"}");
   text+="\n--- !u!114 &"+id+"\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 4958710834160195841}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: "+guid+", type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n";
   File.WriteAllText(path,text);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
  }
 }
 static void ConfigureSpawns(){
  var scene=EditorSceneManager.OpenScene(MapPath);
  var configs=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ConfiguracionEscenarioJugable>(true)).ToArray();
  var old=configs.ToDictionary(c=>c,c=>c.gameObject.activeSelf);var report=new List<string>();
  foreach(var config in configs){
   foreach(var c in configs)c.gameObject.SetActive(c==config);
   Physics.SyncTransforms();
   var existing=config.GetComponentsInChildren<PvPvETeamSpawnPoint>(true);foreach(var point in existing)UnityEngine.Object.DestroyImmediate(point.gameObject);
   var surfaces=config.transform.root.GetComponentsInChildren<NavMeshSurface>(true);
   // The city prefab's serialized GUID is stale; reuse its existing baked data only in this mode scene.
   if(config.TipoEscenario==ConfiguracionPartidaSeleccionada.Escenario.Ciudad){var city=surfaces.First(s=>s.transform==config.transform.root);city.RemoveData();city.navMeshData=AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Prefabs/Escenarios/Escenario_2/NavMesh-Escenario_2.asset");PrefabUtility.RecordPrefabInstancePropertyModifications(city);}
   foreach(var surface in surfaces)surface.AddData();
   Debug.Log("Phase16 nav "+config.TipoEscenario+" spawn="+config.SpawnJugador.position+" root="+config.transform.root.name+" surfaces="+string.Join(";",surfaces.Select(s=>s.name+":"+(s.navMeshData?AssetDatabase.GetAssetPath(s.navMeshData):"NO DATA"))));
   if(!NavMesh.SamplePosition(config.SpawnJugador.position,out var origin,10,NavMesh.AllAreas))throw new Exception(config.TipoEscenario+": original spawn is not near accessible NavMesh");
   var triangles=NavMesh.CalculateTriangulation();var candidates=new List<Vector3>();var path=new NavMeshPath();
   int stride=Math.Max(3,(triangles.indices.Length/2400)/3*3);
   for(int i=0;i+2<triangles.indices.Length;i+=stride){
    var p=(triangles.vertices[triangles.indices[i]]+triangles.vertices[triangles.indices[i+1]]+triangles.vertices[triangles.indices[i+2]])/3;
    if(Vector3.Distance(origin.position,p)>140||!NavMesh.SamplePosition(p,out var sample,.5f,NavMesh.AllAreas)||!PvPvETeamDeployment.ClearCapsule(sample.position)||candidates.Any(c=>Vector3.Distance(c,sample.position)<2.1f))continue;
    if(!NavMesh.CalculatePath(origin.position,sample.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
    candidates.Add(sample.position);
   }
   if(candidates.Count<12)throw new Exception(config.TipoEscenario+": only "+candidates.Count+" accessible clear spawn candidates (triangles="+triangles.indices.Length+")");
   var a=candidates.OrderByDescending(p=>(p-origin.position).sqrMagnitude).First();var b=candidates.OrderByDescending(p=>(p-a).sqrMagnitude).First();
   var group=new GameObject("PvPvE Team Spawns");group.transform.SetParent(config.transform,false);
   foreach(var team in new[]{PvPvETeam.Alpha,PvPvETeam.Bravo}){
    var center=team==PvPvETeam.Alpha?a:b;var enemy=team==PvPvETeam.Alpha?b:a;
    var selected=candidates.Where(p=>Vector3.Distance(p,center)<Vector3.Distance(p,enemy)).OrderBy(p=>(p-center).sqrMagnitude).Take(12).ToArray();
    if(selected.Length<5)throw new Exception(config.TipoEscenario+": insufficient positions for "+team);
    foreach(var p in selected){var go=new GameObject(team+" "+group.transform.childCount);go.transform.SetParent(group.transform,false);go.transform.position=p+Vector3.up*.08f;Vector3 forward=enemy-p;forward.y=0;go.transform.rotation=Quaternion.LookRotation(forward);var point=go.AddComponent<PvPvETeamSpawnPoint>();point.Map=config.TipoEscenario;point.Team=team;}
    report.Add(config.TipoEscenario+" | "+team+" | "+selected.Length+" spawns | center "+center+" | distance "+Vector3.Distance(a,b));
   }
   foreach(var surface in surfaces)surface.RemoveData();
  }
  foreach(var pair in old)pair.Key.gameObject.SetActive(pair.Value);
  EditorSceneManager.SaveScene(scene);File.WriteAllLines(Path.Combine(Output,"Spawns.txt"),report);
 }
 static void Build(){
  var scene=EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)))if(b&&(b is NetworkBootstrap||b.GetType().Name=="CharacterRenameManager"||b.GetType().Name=="AchievementsManager"))b.enabled=false;
  new GameObject("Phase16 native validation").AddComponent<PvPvEPhase16DeploymentDriver>();Directory.CreateDirectory("Assets/Tests/PvPvE/Phase16");AssetDatabase.Refresh();const string menu="Assets/Tests/PvPvE/Phase16/DeploymentMenu.unity";EditorSceneManager.SaveScene(scene,menu);
  string target=Path.Combine(Output,"Game/Deployment.exe");Directory.CreateDirectory(Path.GetDirectoryName(target));string company=PlayerSettings.companyName,product=PlayerSettings.productName;
  try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase16 deployment";var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{menu,MapPath},target=BuildTarget.StandaloneWindows64,locationPathName=target,options=BuildOptions.Development});if(build.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Phase16 build failed");}
  finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
 }
}
#endif
