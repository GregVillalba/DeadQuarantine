using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;

public class SceneMaterialsToURP : EditorWindow
{
    private const string MaterialsFolder = "Assets/Materials/URP_Converted";

    [MenuItem("Tools/Convertir materiales de la escena a URP Lit")]
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
            "Convertir materiales de la escena",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Convierte únicamente los materiales utilizados por los objetos " +
            "de la escena actualmente abierta.\n\n" +
            "Los materiales originales no se modifican. Se crean copias " +
            "dentro de Assets/Materials/URP_Converted.",
            MessageType.Info
        );

        GUILayout.Space(10);

        if (GUILayout.Button(
            "CONVERTIR MATERIALES DE LA ESCENA",
            GUILayout.Height(40)))
        {
            ConvertSceneMaterials();
        }

        GUILayout.Space(10);

        if (GUILayout.Button(
            "ABRIR CARPETA DE MATERIALES"))
        {
            OpenMaterialsFolder();
        }
    }

    private static void ConvertSceneMaterials()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog(
                "Error",
                "No hay una escena válida abierta.",
                "Aceptar"
            );

            return;
        }

        if (!scene.isLoaded)
        {
            EditorUtility.DisplayDialog(
                "Error",
                "La escena no está cargada.",
                "Aceptar"
            );

            return;
        }

        EnsureMaterialsFolder();

        GameObject[] rootObjects = scene.GetRootGameObjects();

        List<Renderer> renderers = new List<Renderer>();

        foreach (GameObject root in rootObjects)
        {
            Renderer[] rootRenderers =
                root.GetComponentsInChildren<Renderer>(true);

            renderers.AddRange(rootRenderers);
        }

        if (renderers.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Sin materiales",
                "No se encontraron Renderer en la escena.",
                "Aceptar"
            );

            return;
        }

        Shader urpLit =
            Shader.Find("Universal Render Pipeline/Lit");

        if (urpLit == null)
        {
            EditorUtility.DisplayDialog(
                "Error",
                "No se encontró el shader:\n\n" +
                "Universal Render Pipeline/Lit\n\n" +
                "Verificá que el proyecto esté configurado con URP.",
                "Aceptar"
            );

            return;
        }

        int convertedMaterials = 0;
        int alreadyURP = 0;
        int objectsModified = 0;

        // Evita crear varias copias del mismo material.
        Dictionary<Material, Material> convertedMaterialsMap =
            new Dictionary<Material, Material>();

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;

                if (materials == null || materials.Length == 0)
                    continue;

                bool rendererModified = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material originalMaterial = materials[i];

                    if (originalMaterial == null)
                        continue;

                    // Ya es URP Lit.
                    if (originalMaterial.shader == urpLit)
                    {
                        alreadyURP++;
                        continue;
                    }

                    // Si ya convertimos este material durante esta ejecución,
                    // reutilizamos la copia.
                    if (convertedMaterialsMap.TryGetValue(
                        originalMaterial,
                        out Material existingConverted))
                    {
                        materials[i] = existingConverted;
                        rendererModified = true;
                        continue;
                    }

                    Material newMaterial =
                        new Material(originalMaterial);

                    newMaterial.name =
                        originalMaterial.name + "_URP";

                    CopyMaterialProperties(
                        originalMaterial,
                        newMaterial
                    );

                    newMaterial.shader = urpLit;

                    string safeName =
                        MakeSafeFileName(newMaterial.name);

                    string assetPath =
                        AssetDatabase.GenerateUniqueAssetPath(
                            MaterialsFolder +
                            "/" +
                            safeName +
                            ".mat"
                        );

                    AssetDatabase.CreateAsset(
                        newMaterial,
                        assetPath
                    );

                    convertedMaterialsMap.Add(
                        originalMaterial,
                        newMaterial
                    );

                    materials[i] = newMaterial;

                    convertedMaterials++;
                    rendererModified = true;
                }

                if (rendererModified)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                    objectsModified++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorSceneManager.MarkSceneDirty(scene);

        string message =
            "Conversión terminada.\n\n" +
            "Escena: " + scene.name + "\n\n" +
            "Materiales convertidos: " + convertedMaterials + "\n" +
            "Objetos modificados: " + objectsModified + "\n" +
            "Materiales que ya eran URP Lit: " + alreadyURP + "\n\n" +
            "Los materiales originales no fueron modificados.";

        EditorUtility.DisplayDialog(
            "Conversión completada",
            message,
            "Aceptar"
        );

        Debug.Log(
            "[SceneMaterialsToURP] " +
            message
        );
    }

    private static void CopyMaterialProperties(
        Material source,
        Material destination)
    {
        /*
         * Intentamos conservar las propiedades más habituales
         * de materiales Standard.
         */

        // Albedo
        if (source.HasProperty("_Color") &&
            destination.HasProperty("_BaseColor"))
        {
            destination.SetColor(
                "_BaseColor",
                source.GetColor("_Color")
            );
        }

        // Albedo / Main Texture
        if (source.HasProperty("_MainTex") &&
            destination.HasProperty("_BaseMap"))
        {
            destination.SetTexture(
                "_BaseMap",
                source.GetTexture("_MainTex")
            );

            destination.SetTextureOffset(
                "_BaseMap",
                source.GetTextureOffset("_MainTex")
            );

            destination.SetTextureScale(
                "_BaseMap",
                source.GetTextureScale("_MainTex")
            );
        }

        // Metallic
        if (source.HasProperty("_Metallic") &&
            destination.HasProperty("_Metallic"))
        {
            destination.SetFloat(
                "_Metallic",
                source.GetFloat("_Metallic")
            );
        }

        // Smoothness
        if (source.HasProperty("_Glossiness") &&
            destination.HasProperty("_Smoothness"))
        {
            destination.SetFloat(
                "_Smoothness",
                source.GetFloat("_Glossiness")
            );
        }

        // Normal Map
        if (source.HasProperty("_BumpMap") &&
            destination.HasProperty("_BumpMap"))
        {
            Texture normal =
                source.GetTexture("_BumpMap");

            if (normal != null)
            {
                destination.SetTexture(
                    "_BumpMap",
                    normal
                );

                if (source.HasProperty("_BumpScale") &&
                    destination.HasProperty("_BumpScale"))
                {
                    destination.SetFloat(
                        "_BumpScale",
                        source.GetFloat("_BumpScale")
                    );
                }
            }
        }

        // Occlusion
        if (source.HasProperty("_OcclusionMap") &&
            destination.HasProperty("_OcclusionMap"))
        {
            destination.SetTexture(
                "_OcclusionMap",
                source.GetTexture("_OcclusionMap")
            );

            if (source.HasProperty("_OcclusionStrength") &&
                destination.HasProperty("_OcclusionStrength"))
            {
                destination.SetFloat(
                    "_OcclusionStrength",
                    source.GetFloat("_OcclusionStrength")
                );
            }
        }

        // Emission
        if (source.HasProperty("_EmissionColor") &&
            destination.HasProperty("_EmissionColor"))
        {
            Color emission =
                source.GetColor("_EmissionColor");

            destination.SetColor(
                "_EmissionColor",
                emission
            );
        }

        if (source.HasProperty("_EmissionMap") &&
            destination.HasProperty("_EmissionMap"))
        {
            destination.SetTexture(
                "_EmissionMap",
                source.GetTexture("_EmissionMap")
            );
        }
    }

    private static void EnsureMaterialsFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder(
                "Assets",
                "Materials"
            );
        }

        if (!AssetDatabase.IsValidFolder(
            MaterialsFolder))
        {
            AssetDatabase.CreateFolder(
                "Assets/Materials",
                "URP_Converted"
            );
        }
    }

    private static string MakeSafeFileName(
        string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }

        return fileName;
    }

    private static void OpenMaterialsFolder()
    {
        EnsureMaterialsFolder();

        Object folder =
            AssetDatabase.LoadAssetAtPath<Object>(
                MaterialsFolder
            );

        if (folder != null)
        {
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }
    }
}