#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class FixSceneSpawn
{
 public static void Apply() {
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/MainSceneMultiPlayer.unity");
  var configs=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ConfiguracionEscenarioJugable>(true)).ToArray();
  string report="";
  foreach(var manager in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GestorEscenariosPartida>(true))) {
   var so=new SerializedObject(manager);var property=so.FindProperty("escenarioCabana");var current=property.objectReferenceValue as ConfiguracionEscenarioJugable;
   report+="Assigned cabin host spawn="+(current && current.SpawnJugador?current.SpawnJugador.position.ToString():"NULL")+Environment.NewLine;
   if(current && !current.SpawnJugador) {
    var valid=configs.Where(c=>c.TipoEscenario==current.TipoEscenario && c.transform.root==current.transform.root && c.SpawnJugador).ToArray();
    if(valid.Length!=1)throw new Exception("Cannot unambiguously resolve cabin spawn configuration");
    property.objectReferenceValue=valid[0];so.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    report+="Corrected manager reference; host spawn="+valid[0].SpawnJugador.position+Environment.NewLine;
   }
  }
  File.WriteAllText(Path.Combine(WeaponRevisionTools.Output,"scene-spawn-fix.txt"),report);
 }
}
#endif
