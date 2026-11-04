#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dead Quarantine - Unico comando para restaurar apuntado FPS sin alterar
/// los modelos, las poses manuales, los clips ni las animaciones TP.
/// Sustituye al antiguo DQWeaponsUnifiedBuilder (menu desordenado).
/// </summary>
public static class DQWeaponsUnifiedBuilder
{
    private const string MenuPath = "Game/Dead Quarantine/Armas/UNIFICADO - Corregir apuntado";

    // Configuracion COMPROBADA en el Player(6).prefab anterior al parche regresivo.
    // AutoPistol, P90 y MAC10 estaban bien sin modificar la proyeccion.
    // SemiRifle, BurstRifle y Vector usaban la compensacion EXISTENTE en Weapon.cs.
    private static readonly string[] Ids =
        { "AutoPistol", "SemiRifle", "BurstRifle", "P90", "Vector", "MAC10" };
    private static readonly bool[] ProjectionEnabled =
        { false, true, true, false, true, false };

    // Quitar SOLO los atributos de menu, no desinstalar los editores ni modificar sus metodos.
    // Esto oculta las opciones viejas; los scripts se conservan y siguen compilando.
    private static readonly string[] OldEditorFileNames =
    {
        "CompleteWeaponsIntegrationBuilder.cs",
        "DQArmasDiagnosticoRig.cs",
        "DQEstabilizarRiflesSinReimportar.cs",
        "DQNewWeaponsAnimationRepair.cs",
        "DQRepairRifleAnimationRigs.cs",
        "DQWeaponsRealRepair.cs",
        "DQRecuperacionSegura.cs"
    };

    private sealed class AimSlot
    {
        public string Id;
        public Weapon Component;
        public bool Projection;
    }

    private sealed class MenuChange
    {
        public string AbsolutePath;
        public string RelativePath;
        public string NewContent;
        public int HiddenCount;
    }

