using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;

public class SceneMaterialsToURP : EditorWindow
{
    private bool convertOnlyNonURP = true;
    private bool copyStandardProperties = true;
    private bool copyAllTextureProperties = true;

    private const string OUTPUT_ROOT = "Assets/Materials/Scene_URP";

    [MenuItem("Tools/Materials/Convertir escena a URP Lit")]
    public static void ShowWindow()
    {
        GetWindow<SceneMaterialsToURP>(
            "Scene Materials → URP"
        );
    }

    private void OnGUI()
    {
        GUILayout.Space(10);

        EditorGUILayout.LabelField(
            "Conversor de materiales de la escena",
            EditorStyles.boldLabel
        );

        GUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "Convierte únicamente los materiales utilizados por la escena activa. " +
            "Los materiales originales y sus texturas NO se modifican.",
            MessageType.Info
        );

        GUILayout.Space(10);

        convertOnlyNonURP = EditorGUILayout.Toggle(
            "Solo materiales que NO sean URP",
            convertOnlyNonURP
        );

        copyStandardProperties = EditorGUILayout.Toggle(
            "Copiar propiedades Standard",
            copyStandardProperties
        );

        copyAllTextureProperties = EditorGUILayout.Toggle(
            "Copiar todas las texturas",
            copyAllTextureProperties
        );

        GUILayout.Space(15);

        if (GUILayout.Button(
            "CONVERTIR ESCENA",
            GUILayout.Height(40)
        ))
        {
            ConvertActiveScene();
        }

        GUILayout.Space(5);

