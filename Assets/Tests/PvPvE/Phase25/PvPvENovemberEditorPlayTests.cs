using System.Linq;
using UnityEditor.Animations;
using System.Reflection;using NUnit.Framework;using UnityEditor;using UnityEngine;using UnityEngine.UI;using TMPro;using DeadQuarantine.PvPvE.UI;
public sealed class PvPvENovemberEditorComponentTests {
 [Test] public void PreviewFrameAndScorePanelAddMissingComponentsInEditor(){
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;var host=new GameObject("Editor Preview",typeof(RectTransform),typeof(RawImage));var preview=host.AddComponent<PvPvEWeaponPreview>();var camera=new GameObject("Editor Preview Camera").AddComponent<Camera>();var holder=GameObject.CreatePrimitive(PrimitiveType.Cube);var rt=new RenderTexture(1000,600,24);
  var hud=new GameObject("HUD_Canvas",typeof(RectTransform));
  var status=new GameObject("AmmoPanel",typeof(RectTransform));
  status.transform.SetParent(hud.transform,false);
  var background=new GameObject("Background").AddComponent<Image>();
  background.transform.SetParent(status.transform,false);
  background.color=new Color(0,.25f,0,.62f);
  var panel=new GameObject("Puntaje",typeof(RectTransform),typeof(Image));
  panel.transform.SetParent(hud.transform,false);
  var frame=new GameObject("Image").AddComponent<Image>();
  frame.transform.SetParent(panel.transform,false);
  var row=new GameObject("FILA1",typeof(RectTransform));
  row.transform.SetParent(frame.transform,false);
  var label=new GameObject("puntajeNumero").AddComponent<TextMeshProUGUI>();
  label.transform.SetParent(row.transform,false);
  var score=panel.AddComponent<ScoreHUD>();
  try{typeof(PvPvEWeaponPreview).GetField("cameraView",flags).SetValue(preview,camera);typeof(PvPvEWeaponPreview).GetField("holder",flags).SetValue(preview,holder);typeof(PvPvEWeaponPreview).GetField("texture",flags).SetValue(preview,rt);typeof(PvPvEWeaponPreview).GetMethod("Frame",flags).Invoke(preview,null);Assert.IsTrue(camera.GetComponent<Light>());typeof(ScoreHUD).GetField("textoPuntaje",flags).SetValue(score,label);typeof(ScoreHUD).GetMethod("StyleScore",flags).Invoke(score,null);
   Assert.IsTrue(panel.GetComponent<CanvasGroup>());
   var styled=panel.transform.Find("Fondo de estado").GetComponent<Image>();
   Assert.AreEqual(background.color,styled.color);
   Assert.AreEqual(Color.clear,panel.GetComponent<Image>().color);
   Assert.Less(Quaternion.Angle(Quaternion.Euler(0,180,0),styled.rectTransform.localRotation),.01f);
   Assert.Less(Quaternion.Angle(Quaternion.Euler(0,0,-1.3f),label.rectTransform.parent.localRotation),.01f);
   Assert.AreEqual(0f,label.rectTransform.localEulerAngles.z,.01f);}
  finally{typeof(PvPvEWeaponPreview).GetField("cameraView",flags).SetValue(preview,null);typeof(PvPvEWeaponPreview).GetField("holder",flags).SetValue(preview,null);typeof(PvPvEWeaponPreview).GetField("texture",flags).SetValue(preview,null);Object.DestroyImmediate(host);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(holder);Object.DestroyImmediate(rt);Object.DestroyImmediate(hud);}
 }

 [TestCase("SemiRifle")]
 [TestCase("P90")]
 [TestCase("MAC10")]
 public void LivePlayerSprintClipAnimatesTheWeapon(string weaponId)
 {
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
  Assert.IsNotNull(prefab);
  var switcher=prefab.GetComponentInChildren<WeaponSwitcher>(true);
  Assert.IsNotNull(switcher);
  var slots=new SerializedObject(switcher).FindProperty("slots");
  var slot=Enumerable.Range(0,slots.arraySize).Select(slots.GetArrayElementAtIndex)
   .Single(item=>item.FindPropertyRelative("weaponId").stringValue==weaponId);
  var overrides=(AnimatorOverrideController)slot.FindPropertyRelative("overrideController").objectReferenceValue;
  var controller=(AnimatorController)overrides.runtimeAnimatorController;
  var state=controller.layers.Single(layer=>layer.name=="MovementActions")
   .stateMachine.states.Single(child=>child.state.name=="Running").state;
  var clip=(AnimationClip)state.motion;
  if (overrides[clip]) clip=overrides[clip];
  Assert.AreEqual("A_FP_PCH_Run_02_Additive",clip.name);
  var bindings=AnimationUtility.GetCurveBindings(clip);
  Assert.IsTrue(bindings.Any(binding=>binding.type==typeof(Transform)&&
   binding.path.EndsWith("/ik_hand_gun")));
  Assert.IsTrue(AnimationUtility.GetAnimationClipSettings(clip).loopTime);
 }
}
