#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class RebindAnimationClipWindow : EditorWindow
{
    private GameObject fpsArmsRoot;
    private AnimationClip sourceClip;

    private string outputFolder = "Assets/Animations/Rebound";
    private string outputName = "";

    private Vector2 scroll;

    private readonly List<string> resolved = new List<string>();
    private readonly List<string> unresolved = new List<string>();

    [MenuItem("Tools/Animation/Rebind Animation Clip V3")]
    private static void OpenWindow()
    {
        GetWindow<RebindAnimationClipWindow>(
            "Rebind Animation V3"
        );
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField(
            "Rebind Animation Clip V3",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Esta herramienta está pensada para clips creados con una jerarquía "
            + "anterior. Seleccioná FPS_Arms como raíz y el clip con Missing. "
            + "La herramienta crea una COPIA y solamente cambia los paths de los "
            + "bindings. Los keyframes y Animation Events se conservan.",
            MessageType.Info
        );

        EditorGUILayout.Space(8);

        fpsArmsRoot = (GameObject)EditorGUILayout.ObjectField(
            "FPS_Arms",
            fpsArmsRoot,
            typeof(GameObject),
            true
        );

        sourceClip = (AnimationClip)EditorGUILayout.ObjectField(
            "Animation Clip",
            sourceClip,
            typeof(AnimationClip),
            false
        );

        outputFolder = EditorGUILayout.TextField(
            "Carpeta salida",
            outputFolder
        );

        if (string.IsNullOrWhiteSpace(outputName))
        {
            outputName =
                sourceClip != null
                    ? sourceClip.name + "_Fixed"
                    : "Animation_Fixed";
        }

        outputName = EditorGUILayout.TextField(
            "Nombre salida",
            outputName
        );

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Analizar"))
                Analizar();

            GUI.enabled =
                fpsArmsRoot != null &&
                sourceClip != null;

            if (GUILayout.Button("Crear Fixed"))
                CrearFixed();

            GUI.enabled = true;
        }

        EditorGUILayout.Space(8);

        scroll =
            EditorGUILayout.BeginScrollView(scroll);

        if (resolved.Count > 0)
        {
            EditorGUILayout.LabelField(
                "Bindings resueltos: " + resolved.Count,
                EditorStyles.boldLabel
            );

            foreach (string item in resolved)
            {
                EditorGUILayout.LabelField(
                    "OK  " + item,
                    EditorStyles.miniLabel
                );
            }
        }

        if (unresolved.Count > 0)
        {
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(
                "Bindings NO resueltos: " + unresolved.Count,
                EditorStyles.boldLabel
            );

            foreach (string item in unresolved)
            {
                EditorGUILayout.LabelField(
                    "MISSING  " + item,
                    EditorStyles.miniLabel
                );
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void Analizar()
    {
        resolved.Clear();
        unresolved.Clear();

        if (!ValidarSeleccion())
            return;

        foreach (
            EditorCurveBinding binding
            in AnimationUtility.GetCurveBindings(sourceClip)
        )
        {
            Transform target =
                ResolverBinding(
                    fpsArmsRoot.transform,
                    binding.path
                );

            if (target != null)
            {
                resolved.Add(
                    binding.path +
                    " -> " +
                    ObtenerRutaRelativa(
                        fpsArmsRoot.transform,
                        target
                    ) +
                    " [" +
                    binding.propertyName +
                    "]"
                );
            }
            else
            {
                unresolved.Add(
                    binding.path +
                    " [" +
                    binding.propertyName +
                    "]"
                );
            }
        }

        foreach (
            EditorCurveBinding binding
            in AnimationUtility.GetObjectReferenceCurveBindings(
                sourceClip
            )
        )
        {
            Transform target =
                ResolverBinding(
                    fpsArmsRoot.transform,
                    binding.path
                );

            if (target != null)
            {
                resolved.Add(
                    binding.path +
                    " -> " +
                    ObtenerRutaRelativa(
                        fpsArmsRoot.transform,
                        target
                    ) +
                    " [ObjectReference:"
                    + binding.propertyName +
                    "]"
                );
            }
            else
            {
                unresolved.Add(
                    binding.path +
                    " [ObjectReference:"
                    + binding.propertyName +
                    "]"
                );
            }
        }

        Repaint();
    }

    private void CrearFixed()
    {
        if (!ValidarSeleccion())
            return;

        CrearCarpeta(outputFolder);

        string fileName =
            string.IsNullOrWhiteSpace(outputName)
                ? sourceClip.name + "_Fixed"
                : outputName;

        fileName =
            SanitizarNombre(fileName);

        string assetPath =
            outputFolder.TrimEnd('/') +
            "/" +
            fileName +
            ".anim";

        assetPath =
            AssetDatabase.GenerateUniqueAssetPath(
                assetPath
            );

        AnimationClip fixedClip =
            new AnimationClip();

        // Copiamos el clip COMPLETO primero.
        // Esto conserva Events, frame rate, settings, etc.
        EditorUtility.CopySerialized(
            sourceClip,
            fixedClip
        );

        fixedClip.name =
            Path.GetFileNameWithoutExtension(
                assetPath
            );

        // Quitamos solamente las curvas.
        // Luego las agregamos con las rutas nuevas.
        LimpiarCurvas(fixedClip);

        int repairedCount = 0;
        int unresolvedCount = 0;

        foreach (
            EditorCurveBinding oldBinding
            in AnimationUtility.GetCurveBindings(
                sourceClip
            )
        )
        {
            AnimationCurve curve =
                AnimationUtility.GetEditorCurve(
                    sourceClip,
                    oldBinding
                );

            if (curve == null)
                continue;

            Transform target =
                ResolverBinding(
                    fpsArmsRoot.transform,
                    oldBinding.path
                );

            if (target == null)
            {
                unresolvedCount++;
                continue;
            }

            EditorCurveBinding newBinding =
                oldBinding;

            newBinding.path =
                ObtenerRutaRelativa(
                    fpsArmsRoot.transform,
                    target
                );

            AnimationUtility.SetEditorCurve(
                fixedClip,
                newBinding,
                curve
            );

            repairedCount++;
        }

        foreach (
            EditorCurveBinding oldBinding
            in AnimationUtility.GetObjectReferenceCurveBindings(
                sourceClip
            )
        )
        {
            ObjectReferenceKeyframe[] keyframes =
                AnimationUtility.GetObjectReferenceCurve(
                    sourceClip,
                    oldBinding
                );

            Transform target =
                ResolverBinding(
                    fpsArmsRoot.transform,
                    oldBinding.path
                );

            if (target == null)
            {
                unresolvedCount++;
                continue;
            }

            EditorCurveBinding newBinding =
                oldBinding;

            newBinding.path =
                ObtenerRutaRelativa(
                    fpsArmsRoot.transform,
                    target
                );

            AnimationUtility.SetObjectReferenceCurve(
                fixedClip,
                newBinding,
                keyframes
            );

            repairedCount++;
        }

        AssetDatabase.CreateAsset(
            fixedClip,
            assetPath
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject =
            fixedClip;

        EditorUtility.DisplayDialog(
            "Fixed creado",
            "Se creó una copia del clip.\\n\\n"
            + "Bindings reparados: "
            + repairedCount
            + "\\nBindings no resueltos: "
            + unresolvedCount
            + "\\n\\n"
            + assetPath,
            "OK"
        );

        Analizar();
    }

    private static Transform ResolverBinding(
        Transform fpsArms,
        string oldPath
    )
    {
        if (fpsArms == null)
            return null;

        // Root del propio FPS_Arms.
        if (string.IsNullOrEmpty(oldPath))
            return fpsArms;

        string[] parts =
            oldPath.Split('/');

        /*
         * Para tu estructura actual, el binding de la animación
         * de pistola tiene el patrón:
         *
         * SKEL_Handgun_03/root/magazine
         *
         * y la jerarquía nueva es:
         *
         * FPS_Arms
         *  -> Armature
         *  -> root
         *  -> ik_hand_root
         *  -> ik_hand_gun
         *  -> Weapon_Inventory
         *  -> SK_Handgun_03
         *  -> SKEL_Handgun_03
         *  -> root
         *  -> magazine
         *
         * Por eso se busca primero el SKEL_* exacto y,
         * a partir de él, se exige que el resto de la ruta
         * exista como hijos directos.
         */

        int skeletonIndex =
            EncontrarSkeletonEnBinding(parts);

        if (skeletonIndex >= 0)
        {
            string skeletonName =
                parts[skeletonIndex];

            List<Transform> skeletons =
                new List<Transform>();

            BuscarPorNombre(
                fpsArms,
                skeletonName,
                skeletons
            );

            if (skeletons.Count == 1)
            {
                Transform current =
                    skeletons[0];

                for (
                    int i = skeletonIndex + 1;
                    i < parts.Length;
                    i++
                )
                {
                    current =
                        BuscarHijoDirecto(
                            current,
                            parts[i]
                        );

                    if (current == null)
                        return null;
                }

                return current;
            }

            // Si aparecen varios skeletons iguales,
            // no adivinamos.
            if (skeletons.Count > 1)
                return null;
        }

        // Algunos clips pueden tener rutas que ya coincidan.
        Transform exact =
            fpsArms.Find(oldPath);

        if (exact != null)
            return exact;

        // Caso especial: binding directo al propio FPS_Arms.
        if (
            parts.Length == 1 &&
            parts[0] == fpsArms.name
        )
        {
            return fpsArms;
        }

        return null;
    }

    private static int EncontrarSkeletonEnBinding(
        string[] parts
    )
    {
        for (int i = 0; i < parts.Length; i++)
        {
            if (
                parts[i].StartsWith(
                    "SKEL_"
                )
            )
            {
                return i;
            }
        }

        return -1;
    }

    private static Transform BuscarHijoDirecto(
        Transform parent,
        string targetName
    )
    {
        if (parent == null)
            return null;

        for (
            int i = 0;
            i < parent.childCount;
            i++
        )
        {
            Transform child =
                parent.GetChild(i);

            if (
                child.name ==
                targetName
            )
            {
                return child;
            }
        }

        return null;
    }

    private static void BuscarPorNombre(
        Transform current,
        string targetName,
        List<Transform> results
    )
    {
        if (current == null)
            return;

        if (
            current.name ==
            targetName
        )
        {
            results.Add(current);
        }

        for (
            int i = 0;
            i < current.childCount;
            i++
        )
        {
            BuscarPorNombre(
                current.GetChild(i),
                targetName,
                results
            );
        }
    }

    private static string ObtenerRutaRelativa(
        Transform root,
        Transform target
    )
    {
        if (root == target)
            return "";

        List<string> names =
            new List<string>();

        Transform current =
            target;

        while (
            current != null &&
            current != root
        )
        {
            names.Add(
                current.name
            );

            current =
                current.parent;
        }

        if (current != root)
            return target.name;

        names.Reverse();

        return string.Join(
            "/",
            names
        );
    }

    private static void LimpiarCurvas(
        AnimationClip clip
    )
    {
        EditorCurveBinding[] bindings =
            AnimationUtility.GetCurveBindings(
                clip
            );

        foreach (
            EditorCurveBinding binding
            in bindings
        )
        {
            AnimationUtility.SetEditorCurve(
                clip,
                binding,
                null
            );
        }

        EditorCurveBinding[] objectBindings =
            AnimationUtility.GetObjectReferenceCurveBindings(
                clip
            );

        foreach (
            EditorCurveBinding binding
            in objectBindings
        )
        {
            AnimationUtility.SetObjectReferenceCurve(
                clip,
                binding,
                null
            );
        }
    }

    private bool ValidarSeleccion()
    {
        if (fpsArmsRoot == null)
        {
            EditorUtility.DisplayDialog(
                "Rebind Animation",
                "Seleccioná FPS_Arms.",
                "OK"
            );

            return false;
        }

        if (sourceClip == null)
        {
            EditorUtility.DisplayDialog(
                "Rebind Animation",
                "Seleccioná un Animation Clip.",
                "OK"
            );

            return false;
        }

        return true;
    }

    private static void CrearCarpeta(
        string folderPath
    )
    {
        folderPath =
            folderPath.Replace(
                "\\",
                "/"
            );

        if (
            AssetDatabase.IsValidFolder(
                folderPath
            )
        )
        {
            return;
        }

        string[] parts =
            folderPath.Split('/');

        if (parts.Length == 0)
            return;

        string current =
            parts[0];

        for (
            int i = 1;
            i < parts.Length;
            i++
        )
        {
            string next =
                current +
                "/" +
                parts[i];

            if (
                !AssetDatabase.IsValidFolder(
                    next
                )
            )
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[i]
                );
            }

            current = next;
        }
    }

    private static string SanitizarNombre(
        string value
    )
    {
        foreach (
            char invalid
            in Path.GetInvalidFileNameChars()
        )
        {
            value =
                value.Replace(
                    invalid.ToString(),
                    "_"
                );
        }

        return value;
    }
}
#endif
