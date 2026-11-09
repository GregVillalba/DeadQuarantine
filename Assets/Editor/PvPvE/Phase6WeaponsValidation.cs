#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
public static class Phase6WeaponsValidation
{
 public static void FinalizeAndBuild(){CompleteWeaponsIntegrationBuilder.RefineMissingPhase6();Build();}
 public static void Build() {

  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  Directory.CreateDirectory("Assets/Tests/PvPvE/Phase6");AssetDatabase.Refresh();
  var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Test Floor";floor.transform.position=new Vector3(0,-.25f,4);floor.transform.localScale=new Vector3(25,.5f,25);
  var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Test Nameplate Wall";wall.transform.position=new Vector3(10,1.5f,0);wall.transform.localScale=new Vector3(2,3,1);
  var target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.name="Shot Endpoint Target";target.transform.position=new Vector3(0,1.2f,12);target.transform.localScale=new Vector3(5,3,.4f);
  var light=new GameObject("Test Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(35,-25,0);
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
  var go=new GameObject("Validation NetworkManager");var transport=go.AddComponent<UnityTransport>();var manager=go.AddComponent<NetworkManager>();
  manager.NetworkConfig.NetworkTransport=transport;manager.NetworkConfig.EnableSceneManagement=false;manager.NetworkConfig.ConnectionApproval=false;manager.NetworkConfig.TickRate=30;
  manager.NetworkConfig.PlayerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");manager.NetworkConfig.Prefabs.Add(new NetworkPrefab{Prefab=manager.NetworkConfig.PlayerPrefab});
  var scenarioRoot=new GameObject("Validation scenario");var configObject=new GameObject("ConfiguracionEscenarioJugable");configObject.transform.SetParent(scenarioRoot.transform);var config=configObject.AddComponent<ConfiguracionEscenarioJugable>();
  var spawnA=new GameObject("Host selected scenario spawn").transform;spawnA.position=new Vector3(-2,.1f,0);spawnA.SetParent(scenarioRoot.transform);
  var spawnB=new GameObject("Client selected scenario spawn").transform;spawnB.position=new Vector3(2,.1f,0);spawnB.SetParent(scenarioRoot.transform);
  var configSettings=new SerializedObject(config);configSettings.FindProperty("spawnJugador").objectReferenceValue=spawnA;var additional=configSettings.FindProperty("spawnsJugadoresAdicionales");additional.arraySize=1;additional.GetArrayElementAtIndex(0).objectReferenceValue=spawnB;configSettings.ApplyModifiedPropertiesWithoutUndo();
  var scenarioManager=new GameObject("Validation scenario manager").AddComponent<GestorEscenariosPartida>();var scenarioSettings=new SerializedObject(scenarioManager);foreach(string field in new[]{"escenarioLaboratorio","escenarioCiudad","escenarioCabana"})scenarioSettings.FindProperty(field).objectReferenceValue=config;scenarioSettings.ApplyModifiedPropertiesWithoutUndo();
  var spawnerObject=new GameObject("Validation original player spawner");spawnerObject.transform.SetParent(scenarioRoot.transform);var spawner=spawnerObject.AddComponent<MultiplayerPlayerSpawner>();
  var fallback=new GameObject("Invalid manual fallback").transform;fallback.position=new Vector3(900,-100,900);var spawnerSettings=new SerializedObject(spawner);spawnerSettings.FindProperty("playerSpawn1").objectReferenceValue=fallback;spawnerSettings.ApplyModifiedPropertiesWithoutUndo();
  var driver=new GameObject("Validation Driver").AddComponent<DeadQuarantine.Development.Phase6WeaponsSmokeDriver>();
  string scene="Assets/Tests/PvPvE/Phase6/WeaponPresentationTest.unity";EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),scene);
  string path=Path.Combine(Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE6_OUTPUT"),"Build/Phase6WeaponsTest.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));
  string company=PlayerSettings.companyName,product=PlayerSettings.productName;
  UnityEditor.Build.Reporting.BuildReport result;
  try {PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="Phase6 Weapons Validation";
  result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scene},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
  }finally {PlayerSettings.companyName=company;PlayerSettings.productName=product;}
  if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Validation player build failed");
 }
}
#endif
