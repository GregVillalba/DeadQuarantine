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
public static partial class OriginalCityWeaponsTools
{
 public static void PreviewWeapons() {
  Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");var player=(GameObject)PrefabUtility.InstantiatePrefab(asset);
  var text=new StringBuilder();
  try {

   foreach(var camera in player.GetComponentsInChildren<Camera>(true))camera.gameObject.SetActive(false);
   foreach(var canvas in player.GetComponentsInChildren<Canvas>(true))canvas.gameObject.SetActive(false);
   var presentation=player.GetComponent<NetworkWeaponPresentation>();var body=presentation.Body;body.gameObject.SetActive(true);
   var hand=body.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Hand_R");
   var light=new GameObject("Preview Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(35,-25,0);
   RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
   for(int i=0;i<presentation.Weapons.Length;i++) {
    var definition=presentation.Weapons[i];string token=i==0?"Handgun":i==1?"AR_01":"Shotgun_01";
    SampleAim(body.GetComponent<Animator>(),definition.Controller,i);
    var model=(GameObject)PrefabUtility.InstantiatePrefab(definition.Model);model.transform.SetParent(hand,false);model.transform.localPosition=definition.GripPosition;model.transform.localRotation=Quaternion.Euler(definition.GripRotation);
    typeof(NetworkWeaponPresentation).GetField("held",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(presentation,model);
    body.GetComponent<Animator>().enabled=true;body.GetComponent<Animator>().Update(0);body.GetComponent<Animator>().enabled=false;
    foreach(var renderer in body.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=false;
    foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))renderer.gameObject.layer=0;
    var muzzle=model.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SOCKET_Muzzle");var flash=muzzle.GetComponent<Muzzle>();var settings=new SerializedObject(flash);var particles=(GameObject)settings.FindProperty("flashParticlesPrefab").objectReferenceValue;
    if(!particles || !muzzle.parent) {File.WriteAllText(Path.Combine(Output,"preview-reference-error.txt"),definition.Id+" particles="+(particles?AssetDatabase.GetAssetPath(particles):"NULL")+" parent="+(muzzle.parent?muzzle.parent.name:"NULL")+" prefab="+(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/P_LPSP_WEP_Flash.prefab")?"loaded":"missing"));throw new InvalidOperationException("Muzzle preview references: "+definition.Id);}
    float alignment=Vector3.Dot(muzzle.TransformDirection(particles.transform.localRotation*Vector3.forward),-muzzle.parent.up);
    text.AppendLine(definition.Id+" alignment="+alignment+" grip="+definition.GripPosition+" rotation="+definition.GripRotation);
    var sourceMeshes=definition.Source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>r.sharedMesh).ToArray();var actualMeshes=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>r.sharedMesh).ToArray();
    if(sourceMeshes.Length==0 || !sourceMeshes.All(m=>actualMeshes.Contains(m)))throw new InvalidOperationException("Weapon does not reuse the first-person meshes: "+definition.Id);
    if(alignment<.97f)throw new InvalidOperationException("Muzzle orientation does not match barrel: "+definition.Id);
    var camera=new GameObject("Preview Camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.25f;camera.nearClipPlane=.01f;camera.farClipPlane=30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.09f,.10f);
    camera.transform.position=body.position+new Vector3(3,1.4f,4);camera.transform.LookAt(body.position+Vector3.up*1.05f);camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
    Render(camera,Path.Combine(Output,"weapon-"+definition.Id+".png"),1200,1000);
    UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(model);
   }
   File.WriteAllText(Path.Combine(Output,"weapon-validation.txt"),text.ToString());
  }finally {UnityEngine.Object.DestroyImmediate(player);}
 }
 static void Render(Camera camera,string path,int width,int height) {
  var rt=new RenderTexture(width,height,24);var active=RenderTexture.active;
  try {camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
  finally {camera.targetTexture=null;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
 }
 public static void BuildValidationPlayer() {
  PreviewWeapons();
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  Directory.CreateDirectory("Assets/Tests/CityWeapons");AssetDatabase.Refresh();
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
  var selected=new GameObject("Validation selected scenario").AddComponent<ConfiguracionPartidaSeleccionada>();selected.escenarioSeleccionado=ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;selected.modoSeleccionado=ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;
  var spawnerObject=new GameObject("Validation original player spawner");spawnerObject.AddComponent<NetworkObject>();var spawner=spawnerObject.AddComponent<MultiplayerPlayerSpawner>();
  var fallback=new GameObject("Invalid manual fallback").transform;fallback.position=new Vector3(900,-100,900);var spawnerSettings=new SerializedObject(spawner);spawnerSettings.FindProperty("playerSpawn1").objectReferenceValue=fallback;spawnerSettings.ApplyModifiedPropertiesWithoutUndo();
  var driver=new GameObject("Validation Driver").AddComponent<DeadQuarantine.Development.OriginalWeaponsSmokeDriver>();
  string scene="Assets/Tests/CityWeapons/WeaponPresentationTest.unity";EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),scene);
  string path=Path.Combine(Output,"Build/OriginalWeaponsTest.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));
  string company=PlayerSettings.companyName,product=PlayerSettings.productName;
  UnityEditor.Build.Reporting.BuildReport result;
  try {PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="City Weapons Validation";
  result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scene},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
  }finally {PlayerSettings.companyName=company;PlayerSettings.productName=product;}
  if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Validation player build failed");
 }
}
#endif