    [MenuItem(MenuPath)]
    public static void FixAiming()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Dead Quarantine", "Sali de Play Mode antes de usar esta opcion.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Dead Quarantine - apuntado FPS",
            "UNA SOLA ACCION: recuperar el ADS de las seis armas nuevas.\n\n" +
            "- Eliminar el DQADSVisualAligner que desplaza todo FPS_Arms.\n" +
            "- Recuperar la configuracion de proyeccion anterior: tres armas ya funcionales quedan sin compensacion; las otras tres mantienen la de Weapon.cs.\n" +
            "- Ocultar las opciones antiguas del menu de armas (sin borrar sus scripts).\n\n" +
            "NO se modifican: posicion/rotacion, rig, clips, overrides TP, materiales, tienda, red, disparos ni Weapon.cs.\n" +
            "Se guardara un respaldo fuera de Assets ANTES de cambiar nada.",
            "Respaldar y corregir", "Cancelar"))
            return;

        string prefabPath = null;
        string backupDir = null;
        GameObject prefab = null;
        bool prefabSaved = false;
        var touchedMenus = new List<MenuChange>();
        var log = new List<string> { "DQ ADS UNIFICADO", DateTime.Now.ToString("O") };
        try
        {
            prefabPath = FindMainPlayer();
            if (string.IsNullOrEmpty(prefabPath))
                throw new InvalidOperationException("No se encontro un Player.prefab unico con WeaponSwitcher. No se cambio nada.");

            prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefab == null) throw new InvalidOperationException("Unity no pudo abrir " + prefabPath);

            WeaponSwitcher switcher = prefab.GetComponentInChildren<WeaponSwitcher>(true);
            if (switcher == null) throw new InvalidOperationException("El prefab no tiene WeaponSwitcher.");
            if (prefab.GetComponentInChildren<WeaponMotionApplier>(true) == null)
                throw new InvalidOperationException("No existe WeaponMotionApplier: se cancela sin inventar una jerarquia FPS.");

            var slotsProperty = new SerializedObject(switcher).FindProperty("slots");
            if (slotsProperty == null || !slotsProperty.isArray)
                throw new InvalidOperationException("No se encontraron los slots de armas del jugador.");

            // Prevalidar TODOS los slots antes de guardar o tocar valores.
            var aims = new List<AimSlot>();
            for (int i = 0; i < Ids.Length; i++)
            {
                SerializedProperty slot = FindSlot(slotsProperty, Ids[i]);
                if (slot == null)
                    throw new InvalidOperationException("Falta el arma " + Ids[i] + " en el Player actual. No se aplica parcialmente.");
                var wp = slot.FindPropertyRelative("weaponComponent");
                Weapon weapon = wp != null ? wp.objectReferenceValue as Weapon : null;
                if (weapon == null) throw new InvalidOperationException("No hay Weapon para " + Ids[i]);
                var properties = new SerializedObject(weapon);
                if (properties.FindProperty("adsCenterVisualSight") == null)
                    throw new InvalidOperationException("Weapon.cs no contiene adsCenterVisualSight (" + Ids[i] + ").");
                if (properties.FindProperty("adsMaxViewportCorrection") == null)
                    throw new InvalidOperationException("Weapon.cs no contiene adsMaxViewportCorrection (" + Ids[i] + ").");
                if (properties.FindProperty("weaponCamera") == null ||
                    properties.FindProperty("weaponCamera").objectReferenceValue == null)
                    throw new InvalidOperationException("Falta WeaponCamera para " + Ids[i] + ".");
                aims.Add(new AimSlot { Id = Ids[i], Component = weapon, Projection = ProjectionEnabled[i] });
            }

            // Planear la limpieza del menu SIN tocar los fuentes hasta que el ADS se guarde.
            touchedMenus = PlanLegacyMenuCleanup(log);

            // Respaldo independiente: no exige TXT ni carpetas OriginalTP de parches anteriores.
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            backupDir = Path.Combine(projectRoot, "DQ_ADS_Backups", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(backupDir);
            BackupFile(Absolute(prefabPath), prefabPath, backupDir);
            foreach (MenuChange edit in touchedMenus)
                BackupFile(edit.AbsolutePath, edit.RelativePath, backupDir);
            log.Add("Respaldo completo: " + backupDir);

            int deleted = 0;
            // No hace falta recompilar ni tener DQADSVisualAligner como dependencia de codigo.
            foreach (MonoBehaviour mb in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType().Name != "DQADSVisualAligner") continue;
                UnityEngine.Object.DestroyImmediate(mb);
                deleted++;
            }
            log.Add("Alineadores FPS eliminados: " + deleted);

            foreach (AimSlot aim in aims)
            {
                var props = new SerializedObject(aim.Component);
                SerializedProperty enabled = props.FindProperty("adsCenterVisualSight");
                SerializedProperty correction = props.FindProperty("adsMaxViewportCorrection");
                bool prior = enabled.boolValue;
                enabled.boolValue = aim.Projection;
                if (aim.Projection)
                {
                    // Era 0.35 en los 3 slots del prefab recibido.
                    // Es correccion VISUAL de proyeccion; NO mueve modelos ni camaras.
                    correction.floatValue = 0.35f;
                }
                props.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(aim.Component);
                log.Add(aim.Id + ": proyeccion " + prior + " -> " + aim.Projection +
                        (aim.Projection ? " (limite 0.35 viewport)" : " (sin compensacion)"));
            }

            if (PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath) == null)
                throw new InvalidOperationException("Unity no pudo guardar Player.prefab; backup disponible.");
            prefabSaved = true;
            PrefabUtility.UnloadPrefabContents(prefab);
            prefab = null;

            // Solo al final se ocultan menus antiguos. No altera ningun metodo de juego.
            foreach (MenuChange edit in touchedMenus)
            {
                File.WriteAllText(edit.AbsolutePath, edit.NewContent);
                log.Add("Menu oculto: " + edit.RelativePath + " (" + edit.HiddenCount + " entradas)");
            }

            string reportPath = Path.Combine(backupDir, "INFORME.txt");
            File.WriteAllLines(reportPath, log);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[DQ ADS UNIFICADO] " + string.Join("\n", log));
            EditorUtility.DisplayDialog("Dead Quarantine - ADS recuperado",
                "Se elimino el movimiento adicional de FPS_Arms: " + deleted + " componente(s).\n" +
                "Restaurados los valores ADS de 6 armas.\n" +
                "Ocultadas " + touchedMenus.Sum(x => x.HiddenCount) + " opciones de builders anteriores.\n\n" +
                "No se tocaron animaciones, materiales, tienda o modelos.\n\n" +
                "BACKUP: " + backupDir + "\n\n" +
                "Proba primero AutoPistol, P90 y MAC10. Luego SemiRifle, BurstRifle y Vector.", "OK");
        }
        catch (Exception error)
        {
            if (prefab != null) { PrefabUtility.UnloadPrefabContents(prefab); prefab = null; }
            // Si el guardado o la limpieza falla, intentar restaurar lo que se llego a escribir.
            if (!string.IsNullOrEmpty(backupDir) && Directory.Exists(backupDir))
            {
                try
                {
                    if (prefabSaved && !string.IsNullOrEmpty(prefabPath))
                        RestoreFile(prefabPath, backupDir);
                    foreach (MenuChange edit in touchedMenus)
                        RestoreFile(edit.RelativePath, backupDir);
                    AssetDatabase.Refresh();
                }
                catch (Exception rollbackError)
                {
                    Debug.LogError("[DQ ADS UNIFICADO] Reversion automatica incompleta: " + rollbackError);
                }
            }
            Debug.LogError("[DQ ADS UNIFICADO] Operacion cancelada: " + error);
            EditorUtility.DisplayDialog("DQ - no se completo la reparacion",
                error.Message + "\n\nRespaldo (si existe): " + (backupDir ?? "ninguno") +
                "\nNo sigas ejecutando reparadores anteriores.", "OK");
        }
        finally
        {
            if (prefab != null) PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    private static SerializedProperty FindSlot(SerializedProperty slots, string id)
    {
        for (int i = 0; i < slots.arraySize; i++)
        {
            var item = slots.GetArrayElementAtIndex(i);
            var name = item.FindPropertyRelative("weaponId");
            if (name != null && name.stringValue == id) return item;
        }
        return null;
    }

    private static string FindMainPlayer()
    {
        const string preferred = "Assets/Prefabs/Player.prefab";
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(preferred);
        if (go != null && go.GetComponentInChildren<WeaponSwitcher>(true) != null) return preferred;

        var results = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("Player t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(Path.GetFileName(path), "Player.prefab", StringComparison.OrdinalIgnoreCase)) continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponentInChildren<WeaponSwitcher>(true) != null)
                results.Add(path);
        }
        if (results.Count > 1)
            throw new InvalidOperationException("Hay varios Player.prefab con WeaponSwitcher: " +
                                                string.Join(", ", results) + ". No adivino cual usa la escena.");
        return results.Count == 1 ? results[0] : null;
    }

    private static List<MenuChange> PlanLegacyMenuCleanup(List<string> log)
    {
        var result = new List<MenuChange>();
        // Reconoce un atributo por linea. No toca el contenido de los metodos ni sus clases.
        var attribute = new Regex(@"(?m)^(?<indent>[ \t]*)\[MenuItem\((?<args>[^\r\n]*)\)\][ \t]*$", RegexOptions.CultureInvariant);
        foreach (string basename in OldEditorFileNames)
        {
            var matches = Directory.GetFiles(Application.dataPath, basename, SearchOption.AllDirectories);
            if (matches.Length == 0) continue;
            if (matches.Length > 1)
            {
                log.Add("AVISO: hay duplicados de " + basename + "; no se oculta su menu.");
                continue;
            }
            string file = matches[0];
            string oldText = File.ReadAllText(file);
            int changed = 0;
            string newText = attribute.Replace(oldText, m =>
            {
                string arg = m.Groups["args"].Value;
                bool oldArmas = arg.Contains("Game/Dead Quarantine/Armas/");
                bool formerRepair = basename == "DQWeaponsRealRepair.cs" && arg.Trim() == "Menu";
                bool recovery = basename == "DQRecuperacionSegura.cs" && arg.TrimStart().StartsWith("MenuRoot + ", StringComparison.Ordinal);
                if (!oldArmas && !formerRepair && !recovery) return m.Value;
                changed++;
                return m.Groups["indent"].Value + "// MENU DESACTIVADO POR DQWeaponsUnifiedBuilder: " +
                       m.Value.TrimStart();
            });
            if (changed < 1) continue;
            string relative = "Assets" + file.Substring(Application.dataPath.Length).Replace('\\', '/');
            result.Add(new MenuChange { AbsolutePath = file, RelativePath = relative,
                                        NewContent = newText, HiddenCount = changed });
        }
        return result;
    }

    private static string Absolute(string assetPath)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void BackupFile(string absolutePath, string relativePath, string backupDir)
    {
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("No se encuentra el archivo a respaldar", absolutePath);
        string dest = Path.Combine(backupDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(dest));
        File.Copy(absolutePath, dest, true);
        if (!File.Exists(dest) || new FileInfo(dest).Length != new FileInfo(absolutePath).Length)
            throw new IOException("No se completo el respaldo de " + relativePath);
    }

    private static void RestoreFile(string relativePath, string backupDir)
    {
        string source = Path.Combine(backupDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(source)) return;
        File.Copy(source, Absolute(relativePath), true);
    }
}
#endif
