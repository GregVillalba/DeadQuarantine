using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using DeadQuarantine.PvPvE.Networking;

public class PvPvEInterfaceAssetTests
{
    static PvPvENetworkSettings Settings => AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");

    [Test]
    public void EquipmentCardsReferenceWholeModelRenders()
    {
        foreach (var gun in Settings.Loadouts.weapons)
        {
            Assert.IsNotNull(gun.model, gun.id);
            Assert.IsNotNull(gun.icon, gun.id);
            StringAssert.StartsWith("Assets/PvPvE/Art/Equipment/", AssetDatabase.GetAssetPath(gun.icon));
            Assert.GreaterOrEqual(gun.icon.rect.width, 256, gun.id);
        }
        foreach (var accessory in Settings.Loadouts.attachments.entries)
        {
            Assert.IsNotNull(accessory.model, accessory.id);
            Assert.IsNotNull(accessory.icon, accessory.id);
            StringAssert.StartsWith("Assets/PvPvE/Art/Equipment/", AssetDatabase.GetAssetPath(accessory.icon));
        }
    }

    [Test]
    public void FiveSuppliedPerkIconsAreSeparateFromOrdinaryModeIcons()
    {
        Assert.AreEqual(5, Settings.Loadouts.perks.entries.Length);
        foreach (var perk in Settings.Loadouts.perks.entries)
        {
            Assert.IsNotNull(perk.pvpveIcon, perk.id);
            Assert.AreNotEqual(perk.icon, perk.pvpveIcon, perk.id);
            StringAssert.StartsWith("Assets/PvPvE/Art/RedesignPerks/", AssetDatabase.GetAssetPath(perk.pvpveIcon));
        }
    }

    [Test]
    public void EveryMapOwnsThreeRealScenePhotographs()
    {
        Assert.AreEqual(3, Settings.Maps.Length);
        foreach (var map in Settings.Maps)
        {
            Assert.AreEqual(3, map.LoadingViews.Length, map.Name);
            Assert.AreEqual(3, map.LoadingViews.Distinct().Count(), map.Name);
            Assert.AreEqual(map.LoadingViews[1], map.Preview);
            foreach (var photo in map.LoadingViews)
            {
                Assert.IsNotNull(photo, map.Name);
                StringAssert.StartsWith("Assets/PvPvE/Art/LoadingViews/" + map.Id + "_", AssetDatabase.GetAssetPath(photo));
            }
        }
    }

    [Test]
    public void AuthorityRejectsUnsupportedPistolAndSubmachineGunCombinations()
    {
        var catalog = Settings.Loadouts.attachments;
        foreach (var id in new[] { "Handgun", "AutoPistol", "Handgun01", "Handgun02" })
            foreach (var entry in catalog.entries.Where(e => e.kind == WeaponAttachmentKind.Support || e.id == "Scope_04" || e.id == "Scope_05" || e.id == "Muzzle_Silencer_03"))
                Assert.IsFalse(catalog.Allows(id, entry.kind, entry.id), id + "/" + entry.id);
        foreach (var id in new[] { "P90", "Vector", "MAC10", "SMG02", "SMG05" })
            foreach (var entry in catalog.entries.Where(e => e.requiresBracing))
                Assert.IsFalse(catalog.Allows(id, entry.kind, entry.id), id + "/" + entry.id);
    }

    [Test]
    public void PistolServerReloadWindowsMatchTheAuthoredAnimations()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        var data = new SerializedObject(player.GetComponentInChildren<WeaponSwitcher>(true));
        var slots = data.FindProperty("slots");
        foreach (var id in new[] { "AutoPistol", "Handgun01", "Handgun02" })
        {
            var slot = Enumerable.Range(0, slots.arraySize).Select(slots.GetArrayElementAtIndex).Single(s => s.FindPropertyRelative("weaponId").stringValue == id);
            var weapon = (Weapon)slot.FindPropertyRelative("weaponComponent").objectReferenceValue;
            var wrapper = (AnimatorOverrideController)slot.FindPropertyRelative("overrideController").objectReferenceValue;
            var controller = (AnimatorController)wrapper.runtimeAnimatorController;
            var states = controller.layers.Single(l => l.name == "Reload").stateMachine.states;
            foreach (var empty in new[] { false, true })
            {
                var state = states.Single(s => s.state.name == (empty ? "Reload Empty" : "Reload")).state;
                var clip = (AnimationClip)state.motion;
                float seconds = clip.length / state.speed / weapon.PresentationReloadSpeed(empty);
                var gun = Settings.Loadouts.Find(id);
                Assert.AreEqual(seconds, empty ? gun.emptyReloadSeconds : gun.reloadSeconds, .05f, id);
            }
        }
    }
}
