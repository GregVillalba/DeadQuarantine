#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
public static partial class OriginalCityWeaponsTools
{
 public static string Output => Environment.GetEnvironmentVariable("DQ_REVIEW_OUTPUT") ?? Path.GetFullPath("Temp/CityAndWeaponsReview");
 public static void ConfigureAndPreview() {ConfigureWeapons();PreviewAndAnalyze();}
 public static void PrepareAndBuildValidation() {ConfigureWeapons();BakeCity();BuildValidationPlayer();}
 public static void FinishAndBuildValidation() {ConfigureWeapons();BakeSceneCities();BuildValidationPlayer();}
 public static void ConfigureAndBuildValidation() {ConfigureWeapons();BuildValidationPlayer();}
 public static void Apply() {ConfigureWeapons();InspectCityAccess();BakeCity();}
 public static void InspectSceneCities() {
  Directory.CreateDirectory(Output);var b=new StringBuilder();
  foreach(string path in new[]{"Assets/Scenes/MainSceneSinglePlayer.unity","Assets/Scenes/MainSceneMultiPlayer.unity"}) {
   UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);b.AppendLine("SCENE "+path);
   foreach(var config in UnityEngine.Object.FindObjectsByType<ConfiguracionEscenarioJugable>(FindObjectsInactive.Include,FindObjectsSortMode.None)) {
    b.AppendLine("CONFIG "+config.TipoEscenario+" | "+PathOf(config.transform)+" source="+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(config));
    if(config.TipoEscenario!=ConfiguracionPartidaSeleccionada.Escenario.Ciudad)continue;
    var root=config.transform.parent;var surface=root.GetComponent<NavMeshSurface>();b.AppendLine("CITY active="+root.gameObject.activeSelf+" position="+root.position+" surface="+(surface?AssetDatabase.GetAssetPath(surface.navMeshData):"NONE"));
   }
  }
  File.WriteAllText(Path.Combine(Output,"scene-cities.txt"),b.ToString());
 }
 public static void FinalReferences() {
  var b=new StringBuilder();
  var player=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");try {
   var p=player.GetComponent<NetworkWeaponPresentation>();b.AppendLine("Player body colliders: "+p.Body.GetComponentsInChildren<Collider>(true).Length);
   b.AppendLine("Player weapon definitions: "+p.Weapons.Length);
  }finally {PrefabUtility.UnloadPrefabContents(player);}
  foreach(string path in new[]{"Assets/Scenes/MainSceneMultiPlayer.unity","Assets/Scenes/MainSceneSinglePlayer.unity"}) {
   UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);b.AppendLine("SCENE "+path);
   foreach(var config in UnityEngine.Object.FindObjectsByType<ConfiguracionEscenarioJugable>(FindObjectsInactive.Include,FindObjectsSortMode.None)) {
    var root=config.transform.parent;b.AppendLine("ROOT "+PathOf(root)+" type="+config.TipoEscenario+" renderers="+root.GetComponentsInChildren<Renderer>(true).Length);
   }
   var manager=UnityEngine.Object.FindAnyObjectByType<GestorEscenariosPartida>();
   if(manager) {var city=new SerializedObject(manager).FindProperty("escenarioCiudad").objectReferenceValue as ConfiguracionEscenarioJugable;b.AppendLine("Selected city reference: "+(city?PathOf(city.transform):"NONE"));}
  }
  File.WriteAllText(Path.Combine(Output,"final-references.txt"),b.ToString());
 }
 public static void InspectDetails() {
  Directory.CreateDirectory(Output);var b=new StringBuilder();var player=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
  try {
   var sw=player.GetComponentInChildren<WeaponSwitcher>(true);var so=new SerializedObject(sw);var slots=so.FindProperty("slots");
   foreach(string field in new[]{"weaponAnimator","thirdPersonAnimator","weaponAnimationEvents"}) {var c=so.FindProperty(field).objectReferenceValue as Component;b.AppendLine(field+"="+(c?PathOf(c.transform):"NULL"));}
   for(int i=0;i<slots.arraySize;i++) {var slot=slots.GetArrayElementAtIndex(i);var root=(GameObject)slot.FindPropertyRelative("weaponRoot").objectReferenceValue;var w=(Weapon)slot.FindPropertyRelative("weaponComponent").objectReferenceValue;var ws=new SerializedObject(w);b.AppendLine("WEAPON "+slot.FindPropertyRelative("weaponId").stringValue+" root="+PathOf(root.transform));
    foreach(var t in root.GetComponentsInChildren<Transform>(true))b.AppendLine(" T "+PathOf(t)+" p="+t.localPosition+" r="+t.localEulerAngles+" s="+t.localScale);
    foreach(string field in new[]{"firePoint","muzzle","partsAnimator"}) {var c=ws.FindProperty(field).objectReferenceValue as Component;b.AppendLine(field+"="+(c?PathOf(c.transform):"NULL"));}
    b.AppendLine("EJECT="+PathOf(((Transform)slot.FindPropertyRelative("casingEjectPoint").objectReferenceValue)));
    var ctrl=slot.FindPropertyRelative("overrideController").objectReferenceValue as RuntimeAnimatorController;if(!ctrl)ctrl=so.FindProperty("baseController").objectReferenceValue as RuntimeAnimatorController;
    foreach(var clip in ctrl.animationClips.Distinct().Where(c=>c.name.Contains("Reload")||c.name.Contains("Shotgun")||c.name.Contains("_Fire")))b.AppendLine("CLIP "+clip.name+" length="+clip.length+" events="+string.Join(",",AnimationUtility.GetAnimationEvents(clip).Select(e=>e.time+":"+e.functionName)));
   }
  }finally {PrefabUtility.UnloadPrefabContents(player);}
  var city=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Escenarios/Escenario_2.prefab");try{var surf=city.GetComponent<NavMeshSurface>();b.AppendLine("AGENT "+JsonUtility.ToJson(surf.GetBuildSettings()));var config=city.GetComponentInChildren<ConfiguracionEscenarioJugable>(true);b.AppendLine("CITYPOS "+city.transform.position+" SPAWN "+config.SpawnJugador.position);b.AppendLine("NAVPATH "+AssetDatabase.GetAssetPath(surf.navMeshData));}finally{PrefabUtility.UnloadPrefabContents(city);}
  File.WriteAllText(Path.Combine(Output,"details.txt"),b.ToString());
 }
 public static void Inspect() {
  Directory.CreateDirectory(Output); var b=new StringBuilder();
  var player=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
  try {
   foreach(var c in player.GetComponentsInChildren<Component>(true)) {
    if(!c) {b.AppendLine("MISSING SCRIPT");continue;}
    if(c is Transform)continue;
    string type=c.GetType().Name;
    if(c is Animator a) {b.AppendLine("ANIMATOR "+PathOf(a.transform)+" | "+AssetDatabase.GetAssetPath(a.runtimeAnimatorController));continue;}
    if(c is Renderer r) {b.AppendLine("MESH "+PathOf(r.transform)+" | "+(r is SkinnedMeshRenderer sk?AssetDatabase.GetAssetPath(sk.sharedMesh):(r.GetComponent<MeshFilter>() ? AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh) : "")));continue;}
    if(new[]{"Weapon","WeaponSwitcher","ThirdPersonAnimatorSync","ThirdPersonSync","NetworkPlayerSetup","NetworkAnimator","WeaponAnimationEvents","PlayerNameplate","PlayerCharacter"}.Contains(type)) {
      b.AppendLine("COMPONENT "+type+" | "+PathOf(c.transform));b.AppendLine(EditorJsonUtility.ToJson(c,true));
    }
   }
  } finally {PrefabUtility.UnloadPrefabContents(player);}
  foreach(string path in AssetDatabase.FindAssets("t:AnimatorController",new[]{"Assets/Animations"}).Select(AssetDatabase.GUIDToAssetPath)) {
    var ctrl=AssetDatabase.LoadAssetAtPath<AnimatorController>(path); b.AppendLine("CONTROLLER "+path);b.AppendLine(string.Join(",",ctrl.parameters.Select(p=>p.name+":"+p.type)));
    foreach(var layer in ctrl.layers) {b.AppendLine("LAYER "+layer.name+" mask="+AssetDatabase.GetAssetPath(layer.avatarMask)+" blend="+layer.blendingMode);States(layer.stateMachine,b,"");}
  }
  foreach(string path in AssetDatabase.FindAssets("t:AnimatorOverrideController",new[]{"Assets/Animations"}).Select(AssetDatabase.GUIDToAssetPath)) {
   var ctrl=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);b.AppendLine("OVERRIDE "+path);var pairs=new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip,AnimationClip>>();ctrl.GetOverrides(pairs);
   foreach(var pair in pairs)b.AppendLine(pair.Key.name+" => "+(pair.Value?pair.Value.name:"BASE"));
  }
  foreach(string path in new[]{"Assets/Prefabs/Escenarios/Escenario_2.prefab","Assets/Prefabs/Escenarios/EscenarioDef.prefab"}) {
   if(!File.Exists(path))continue;var root=PrefabUtility.LoadPrefabContents(path);
   try {b.AppendLine("CITY "+path);foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==root.transform || t.name.Contains("Escaler") || t.name.Contains("Stair")))b.AppendLine("GROUP "+PathOf(t)+" position="+t.position);
    foreach(var surface in root.GetComponentsInChildren<NavMeshSurface>(true))b.AppendLine("SURFACE "+PathOf(surface.transform)+" "+EditorJsonUtility.ToJson(surface,true));
    foreach(var mod in root.GetComponentsInChildren<NavMeshModifier>(true))b.AppendLine("MOD "+PathOf(mod.transform)+" "+EditorJsonUtility.ToJson(mod));
   }finally {PrefabUtility.UnloadPrefabContents(root);}
  }
  File.WriteAllText(Path.Combine(Output,"inspection.txt"),b.ToString());
 }
 public static string PathOf(Transform t) => t.parent?PathOf(t.parent)+"/"+t.name:t.name;
 static void States(AnimatorStateMachine sm,StringBuilder b,string prefix) {
  foreach(var item in sm.states) {var s=item.state;b.AppendLine("STATE "+prefix+s.name+" | motion="+AssetDatabase.GetAssetPath(s.motion)+" speed="+s.speed+" time="+s.timeParameter+":"+s.timeParameterActive+" additive="+(s.motion is AnimationClip clip?clip.isHumanMotion:false));
   if(s.motion is BlendTree tree)foreach(var c in tree.children)b.AppendLine(" BLEND "+c.motion?.name+" "+AssetDatabase.GetAssetPath(c.motion)+" pos="+c.position+" threshold="+c.threshold);
   foreach(var tr in s.transitions)b.AppendLine(" -> "+tr.destinationState?.name+" exit="+tr.hasExitTime+":"+tr.exitTime+" duration="+tr.duration+" conditions="+string.Join(",",tr.conditions.Select(c=>c.parameter+":"+c.mode+":"+c.threshold)));
  }
  foreach(var tr in sm.anyStateTransitions)b.AppendLine("ANY -> "+tr.destinationState?.name+" self="+tr.canTransitionToSelf+" conditions="+string.Join(",",tr.conditions.Select(c=>c.parameter+":"+c.mode+":"+c.threshold)));
  foreach(var child in sm.stateMachines)States(child.stateMachine,b,prefix+child.stateMachine.name+"/");
 }
}
#endif
