#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Text;using UnityEditor;using UnityEditor.Animations;using UnityEngine;using DeadQuarantine.PvPvE;using DeadQuarantine.PvPvE.Networking;
public static class PvPvECaptureCorrections {
 public static void BuildLegacy(){
  var originalPrefabs=File.ReadAllBytes("Assets/DefaultNetworkPrefabs.asset");string company=PlayerSettings.companyName,product=PlayerSettings.productName;
  try{
   UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Tests/CityWeapons/WeaponPresentationTest.unity");
   var driver=UnityEngine.Object.FindFirstObjectByType<DeadQuarantine.Development.OriginalWeaponsSmokeDriver>();driver.MerchantPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mercader.prefab");driver.LoadingSettings=AssetDatabase.LoadAssetAtPath<DeadQuarantine.PvPvE.Networking.PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");driver.ZombiePrefab=driver.LoadingSettings.WalkerPrefab;
   const string scene="Assets/Tests/CityWeapons/FlashlightMerchantTest.unity";UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),scene);
   string path=Path.Combine(Environment.GetEnvironmentVariable("DQ_POSE_OUTPUT"),"Legacy/Legacy.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="Legacy merchant utility validation";
   var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scene},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});if(build.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Legacy merchant build failed");
  }finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;File.WriteAllBytes("Assets/DefaultNetworkPrefabs.asset",originalPrefabs);UnityEditor.SceneManagement.EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);}
 }
 public static void PrepareAndBuildAll(){PrepareAndBuild();File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("DQ_CAPTURE_OUTPUT"),"ModeReady.txt"),"ready");BuildLegacy();}

 public static void PrepareAndBuild(){Prepare();Diagnose();WeaponPoseModeBuild.Build();}
 public static void Prepare(){
  var settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");var catalog=settings.Loadouts;
  Directory.CreateDirectory("Assets/Resources/PvPvE");
  var white=EnsureMaterial("Assets/Resources/PvPvE/WhiteIcon.mat",Shader.Find("DeadQuarantine/UI White Icon"));
  var head=EnsureMaterial("Assets/Resources/PvPvE/HeadshotIcon.mat",white.shader);head.SetFloat("_BlackIsTransparent",1);EditorUtility.SetDirty(head);
  const string headPath="Assets/Resources/PvPvE/Headshot.png";
  if(!File.Exists(headPath))File.Copy(@"C:\Users\andre\Downloads\Imagen de ChatGPT 7 nov 2026, 11_39_21 p.m.png",headPath);
  AssetDatabase.ImportAsset(headPath);FullCanvas(headPath);
  foreach(var wc in catalog.wildcards.entries)if(wc.kind==PvPvEWildcard.PerkGreed)wc.icon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PvPvE/Art/RedesignPerks/Gloves.png");EditorUtility.SetDirty(catalog.wildcards);
  if(catalog.attachments.Find("Flashlight_01")==null){
   var sourceUtility=catalog.attachments.Find("Laser_Flashlight_01");
   var flashlight=new WeaponAttachmentCatalog.Entry{id="Flashlight_01",displayName="Linterna",kind=WeaponAttachmentKind.Utility,socket=sourceUtility.socket,model=sourceUtility.model,icon=sourceUtility.icon,weaponIcons=sourceUtility.weaponIcons,weapons=sourceUtility.weapons,mounts=sourceUtility.mounts,flashlight=true,laser=false};
   catalog.attachments.entries=catalog.attachments.entries.Concat(new[]{flashlight}).ToArray();
  }
  // Pack overlays share the original full canvas. Multiple-sprite trimming changes their mount coordinates.
  foreach(var gun in catalog.weapons){gun.whiteBody=FullCanvas(AssetDatabase.GetAssetPath(gun.whiteBody));gun.whiteMagazine=FullCanvas(AssetDatabase.GetAssetPath(gun.whiteMagazine));gun.whiteDefaultSight=FullCanvas(AssetDatabase.GetAssetPath(gun.whiteDefaultSight));}
  foreach(var e in catalog.attachments.entries)foreach(var icon in e.weaponIcons)icon.icon=FullCanvas(AssetDatabase.GetAssetPath(icon.icon));
  var lens=EnsureMaterial("Assets/Resources/PvPvE/ScopeLens.mat",Shader.Find("Universal Render Pipeline/Unlit"));lens.SetColor("_BaseColor",Color.white);lens.SetTexture("_BaseMap",Texture2D.blackTexture);EditorUtility.SetDirty(lens);
  var reticle=EnsureMaterial("Assets/Resources/PvPvE/ScopeReticle.mat",Shader.Find("Universal Render Pipeline/Unlit"));
  var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Attachments/M_ATT_Sight_Crosshair_001.mat");
  reticle.SetTexture("_BaseMap",source.GetTexture("_MainTex"));reticle.SetColor("_BaseColor",Color.black);reticle.SetFloat("_Surface",1);reticle.SetFloat("_Blend",0);reticle.SetFloat("_SrcBlend",5);reticle.SetFloat("_DstBlend",10);reticle.SetFloat("_ZWrite",0);reticle.SetFloat("_Cull",0);reticle.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");reticle.renderQueue=3001;EditorUtility.SetDirty(reticle);
  catalog.attachments.scopeLensMaterial=lens;catalog.attachments.scopeReticleMaterial=reticle;
  foreach(var e in catalog.attachments.entries)if(e.kind==WeaponAttachmentKind.Scope){e.magnifiedLens=new[]{"Scope_05","Scope_06","Scope_07","Scope_08"}.Contains(e.id);e.lensFieldOfView=4.25f;}
  var merchant=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Mercader.prefab");try{
   var vendor=merchant.GetComponentInChildren<NPCWeaponVendor>(true);var so=new SerializedObject(vendor);var offers=so.FindProperty("offers");
   if(!vendor.Offers.Any(o=>o.esLinterna)){int index=offers.arraySize++;var item=offers.GetArrayElementAtIndex(index);
    // Reset the duplicated array element before authoring the new offer.
    foreach(var field in typeof(WeaponOffer).GetFields()){var property=item.FindPropertyRelative(field.Name);if(property==null)continue;if(property.propertyType==SerializedPropertyType.Boolean)property.boolValue=false;else if(property.propertyType==SerializedPropertyType.Integer||property.propertyType==SerializedPropertyType.Enum)property.intValue=0;else if(property.propertyType==SerializedPropertyType.Float)property.floatValue=0;else if(property.propertyType==SerializedPropertyType.String)property.stringValue="";else if(property.propertyType==SerializedPropertyType.ObjectReference)property.objectReferenceValue=null;}
    item.FindPropertyRelative("weaponId").stringValue="Accesorio_Linterna";item.FindPropertyRelative("weaponName").stringValue="Linterna";item.FindPropertyRelative("esLinterna").boolValue=true;item.FindPropertyRelative("esEquipamiento").boolValue=true;item.FindPropertyRelative("disponible").boolValue=true;item.FindPropertyRelative("categoria").enumValueIndex=(int)CategoriaTienda.Equipamiento;
    item.FindPropertyRelative("precioPilaDeLatas").intValue=3;item.FindPropertyRelative("precioBidon").intValue=1;
    item.FindPropertyRelative("dificultades").intValue=(int)DisponibilidadTienda.TodasLasDificultades;item.FindPropertyRelative("modos").intValue=(int)DisponibilidadTienda.TodosLosModos;
    item.FindPropertyRelative("descripcion").stringValue="Linterna para el arma equipada. Activar con F. Podés equiparla en otra arma desde el mercader sin volver a pagar. Se pierde al morir definitivamente.";
    var icon=catalog.attachments.Find("Flashlight_01").icon;item.FindPropertyRelative("weaponIcon").objectReferenceValue=icon;item.FindPropertyRelative("imagenTarjeta").objectReferenceValue=icon;so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(merchant,"Assets/Prefabs/Mercader.prefab");
   }
  }finally{PrefabUtility.UnloadPrefabContents(merchant);}
  EditorUtility.SetDirty(catalog);EditorUtility.SetDirty(catalog.attachments);AssetDatabase.SaveAssets();
  File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("DQ_CAPTURE_OUTPUT"),"Prepared.txt"),"Full-canvas overlays, supplied headshot/perk, URP scope lens/reticle prepared.");
 }
 static Material EnsureMaterial(string path,Shader shader){if(!shader)throw new Exception("Missing shader for "+path);var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}return m;}
 static Sprite FullCanvas(string path){
  if(string.IsNullOrEmpty(path))return null;var i=AssetImporter.GetAtPath(path) as TextureImporter;if(!i)return null;
  var slices=i.spritesheet;
  if(slices.Length>0){
   i.GetSourceTextureWidthAndHeight(out int width,out int height);bool changed=i.spriteImportMode!=SpriteImportMode.Multiple;
   for(int n=0;n<slices.Length;n++){var full=new Rect(0,0,width,height);if(slices[n].rect!=full||slices[n].pivot!=new Vector2(.5f,.5f))changed=true;slices[n].rect=full;slices[n].alignment=0;slices[n].pivot=new Vector2(.5f,.5f);}
   if(changed){i.spriteImportMode=SpriteImportMode.Multiple;i.spritesheet=slices;i.SaveAndReimport();}
   return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
  }
  if(i.spriteImportMode!=SpriteImportMode.Single||i.textureType!=TextureImporterType.Sprite){i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;i.alphaIsTransparency=true;var ts=new TextureImporterSettings();i.ReadTextureSettings(ts);ts.spriteMeshType=SpriteMeshType.FullRect;i.SetTextureSettings(ts);i.SaveAndReimport();}
  return AssetDatabase.LoadAssetAtPath<Sprite>(path);
 }

 public static void Diagnose(){
  var b=new StringBuilder();var settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");
  var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");var sw=player.GetComponentInChildren<WeaponSwitcher>(true);var presentation=player.GetComponent<NetworkWeaponPresentation>();
  b.AppendLine("QUALITY "+QualitySettings.GetQualityLevel()+" "+QualitySettings.names[QualitySettings.GetQualityLevel()]);
  foreach(var gun in settings.Loadouts.weapons){b.AppendLine("GUN "+gun.id+" MODEL "+AssetDatabase.GetAssetPath(gun.model));
   foreach(var sprite in new[]{gun.whiteBody,gun.whiteMagazine,gun.whiteDefaultSight})b.AppendLine(" SPRITE "+(sprite?AssetDatabase.GetAssetPath(sprite)+" rect="+sprite.rect+" pivot="+sprite.pivot:"null"));
   var def=presentation.Weapons.Single(d=>d.Id==gun.id);var so=new SerializedObject(def.Source);foreach(var property in new[]{"weaponAnimator","animatorController","animatorOverrideController"}){var v=so.FindProperty(property);if(v!=null)b.AppendLine(" "+property+"="+v.objectReferenceValue);}
  }
  var melee=player.GetComponentInChildren<MeleeAttack>(true);var m=new SerializedObject(melee);b.AppendLine("MELEE "+melee.enabled+" animator="+m.FindProperty("weaponAnimator").objectReferenceValue+" knife="+m.FindProperty("knifeObject").objectReferenceValue);
  foreach(var id in AssetDatabase.FindAssets("t:AnimatorController",new[]{"Assets/Generated/NewWeapons","Assets/Animations"})){var path=AssetDatabase.GUIDToAssetPath(id);var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(c.layers.Any(l=>l.name.Contains("Granade")))b.AppendLine("CONTROLLER "+path+" : "+string.Join(";",c.layers.Select(l=>l.name+"="+l.defaultWeight+" ["+string.Join(",",l.stateMachine.states.Select(t=>t.state.name))+ "]")));}
  foreach(var entry in settings.Loadouts.attachments.entries){b.AppendLine("ATT "+entry.id+" MODEL="+AssetDatabase.GetAssetPath(entry.model)+" light="+entry.flashlight+" laser="+entry.laser);foreach(var icon in entry.weaponIcons)b.AppendLine(" "+icon.weapon+" "+AssetDatabase.GetAssetPath(icon.icon));}
  foreach(var id in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Art/Materials"})){var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(id));if(mat.name.ToLower().Contains("scope")||mat.name=="Render"||mat.name.StartsWith("Layer"))b.AppendLine("MAT "+AssetDatabase.GetAssetPath(mat)+" "+mat.shader.name);}
  var wc=settings.Loadouts.wildcards;foreach(var entry in wc.entries)b.AppendLine("WILDCARD "+entry.kind+"="+AssetDatabase.GetAssetPath(entry.icon));
  File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("DQ_CAPTURE_OUTPUT"),"Diagnostic.txt"),b.ToString());
 }
}
#endif
