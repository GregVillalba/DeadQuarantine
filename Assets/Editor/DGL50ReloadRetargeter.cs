using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DGL50ReloadBoneRetargeter
{
    private const string SourceFileToken = "A_FP_DGL50_Reload_Empty";
    private const string TargetModelName = "SK_TP_CH_Default";
    private const string OutputName = "A_FP_DGL50_Reload_Empty_MisBrazos.anim";

    // Solo transferimos la parte del cuerpo necesaria para una animación FPS.
    private static readonly string[] AllowedBonePrefixes =
    {
        "root",
        "pelvis",
        "spine_",
        "neck_",
        "head",
        "clavicle_",
        "upperarm_",
        "lowerarm_",
        "hand_",
        "thumb_",
        "index_",
        "middle_",
        "ring_",
        "pinky_",
        "ik_hand_"
    };

    [MenuItem("Tools/Dead Quarantine/Armas/Crear recarga DGL50 para mis brazos")]
    public static void Build()
    {
        string sourcePath = FindSourceFbxPath();

        if (string.IsNullOrEmpty(sourcePath))
        {
            Debug.LogError(
                "[DGL50] No encontré el FBX de recarga. " +
                "Seleccioná A_FP_DGL50_Reload_Empty.FBX en Project y ejecutá nuevamente la herramienta."
            );
            return;
        }

        GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        AnimationClip sourceClip = FindBestClipInFbx(sourcePath);
        GameObject targetAsset = FindTargetModel();

        if (sourceAsset == null)
        {
            Debug.LogError("[DGL50] No pude cargar el modelo fuente desde: " + sourcePath);
            return;
        }

        if (sourceClip == null)
        {
            Debug.LogError(
                "[DGL50] El FBX contiene datos de animación, pero Unity no expuso un AnimationClip utilizable. " +
                "Revisá la pestaña Animation del FBX y confirmá que Import Animation esté activado."
            );
            return;
        }

        if (targetAsset == null)
        {
            Debug.LogError(
                "[DGL50] No encontré el modelo objetivo '" + TargetModelName + "' dentro de Assets."
            );
            return;
        }

        GameObject sourceInstance = null;
        GameObject targetInstance = null;

        try
        {
            sourceInstance = UnityEngine.Object.Instantiate(sourceAsset);
            targetInstance = UnityEngine.Object.Instantiate(targetAsset);

            sourceInstance.name = "__DGL50_SOURCE__";
            targetInstance.name = "__DGL50_TARGET__";

            sourceInstance.hideFlags = HideFlags.HideAndDontSave;
            targetInstance.hideFlags = HideFlags.HideAndDontSave;

            Dictionary<string, Transform> sourceBones = BuildBoneMap(sourceInstance.transform);
            Dictionary<string, Transform> targetBones = BuildBoneMap(targetInstance.transform);

            Dictionary<string, BonePose> sourceBind = CapturePose(sourceBones);
            Dictionary<string, BonePose> targetBind = CapturePose(targetBones);

            List<string> sharedBones = sourceBones.Keys
                .Where(targetBones.ContainsKey)
                .Where(IsAllowedBone)
                .OrderBy(GetBoneSortOrder)
                .ThenBy(n => n)
                .ToList();

            if (sharedBones.Count < 20)
            {
                Debug.LogError(
                    "[DGL50] Solo encontré " + sharedBones.Count +
                    " huesos compatibles. Se esperaba un esqueleto mucho más parecido."
                );
                return;
            }

            float globalScale = CalculateGlobalTranslationScale(
                sharedBones,
                sourceBind,
                targetBind
            );

            float frameRate = Mathf.Max(1f, sourceClip.frameRate);
            float length = Mathf.Max(0.01f, sourceClip.length);
            int totalFrames = Mathf.Max(1, Mathf.CeilToInt(length * frameRate));

            AnimationClip output = new AnimationClip
            {
                name = Path.GetFileNameWithoutExtension(OutputName),
                frameRate = frameRate
            };

            Dictionary<string, Curves> curves = new Dictionary<string, Curves>();

            foreach (string boneName in sharedBones)
                curves[boneName] = new Curves();

            for (int frame = 0; frame <= totalFrames; frame++)
            {
                float time = Mathf.Min(length, frame / frameRate);

                RestorePose(sourceBones, sourceBind);

                // A diferencia de AnimationMode.SampleAnimationClip, esta API
                // puede muestrear directamente el clip sobre su propio modelo.
                sourceClip.SampleAnimation(sourceInstance, time);

                foreach (string boneName in sharedBones)
                {
                    Transform sourceBone = sourceBones[boneName];

                    BonePose sourceBase = sourceBind[boneName];
                    BonePose targetBase = targetBind[boneName];

                    // Movimiento ROTACIONAL del DGL50 respecto de su pose base.
                    Quaternion relativeRotation =
                        Quaternion.Inverse(sourceBase.localRotation) *
                        sourceBone.localRotation;

                    // Aplicamos ese mismo cambio sobre el bind pose de tus brazos.
                    Quaternion finalRotation =
                        targetBase.localRotation *
                        relativeRotation;

                    finalRotation = Normalize(finalRotation);

                    // Movimiento de POSICIÓN relativo. Se escala automáticamente
                    // porque el FBX DGL50 está aproximadamente en centímetros y
                    // el modelo objetivo en metros.
                    Vector3 sourcePositionDelta =
                        sourceBone.localPosition -
                        sourceBase.localPosition;

                    float boneScale = GetBoneTranslationScale(
                        boneName,
                        sourceBase.localPosition,
                        targetBase.localPosition,
                        globalScale
                    );

                    Vector3 finalPosition =
                        targetBase.localPosition +
                        sourcePositionDelta * boneScale;

                    Curves c = curves[boneName];

                    AddKey(c.px, time, finalPosition.x);
                    AddKey(c.py, time, finalPosition.y);
                    AddKey(c.pz, time, finalPosition.z);

                    AddKey(c.rx, time, finalRotation.x);
                    AddKey(c.ry, time, finalRotation.y);
                    AddKey(c.rz, time, finalRotation.z);
                    AddKey(c.rw, time, finalRotation.w);
                }
            }

            foreach (string boneName in sharedBones)
            {
                Transform targetBone = targetBones[boneName];
                string targetPath = AnimationUtility.CalculateTransformPath(
                    targetBone,
                    targetInstance.transform
                );

                Curves c = curves[boneName];

                SetCurve(output, targetPath, "m_LocalPosition.x", c.px);
                SetCurve(output, targetPath, "m_LocalPosition.y", c.py);
                SetCurve(output, targetPath, "m_LocalPosition.z", c.pz);

                SetCurve(output, targetPath, "m_LocalRotation.x", c.rx);
                SetCurve(output, targetPath, "m_LocalRotation.y", c.ry);
                SetCurve(output, targetPath, "m_LocalRotation.z", c.rz);
                SetCurve(output, targetPath, "m_LocalRotation.w", c.rw);
            }

            output.EnsureQuaternionContinuity();

            AnimationClipSettings clipSettings =
                AnimationUtility.GetAnimationClipSettings(output);

            clipSettings.loopTime = false;
            clipSettings.loopBlend = false;
            clipSettings.keepOriginalOrientation = true;
            clipSettings.keepOriginalPositionY = true;
            clipSettings.keepOriginalPositionXZ = true;
            clipSettings.heightFromFeet = false;

            AnimationUtility.SetAnimationClipSettings(output, clipSettings);

            string outputPath = Path.Combine(
                Path.GetDirectoryName(sourcePath) ?? "Assets",
                OutputName
            ).Replace("\\", "/");

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outputPath) != null)
                AssetDatabase.DeleteAsset(outputPath);

            AssetDatabase.CreateAsset(output, outputPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);

            EditorGUIUtility.PingObject(Selection.activeObject);

            Debug.Log(
                "[DGL50] Recarga generada correctamente.\n" +
                "Clip fuente real: " + sourceClip.name + "\n" +
                "Duración: " + sourceClip.length.ToString("0.###") + " s\n" +
                "FPS: " + sourceClip.frameRate.ToString("0.##") + "\n" +
                "Huesos transferidos: " + sharedBones.Count + "\n" +
                "Escala de posición detectada: " + globalScale.ToString("0.#####") + "\n" +
                "Salida: " + outputPath
            );
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            if (sourceInstance != null)
                UnityEngine.Object.DestroyImmediate(sourceInstance);

            if (targetInstance != null)
                UnityEngine.Object.DestroyImmediate(targetInstance);
        }
    }

    private static string FindSourceFbxPath()
    {
        UnityEngine.Object selected = Selection.activeObject;

        if (selected != null)
        {
            string selectedPath = AssetDatabase.GetAssetPath(selected);

            if (!string.IsNullOrEmpty(selectedPath) &&
                selectedPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileNameWithoutExtension(selectedPath)
                    .IndexOf(SourceFileToken, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return selectedPath;
            }
        }

        string[] guids = AssetDatabase.FindAssets(SourceFileToken);

        string bestPath = null;
        int bestScore = int.MinValue;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                continue;

            string fileName = Path.GetFileNameWithoutExtension(path);

            if (fileName.IndexOf(
                    SourceFileToken,
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            // Preferimos el nombre exacto por sobre copias "(1)".
            int score = string.Equals(
                fileName,
                SourceFileToken,
                StringComparison.OrdinalIgnoreCase)
                ? 1000
                : 100 - fileName.Length;

            if (score > bestScore)
            {
                bestScore = score;
                bestPath = path;
            }
        }

        return bestPath;
    }

    private static AnimationClip FindBestClipInFbx(string fbxPath)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);

        AnimationClip best = null;

        foreach (UnityEngine.Object asset in assets)
        {
            AnimationClip clip = asset as AnimationClip;

            if (clip == null)
                continue;

            if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                continue;

            if (best == null || clip.length > best.length)
                best = clip;
        }

        return best;
    }

    private static GameObject FindTargetModel()
    {
        string[] guids = AssetDatabase.FindAssets(TargetModelName);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (go != null &&
                string.Equals(
                    go.name,
                    TargetModelName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return go;
            }
        }

        return null;
    }

    private static Dictionary<string, Transform> BuildBoneMap(Transform root)
    {
        Dictionary<string, Transform> result =
            new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);

        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            string cleanName = CleanBoneName(transform.name);

            if (string.IsNullOrEmpty(cleanName))
                continue;

            if (!result.ContainsKey(cleanName))
                result.Add(cleanName, transform);
        }

        return result;
    }

    private static string CleanBoneName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        int colon = value.LastIndexOf(':');

        if (colon >= 0 && colon < value.Length - 1)
            value = value.Substring(colon + 1);

        return value.Trim();
    }

    private static bool IsAllowedBone(string boneName)
    {
        foreach (string prefix in AllowedBonePrefixes)
        {
            if (boneName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static int GetBoneSortOrder(string name)
    {
        if (string.Equals(name, "root", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (string.Equals(name, "pelvis", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (name.StartsWith("spine_", StringComparison.OrdinalIgnoreCase))
            return 2;
        if (name.StartsWith("clavicle_", StringComparison.OrdinalIgnoreCase))
            return 3;
        if (name.StartsWith("upperarm_", StringComparison.OrdinalIgnoreCase))
            return 4;
        if (name.StartsWith("lowerarm_", StringComparison.OrdinalIgnoreCase))
            return 5;
        if (name.StartsWith("hand_", StringComparison.OrdinalIgnoreCase))
            return 6;

        return 10;
    }

    private static Dictionary<string, BonePose> CapturePose(
        Dictionary<string, Transform> bones)
    {
        Dictionary<string, BonePose> pose =
            new Dictionary<string, BonePose>(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, Transform> pair in bones)
        {
            Transform transform = pair.Value;

            pose[pair.Key] = new BonePose
            {
                localPosition = transform.localPosition,
                localRotation = transform.localRotation,
                localScale = transform.localScale
            };
        }

        return pose;
    }

    private static void RestorePose(
        Dictionary<string, Transform> bones,
        Dictionary<string, BonePose> pose)
    {
        foreach (KeyValuePair<string, Transform> pair in bones)
        {
            BonePose p;

            if (!pose.TryGetValue(pair.Key, out p))
                continue;

            pair.Value.localPosition = p.localPosition;
            pair.Value.localRotation = p.localRotation;
            pair.Value.localScale = p.localScale;
        }
    }

    private static float CalculateGlobalTranslationScale(
        List<string> sharedBones,
        Dictionary<string, BonePose> sourceBind,
        Dictionary<string, BonePose> targetBind)
    {
        List<float> ratios = new List<float>();

        foreach (string boneName in sharedBones)
        {
            float sourceLength = sourceBind[boneName].localPosition.magnitude;
            float targetLength = targetBind[boneName].localPosition.magnitude;

            if (sourceLength < 0.0001f || targetLength < 0.000001f)
                continue;

            float ratio = targetLength / sourceLength;

            if (ratio > 0.0001f && ratio < 10f)
                ratios.Add(ratio);
        }

        if (ratios.Count == 0)
            return 0.01f;

        ratios.Sort();

        return ratios[ratios.Count / 2];
    }

    private static float GetBoneTranslationScale(
        string boneName,
        Vector3 sourceBindPosition,
        Vector3 targetBindPosition,
        float globalScale)
    {
        float sourceLength = sourceBindPosition.magnitude;
        float targetLength = targetBindPosition.magnitude;

        if (sourceLength > 0.0001f && targetLength > 0.000001f)
        {
            float ratio = targetLength / sourceLength;

            // Evitamos que ruido de huesos muy pequeños genere escalas absurdas.
            if (ratio >= globalScale * 0.5f &&
                ratio <= globalScale * 2f)
            {
                return ratio;
            }
        }

        return globalScale;
    }

    private static Quaternion Normalize(Quaternion q)
    {
        float magnitude = Mathf.Sqrt(
            q.x * q.x +
            q.y * q.y +
            q.z * q.z +
            q.w * q.w
        );

        if (magnitude < 0.000001f)
            return Quaternion.identity;

        float inverse = 1f / magnitude;

        q.x *= inverse;
        q.y *= inverse;
        q.z *= inverse;
        q.w *= inverse;

        return q;
    }

    private static void SetCurve(
        AnimationClip clip,
        string path,
        string propertyName,
        AnimationCurve curve)
    {
        EditorCurveBinding binding = new EditorCurveBinding
        {
            path = path,
            type = typeof(Transform),
            propertyName = propertyName
        };

        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static void AddKey(
        AnimationCurve curve,
        float time,
        float value)
    {
        curve.AddKey(new Keyframe(time, value));
    }

    private struct BonePose
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    private sealed class Curves
    {
        public readonly AnimationCurve px = new AnimationCurve();
        public readonly AnimationCurve py = new AnimationCurve();
        public readonly AnimationCurve pz = new AnimationCurve();

        public readonly AnimationCurve rx = new AnimationCurve();
        public readonly AnimationCurve ry = new AnimationCurve();
        public readonly AnimationCurve rz = new AnimationCurve();
        public readonly AnimationCurve rw = new AnimationCurve();
    }
}
