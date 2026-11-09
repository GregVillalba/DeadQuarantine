#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using DeadQuarantine.PvPvE;using DeadQuarantine.PvPvE.Networking;
public static class PvPvEFollowupAssets {
 public static void Prepare(){
 var settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");
 var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");var presentation=player.GetComponent<NetworkWeaponPresentation>();var switcher=player.GetComponentInChildren<WeaponSwitcher>(true);
 foreach(var gun in settings.Loadouts.weapons){var definition=presentation.Weapons.Single(x=>x.Id==gun.id);var source=definition.Source.GetComponent<WeaponAttachments>();
 gun.whiteBody=source.BodySprite;if(!gun.whiteBody)gun.whiteBody=ImageWeapon.FindBodySprite(switcher.GetIconObject(Array.FindIndex(presentation.Weapons,d=>d.Id==gun.id)));
 gun.whiteMagazine=source.MagazineSprite;gun.whiteDefaultSight=source.ScopeDefaultSprite;
 if(!gun.whiteBody)throw new Exception("Missing original white weapon body: "+gun.id);
 gun.family=gun.secondary?PvPvELoadoutCatalog.Gun.Family.Pistola:gun.id=="Escopeta"?PvPvELoadoutCatalog.Gun.Family.Escopeta:gun.id.StartsWith("Sniper")?PvPvELoadoutCatalog.Gun.Family.Sniper:new[]{"P90","Vector","MAC10","SMG02","SMG05"}.Contains(gun.id)?PvPvELoadoutCatalog.Gun.Family.Subfusil:PvPvELoadoutCatalog.Gun.Family.Fusil;
 }
 var paths=Directory.EnumerateFiles("Assets/Art/Sprites/Icons/Weapons","*.png",SearchOption.AllDirectories).GroupBy(x=>Path.GetFileNameWithoutExtension(x)).ToDictionary(x=>x.Key,x=>x.First().Replace('\\','/'));
 foreach(var entry in settings.Loadouts.attachments.entries){var icons=entry.weaponIcons.ToList();foreach(var id in entry.weapons){if(icons.Any(x=>x.weapon==id&&x.icon))continue;
  var gun=settings.Loadouts.Find(id);string model=id=="Handgun"?"SM_Handgun_03":id=="Rifle"?"SM_AR_01":id=="Escopeta"?"SM_Shotgun_01":Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(gun.model));string key="T_HUD_"+(model.StartsWith("SM_")?model.Substring(3):model)+"_"+entry.id.Replace("Muzzle_","")+"_D";
  Sprite icon=paths.TryGetValue(key,out string iconPath)?AssetDatabase.LoadAssetAtPath<Sprite>(iconPath):null;
  if(!icon)throw new Exception("Missing aligned white attachment layer: "+id+" / "+entry.id+" / "+key);
  icons.RemoveAll(x=>x.weapon==id);icons.Add(new WeaponAttachmentCatalog.WeaponIcon{weapon=id,icon=icon});
 }entry.weaponIcons=icons.ToArray();}EditorUtility.SetDirty(settings.Loadouts.attachments);
 EditorUtility.SetDirty(settings.Loadouts);
 const string materialPath="Assets/Resources/PvPvE/ModelPreview.mat";if(!AssetDatabase.LoadAssetAtPath<Material>(materialPath)){var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(material,materialPath);}
 AssetDatabase.SaveAssets();
 }
 public static void BuildFinalReviews(){WeaponPoseModeBuild.Build();Phase25WeaponsValidation.Build();}
 public static void PrepareAndBuild(){Prepare();WeaponPoseModeBuild.Build();}
}
#endif
