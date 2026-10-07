using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class RepairURPMaterialTextures : EditorWindow
{
    private const string ConvertedFolder =
        "Assets/Materials/URP_Converted";

    [MenuItem("Tools/Reparar texturas de materiales URP")]
    public static void ShowWindow()
    {
        GetWindow<RepairURPMaterialTextures>(
            "Repair URP Textures"
        );
    }

    private void OnGUI()
    {
        GUILayout.Space(10);

        EditorGUILayout.LabelField(
            "Reparar referencias de texturas",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Busca los materiales originales correspondientes a los " +
            "materiales *_URP y copia sus texturas al material URP.\n\n" +
            "No modifica los materiales originales.",
            MessageType.Info
        );

        GUILayout.Space(10);

        if (GUILayout.Button(
            "REPARAR TEXTURAS",
            GUILayout.Height(40)))
        {
            RepairTextures();
        }
    }

    private static void RepairTextures()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:Material",
                new[] { ConvertedFolder }
            );

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Sin materiales",
                "No se encontraron materiales dentro de:\n\n" +
                ConvertedFolder,
                "Aceptar"
            );

            return;
        }

        int repairedMaterials = 0;
        int repairedTextures = 0;
        int skippedMaterials = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string guid in guids)
            {
                string urpPath =
                    AssetDatabase.GUIDToAssetPath(guid);

                Material urpMaterial =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        urpPath
                    );

                if (urpMaterial == null)
                    continue;

                // Solo materiales creados por el conversor.
                if (!urpMaterial.name.EndsWith("_URP"))
                    continue;

                string originalName =
                    urpMaterial.name.Substring(
                        0,
                        urpMaterial.name.Length - 4
                    );

                Material originalMaterial =
                    FindOriginalMaterial(originalName);

                if (originalMaterial == null)
                {
                    Debug.LogWarning(
                        "[RepairURPMaterialTextures] " +
                        "No se encontró material original para: " +
                        urpMaterial.name
                    );

                    skippedMaterials++;
                    continue;
                }

                bool materialModified = false;

                // =====================================================
                // BASE MAP
                // =====================================================

                if (urpMaterial.HasProperty("_BaseMap") &&
                    originalMaterial.HasProperty("_MainTex"))
                {
                    Texture texture =
                        originalMaterial.GetTexture("_MainTex");

                    if (texture != null)
                    {
                        urpMaterial.SetTexture(
                            "_BaseMap",
                            texture
                        );

                        CopyTextureTransform(
                            originalMaterial,
                            urpMaterial,
                            "_MainTex",
                            "_BaseMap"
                        );

                        materialModified = true;
                        repairedTextures++;
                    }
                }

                // =====================================================
                // NORMAL MAP
                // =====================================================

                if (urpMaterial.HasProperty("_BumpMap") &&
                    originalMaterial.HasProperty("_BumpMap"))
                {
                    Texture texture =
                        originalMaterial.GetTexture("_BumpMap");

                    if (texture != null)
                    {
                        urpMaterial.SetTexture(
                            "_BumpMap",
                            texture
                        );

                        CopyTextureTransform(
                            originalMaterial,
                            urpMaterial,
                            "_BumpMap",
                            "_BumpMap"
                        );

                        if (originalMaterial.HasProperty(
                            "_BumpScale") &&
                            urpMaterial.HasProperty(
                            "_BumpScale"))
                        {
                            urpMaterial.SetFloat(
                                "_BumpScale",
                                originalMaterial.GetFloat(
                                    "_BumpScale"
                                )
                            );
                        }

                        materialModified = true;
                        repairedTextures++;
                    }
                }

                // =====================================================
                // METALLIC MAP
                // =====================================================

                if (urpMaterial.HasProperty("_MetallicGlossMap") &&
                    originalMaterial.HasProperty(
                        "_MetallicGlossMap"))
                {
                    Texture texture =
                        originalMaterial.GetTexture(
                            "_MetallicGlossMap"
                        );

                    if (texture != null)
                    {
                        urpMaterial.SetTexture(
                            "_MetallicGlossMap",
                            texture
                        );

                        CopyTextureTransform(
                            originalMaterial,
                            urpMaterial,
                            "_MetallicGlossMap",
                            "_MetallicGlossMap"
                        );

                        materialModified = true;
                        repairedTextures++;
                    }
                }

                // =====================================================
                // OCCLUSION MAP
                // =====================================================

                if (urpMaterial.HasProperty("_OcclusionMap") &&
                    originalMaterial.HasProperty(
                        "_OcclusionMap"))
                {
                    Texture texture =
                        originalMaterial.GetTexture(
                            "_OcclusionMap"
                        );

                    if (texture != null)
                    {
                        urpMaterial.SetTexture(
                            "_OcclusionMap",
                            texture
                        );

                        CopyTextureTransform(
                            originalMaterial,
                            urpMaterial,
                            "_OcclusionMap",
                            "_OcclusionMap"
                        );

                        if (originalMaterial.HasProperty(
                            "_OcclusionStrength") &&
                            urpMaterial.HasProperty(
                            "_OcclusionStrength"))
                        {
                            urpMaterial.SetFloat(
                                "_OcclusionStrength",
                                originalMaterial.GetFloat(
                                    "_OcclusionStrength"
                                )
                            );
                        }

                        materialModified = true;
                        repairedTextures++;
                    }
                }

                // =====================================================
                // EMISSION MAP
                // =====================================================

                if (urpMaterial.HasProperty("_EmissionMap") &&
                    originalMaterial.HasProperty(
                        "_EmissionMap"))
                {
                    Texture texture =
                        originalMaterial.GetTexture(
                            "_EmissionMap"
                        );

                    if (texture != null)
                    {
                        urpMaterial.SetTexture(
                            "_EmissionMap",
                            texture
                        );

                        CopyTextureTransform(
                            originalMaterial,
                            urpMaterial,
                            "_EmissionMap",
                            "_EmissionMap"
                        );

                        materialModified = true;
                        repairedTextures++;
                    }
                }

                // =====================================================
                // COLOR
                // =====================================================

                if (originalMaterial.HasProperty("_Color") &&
                    urpMaterial.HasProperty("_BaseColor"))
                {
                    urpMaterial.SetColor(
                        "_BaseColor",
                        originalMaterial.GetColor("_Color")
                    );

                    materialModified = true;
                }

                // =====================================================
                // METALLIC
                // =====================================================

                if (originalMaterial.HasProperty("_Metallic") &&
                    urpMaterial.HasProperty("_Metallic"))
                {
                    urpMaterial.SetFloat(
                        "_Metallic",
                        originalMaterial.GetFloat("_Metallic")
                    );

                    materialModified = true;
                }

                // =====================================================
                // SMOOTHNESS
                // =====================================================

                if (originalMaterial.HasProperty("_Glossiness") &&
                    urpMaterial.HasProperty("_Smoothness"))
                {
                    urpMaterial.SetFloat(
                        "_Smoothness",
                        originalMaterial.GetFloat("_Glossiness")
                    );

                    materialModified = true;
                }

                if (materialModified)
                {
                    EditorUtility.SetDirty(urpMaterial);
                    repairedMaterials++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message =
            "Reparación terminada.\n\n" +
            "Materiales reparados: " +
            repairedMaterials + "\n" +
            "Texturas reasignadas: " +
            repairedTextures + "\n" +
            "Materiales sin original encontrado: " +
            skippedMaterials;

        Debug.Log(
            "[RepairURPMaterialTextures] " +
            message
        );

        EditorUtility.DisplayDialog(
            "Reparación completada",
            message,
            "Aceptar"
        );
    }

    private static Material FindOriginalMaterial(
        string materialName)
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:Material " + materialName
            );

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            // No buscar dentro de los materiales convertidos.
            if (path.StartsWith(ConvertedFolder))
                continue;

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    path
                );

            if (material == null)
                continue;

            if (material.name == materialName)
                return material;
        }

        return null;
    }

    private static void CopyTextureTransform(
        Material source,
        Material destination,
        string sourceProperty,
        string destinationProperty)
    {
        if (!source.HasProperty(sourceProperty))
            return;

        if (!destination.HasProperty(destinationProperty))
            return;

        destination.SetTextureOffset(
            destinationProperty,
            source.GetTextureOffset(sourceProperty)
        );

        destination.SetTextureScale(
            destinationProperty,
            source.GetTextureScale(sourceProperty)
        );
    }
}