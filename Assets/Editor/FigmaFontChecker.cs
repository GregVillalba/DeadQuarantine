using UnityEditor;
using UnityEngine;
using TMPro;
using System.IO;

public static class FigmaFontChecker
{
    [MenuItem("Figma Bridge/Check Geist Mono Fonts")]
    public static void CheckGeistMono()
    {
        string family = "Geist Mono";
        int weight400 = 400;
        int weight600 = 600;
        string ttfPath400 = $"Assets/Figma/Fonts/{family}_{weight400}.ttf";
        string ttfPath600 = $"Assets/Figma/Fonts/{family}_{weight600}.ttf";
        string tmpPath400 = $"Assets/Figma/Fonts/{family}_{weight400}_SDF.asset";
        string tmpPath600 = $"Assets/Figma/Fonts/{family}_{weight600}_SDF.asset";

        Debug.Log($"Checking file system existence:");
        Debug.Log($"  TTF 400 path: {ttfPath400} -> File.Exists = {File.Exists(ttfPath400)}");
        Debug.Log($"  TTF 600 path: {ttfPath600} -> File.Exists = {File.Exists(ttfPath600)}");
        Debug.Log($"  TMP 400 path: {tmpPath400} -> File.Exists = {File.Exists(tmpPath400)}");
        Debug.Log($"  TMP 600 path: {tmpPath600} -> File.Exists = {File.Exists(tmpPath600)}");

        var tmp400 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(tmpPath400);
        var tmp600 = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(tmpPath600);

        Debug.Log($"AssetDatabase.LoadAssetAtPath TMP 400 -> {(tmp400 != null ? tmp400.name : "null")}");
        Debug.Log($"AssetDatabase.LoadAssetAtPath TMP 600 -> {(tmp600 != null ? tmp600.name : "null")}");

        var allTmpGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        Debug.Log($"Total TMP_FontAsset in project: {allTmpGuids.Length}");
        foreach (var g in allTmpGuids)
        {
            Debug.Log($"  {AssetDatabase.GUIDToAssetPath(g)}");
        }
    }
}