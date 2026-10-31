#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static partial class OriginalCityWeaponsTools
{
 public static void BuildRevisionValidation() {
  ConfigureWeapons();
  var b=new StringBuilder();
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
  foreach(var definition in prefab.GetComponent<NetworkWeaponPresentation>().Weapons) {
   var model=UnityEngine.Object.Instantiate(definition.Model);model.SetActive(true);
   try {
    var animator=model.GetComponent<Animator>();animator.Rebind();animator.Update(0);
    var root=model.GetComponentsInChildren<Transform>().First(t=>t.name=="root");var skeleton=root.parent;
    var initial=root.localRotation;var skeletonRotation=skeleton.localRotation;
    foreach(string action in new[]{"Fire","Reload","Reload Open","Reload Insert","Reload Close","Bolt Action"}) {
     if(!animator.HasState(0,Animator.StringToHash(action)))continue;
     foreach(float time in new[]{0f,.2f,.6f,.95f}) {
      animator.Play(action,0,time);animator.Update(0);
      if(Quaternion.Angle(initial,root.localRotation)>.01f || Quaternion.Angle(skeletonRotation,skeleton.localRotation)>.01f)throw new Exception("Weapon root rotates during "+definition.Id+" "+action);
     }
    }
    b.AppendLine("PASS "+definition.Id+": mechanical animation preserves weapon attachment rotation");
   }finally{UnityEngine.Object.DestroyImmediate(model);}
  }
  File.WriteAllText(Path.Combine(Output,"revision-validation.txt"),b.ToString());
  InspectOriginalSpawns();
  BuildValidationPlayer();
 }
 public static void InspectOriginalSpawns() {
  var b=new StringBuilder();
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/MainSceneMultiPlayer.unity");
  foreach(var root in scene.GetRootGameObjects())foreach(var config in root.GetComponentsInChildren<ConfiguracionEscenarioJugable>(true)) {
   b.AppendLine("CONFIG "+config.name+" type="+config.TipoEscenario+" root="+config.transform.root.name+" position="+config.transform.root.position+" renderers="+config.transform.root.GetComponentsInChildren<Renderer>(true).Length);
   for(int i=0;i<4;i++){var spawn=config.ObtenerSpawnJugador(i);b.AppendLine("  spawn"+i+" "+(spawn?AnimationUtility.CalculateTransformPath(spawn,null)+" world="+spawn.position:"NULL"));}
  }
  foreach(var root in scene.GetRootGameObjects())foreach(var manager in root.GetComponentsInChildren<GestorEscenariosPartida>(true)) {
   var so=new SerializedObject(manager);foreach(string field in new[]{"escenarioLaboratorio","escenarioCiudad","escenarioCabana"}){var config=so.FindProperty(field).objectReferenceValue as ConfiguracionEscenarioJugable;b.AppendLine("MANAGER "+field+"="+(config?AnimationUtility.CalculateTransformPath(config.transform,null):"NULL"));}
  }
  File.WriteAllText(Path.Combine(Output,"original-scene-spawns.txt"),b.ToString());
 }
}
#endif
