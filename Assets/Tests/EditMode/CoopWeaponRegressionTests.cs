using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;
public class CoopWeaponRegressionTests
{
 GameObject root;Weapon weapon;
 void Set(string name,object value)=>typeof(Weapon).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(weapon,value);
 T Get<T>(string name)=>(T)typeof(Weapon).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(weapon);
 [SetUp] public void Setup(){root=new GameObject("Weapon regression");root.SetActive(false);weapon=root.AddComponent<Weapon>();Set("maxAmmo",10);Set("currentAmmo",3);Set("isReloading",true);Set("usesMultiPartReload",true);Set("shellLoading",true);}
 [TearDown] public void Cleanup(){Object.DestroyImmediate(root);}
 [Test] public void MultipartMagazineEventDoesNotFillAllShells(){weapon.AnimationAmmunitionFill();Assert.AreEqual(3,weapon.CurrentAmmo);}
 [Test] public void MultipartCompletionKeepsOnlyInsertedShells(){weapon.AnimationReloadFinished();Assert.AreEqual(3,weapon.CurrentAmmo);Assert.IsFalse(weapon.IsReloading);}
 [Test] public void DuplicateInsertEventAddsOnlyOneShell(){weapon.AnimationInsertOneShell();weapon.AnimationInsertOneShell();Assert.AreEqual(4,weapon.CurrentAmmo);}
 [Test] public void MagazineReloadStillFillsNormally(){Set("usesMultiPartReload",false);weapon.AnimationReloadFinished();Assert.AreEqual(10,weapon.CurrentAmmo);}
 [Test] public void ReloadDuringPumpIsQueued(){Set("isReloading",false);Set("isChambering",true);typeof(Weapon).GetMethod("OnReload",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(weapon,new object[]{default(InputAction.CallbackContext)});Assert.IsFalse(weapon.IsReloading);Assert.IsTrue(Get<bool>("pendingReload"));}
 [Test] public void PlayerPrefabKeepsOriginalThreeAndAllCatalogWeaponDefinitions() {
  var player=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
  try {
   var presentation=player.GetComponent<NetworkWeaponPresentation>();Assert.IsNotNull(presentation);var catalog=AssetDatabase.LoadAssetAtPath<DeadQuarantine.PvPvE.PvPvELoadoutCatalog>("Assets/PvPvE/Data/LoadoutCatalog.asset");Assert.IsNotNull(catalog);Assert.AreEqual(catalog.weapons.Length,presentation.Weapons.Length);
   foreach(var id in new[]{"Handgun","Rifle","Escopeta"})Assert.IsTrue(System.Array.Exists(presentation.Weapons,w=>w.Id==id),id);
   Assert.IsNotNull(presentation.Body.GetComponent<OwnerNetworkAnimator>());
   foreach(var entry in presentation.Weapons) {
    Assert.IsNotNull(entry.Source);Assert.IsNotNull(entry.Model);Assert.IsNotNull(entry.Controller);Assert.IsNotNull(entry.Casing);
    var sourceMeshes=entry.Source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    foreach(var source in sourceMeshes)Assert.IsTrue(System.Array.Exists(entry.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true),r=>r.sharedMesh==source.sharedMesh));
    var muzzle=entry.Model.GetComponentInChildren<Muzzle>(true);Assert.IsNotNull(muzzle);
    Assert.IsNotNull(new SerializedObject(muzzle).FindProperty("flashParticlesPrefab").objectReferenceValue);
   }
  }finally {PrefabUtility.UnloadPrefabContents(player);}
 }
 [Test] public void ShotgunSprintUsesSprintStateEvenBelowSevenMetersPerSecond() {
  var player=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
  try {
   var switcher=player.GetComponentInChildren<WeaponSwitcher>(true);var config=new SerializedObject(switcher);var animator=(Animator)config.FindProperty("weaponAnimator").objectReferenceValue;
   for(var t=animator.transform;t!=null;t=t.parent)t.gameObject.SetActive(true);
   animator.runtimeAnimatorController=(RuntimeAnimatorController)config.FindProperty("slots").GetArrayElementAtIndex(2).FindPropertyRelative("overrideController").objectReferenceValue;
   animator.Rebind();animator.Update(0);animator.SetBool("Running",true);animator.SetFloat("Speed",6);animator.Update(.1f);animator.Update(.8f);
   int layer=animator.GetLayerIndex("MovementActions");Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(layer).IsName("Running"));
   var expected=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/CoopPresentation/Shotgun_Run_Additive.anim");Assert.IsNotNull(expected);Assert.IsTrue(expected.isLooping);
   Assert.IsTrue(System.Array.Exists(animator.GetCurrentAnimatorClipInfo(layer),info=>info.clip==expected));
  }finally {PrefabUtility.UnloadPrefabContents(player);}
 }

 [TestCase(false,false,"Tutorial")]
 [TestCase(true,false,"Normal")]
 [TestCase(true,true,"LimitedDifficulty")]
 public void AllWeaponsFollowTheCurrentModesReserveRule(bool hasRoundSettings,bool limited,string mode) {
  var roundInstance=typeof(RoundManager).GetField("<Instance>k__BackingField",BindingFlags.NonPublic|BindingFlags.Static);
  var difficultyInstance=typeof(DifficultyManager).GetField("<Instance>k__BackingField",BindingFlags.NonPublic|BindingFlags.Static);
  var previousRound=roundInstance.GetValue(null);var previousDifficulty=difficultyInstance.GetValue(null);
  var roundRoot=new GameObject("Ammo policy round");roundRoot.SetActive(false);
  var difficultyRoot=new GameObject("Ammo policy difficulty");difficultyRoot.SetActive(false);
  var rules=ScriptableObject.CreateInstance<DifficultySettings>();rules.level=DifficultyLevel.Normal;rules.limitedReserveAmmo=limited;rules.reserveAmmoMultiplier=0.5f;
  var player=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
  try {
   var manager=difficultyRoot.AddComponent<DifficultyManager>();typeof(DifficultyManager).GetField("difficulties",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(manager,new[]{rules});
   difficultyInstance.SetValue(null,manager);roundInstance.SetValue(null,hasRoundSettings?roundRoot.AddComponent<RoundManager>():null);
   foreach(var definition in player.GetComponent<NetworkWeaponPresentation>().Weapons) {
    var gun=definition.Source;Assert.AreEqual(limited,gun.UsesLimitedReserve,mode+" "+definition.Id);
    if(limited){var serialized=new SerializedObject(gun);int capacity=serialized.FindProperty("reserveAmmoCapacity").intValue;bool forced=serialized.FindProperty("forceReserveLimit").boolValue;Assert.AreEqual(forced?capacity:Mathf.RoundToInt(capacity*rules.reserveAmmoMultiplier),gun.ReserveCapacity,"Existing limited-mode capacity "+definition.Id);}
    for(int reload=0;reload<2;reload++) {
     gun.RestaurarMunicion(0,0);
     typeof(Weapon).GetField("reserveInitialized",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(gun,true);
     typeof(Weapon).GetMethod("FillMagazine",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(gun,null);
     Assert.AreEqual(limited?0:gun.CapacidadCargador,gun.CurrentAmmo,mode+" empty-reserve reload "+definition.Id);
    }
   }
  } finally {
   PrefabUtility.UnloadPrefabContents(player);Object.DestroyImmediate(roundRoot);Object.DestroyImmediate(difficultyRoot);Object.DestroyImmediate(rules);
   roundInstance.SetValue(null,previousRound);difficultyInstance.SetValue(null,previousDifficulty);
  }
 }
}
