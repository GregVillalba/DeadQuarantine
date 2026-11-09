using UnityEditor;using UnityEditor.Build;
public static class NovemberGameplayBuild {
 public static void Build(){var target=NamedBuildTarget.Standalone;string symbols=PlayerSettings.GetScriptingDefineSymbols(target);try{PlayerSettings.SetScriptingDefineSymbols(target,symbols.Replace("SENTIS_ANALYTICS_ENABLED;","").Replace(";SENTIS_ANALYTICS_ENABLED","").Replace("SENTIS_ANALYTICS_ENABLED",""));WeaponPoseModeBuild.Build();PvPvECaptureCorrections.BuildLegacy();}finally{PlayerSettings.SetScriptingDefineSymbols(target,symbols);}}
}

