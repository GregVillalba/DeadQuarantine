using System;using System.Linq;using NUnit.Framework;using UnityEditor;using UnityEngine;using DeadQuarantine.PvPvE;using DeadQuarantine.PvPvE.Networking;
public class PvPvEFollowupAssetTests {
 PvPvELoadoutCatalog Catalog=>AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset").Loadouts;
 [Test]public void EveryWeaponHasOriginalWhiteBodyAndCategory(){foreach(var gun in Catalog.weapons){Assert.IsNotNull(gun.whiteBody,gun.id);Assert.IsFalse(AssetDatabase.GetAssetPath(gun.whiteBody).Contains("Art/Equipment"),gun.id);Assert.IsTrue(Enum.IsDefined(typeof(PvPvELoadoutCatalog.Gun.Family),gun.family));}Assert.AreEqual("SUBFUSIL",Catalog.Find("P90").Category);Assert.AreEqual("SNIPER",Catalog.Find("Sniper01").Category);}
 [Test]public void EveryCompatibleAccessoryHasAnAlignedOriginalOverlay(){foreach(var entry in Catalog.attachments.entries)foreach(var id in entry.weapons){var overlay=Array.Find(entry.weaponIcons,x=>x.weapon==id)?.icon;Assert.IsNotNull(overlay,id+"/"+entry.id);Assert.IsFalse(AssetDatabase.GetAssetPath(overlay).Contains("Art/Equipment"),id+"/"+entry.id);}}
 [Test]public void PreviewMaterialDoesNotDependOnSceneLights(){var m=Resources.Load<Material>("PvPvE/ModelPreview");Assert.IsNotNull(m);Assert.AreEqual("Universal Render Pipeline/Unlit",m.shader.name);}
}
