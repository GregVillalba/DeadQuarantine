#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DeadQuarantine.PvPvE.Networking;
using DeadQuarantine.Development;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
public static class Phase15PreparationBuild {
 const string MapPath="Assets/PvPvE/Scenes/PvPvEMatch.unity";
 static string Output=>Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE15_OUTPUT");
 public static void BuildAll(){
  byte[] globals=File.ReadAllBytes("Assets/DefaultNetworkPrefabs.asset");
  try{Install();CreateMap();Build();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);File.WriteAllBytes("Assets/DefaultNetworkPrefabs.asset",globals);}
 }
 public static void RebuildOnly(){byte[] globals=File.ReadAllBytes("Assets/DefaultNetworkPrefabs.asset");try{Build();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);File.WriteAllBytes("Assets/DefaultNetworkPrefabs.asset",globals);}}
 public static void FinalValidationBuild(){RebuildOnly();try{BuildLegacy();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);}}
 static void BuildLegacy(){
  EditorSceneManager.OpenScene("Assets/Tests/PvPvE/Phase11/PerksValidation.unity");foreach(var old in UnityEngine.Object.FindObjectsByType<PvPvEPhase11PerksDriver>(FindObjectsInactive.Include))UnityEngine.Object.DestroyImmediate(old.gameObject);var go=new GameObject("Phase15 ordinary mode validation");go.AddComponent<PvPvEMatchmaking>();var driver=go.AddComponent<PvPvEPhase14InitialClassDriver>();driver.Settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");driver.PlayerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
  const string path="Assets/Tests/PvPvE/Phase15/OrdinaryValidation.unity";EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),path);string target=Path.Combine(Output,"Legacy/Ordinary.exe");Directory.CreateDirectory(Path.GetDirectoryName(target));string company=PlayerSettings.companyName,product=PlayerSettings.productName;try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase15 ordinary regression";var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{path},target=BuildTarget.StandaloneWindows64,locationPathName=target,options=BuildOptions.Development});if(build.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Ordinary validation build failed");}finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
 }
 public static void Install(){
  var settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");settings.ProtocolVersion=5015;settings.PlayerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");settings.MatchScene="PvPvEMatch";settings.GamePrefabs=AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
  const string path="Assets/PvPvE/Prefabs/PvPvETeamRoster.prefab";string text=File.ReadAllText(path);string guid=AssetDatabase.AssetPathToGUID("Assets/Scripts/PvPvE/Networking/PvPvEPreparationFlow.cs");
  if(!text.Contains("guid: "+guid)){
   const string id="5150000000000000015";text=text.Replace("  - component: {fileID: 5140000000000000014}","  - component: {fileID: 5140000000000000014}\n  - component: {fileID: "+id+"}");
   text+="\n--- !u!114 &"+id+"\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 4958710834160195841}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: "+guid+", type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n";
   File.WriteAllText(path,text);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
  }
 }
 static void CreateMap(){
  Directory.CreateDirectory("Assets/PvPvE/Scenes");AssetDatabase.Refresh();
  if(!File.Exists(MapPath)){if(!AssetDatabase.CopyAsset("Assets/Scenes/MainSceneMultiPlayer.unity",MapPath))throw new Exception("Could not copy multiplayer map");}
  var scene=EditorSceneManager.OpenScene(MapPath);
  var existing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ConfiguracionEscenarioJugable>(true)).ToArray();if(!existing.Any(c=>c.TipoEscenario==ConfiguracionPartidaSeleccionada.Escenario.Ciudad))PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Escenarios/Escenario_2.prefab"),scene);
  var manager=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GestorEscenariosPartida>(true)).Single();var managerData=new SerializedObject(manager);foreach(var pair in new[]{("escenarioLaboratorio",ConfiguracionPartidaSeleccionada.Escenario.Laboratorio),("escenarioCiudad",ConfiguracionPartidaSeleccionada.Escenario.Ciudad),("escenarioCabana",ConfiguracionPartidaSeleccionada.Escenario.Cabana)})managerData.FindProperty(pair.Item1).objectReferenceValue=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ConfiguracionEscenarioJugable>(true)).First(c=>c.TipoEscenario==pair.Item2);managerData.ApplyModifiedPropertiesWithoutUndo();
  var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(b=>b).ToArray();
  foreach(var curtain in all.OfType<GameStartCurtain>()){var canvas=new SerializedObject(curtain).FindProperty("curtainCanvas").objectReferenceValue as GameObject;if(canvas)UnityEngine.Object.DestroyImmediate(canvas);}
  foreach(var zombie in all.OfType<ZombieHealth>()){if(!zombie)continue;var net=zombie.GetComponentInParent<NetworkObject>();UnityEngine.Object.DestroyImmediate(net?net.gameObject:zombie.gameObject);}
  foreach(var b in all)if(b&&(b is NetworkManager||b is NetworkBootstrap||b is RoundManager||b is MultiplayerPlayerSpawner||b is GameStartCurtain||b is StoryIntroController||b.GetType().Name=="CharacterRenameManager"||b.GetType().Name=="AchievementsManager"))UnityEngine.Object.DestroyImmediate(b);
  foreach(var input in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EventSystem>(true)).ToArray())UnityEngine.Object.DestroyImmediate(input.gameObject);
  if(!scene.GetRootGameObjects().Any(g=>g.GetComponent<PvPvEMapContext>()))new GameObject("PvPvE map context").AddComponent<PvPvEMapContext>();
  var configs=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ConfiguracionEscenarioJugable>(true)).ToArray();File.WriteAllLines(Path.Combine(Output,"MapConfigs.txt"),configs.Select(c=>c.TipoEscenario+" | "+c.name+" | "+c.transform.root.name));if(configs.Select(c=>c.TipoEscenario).Where(t=>t!=ConfiguracionPartidaSeleccionada.Escenario.Ninguno).Distinct().Count()!=3)throw new Exception("All three original maps must remain configured: "+string.Join(",",configs.Select(c=>c.TipoEscenario)));
  EditorSceneManager.SaveScene(scene,MapPath);
  var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path==MapPath)){scenes.Add(new EditorBuildSettingsScene(MapPath,true));EditorBuildSettings.scenes=scenes.ToArray();}
 }
 static void Build(){
  var scene=EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)))if(b&&(b is NetworkBootstrap||b.GetType().Name=="CharacterRenameManager"||b.GetType().Name=="AchievementsManager"))b.enabled=false;
  new GameObject("Phase15 native validation").AddComponent<PvPvEPhase15ReadyDriver>();Directory.CreateDirectory("Assets/Tests/PvPvE/Phase15");AssetDatabase.Refresh();const string menu="Assets/Tests/PvPvE/Phase15/ReadyMenu.unity";EditorSceneManager.SaveScene(scene,menu);
  string target=Path.Combine(Output,"Game/Preparation.exe");Directory.CreateDirectory(Path.GetDirectoryName(target));string company=PlayerSettings.companyName,product=PlayerSettings.productName;
  try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase15 preparation";var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{menu,MapPath},target=BuildTarget.StandaloneWindows64,locationPathName=target,options=BuildOptions.Development});if(build.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Phase15 build failed");}
  finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
 }
}
#endif
