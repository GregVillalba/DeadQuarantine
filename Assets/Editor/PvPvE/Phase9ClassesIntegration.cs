#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class Phase9ClassesIntegration
{
 public static void InstallAndAnalyze(){Install();CompleteWeaponsIntegrationBuilder.AnalyzeAnimationsPhase9();}
 public const string CatalogPath="Assets/PvPvE/Data/LoadoutCatalog.asset";
 public static void Install(){
  Directory.CreateDirectory("Assets/PvPvE/Data");AssetDatabase.Refresh();
  var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");var presentation=player.GetComponent<NetworkWeaponPresentation>();
  var catalog=AssetDatabase.LoadAssetAtPath<PvPvELoadoutCatalog>(CatalogPath);
  if(!catalog){catalog=ScriptableObject.CreateInstance<PvPvELoadoutCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
  catalog.attachments=AssetDatabase.LoadAssetAtPath<WeaponAttachmentCatalog>("Assets/Generated/NewWeapons/Phase8/AttachmentCatalog.asset");
  catalog.weapons=presentation.Weapons.Select(d=>{
   var so=new SerializedObject(d.Source);var root=d.Source.gameObject;
   var meshes=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);string mesh=meshes.FirstOrDefault()?.sharedMesh.name??"";
   var icons=AssetDatabase.FindAssets("Tienda_"+mesh+" t:Sprite",new[]{"Assets/Art/Sprites/Tienda"});
   var attachments=root.GetComponent<WeaponAttachments>();
   return new PvPvELoadoutCatalog.Gun{id=d.Id,name=d.Source.WeaponName,secondary=d.AnimatorWeaponId==1||(d.AnimatorWeaponId==0&&d.Id=="Handgun"),model=d.Model,
    icon=icons.Length>0?AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(icons[0])):attachments?attachments.BodySprite:null,
    damage=so.FindProperty("damage").intValue,magazine=so.FindProperty("maxAmmo").intValue,range=d.Source.PresentationRange,fireInterval=d.Source.PresentationFireInterval};
  }).ToArray();
  var vendor=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mercader.prefab").GetComponentInChildren<NPCWeaponVendor>(true);
  var grenadeIds=new SerializedObject(player.GetComponentInChildren<GrenadeThrow>(true)).FindProperty("tipos");
  var ids=new HashSet<string>();for(int i=0;i<grenadeIds.arraySize;i++)ids.Add(grenadeIds.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue);
  Func<WeaponOffer,PvPvELoadoutCatalog.Utility> utility=o=>new(){id=o.weaponId,name=o.weaponName,icon=o.imagenTarjeta?o.imagenTarjeta:o.weaponIcon?o.weaponIcon:o.iconoHud,
   healing=catalog.FindHealing(o.weaponId)?.healing??o.puntosDeSalud,carryCount=catalog.FindHealing(o.weaponId)?.carryCount??1};
  catalog.grenades=vendor.Offers.Where(o=>ids.Contains(o.weaponId)).GroupBy(o=>o.weaponId).Select(g=>utility(g.First())).ToArray();
  catalog.healings=vendor.Offers.Where(o=>o.EsCuracion).GroupBy(o=>o.weaponId).Select(g=>utility(g.First())).ToArray();
  if(catalog.weapons.Length!=16||catalog.grenades.Length==0||catalog.healings.Length==0)throw new InvalidOperationException("Existing loadout catalog references are incomplete");
  foreach(int i in Enumerable.Range(0,5))if(!catalog.Validate(catalog.Default(i),out string error))throw new InvalidOperationException(error);
  EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
  string scenePath=PvPvEMenuPhase2Tools.ScenePath,before=File.ReadAllText(scenePath);
  var scene=EditorSceneManager.OpenScene(scenePath);var menu=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PvPvEModeMenu>(true)).Single();
  var container=(RectTransform)menu.transform.Find("PvPvELobby");var panel=(RectTransform)container.Find("LobbyPanel");
  var allowed=new HashSet<string>();Action<UnityEngine.Object> allow=obj=>allowed.Add(GlobalObjectId.GetGlobalObjectIdSlow(obj).targetObjectId.ToString());allow(menu.gameObject);allow(panel);allow(container);
  var editor=menu.GetComponent<PvPvEClassEditor>()??menu.gameObject.AddComponent<PvPvEClassEditor>();editor.catalog=catalog;editor.container=container;editor.font=panel.Find("Title").GetComponent<TMP_Text>().font;
  allow(editor);
  var existing=panel.Find("CreateClassButton");if(existing)UnityEngine.Object.DestroyImmediate(existing.gameObject);
  var go=new GameObject("CreateClassButton",typeof(RectTransform),typeof(Image),typeof(Button));var rect=go.GetComponent<RectTransform>();rect.SetParent(panel,false);rect.anchorMin=new Vector2(.37f,.05f);rect.anchorMax=new Vector2(.66f,.14f);rect.offsetMin=rect.offsetMax=Vector2.zero;
  go.GetComponent<Image>().color=new Color(.03f,.18f,.12f,.98f);var button=go.GetComponent<Button>();button.targetGraphic=go.GetComponent<Image>();editor.entryButton=button;
  var label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));var r=label.GetComponent<RectTransform>();r.SetParent(rect,false);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
  var text=label.GetComponent<TMP_Text>();text.font=editor.font;text.fontSize=24;text.text="CREAR UNA CLASE";text.color=new Color(.4f,1,.7f);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
  UnityEventTools.AddPersistentListener(button.onClick,editor.Open);
  EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save class editor entry");
  var records=Regex.Matches(before,@"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)",RegexOptions.Multiline|RegexOptions.Singleline).Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
  string saved=Regex.Replace(File.ReadAllText(scenePath),@"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)",m=>records.TryGetValue(m.Groups[1].Value,out var value)&&!allowed.Contains(m.Groups[1].Value)?value:m.Value,RegexOptions.Multiline|RegexOptions.Singleline);
  File.WriteAllText(scenePath,saved);AssetDatabase.ImportAsset(scenePath,ImportAssetOptions.ForceUpdate);
  string output=Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE9_OUTPUT");Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"AllowedSceneRecords.txt"),allowed);
  AssetDatabase.SaveAssets();
 }
}
#endif
