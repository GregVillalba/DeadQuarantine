#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class CompleteWeaponsIntegrationBuilder
{
    const string Catalog8="Assets/Generated/NewWeapons/Phase8/AttachmentCatalog.asset";
    public static void IntegrateAttachmentsPhase8(){
        Directory.CreateDirectory("Assets/Generated/NewWeapons/Phase8");AssetDatabase.Refresh();
        const string path="Assets/Prefabs/Player.prefab";var before=File.ReadAllText(path);
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);var oldSettings=new SerializedObject(asset.GetComponentInChildren<WeaponSwitcher>(true));
        var specs=Specs7();var allowed=new HashSet<string>();
        Action<UnityEngine.Object> allow=obj=>{if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string _,out long id))allowed.Add(id.ToString());};
        allow(asset);
        foreach(var s in specs)allow(ReadSlotTemplate(oldSettings,s.Id).Root.GetComponent<WeaponAttachments>());
        var root=PrefabUtility.LoadPrefabContents(path);var log=new StringBuilder();
        try{
            var settings=new SerializedObject(root.GetComponentInChildren<WeaponSwitcher>(true));
            var slots=specs.ToDictionary(s=>s.Id,s=>ReadSlotTemplate(settings,s.Id));
            var catalog=AssetDatabase.LoadAssetAtPath<WeaponAttachmentCatalog>(Catalog8);
            if(!catalog){catalog=ScriptableObject.CreateInstance<WeaponAttachmentCatalog>();AssetDatabase.CreateAsset(catalog,Catalog8);}
            catalog.eligibleWeapons=specs.Select(s=>s.Id).ToArray();
            var shader=Shader.Find("Universal Render Pipeline/Unlit");if(!shader)throw new InvalidOperationException("Missing URP unlit shader");
            Func<string,Color,bool,Material> material=(name,color,transparent)=>{
                string file="Assets/Generated/NewWeapons/Phase8/"+name+".mat";
                var result=AssetDatabase.LoadAssetAtPath<Material>(file);if(!result){result=new Material(shader);AssetDatabase.CreateAsset(result,file);}
                result.SetColor("_BaseColor",color);
                if(transparent){result.SetFloat("_Surface",1);result.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);result.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);result.SetFloat("_ZWrite",0);result.SetFloat("_Cull",0);result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");result.renderQueue=3000;}
                EditorUtility.SetDirty(result);return result;
            };
            catalog.laserMaterial=material("Laser",Color.red,false);
            catalog.transparentLens=material("Lens",Color.clear,true);
            catalog.reticleMaterial=material("Reticle",Color.red,true);
            var authored=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Attachments/M_ATT_Sight_Dot_Red_01.mat");
            catalog.reticleMaterial.SetTexture("_BaseMap",authored.GetTexture("_MainTex"));
            string directory="Assets/Art/Meshes/Weapons/_Common/Attachments/";
            var entries=new List<WeaponAttachmentCatalog.Entry>();
            var hudPaths=Directory.EnumerateFiles("Assets/Art/Sprites/Icons/Weapons","*.png",SearchOption.AllDirectories)
                .GroupBy(file=>Path.GetFileNameWithoutExtension(file)).ToDictionary(g=>g.Key,g=>g.First().Replace('\\','/'));
            Action<string,string,WeaponAttachmentKind,string,Func<Spec,bool>> add=(file,title,kind,socket,compatible)=>{
                var e=new WeaponAttachmentCatalog.Entry{id=file.Replace("SM_ATT_",""),displayName=title,kind=kind,socket=socket,
                    model=AssetDatabase.LoadAssetAtPath<GameObject>(directory+file+".fbx"),
                    weapons=specs.Where(s=>compatible(s)&&WeaponAttachments.Find(slots[s.Id].Root.transform,socket)).Select(s=>s.Id).ToArray()};
                if(!e.model||e.weapons.Length==0)throw new InvalidOperationException(file+" has no valid model or compatible mount");
                string token=e.id.Replace("Muzzle_","");
                string icon="Assets/Art/Sprites/Icons/Attachments/"+(kind==WeaponAttachmentKind.Scope?"HUD_":"T_HUD_")+token+"_D.png";
                e.icon=AssetDatabase.LoadAssetAtPath<Sprite>(icon);
                e.weaponIcons=specs.Where(s=>e.weapons.Contains(s.Id)).Select(s=>{
                    string key="T_HUD_"+s.Model.Substring(3)+"_"+token+"_D";
                    return new WeaponAttachmentCatalog.WeaponIcon{weapon=s.Id,icon=hudPaths.TryGetValue(key,out var file)?AssetDatabase.LoadAssetAtPath<Sprite>(file):null};
                }).ToArray();
                log.AppendLine(file+" materials: "+string.Join(",",e.model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Select(m=>m?m.name:"NULL")));
                entries.Add(e);
            };
            for(int i=1;i<=8;i++){
                int number=i;add("SM_ATT_Scope_"+i.ToString("00"),"Mira "+i,WeaponAttachmentKind.Scope,"SOCKET_Scope",s=>s.Family!=Family.Handgun||number<=2);
                entries.Last().aimFov=i<=4?50:i<=6?32:24;
            }
            add("SM_ATT_Grip_01","Empuñadura",WeaponAttachmentKind.Support,"SOCKET_Grip",s=>s.Family!=Family.Handgun);
            entries.Last().recoil=.8f;
            for(int i=1;i<=2;i++){
                add("SM_ATT_Bipod_"+i.ToString("00"),"Bípode "+i,WeaponAttachmentKind.Support,"SOCKET_Grip",s=>s.Id.Contains("Rifle")||s.Id.StartsWith("Sniper"));
                entries.Last().requiresBracing=true;entries.Last().recoil=.65f;entries.Last().spread=.75f;
            }
            add("SM_ATT_Laser_Sight_01","Láser",WeaponAttachmentKind.Utility,"SOCKET_Laser",s=>true);
            entries.Last().laser=true;entries.Last().spread=.8f;
            add("SM_ATT_Laser_Flashlight_01","Láser / linterna",WeaponAttachmentKind.Utility,"SOCKET_Laser",s=>s.Family!=Family.Handgun);
            entries.Last().laser=true;entries.Last().flashlight=true;entries.Last().spread=.85f;
            var suppressed=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/SFX/Weapons/Fire/S_WEP_Fire_Silencer.wav");if(!suppressed)throw new InvalidOperationException("Missing authored suppressed sound");
            for(int i=1;i<=3;i++){
                int number=i;add("SM_ATT_Muzzle_Silencer_"+i.ToString("00"),"Silenciador "+i,WeaponAttachmentKind.Muzzle,"SOCKET_Muzzle",s=>number<3||s.Id.Contains("Rifle")||s.Id.StartsWith("Sniper"));
                entries.Last().damage=.9f;entries.Last().spread=.9f;entries.Last().suppressed=true;entries.Last().shotSound=suppressed;
            }
            catalog.entries=entries.ToArray();catalog.mountingHardware=Enumerable.Range(1,3).Select(i=>AssetDatabase.LoadAssetAtPath<GameObject>(directory+"SM_ATT_Rails_"+i.ToString("00")+".fbx")).ToArray();
            foreach(var pair in slots){
                var so=new SerializedObject(pair.Value.Root.GetComponent<WeaponAttachments>());
                so.FindProperty("catalog").objectReferenceValue=catalog;so.FindProperty("stableWeaponId").stringValue=pair.Key;so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine(pair.Key+": "+string.Join(", ",pair.Value.Root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("SOCKET_")).Select(t=>t.name)));
            }
            var network=root.GetComponent<NetworkWeaponAttachments>();if(!network)network=root.AddComponent<NetworkWeaponAttachments>();network.catalog=catalog;
            EditorUtility.SetDirty(catalog);PrefabUtility.SaveAsPrefabAsset(root,path);
            var old=Regex.Matches(before,@"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)",RegexOptions.Multiline|RegexOptions.Singleline).Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
            string saved=Regex.Replace(File.ReadAllText(path),@"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)",m=>old.TryGetValue(m.Groups[1].Value,out var value)&&!allowed.Contains(m.Groups[1].Value)?value:m.Value,RegexOptions.Multiline|RegexOptions.Singleline);
            File.WriteAllText(path,saved);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);AssetDatabase.SaveAssets();
            string output=Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE8_OUTPUT")??"Temp/Phase8";Directory.CreateDirectory(output);
            File.WriteAllLines(Path.Combine(output,"AllowedPrefabRecords.txt"),allowed);
            foreach(var e in entries)log.AppendLine(e.id+" => "+string.Join(",",e.weapons));
            File.WriteAllText(Path.Combine(output,"CatalogIntegration.txt"),log.ToString());
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
#endif
