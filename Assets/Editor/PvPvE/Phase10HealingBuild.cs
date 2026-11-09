#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DeadQuarantine.Development;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Phase10HealingBuild
{
    const string SettingsPath = "Assets/PvPvE/Settings/PvPvENetworkSettings.asset";
    static string Output => Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE10_OUTPUT");
    public static void Install()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PvPvELoadoutCatalog>(Phase9ClassesIntegration.CatalogPath);
        var settings = AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>(SettingsPath);
        bool first = !settings.Loadouts;
        var vendor = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mercader.prefab").GetComponentInChildren<NPCWeaponVendor>(true);
        foreach (var entry in catalog.healings)
        {
            var offer = vendor.Offers.Single(o => o.weaponId == entry.id && o.EsCuracion);
            if (entry.healing <= 0) entry.healing = offer.puntosDeSalud;
            if (first) entry.carryCount = Math.Max(1, 100 / entry.healing);
            if (!entry.icon || entry.healing <= 0 || entry.carryCount < 1 || entry.carryCount > 99) throw new InvalidOperationException("Invalid healing: " + entry.id);
        }
        settings.Loadouts = catalog;
        // PlayerHealth acquired synchronized class-supply data: reject older PvPvE binaries.
        settings.ProtocolVersion = Math.Max(settings.ProtocolVersion, 5010);
        for (int i = 0; i < 5; i++) if (!catalog.Validate(catalog.Default(i), out var error)) throw new InvalidOperationException(error);
        EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
    }
    public static void BuildAll()
    {
        Install();
        try { BuildHealing(); BuildMenu(); }
        finally { EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath); }
    }
    public static void BuildHealing()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Tests/PvPvE/Phase9/WeaponPresentationTest.unity");
        foreach (var driver in UnityEngine.Object.FindObjectsByType<Phase6WeaponsSmokeDriver>(FindObjectsInactive.Include)) UnityEngine.Object.DestroyImmediate(driver.gameObject);
        var go = new GameObject("Phase 10 healing validation");
        go.AddComponent<PvPvEMatchmaking>();
        var driver10 = go.AddComponent<PvPvEPhase10HealingDriver>();
        driver10.Settings = AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>(SettingsPath);
        driver10.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        SaveAndBuild("HealingValidation", "Healing/Healing.exe");
    }
    public static void BuildHealingAndRestoreMenu()
    {
        try { BuildHealing(); }
        finally { EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath); }
    }
    public static void BuildMenu()
    {
        var scene = EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
        foreach (var b in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)))
            if (b && (b is NetworkBootstrap || b.GetType().Name == "CharacterRenameManager" || b.GetType().Name == "AchievementsManager")) b.enabled = false;
        new GameObject("Phase 10 existing class UI validation").AddComponent<PvPvEPhase9SmokeDriver>();
        SaveAndBuild("ClassHealingMenu", "Menu/Classes.exe");
    }
    static void SaveAndBuild(string sceneName, string executable)
    {
        Directory.CreateDirectory("Assets/Tests/PvPvE/Phase10"); AssetDatabase.Refresh();
        string scenePath = "Assets/Tests/PvPvE/Phase10/" + sceneName + ".unity";
        if (!EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), scenePath)) throw new InvalidOperationException("Scene save failed");
        string target = Path.Combine(Output, executable); Directory.CreateDirectory(Path.GetDirectoryName(target));
        string company = PlayerSettings.companyName, product = PlayerSettings.productName;
        try
        {
            PlayerSettings.companyName = "DeadQuarantineValidation"; PlayerSettings.productName = "PvPvE Phase10 " + sceneName;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scenePath }, target = BuildTarget.StandaloneWindows64, locationPathName = target, options = BuildOptions.Development });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Phase10 build failed");
        }
        finally { PlayerSettings.companyName = company; PlayerSettings.productName = product; }
    }
}
#endif
