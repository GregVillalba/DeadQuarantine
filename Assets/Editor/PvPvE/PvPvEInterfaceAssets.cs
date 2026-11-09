#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Text;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.Rendering;using DeadQuarantine.PvPvE.Networking;
public static partial class CompleteWeaponsIntegrationBuilder {
 static string UIOutput=>Environment.GetEnvironmentVariable("DQ_UI_OUTPUT");
 public static void PrepareInterfaceAssets(){
  var settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");var loadouts=settings.Loadouts;
  const string folder="Assets/PvPvE/Art/Equipment";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
  foreach(var perk in loadouts.perks.entries){string name=perk.perk==DeadQuarantine.PvPvE.PassivePerk.Canteen?"EnergyDrink":perk.perk.ToString();perk.pvpveIcon=ImportUISprite("Assets/PvPvE/Art/RedesignPerks/"+name+".png");}EditorUtility.SetDirty(loadouts.perks);
  foreach(var gun in loadouts.weapons){gun.icon=CaptureCatalogModel(gun.model,true,folder+"/"+gun.id+".png");if(gun.id is "AutoPistol" or "Handgun01" or "Handgun02"){gun.reloadSeconds=2.2333333f;gun.emptyReloadSeconds=2.7333333f;}}
  var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab").GetComponent<NetworkWeaponPresentation>();var catalog=loadouts.attachments;var backlog=new StringBuilder("# Compatibilidad de accesorios por arma\n\nMisma tabla usada por Create-a-Class y la validación del servidor. Se conservan los IDs y efectos existentes.\n\n| Arma | Permitidos | Excluidos | Motivo de las exclusiones |\n|---|---|---|---|\n");
  foreach(var entry in catalog.entries){
   entry.icon=CaptureCatalogModel(entry.model,false,folder+"/"+entry.id+".png");entry.weaponIcons=Array.Empty<WeaponAttachmentCatalog.WeaponIcon>();
   entry.weapons=entry.weapons.Where(id=>{bool pistol=id is "Handgun" or "AutoPistol" or "Handgun01" or "Handgun02";bool smg=id is "P90" or "Vector" or "MAC10" or "SMG02" or "SMG05";var def=player.Weapons.Single(d=>d.Id==id);if(!WeaponAttachments.Find(def.Source.transform,entry.socket))return false;
    if(pistol)return entry.kind!=WeaponAttachmentKind.Support&&(entry.kind!=WeaponAttachmentKind.Scope||entry.id is "Scope_01" or "Scope_02")&&entry.id!="Muzzle_Silencer_03"&&!entry.flashlight;
    if(id=="Escopeta")return entry.kind!=WeaponAttachmentKind.Support&&(entry.kind!=WeaponAttachmentKind.Scope||entry.id is "Scope_01" or "Scope_02")&&entry.id!="Muzzle_Silencer_03";
    if(smg&&(entry.requiresBracing||entry.kind==WeaponAttachmentKind.Scope&&(entry.id is "Scope_04" or "Scope_05")))return false;
    return true;}).ToArray();
  }
  foreach(var gun in loadouts.weapons){var yes=catalog.entries.Where(e=>e.weapons.Contains(gun.id)).Select(e=>e.id);var no=catalog.entries.Where(e=>!e.weapons.Contains(gun.id)).Select(e=>e.id);string why=gun.secondary?"Pistola: ópticas compactas; sin bípode/empuñadura, silenciador pesado ni linterna voluminosa.":gun.id=="Escopeta"?"Ópticas compactas; se despeja el recorrido de la corredera y no se monta un bípode ni silenciador pesado.":gun.id is "P90" or "Vector" or "MAC10" or "SMG02" or "SMG05"?"Subfusil: sin bípode ni ópticas largas; solo anclajes reales.":"Rifle: anclajes presentes y modelos compatibles; la empuñadura usa su pose y montaje específicos.";backlog.AppendLine("| "+gun.name+" (`"+gun.id+"`) | "+string.Join(", ",yes)+" | "+string.Join(", ",no)+" | "+why+" |");}
  backlog.AppendLine("\nReglas: no se agregan anclajes ficticios. Las pistolas solo reciben Scope_01/02; los subfusiles no reciben Scope_04/05 ni soportes de apoyo fijo; la escopeta conserva libres sus partes móviles. Los rifles conservan las variantes ya montadas y verificadas. No se inventan calibres o multiplicadores nuevos. El reparador existente conserva las armas de las clases antiguas y descarta los accesorios que dejan de ser compatibles; el guardado anterior permanece como respaldo. La nueva combinación se muestra en Create-a-Class antes de confirmar.");
  File.WriteAllText(Path.Combine(UIOutput,"CompatibilidadAccesorios.md"),backlog.ToString());EditorUtility.SetDirty(loadouts);EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();CaptureLoadingViews(settings);EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
 }
 public static void AssignExistingInterfacePhotographs(){
 var settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");var report=new StringBuilder("Real Unity captures from Assets/PvPvE/Scenes/PvPvEMatch.unity; scenario activated individually; camera at configured player spawn + 1.7m; clear street-facing direction.\n");
 foreach(var map in settings.Maps){map.LoadingViews=Enumerable.Range(0,3).Select(n=>AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PvPvE/Art/LoadingViews/"+map.Id+"_"+n+".png")).ToArray();if(map.LoadingViews.Any(p=>!p))throw new Exception("Missing real map capture");map.Preview=map.LoadingViews[1];report.AppendLine(map.Name+": "+string.Join(", ",map.LoadingViews.Select(AssetDatabase.GetAssetPath)));}EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(UIOutput,"MapPhotography.txt"),report.ToString());
 }
 public static void BindPreparedInterfaceArt(){
 var settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");var loadouts=settings.Loadouts;
 foreach(var gun in loadouts.weapons)gun.icon=ImportUISprite("Assets/PvPvE/Art/Equipment/"+gun.id+".png");
 foreach(var entry in loadouts.attachments.entries)entry.icon=ImportUISprite("Assets/PvPvE/Art/Equipment/"+entry.id+".png");
 foreach(var perk in loadouts.perks.entries)perk.pvpveIcon=ImportUISprite("Assets/PvPvE/Art/RedesignPerks/"+(perk.perk==DeadQuarantine.PvPvE.PassivePerk.Canteen?"EnergyDrink":perk.perk.ToString())+".png");
 if(loadouts.weapons.Any(g=>!g.icon)||loadouts.attachments.entries.Any(e=>!e.icon)||loadouts.perks.entries.Any(p=>!p.pvpveIcon))throw new Exception("A prepared icon was not imported as a whole sprite.");EditorUtility.SetDirty(loadouts);EditorUtility.SetDirty(loadouts.attachments);EditorUtility.SetDirty(loadouts.perks);AssetDatabase.SaveAssets();
 }
 public static void BindAndBuildInterfaceReview(){EnsureInterfaceFonts();BindPreparedInterfaceArt();InstallPistolPresentation();WeaponPoseModeBuild.Build();}
 static void EnsureInterfaceFonts(){
 const string folder="Assets/Resources/PvPvE";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
 foreach(var pair in new[]{("BodyFont","Assets/Figma/Fonts/Geist Mono_400.ttf"),("HeadingFont","Assets/Figma/Fonts/Big Shoulders Display_800.ttf")}){
  string path=folder+"/"+pair.Item1+".asset";if(AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path))continue;
  var source=AssetDatabase.LoadAssetAtPath<Font>(pair.Item2);if(!source)throw new Exception("Missing existing UI typeface: "+pair.Item2);
  var font=TMPro.TMP_FontAsset.CreateFontAsset(source,72,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,TMPro.AtlasPopulationMode.Dynamic,true);font.name=pair.Item1;
  font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 áéíóúÁÉÍÓÚñÑüÜ¿¡!?:;.,'\"-_/+()[]%—·×…→",out string missing);AssetDatabase.CreateAsset(font,path);AssetDatabase.AddObjectToAsset(font.material,font);foreach(var atlas in font.atlasTextures)if(atlas)AssetDatabase.AddObjectToAsset(atlas,font);EditorUtility.SetDirty(font);
 }AssetDatabase.SaveAssets();
 }
 public static void RefreshInterfaceEquipment(){
  var loadouts=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset").Loadouts;var catalog=loadouts.attachments;var smgs=new[]{"P90","Vector","MAC10","SMG02","SMG05"};
  foreach(var e in catalog.entries){
   if(e.id=="Scope_04")e.weapons=e.weapons.Concat(smgs).Distinct().ToArray();
   if(e.kind==WeaponAttachmentKind.Scope&&e.aimFov<45)e.weapons=e.weapons.Except(smgs).ToArray();
   if(e.id=="Scope_08")e.weapons=Array.Empty<string>();
   e.displayName=e.id switch{"Scope_01"=>"Réflex compacta","Scope_02"=>"Punto rojo","Scope_03"=>"Holográfica I","Scope_04"=>"Holográfica II","Scope_05"=>"Telescópica I","Scope_06"=>"Telescópica II","Scope_07"=>"Largo alcance","Scope_08"=>"Óptica lateral","Grip_01"=>"Empuñadura vertical","Bipod_01"=>"Bípode ligero","Bipod_02"=>"Bípode reforzado","Laser_Sight_01"=>"Láser","Laser_Flashlight_01"=>"Láser + linterna","Muzzle_Silencer_01"=>"Silenciador corto","Muzzle_Silencer_02"=>"Silenciador largo","Muzzle_Silencer_03"=>"Silenciador pesado",_=>e.displayName};
   e.icon=CaptureCatalogModel(e.model,true,"Assets/PvPvE/Art/Equipment/"+e.id+".png");
  }
  foreach(var file in Directory.GetFiles("Assets/PvPvE/Art/Equipment","*.png"))TrimEquipmentImage(file);
  EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();BindPreparedInterfaceArt();
  var notes=new StringBuilder("# Compatibilidad de accesorios por arma\n\nTabla compartida por Create-a-Class y la autoridad multiplayer. Se reutilizan IDs, montajes y efectos existentes.\n\n| Arma | Permitidos | Excluidos | Justificación |\n|---|---|---|---|\n");
  foreach(var gun in loadouts.weapons){string why=gun.secondary?"Ópticas compactas 01/02; sin soporte ni linterna voluminosa; sin silenciador pesado.":gun.id=="Escopeta"?"Se despeja la corredera; ópticas compactas; sin soporte ni silenciador pesado.":smgs.Contains(gun.id)?"Montajes compactos, sin bípodes ni telescópicas 05–08.":"Riel superior y puntos reales de boca/soporte; sin el montaje lateral de Scope_08.";notes.AppendLine("| "+gun.name+" (`"+gun.id+"`) | "+string.Join(", ",catalog.entries.Where(e=>e.weapons.Contains(gun.id)).Select(e=>e.id))+" | "+string.Join(", ",catalog.entries.Where(e=>!e.weapons.Contains(gun.id)).Select(e=>e.id))+" | "+why+" |");}
  notes.AppendLine("\nScope_08 queda excluida de las armas actuales: su abrazadera lateral no coincide con los anclajes superiores verificados. No se inventa un adaptador. Los subfusiles conservan las cuatro ópticas compactas; las telescópicas 05/06/07 quedan para rifles. Las pistolas no admiten empuñaduras o bípodes. La escopeta mantiene libre el recorrido de su mecanismo.\n\nEl reparador de clases existente conserva las armas y descarta accesorios que dejan de ser compatibles; el documento guardado previo no se sobreescribe hasta guardar una clase. No se agregan multiplicadores ni accesorios nuevos.");File.WriteAllText(Path.Combine(UIOutput,"CompatibilidadAccesorios.md"),notes.ToString());
 }
 static void TrimEquipmentImage(string path){
  var image=new Texture2D(2,2,TextureFormat.RGBA32,false);image.LoadImage(File.ReadAllBytes(path));var pixels=image.GetPixels32();int minX=image.width,minY=image.height,maxX=-1,maxY=-1;
  for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)if(pixels[y*image.width+x].a>8){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
  if(maxX>=minX){minX=Mathf.Max(0,minX-8);minY=Mathf.Max(0,minY-8);maxX=Mathf.Min(image.width-1,maxX+8);maxY=Mathf.Min(image.height-1,maxY+8);int w=maxX-minX+1,h=maxY-minY+1;var cropped=new Texture2D(w,h,TextureFormat.RGBA32,false);cropped.SetPixels(image.GetPixels(minX,minY,w,h));cropped.Apply();File.WriteAllBytes(path,cropped.EncodeToPNG());UnityEngine.Object.DestroyImmediate(cropped);}UnityEngine.Object.DestroyImmediate(image);
 }
 public static void BuildInterfaceAndWeaponsReview(){WeaponPoseModeBuild.Build();Phase25WeaponsValidation.Build();}
 public static void RefreshAndBuildInterfaceReview(){RefreshInterfaceEquipment();WeaponPoseModeBuild.Build();}
 static Sprite CaptureCatalogModel(GameObject model,bool weapon,string path){var texture=GeneradorImagenTienda.RenderCatalogModel(model,weapon);try{File.WriteAllBytes(path,texture.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(texture);}return ImportUISprite(path);}
 static Sprite ImportUISprite(string path){AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=1024;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);}
 static void CaptureLoadingViews(PvPvENetworkSettings settings){
  const string folder="Assets/PvPvE/Art/LoadingViews";Directory.CreateDirectory(folder);var scene=EditorSceneManager.OpenScene("Assets/PvPvE/Scenes/PvPvEMatch.unity");settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");var roots=scene.GetRootGameObjects();foreach(var camera in roots.SelectMany(g=>g.GetComponentsInChildren<Camera>(true)))camera.enabled=false;
  var configs=roots.SelectMany(g=>g.GetComponentsInChildren<ConfiguracionEscenarioJugable>(true)).ToArray();var go=new GameObject("Map photography camera");var view=go.AddComponent<Camera>();view.fieldOfView=65;view.nearClipPlane=.1f;view.farClipPlane=1200;view.clearFlags=CameraClearFlags.Skybox;var lamp=new GameObject("Map photography fill").AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=1.3f;lamp.transform.rotation=Quaternion.Euler(40,-25,0);var report=new StringBuilder();
  foreach(var map in settings.Maps){var config=configs.Single(c=>c.TipoEscenario==map.Id);foreach(var c in configs)(c.transform.parent?c.transform.parent:c.transform).gameObject.SetActive(c==config);Physics.SyncTransforms();map.LoadingViews=new Texture2D[3];
   for(int n=0;n<3;n++){var spawn=config.ObtenerSpawnJugador(n);var origin=spawn.position+Vector3.up*1.7f;Vector3 direction=spawn.forward;float best=-1;
    for(int a=0;a<24;a++){var d=Quaternion.Euler(0,a*15,0)*Vector3.forward;float length=Physics.Raycast(origin,d,out var hit,45,~0,QueryTriggerInteraction.Ignore)?hit.distance:45;float score=length-Vector3.Angle(d,spawn.forward)*.025f;if(score>best){best=score;direction=d;}}
    view.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(Quaternion.AngleAxis((n-1)*12,Vector3.up)*direction));var target=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32);var before=RenderTexture.active;
    try{view.targetTexture=target;RenderPipeline.SubmitRenderRequest(view,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();string path=folder+"/"+map.Id+"_"+n+".png";File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();map.LoadingViews[n]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);report.AppendLine(map.Name+" | "+path+" | actual scene="+scene.path+" | "+origin+" | "+view.transform.eulerAngles);}
    finally{RenderTexture.active=before;view.targetTexture=null;RenderTexture.ReleaseTemporary(target);}
   }map.Preview=map.LoadingViews[1];
  }UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(lamp.gameObject);EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(UIOutput,"MapPhotography.txt"),report.ToString());
 }
}
#endif
