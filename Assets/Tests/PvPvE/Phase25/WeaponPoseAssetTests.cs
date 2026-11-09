using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class WeaponPoseAssetTests
{
    static GameObject Player => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
    static AnimatorController Controller(RuntimeAnimatorController c) =>
        (AnimatorController)(c is AnimatorOverrideController o ? o.runtimeAnimatorController : c);

    [Test]
    public void EveryEquipmentVariantHasValidRestStatesFromItsOwnController()
    {
        var data = new SerializedObject(Player.GetComponentInChildren<WeaponSwitcher>(true));
        var slots = data.FindProperty("slots");
        Assert.AreEqual(16, slots.arraySize);
        for (int i = 0; i < slots.arraySize; i++)
        {
            var slot = slots.GetArrayElementAtIndex(i);
            var weapon = (Weapon)slot.FindPropertyRelative("weaponComponent").objectReferenceValue;
            var profile = weapon.GetComponent<PackWeaponPresentation>();
            var current = (RuntimeAnimatorController)slot.FindPropertyRelative("overrideController").objectReferenceValue;
            if (!current) current = (RuntimeAnimatorController)data.FindProperty("baseController").objectReferenceValue;
            var layers = Controller(current).layers;
            CollectionAssert.AreEqual(layers.Select(l => Animator.StringToHash(l.name + "." + l.stateMachine.defaultState.name)),
                profile.firstPersonRestStates, slot.FindPropertyRelative("weaponId").stringValue);
        }
    }

    [Test]
    public void InactiveActionLayersCannotWriteBindPoseOnTheThirteenNewWeapons()
    {
        foreach (var definition in Player.GetComponent<NetworkWeaponPresentation>().Weapons)
        {
            var profile = definition.Source.GetComponent<PackWeaponPresentation>();
            if (profile.preserveLegacyAnimation) continue;
            foreach (var layer in Controller(definition.Controller).layers.Skip(1))
            {
                var state = layer.stateMachine.defaultState;
                if (state.motion) continue;
                Assert.AreEqual(0, layer.defaultWeight, definition.Id + "/" + layer.name);
                Assert.IsFalse(state.writeDefaultValues);
                Assert.IsTrue(state.behaviours.OfType<WeaponActionLayerWeight>().Any());
            }
        }
    }

    [Test]
    public void BareRifleSupportTargetsStayUnderTheGuardAndProtectedDefaultsStayAuthored()
    {
        foreach (var definition in Player.GetComponent<NetworkWeaponPresentation>().Weapons)
        {
            var profile = definition.Source.GetComponent<PackWeaponPresentation>();
            if (profile.preserveLegacyAnimation) Assert.IsFalse(profile.bareSupportPoseEnabled, definition.Id);
            if (!profile.bareSupportPoseEnabled) continue;
            foreach (var pose in new[] { profile.bareFirstPerson, profile.bareThirdPerson })
            {
                Assert.Less(Mathf.Abs(pose.position.x), .08f, definition.Id + " lateral wrist");
                Assert.Less(pose.position.z, .03f, definition.Id + " wrist above barrel");
                Assert.Greater(pose.fingers.Length, 0, definition.Id + " fingers");
            }
        }
    }
}
