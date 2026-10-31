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
 [Test] public void PlayerPrefabHasAllThreeRealWeaponDefinitions() {
  var player=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
  try {
   var presentation=player.GetComponent<NetworkWeaponPresentation>();Assert.IsNotNull(presentation);Assert.AreEqual(3,presentation.Weapons.Length);
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
}
