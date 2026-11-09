#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.Networking;
using DeadQuarantine.PvPvE.UI;
public static class PvPvEDevelopMigrationValidation {
 [Serializable] public class Result {public bool ok;public int weapons,characters,maps,checkedPrefabs;public string[] checks,issues,unresolvedGuids;}
 static readonly List<string> checks=new();static readonly List<string> issues=new();
 static void Check(bool condition,string message){if(condition)checks.Add(message);else issues.Add(message);}
 public static void Run(){var result=new Result();var setup=EditorSceneManager.GetSceneManagerSetup();try{
  var settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");Check(settings,"Settings asset");
  Check(settings&&settings.PlayerPrefab&&settings.RosterPrefab&&settings.GamePrefabs,"Network player, roster and prefab registration");
  if(settings){result.weapons=settings.Loadouts.weapons.Length;result.maps=settings.Maps.Length;result.characters=settings.Characters.VariantCount;Check(result.weapons==16,"16 migrated weapon definitions");Check(settings.HasValidMaps,"Three map configurations");Check(settings.Characters,"Existing character definitions");
   foreach(var asset in new[]{settings.PlayerPrefab,settings.RosterPrefab}){foreach(var tr in asset.GetComponentsInChildren<Transform>(true))Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject)==0,"No missing scripts: "+asset.name+"/"+tr.name);}
   var presentation=settings.PlayerPrefab.GetComponent<NetworkWeaponPresentation>();foreach(var gun in settings.Loadouts.weapons){var w=presentation.Weapons.SingleOrDefault(x=>x.Id==gun.id);Check(w!=null&&w.Source&&w.Model&&w.Controller&&gun.icon,"Weapon references: "+gun.id);}
  }
  foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/PvPvE","Assets/Generated/NewWeapons"})){var path=AssetDatabase.GUIDToAssetPath(guid);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Check(prefab,"Prefab loads: "+path);if(prefab){result.checkedPrefabs++;foreach(var tr in prefab.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject)>0)issues.Add("Missing script: "+path+"/"+tr.name);}}
  foreach(var path in new[]{"Assets/Scenes/PantallasUI.unity","Assets/PvPvE/Scenes/PvPvEMatch.unity","Assets/Scenes/MainSceneMultiPlayer.unity"}){var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);var roots=scene.GetRootGameObjects();Check(scene.IsValid()&&scene.isLoaded,"Scene loads: "+path);if(path.Contains("PantallasUI")){Check(roots.Any(r=>r.GetComponentInChildren<PvPvEModeMenu>(true)),"Main menu has PvPvE entry");Check(roots.Any(r=>r.GetComponentInChildren<PvPvEMatchmaking>(true)),"Main menu has matchmaking");}if(path.Contains("PvPvEMatch")){Check(roots.Any(r=>r.GetComponentInChildren<PvPvEMapContext>(true)),"Match map context");Check(roots.SelectMany(r=>r.GetComponentsInChildren<ConfiguracionEscenarioJugable>(true)).Select(c=>c.TipoEscenario).Distinct().Count()>=3,"Match contains three scenarios");}if(path.Contains("MainSceneMultiPlayer"))Check(roots.Any(r=>r.GetComponentInChildren<GestorEscenariosPartida>(true)?.UsaEscenasAditivasMultiplayer==true),"Develop additive multiplayer map flow preserved");}
  Check(EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path=="Assets/PvPvE/Scenes/PvPvEMatch.unity"),"PvPvE included in build");
 }catch(Exception e){issues.Add(e.ToString());}finally{if(setup.Length>0&&setup.Any(s=>s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
 result.unresolvedGuids=File.ReadAllLines(Environment.GetEnvironmentVariable("DQ_MIGRATION_GUIDS")).Where(g=>string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(g))).ToArray();result.ok=issues.Count==0;result.checks=checks.ToArray();result.issues=issues.ToArray();File.WriteAllText(Environment.GetEnvironmentVariable("DQ_MIGRATION_RESULT"),JsonUtility.ToJson(result,true));}if(!result.ok)throw new Exception("Migration validation failed: "+string.Join("; ",issues));Debug.Log("PvPvE develop migration references verified");
 }
}
#endif