        if (GUILayout.Button(
            "REPARAR TEXTURAS DE LA ESCENA",
            GUILayout.Height(35)
        ))
        {
            RepairActiveSceneTextures();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("ABRIR CARPETA DE MATERIALES"))
        {
            OpenMaterialsFolder();
        }
    }

    // ============================================================
    // CONVERTIR ESCENA
    // ============================================================

    private void ConvertActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid())
        {
            Debug.LogError(
                "[SceneMaterialsToURP] La escena activa no es válida."
            );

            return;
        }

        if (string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError(
                "[SceneMaterialsToURP] La escena debe estar guardada."
            );

            return;
        }

        string outputFolder = GetSceneFolder();

        Renderer[] renderers =
            GetSceneRenderers(scene);

        int materialsConverted = 0;
        int materialsSkipped = 0;
        int objectsProcessed = 0;

        Dictionary<Material, Material> convertedMaterials =
            new Dictionary<Material, Material>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            bool rendererChanged = false;

            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material originalMaterial = materials[i];

                if (originalMaterial == null)
                    continue;

                if (convertOnlyNonURP &&
                    IsURPMaterial(originalMaterial))
                {
                    materialsSkipped++;
                    continue;
                }

                Material convertedMaterial;

                if (convertedMaterials.TryGetValue(
                    originalMaterial,
                    out convertedMaterial))
                {
                    materials[i] = convertedMaterial;
                    rendererChanged = true;
                    continue;
                }

                convertedMaterial =
                    CreateURPMaterial(
                        originalMaterial,
                        outputFolder
                    );

                if (convertedMaterial == null)
                    continue;

                convertedMaterials.Add(
                    originalMaterial,
                    convertedMaterial
                );

                materials[i] = convertedMaterial;

                rendererChanged = true;
                materialsConverted++;
            }

            if (rendererChanged)
            {
                renderer.sharedMaterials = materials;
                objectsProcessed++;
                EditorUtility.SetDirty(renderer);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            "[SceneMaterialsToURP] Conversión terminada.\n" +
            "Escena: " + scene.name + "\n" +
            "Objetos procesados: " + objectsProcessed + "\n" +
            "Materiales convertidos: " + materialsConverted + "\n" +
            "Materiales omitidos: " + materialsSkipped + "\n" +
            "Carpeta: " + outputFolder
        );

        EditorUtility.DisplayDialog(
            "Conversión terminada",
            "La escena fue convertida correctamente.\n\n" +
            "Materiales convertidos: " +
            materialsConverted +
            "\n\n" +
            "Los materiales originales no fueron modificados.",
            "OK"
        );
    }

    // ============================================================
    // CREAR MATERIAL URP
    // ============================================================

    private Material CreateURPMaterial(
        Material original,
        string outputFolder
    )
    {
        if (original == null)
            return null;

        Shader urpShader = Shader.Find(
            "Universal Render Pipeline/Lit"
        );

        if (urpShader == null)
        {
            Debug.LogError(
                "[SceneMaterialsToURP] No se encontró " +
                "Universal Render Pipeline/Lit."
            );

            return null;
        }

        string safeName =
            MakeSafeFileName(original.name);

        string path =
            outputFolder +
            "/" +
            safeName +
            "_URP.mat";

        // Si ya existe, reutilizamos el material.
        Material existing =
            AssetDatabase.LoadAssetAtPath<Material>(path);

        Material converted;

        if (existing != null)
        {
            converted = existing;
        }
        else
        {
            converted = new Material(urpShader);

            AssetDatabase.CreateAsset(
                converted,
                path
            );
        }

        converted.shader = urpShader;

        if (copyStandardProperties)
        {
            CopyStandardProperties(
                original,
                converted
            );
        }

        if (copyAllTextureProperties)
        {
            CopyTextureProperties(
                original,
                converted
            );
        }

        EditorUtility.SetDirty(converted);

        return converted;
    }

    // ============================================================
    // PROPIEDADES STANDARD
    // ============================================================

    private void CopyStandardProperties(
        Material original,
        Material converted
    )
    {
        // Color principal

        if (original.HasProperty("_Color") &&
            converted.HasProperty("_BaseColor"))
        {
            converted.SetColor(
                "_BaseColor",
                original.GetColor("_Color")
            );
        }

        // Metallic

        if (original.HasProperty("_Metallic") &&
            converted.HasProperty("_Metallic"))
        {
            converted.SetFloat(
                "_Metallic",
                original.GetFloat("_Metallic")
            );
        }

        // Smoothness

        if (original.HasProperty("_Glossiness") &&
            converted.HasProperty("_Smoothness"))
        {
            converted.SetFloat(
                "_Smoothness",
                original.GetFloat("_Glossiness")
            );
        }

        // Algunas versiones usan _GlossMapScale

        if (original.HasProperty("_GlossMapScale") &&
            converted.HasProperty("_Smoothness"))
        {
            converted.SetFloat(
                "_Smoothness",
                original.GetFloat("_GlossMapScale")
            );
        }

        // Emission Color

        if (original.HasProperty("_EmissionColor") &&
            converted.HasProperty("_EmissionColor"))
        {
            converted.SetColor(
                "_EmissionColor",
                original.GetColor("_EmissionColor")
            );
        }
    }

    // ============================================================
    // TEXTURAS
    // ============================================================

    private void CopyTextureProperties(
        Material original,
        Material converted
    )
    {
        // --------------------------------------------------------
        // ALBEDO / BASE MAP
        // --------------------------------------------------------

        CopyTexture(
            original,
            "_MainTex",
            converted,
            "_BaseMap"
        );

        // --------------------------------------------------------
        // NORMAL MAP
        // --------------------------------------------------------

        CopyTexture(
            original,
            "_BumpMap",
            converted,
            "_BumpMap"
        );

        // --------------------------------------------------------
        // METALLIC
        // --------------------------------------------------------

        CopyTexture(
            original,
            "_MetallicGlossMap",
            converted,
            "_MetallicGlossMap"
        );

        // --------------------------------------------------------
        // OCCLUSION
        // --------------------------------------------------------

        CopyTexture(
            original,
            "_OcclusionMap",
            converted,
            "_OcclusionMap"
        );

        // --------------------------------------------------------
        // EMISSION
        // --------------------------------------------------------

        CopyTexture(
            original,
            "_EmissionMap",
            converted,
            "_EmissionMap"
        );

        // --------------------------------------------------------
        // HEIGHT
        // --------------------------------------------------------

        CopyTexture(
            original,
            "_ParallaxMap",
            converted,
            "_ParallaxMap"
        );

        // --------------------------------------------------------
        // TILING Y OFFSET
        // --------------------------------------------------------

        if (original.HasProperty("_MainTex") &&
            converted.HasProperty("_BaseMap"))
        {
            converted.SetTextureScale(
                "_BaseMap",
                original.GetTextureScale("_MainTex")
            );

            converted.SetTextureOffset(
                "_BaseMap",
                original.GetTextureOffset("_MainTex")
            );
        }

        if (original.HasProperty("_BumpMap") &&
            converted.HasProperty("_BumpMap"))
        {
            converted.SetTextureScale(
                "_BumpMap",
                original.GetTextureScale("_BumpMap")
            );

            converted.SetTextureOffset(
                "_BumpMap",
                original.GetTextureOffset("_BumpMap")
            );
        }

        if (original.HasProperty("_MetallicGlossMap") &&
            converted.HasProperty("_MetallicGlossMap"))
        {
            converted.SetTextureScale(
                "_MetallicGlossMap",
                original.GetTextureScale("_MetallicGlossMap")
            );

            converted.SetTextureOffset(
                "_MetallicGlossMap",
                original.GetTextureOffset("_MetallicGlossMap")
            );
        }

        if (original.HasProperty("_OcclusionMap") &&
            converted.HasProperty("_OcclusionMap"))
        {
            converted.SetTextureScale(
                "_OcclusionMap",
                original.GetTextureScale("_OcclusionMap")
            );

            converted.SetTextureOffset(
                "_OcclusionMap",
                original.GetTextureOffset("_OcclusionMap")
            );
        }

        if (original.HasProperty("_EmissionMap") &&
            converted.HasProperty("_EmissionMap"))
        {
            converted.SetTextureScale(
                "_EmissionMap",
                original.GetTextureScale("_EmissionMap")
            );

            converted.SetTextureOffset(
                "_EmissionMap",
                original.GetTextureOffset("_EmissionMap")
            );
        }
    }

    // ============================================================
    // COPIAR UNA TEXTURA
    // ============================================================

    private void CopyTexture(
        Material original,
        string originalProperty,
        Material converted,
        string convertedProperty
    )
    {
        if (!original.HasProperty(originalProperty))
            return;

        if (!converted.HasProperty(convertedProperty))
            return;

        Texture texture =
            original.GetTexture(originalProperty);

        if (texture == null)
            return;

        /*
         * IMPORTANTE:
         *
         * No buscamos la textura en ninguna carpeta.
         *
         * Unity ya conoce exactamente dónde está.
         *
         * Esto permite que funcione aunque esté en:
         *
         * Assets/SciFi Office Lit/Textures/
         *
         * Assets/SciFi Office Lit/Materials/
         *
         * Assets/OtraCarpeta/
         *
         * etc.
         */

        converted.SetTexture(
            convertedProperty,
            texture
        );
    }

    // ============================================================
    // REPARAR TEXTURAS
    // ============================================================

    private void RepairActiveSceneTextures()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid())
        {
            Debug.LogError(
                "[SceneMaterialsToURP] Escena inválida."
            );

            return;
        }

        Renderer[] renderers =
            GetSceneRenderers(scene);

        int repaired = 0;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Material[] materials =
                renderer.sharedMaterials;

            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];

                if (material == null)
                    continue;

                if (!IsURPMaterial(material))
                    continue;

                string originalName =
                    material.name;

                if (originalName.EndsWith("_URP"))
                {
                    originalName =
                        originalName.Substring(
                            0,
                            originalName.Length - 4
                        );
                }

                Material original =
                    FindOriginalMaterial(
                        originalName
                    );

                if (original == null)
                {
                    Debug.LogWarning(
                        "[SceneMaterialsToURP] " +
                        "No se encontró material original para: " +
                        material.name
                    );

                    continue;
                }

                CopyStandardProperties(
                    original,
                    material
                );

                CopyTextureProperties(
                    original,
                    material
                );

                EditorUtility.SetDirty(material);

                changed = true;
                repaired++;
            }

            if (changed)
            {
                renderer.sharedMaterials =
                    materials;

                EditorUtility.SetDirty(renderer);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            "[SceneMaterialsToURP] Reparación terminada. " +
            "Materiales reparados: " +
            repaired
        );

        EditorUtility.DisplayDialog(
            "Reparación terminada",
            "Se repararon " +
            repaired +
            " materiales de la escena.",
            "OK"
        );
    }

    // ============================================================
    // BUSCAR MATERIAL ORIGINAL
    // ============================================================

    private Material FindOriginalMaterial(
        string materialName
    )
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:Material " +
                materialName
            );

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid
                );

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    path
                );

            if (material == null)
                continue;

            if (material.name != materialName)
                continue;

            // Evitar recuperar uno de nuestros materiales URP.

            if (path.Contains(
                "/Scene_URP/"
            ))
            {
                continue;
            }

            return material;
        }

        return null;
    }

    // ============================================================
    // DETECTAR URP
    // ============================================================

    private bool IsURPMaterial(
        Material material
    )
    {
        if (material == null)
            return false;

        if (material.shader == null)
            return false;

        return material.shader.name ==
            "Universal Render Pipeline/Lit";
    }

    // ============================================================
    // OBTENER RENDERERS DE LA ESCENA
    // ============================================================

    private Renderer[] GetSceneRenderers(
        Scene scene
    )
    {
        List<Renderer> renderers =
            new List<Renderer>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Renderer[] rootRenderers =
                root.GetComponentsInChildren<Renderer>(
                    true
                );

            renderers.AddRange(
                rootRenderers
            );
        }

        return renderers.ToArray();
    }

    // ============================================================
    // CARPETA DE LA ESCENA
    // ============================================================

    private string GetSceneFolder()
    {
        string sceneName =
            SceneManager.GetActiveScene().name;

        if (string.IsNullOrEmpty(sceneName))
        {
            sceneName = "Scene";
        }

        sceneName =
            MakeSafeFileName(sceneName);

        string folder =
            OUTPUT_ROOT +
            "/" +
            sceneName;

        EnsureFolder("Assets/Materials");
        EnsureFolder(OUTPUT_ROOT);
        EnsureFolder(folder);

        return folder;
    }

    // ============================================================
    // CREAR CARPETAS
    // ============================================================

    private void EnsureFolder(
        string folder
    )
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent =
            Path.GetDirectoryName(folder)?
                .Replace("\\", "/");

        string folderName =
            Path.GetFileName(folder);

        if (!string.IsNullOrEmpty(parent) &&
            !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        if (!string.IsNullOrEmpty(parent) &&
            !string.IsNullOrEmpty(folderName))
        {
            AssetDatabase.CreateFolder(
                parent,
                folderName
            );
        }
    }

    // ============================================================
    // NOMBRE SEGURO
    // ============================================================

    private static string MakeSafeFileName(
        string fileName
    )
    {
        foreach (
            char c in Path.GetInvalidFileNameChars()
        )
        {
            fileName =
                fileName.Replace(
                    c.ToString(),
                    "_"
                );
        }

        return fileName;
    }

    // ============================================================
    // ABRIR CARPETA
    // ============================================================

    private void OpenMaterialsFolder()
    {
        string folder =
            GetSceneFolder();

        UnityEngine.Object folderObject =
            AssetDatabase.LoadAssetAtPath<
                UnityEngine.Object
            >(folder);

        if (folderObject != null)
        {
            Selection.activeObject =
                folderObject;

            EditorGUIUtility.PingObject(
                folderObject
            );
        }
    }
}