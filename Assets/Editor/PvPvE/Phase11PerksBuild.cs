#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.Networking;
using DeadQuarantine.Development;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Phase11PerksBuild
{
    const string Data = "Assets/PvPvE/Data/PassivePerks.asset";
    const string Settings = "Assets/PvPvE/Settings/PvPvENetworkSettings.asset";
    static string Output => Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE11_OUTPUT");
    public static void Install()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PassivePerkCatalog>(Data);
        if (!catalog)
        {
            catalog = ScriptableObject.CreateInstance<PassivePerkCatalog>();
            catalog.entries = new[] {
                Entry("gloves","Guantes tácticos","Recargas un 50% más rápidas.",PassivePerk.Gloves,PerkCategory.Handling,3,1,0),
                Entry("boots","Botas tácticas","Consume 30% menos stamina al correr.",PassivePerk.Boots,PerkCategory.Mobility,2,1,0),
                Entry("kneepads","Rodilleras tácticas","Ctrl corriendo: slide con coste de stamina.",PassivePerk.Kneepads,PerkCategory.Mobility,4,0,1),
                Entry("holster","Funda de arma","Cambio de arma un 20% más corto.",PassivePerk.Holster,PerkCategory.Handling,3,0,1),
                Entry("canteen","Cantimplora militar","Regenera stamina un 15% más rápido.",PassivePerk.Canteen,PerkCategory.Endurance,2,2,0)
            };
            AssetDatabase.CreateAsset(catalog, Data);
        }
        foreach (var e in catalog.entries) if (!e.icon) e.icon = Icon(e.perk);
        if (catalog.entries.Length != 5 || catalog.entries.Select(e => e.id).Distinct().Count() != 5 || catalog.entries.Any(e => !e.icon)) throw new Exception("Invalid passive catalog");
        var loadouts = AssetDatabase.LoadAssetAtPath<PvPvELoadoutCatalog>(Phase9ClassesIntegration.CatalogPath); loadouts.perks = catalog;
        var settings = AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>(Settings); settings.ProtocolVersion = Math.Max(5011, settings.ProtocolVersion);
        EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(loadouts); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        EditPrefab("Assets/Prefabs/Player.prefab", root => {
            var health = root.GetComponent<PlayerHealth>(); var so = new SerializedObject(health); so.FindProperty("perksCatalog").objectReferenceValue = catalog; so.ApplyModifiedPropertiesWithoutUndo();
        }, typeof(PlayerHealth), true);
        EditPrefab("Assets/Prefabs/Mercader.prefab", root => {
            var vendor = root.GetComponentInChildren<NPCWeaponVendor>(true);
            var offers = (List<WeaponOffer>)typeof(NPCWeaponVendor).GetField("offers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(vendor);
            foreach (var entry in catalog.entries) if (!offers.Any(o => o.weaponId == entry.id))
                offers.Add(new WeaponOffer { weaponId = entry.id, weaponName = entry.name, perk = entry.perk, descripcion = entry.description, weaponIcon = entry.icon, imagenTarjeta = entry.icon, iconoHud = entry.icon,
                    categoria = CategoriaTienda.Equipamiento, esEquipamiento = true, precioPilaDeLatas = entry.cans, precioCantimplora = entry.water, precioBidon = entry.fuel });
            EditorUtility.SetDirty(vendor);
        }, typeof(NPCWeaponVendor), false);
        for (int i = 0; i < 5; i++) if (!loadouts.Validate(loadouts.Default(i), out var error)) throw new Exception(error);
    }
    static PassivePerkCatalog.Entry Entry(string id,string name,string description,PassivePerk perk,PerkCategory category,int cans,int water,int fuel)
        => new() { id="perk."+id,name=name,description=description,perk=perk,category=category,cans=cans,water=water,fuel=fuel };
    static Dictionary<string,string> Records(string text) => Regex.Matches(text,@"^--- !u!\d+ &(-?\d+).*?(?=^--- !u!|\z)",RegexOptions.Multiline|RegexOptions.Singleline).Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
    static void EditPrefab(string path,Action<GameObject> change,Type component,bool health)
    {
        var before = Records(File.ReadAllText(path)); var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset.GetComponentInChildren(component,true),out string _,out long id);
        var root = PrefabUtility.LoadPrefabContents(path);
        try { change(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var after = Records(File.ReadAllText(path)); string key=id.ToString(), original=before[key], saved=after[key];
        if (health)
        {
            string line=Regex.Match(saved,@"(?m)^  perksCatalog:.*$").Value;
            after[key]=Regex.IsMatch(original,@"(?m)^  perksCatalog:")?Regex.Replace(original,@"(?m)^  perksCatalog:.*$",line):original.TrimEnd()+"\n"+line+"\n";
        }
        else
        {
            const string array=@"(?ms)^  offers:\r?\n(.*?)(?=^  \w|\z)";
            var oldBody=Regex.Match(original,array).Groups[1].Value;var newBody=Regex.Match(saved,array).Groups[1].Value;
            var ids=Regex.Matches(oldBody,@"(?m)^  - weaponId: (.+)$").Cast<Match>().Select(m=>m.Groups[1].Value.Trim()).ToHashSet();
            string appended=string.Concat(Regex.Matches(newBody,@"(?ms)^  - weaponId: ([^\r\n]+).*?(?=^  - weaponId:|\z)").Cast<Match>().Where(m=>!ids.Contains(m.Groups[1].Value.Trim())).Select(m=>m.Value));
            after[key]=Regex.Replace(original,array,m=>m.Value.Substring(0,m.Value.Length-m.Groups[1].Length)+oldBody+appended);
        }
        string result=Regex.Replace(File.ReadAllText(path),@"^--- !u!\d+ &(-?\d+).*?(?=^--- !u!|\z)",m=>m.Groups[1].Value==key?after[key]:before.GetValueOrDefault(m.Groups[1].Value,m.Value),RegexOptions.Multiline|RegexOptions.Singleline);
        File.WriteAllText(path,result); AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,health?"AllowedPlayerRecord.txt":"AllowedMerchantRecord.txt"),key);
    }
    // Reuse the supplied equipment artwork in every mode.
    static Sprite Icon(PassivePerk perk)
    {
        string name=perk==PassivePerk.Canteen?"EnergyDrink":perk.ToString();
        var icon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PvPvE/Art/RedesignPerks/"+name+".png");
        if(!icon)throw new InvalidOperationException("Missing equipment icon: "+name);
        return icon;
    }
    public static void BuildAll(){Install();try{BuildGame();BuildMenu();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);}}
    public static void BuildGameAndRestoreMenu(){try{BuildGame();}finally{EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);}}
    public static void BuildGame()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Tests/PvPvE/Phase10/HealingValidation.unity");
        foreach(var old in UnityEngine.Object.FindObjectsByType<PvPvEPhase10HealingDriver>(FindObjectsInactive.Include))UnityEngine.Object.DestroyImmediate(old.gameObject);
        var merchant=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mercader.prefab"));merchant.transform.position=new Vector3(0,0,2);
        foreach(var b in merchant.GetComponentsInChildren<MonoBehaviour>(true))if(b is not NPCWeaponVendor)b.enabled=false;
        foreach(var agent in merchant.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))agent.enabled=false;
        var go=new GameObject("Phase11 native driver");go.AddComponent<PvPvEMatchmaking>();var driver=go.AddComponent<PvPvEPhase11PerksDriver>();
        driver.Settings=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>(Settings);driver.PlayerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        SaveBuild("PerksValidation","Game/Perks.exe");
    }
    public static void BuildMenu()
    {
        var scene=EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
        foreach(var b in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)))if(b&&(b is NetworkBootstrap||b.GetType().Name=="CharacterRenameManager"||b.GetType().Name=="AchievementsManager"))b.enabled=false;
        new GameObject("Phase11 class validation").AddComponent<PvPvEPhase9SmokeDriver>();SaveBuild("PerksMenu","Menu/Classes.exe");
    }
    static void SaveBuild(string name,string relative)
    {
        Directory.CreateDirectory("Assets/Tests/PvPvE/Phase11");AssetDatabase.Refresh();string scene="Assets/Tests/PvPvE/Phase11/"+name+".unity";EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),scene);
        string target=Path.Combine(Output,relative);Directory.CreateDirectory(Path.GetDirectoryName(target));string company=PlayerSettings.companyName,product=PlayerSettings.productName;
        try{PlayerSettings.companyName="DeadQuarantineValidation";PlayerSettings.productName="PvPvE Phase11 "+name;var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scene},target=BuildTarget.StandaloneWindows64,locationPathName=target,options=BuildOptions.Development});if(build.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Phase11 build failed");}
        finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;}
    }
}
#endif
