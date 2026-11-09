using System;using System.Linq;using NUnit.Framework;using UnityEditor;using UnityEngine;using DeadQuarantine.PvPvE;using DeadQuarantine.PvPvE.Networking;
public class PvPvECaptureAssetTests {
 PvPvELoadoutCatalog Catalog=>AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset").Loadouts;
 [Test] public void OverlaysRetainTheOriginalSharedCanvas(){
  foreach(var gun in Catalog.weapons){var sprites=new[]{gun.whiteBody,gun.whiteMagazine,gun.whiteDefaultSight}.Where(s=>s);foreach(var sprite in sprites)Assert.AreEqual(gun.whiteBody.rect.size,sprite.rect.size,gun.id+"/"+sprite.name);}
  foreach(var entry in Catalog.attachments.entries)foreach(var icon in entry.weaponIcons)Assert.AreEqual(Catalog.Find(icon.weapon).whiteBody.rect.size,icon.icon.rect.size,icon.weapon+"/"+entry.id);
 }
 [Test] public void ExistingPlayerIconsKeepTheirSpriteReferences(){
  var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");var definitions=player.GetComponent<NetworkWeaponPresentation>().Weapons;
  foreach(var gun in Catalog.weapons){var source=definitions.Single(d=>d.Id==gun.id).Source.GetComponent<WeaponAttachments>();Assert.IsNotNull(source.BodySprite,gun.id);if(gun.whiteDefaultSight)Assert.IsNotNull(source.ScopeDefaultSprite,gun.id+" default sight");}
 }
 [Test] public void SuppliedIconsAndUrpScopeMaterialsAreAssigned(){
  Assert.IsTrue(AssetDatabase.GetAssetPath(Catalog.wildcards.entries.Single(w=>w.kind==PvPvEWildcard.Perk1Greed).icon).Contains("RedesignPerks"));
  Assert.IsNotNull(Resources.Load<Sprite>("PvPvE/Headshot"));Assert.IsNotNull(Resources.Load<Material>("PvPvE/WhiteIcon"));
  foreach(var id in new[]{"Scope_05","Scope_06","Scope_07","Scope_08"})Assert.IsTrue(Catalog.attachments.Find(id).magnifiedLens,id);
  Assert.AreEqual("Universal Render Pipeline/Unlit",Catalog.attachments.scopeLensMaterial.shader.name);Assert.IsNotNull(Catalog.attachments.scopeReticleMaterial.GetTexture("_BaseMap"));
 }
 [Test] public void MerchantFlashlightHasRealPriceAndIsAvailableInOriginalModes(){
  var merchant=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mercader.prefab").GetComponentInChildren<NPCWeaponVendor>(true);var offer=merchant.Offers.Single(o=>o.esLinterna);
  Assert.IsTrue(offer.disponible);Assert.AreEqual(CategoriaTienda.Equipamiento,offer.categoria);Assert.AreEqual(DisponibilidadTienda.TodosLosModos,offer.modos);
  Assert.Greater(Enum.GetValues(typeof(TipoRecurso)).Cast<TipoRecurso>().Sum(t=>offer.Precio.Cantidad(t)),0);Assert.IsNotNull(offer.imagenTarjeta);
 }
 [Test] public void IndependentFlashlightReusesExistingCompatibleModel(){
  var only=Catalog.attachments.Find("Flashlight_01");var combined=Catalog.attachments.Find("Laser_Flashlight_01");Assert.IsNotNull(only);Assert.IsTrue(only.flashlight);Assert.IsFalse(only.laser);Assert.AreEqual(combined.model,only.model);CollectionAssert.AreEquivalent(combined.weapons,only.weapons);
 }

}
