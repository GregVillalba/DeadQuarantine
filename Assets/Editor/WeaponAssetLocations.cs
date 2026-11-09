#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// Keeps historical editor tools working after assets are moved; no runtime code depends on this map.
public static class WeaponAssetLocations {
 [Serializable] public class Entry {public string old,guid;}
 [Serializable] public class Map {public Entry[] entries;}
 static Dictionary<string,string> aliases;
 public static string Resolve(string path){
  if(string.IsNullOrEmpty(path))return path;string normalized=path.Replace('\\','/');string project=Directory.GetParent(Application.dataPath).FullName.Replace('\\','/')+"/";bool absolute=normalized.StartsWith(project,StringComparison.OrdinalIgnoreCase);string key=absolute?normalized.Substring(project.Length):normalized;
  const string legacy="weapon-assets:";if(key!=legacy&&!key.StartsWith(legacy+"/",StringComparison.Ordinal))return path;
  if(key.EndsWith(".meta",StringComparison.Ordinal))return Resolve(path.Substring(0,path.Length-5))+".meta";
  if(aliases==null){var map=JsonUtility.FromJson<Map>(File.ReadAllText("Assets/Editor/WeaponAssetLocations.json"));aliases=map.entries.ToDictionary(e=>e.old,e=>e.guid);}
  string destination=null;if(aliases.TryGetValue(key,out string guid))destination=AssetDatabase.GUIDToAssetPath(guid);
  if(string.IsNullOrEmpty(destination)){
   string name=Path.GetFileName(key),ext=Path.GetExtension(key).ToLowerInvariant(),folder="Assets/Animations";
   if(ext==".prefab")folder=key.Contains("/HUD/")?"Assets/Prefabs/UI":"Assets/Prefabs/Weapons/ThirdPerson";
   else if(ext==".anim")folder="Assets/Animations/CoopPresentation";
   else if(ext==".overridecontroller")folder=name.Contains("_TP")?"Assets/Animations/OverrideThirdPerson":"Assets/Animations/OverrideFirstPerson";
   else if(ext==".fbx")folder="Assets/Art/Animations/Character";
   else if(ext==".mat")folder="Assets/Art/Materials/Attachments";
   else if(ext==".asset")folder="Assets/Data";
   else if(ext==".png")folder="Assets/Art/Sprites/Icons/Weapons";
   else if(ext==".txt"||key.Contains("/Backup"))folder="Assets/Editor";
   if(string.IsNullOrEmpty(ext))destination=key.Contains("Source431")?"Assets/Art/Animations/Character":key.EndsWith("HUD")?"Assets/Prefabs/UI":key.EndsWith("ThirdPerson")?"Assets/Prefabs/Weapons/ThirdPerson":key.EndsWith("Tienda")?"Assets/Art/Sprites/Icons/Weapons":folder;
   else destination=folder+"/"+Path.GetFileNameWithoutExtension(name)+"_"+Hash128.Compute(key).ToString().Substring(0,8)+ext;
  }
  return absolute?project+destination:destination;
 }
}
#endif
