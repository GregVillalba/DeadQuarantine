#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.Networking;
using DeadQuarantine.Development;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class Phase12WildcardsBuild
{
 static string Output=>Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE12_OUTPUT");
 public static void Install(){
  var loadouts=AssetDatabase.LoadAssetAtPath<PvPvELoadoutCatalog>("Assets/PvPvE/Data/LoadoutCatalog.asset");
  var catalog=AssetDatabase.LoadAssetAtPath<PvPvEWildcardCatalog>("Assets/PvPvE/Data/Wildcards.asset");
  if(!catalog){catalog=ScriptableObject.CreateInstance<PvPvEWildcardCatalog>();catalog.entries=new[]{
   new PvPvEWildcardCatalog.Entry{id="wildcard.healing",name="Curación extra",description="Más unidades de tu curación seleccionada.",kind=PvPvEWildcard.ExtraHealing,icon=loadouts.healings[0].icon},
   new PvPvEWildcardCatalog.Entry{id="wildcard.grenades",name="Granadas extra",description="Más unidades de tu granada seleccionada.",kind=PvPvEWildcard.ExtraGrenades,icon=loadouts.grenades[0].icon},
   new PvPvEWildcardCatalog.Entry{id="wildcard.overkill",name="Overkill",description="Permite otra primaria en el segundo espacio.",kind=PvPvEWildcard.Overkill,icon=loadouts.weapons.First(w=>!w.secondary).icon},
   new PvPvEWildcardCatalog.Entry{id="wildcard.greed",name="Perk Greed",description="Dos perks de la misma categoría. Máximo: dos.",kind=PvPvEWildcard.PerkGreed,icon=loadouts.perks.entries[0].icon}};
   AssetDatabase.CreateAsset(catalog,"Assets/PvPvE/Data/Wildcards.asset");}
  loadouts.wildcards=catalog;EditorUtility.SetDirty(loadouts);
  var settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");settings.ProtocolVersion=Math.Max(settings.ProtocolVersion,5012);EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
  for(int i=0;i<5;i++)if(!loadouts.Validate(loadouts.Default(i),out var error))throw new Exception(error);
 }
 public static void BuildAll(){Install();try{BuildGame();BuildMenu();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);}}
 public static void BuildGame(){
  EditorSceneManager.OpenScene("Assets/Tests/PvPvE/Phase11/PerksValidation.unity");
  foreach(var old in UnityEngine.Object.FindObjectsByType<PvPvEPhase11PerksDriver>(FindObjectsInactive.Include))UnityEngine.Object.DestroyImmediate(old.gameObject);
  var go=new GameObject("Phase12 native validation");go.AddComponent<PvPvEMatchmaking>();var driver=go.AddComponent<PvPvEPhase12WildcardsDriver>();
  driver.Settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");driver.PlayerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
  var grenade=driver.PlayerPrefab.GetComponentInChildren<GrenadeThrow>(true);var so=new SerializedObject(grenade);var types=so.FindProperty("tipos");
  driver.GrenadePrefabs=Enumerable.Range(0,types.arraySize).Select(i=>types.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue as GameObject).Append(so.FindProperty("grenadePrefab").objectReferenceValue as GameObject).Where(g=>g).Distinct().ToArray();
  SaveBuild("WildcardsValidation","Game/Wildcards.exe");
 }
 public static void BuildMenu(){
  var scene=EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
  foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)))if(b&&(b is NetworkBootstrap||b.GetType().Name=="CharacterRenameManager"||b.GetType().Name=="AchievementsManager"))b.enabled=false;
  new GameObject("Phase12 class validation").AddComponent<PvPvEPhase9SmokeDriver>();SaveBuild("WildcardsMenu","Menu/Classes.exe");
 }
 static void SaveBuild(string name,string relative){
  Directory.CreateDirectory("Assets/Tests/PvPvE/Phase12");AssetDatabase.Refresh();string scene="Assets/Tests/PvPvE/Phase12/"+name+".unity";EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),scene);
  string target=Path.Combine(Output,relative);Directory.CreateDirectory(Path.GetDirectoryName(target));string company=PlayerSettings.companyName,product=PlayerSettings.productName;
  try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase12 "+name;var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scene},target=BuildTarget.StandaloneWindows64,locationPathName=target,options=BuildOptions.Development});if(build.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Phase12 build failed");}
  finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
 }
}
#endif
