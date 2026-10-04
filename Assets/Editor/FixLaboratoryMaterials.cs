using UnityEngine;
using UnityEditor;
using System;
using System.IO;

public class FixLaboratoryMaterials : EditorWindow
{
    [MenuItem("Tools/Laboratory/Convert Materials to URP")]
    public static void FixMaterials()
    {
        Debug.Log("============================================");
        Debug.Log(" INICIANDO CONVERSIÓN DE MATERIALES");
        Debug.Log("============================================");

        string[] materialGuids =
            AssetDatabase.FindAssets("t:Material");

        string[] textureGuids =
            AssetDatabase.FindAssets("t:Texture2D");

        int materialsFound = 0;
        int materialsChanged = 0;

        int baseFound = 0;
        int normalFound = 0;
        int maskFound = 0;

        foreach (string materialGuid in materialGuids)
        {
            string materialPath =
                AssetDatabase.GUIDToAssetPath(materialGuid);

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (material == null)
                continue;

            // Solo materiales del laboratorio
            if (!material.name.StartsWith(
                    "Laboratory03",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            materialsFound++;

            Debug.Log(
                $"[Laboratory] Procesando: {material.name}"
            );

            // ------------------------------------------------------------
            // OBTENER NOMBRE BASE
            // ------------------------------------------------------------

            string baseName =
                RemoveDuplicateSuffix(material.name);

            Debug.Log(
                $"    Nombre base detectado: {baseName}"
            );

            // ------------------------------------------------------------
            // BUSCAR TEXTURAS
            // ------------------------------------------------------------

            Texture2D baseMap = null;
            Texture2D maskMap = null;
            Texture2D normalMap = null;

            foreach (string textureGuid in textureGuids)
            {
                string texturePath =
                    AssetDatabase.GUIDToAssetPath(textureGuid);

                string textureName =
                    Path.GetFileNameWithoutExtension(texturePath);

                // BASE MAP
                if (textureName.Equals(
                        baseName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    baseMap =
                        AssetDatabase.LoadAssetAtPath<Texture2D>(
                            texturePath
                        );
                }

                // MASK MAP
                string expectedMask =
                    GetTexturePrefix(baseName) + "_MaskMap";

                if (textureName.Equals(
                        expectedMask,
                        StringComparison.OrdinalIgnoreCase))
                {
                    maskMap =
                        AssetDatabase.LoadAssetAtPath<Texture2D>(
                            texturePath
                        );
                }

                // NORMAL
                string expectedNormal =
                    GetTexturePrefix(baseName) + "_Normal";

                if (textureName.Equals(
                        expectedNormal,
                        StringComparison.OrdinalIgnoreCase))
                {
                    normalMap =
                        AssetDatabase.LoadAssetAtPath<Texture2D>(
                            texturePath
                        );
                }
            }

            // ------------------------------------------------------------
            // MOSTRAR RESULTADOS
            // ------------------------------------------------------------

            Debug.Log(
                $"    BaseMap: {(baseMap != null ? baseMap.name : "NO ENCONTRADO")}"
            );

            Debug.Log(
                $"    MaskMap: {(maskMap != null ? maskMap.name : "NO ENCONTRADO")}"
            );

            Debug.Log(
                $"    Normal: {(normalMap != null ? normalMap.name : "NO ENCONTRADO")}"
            );

            bool changed = false;

            // ------------------------------------------------------------
            // BASE MAP
            // ------------------------------------------------------------

            if (baseMap != null)
            {
                baseFound++;

                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture(
                        "_BaseMap",
                        baseMap
                    );

                    changed = true;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor(
                        "_BaseColor",
                        Color.white
                    );
                }
            }

            // ------------------------------------------------------------
            // MASK MAP
            // ------------------------------------------------------------

            if (maskMap != null)
            {
                maskFound++;

                ConfigureMaskTexture(maskMap);

                if (material.HasProperty(
                        "_MetallicGlossMap"))
                {
                    material.SetTexture(
                        "_MetallicGlossMap",
                        maskMap
                    );

                    changed = true;
                }
            }

            // ------------------------------------------------------------
            // NORMAL MAP
            // ------------------------------------------------------------

            if (normalMap != null)
            {
                normalFound++;

                ConfigureNormalTexture(normalMap);

                if (material.HasProperty("_BumpMap"))
                {
                    material.SetTexture(
                        "_BumpMap",
                        normalMap
                    );

                    if (material.HasProperty("_BumpScale"))
                    {
                        material.SetFloat(
                            "_BumpScale",
                            1f
                        );
                    }

                    changed = true;
                }
            }

            // ------------------------------------------------------------
            // CONFIGURACIÓN URP
            // ------------------------------------------------------------

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat(
                    "_Surface",
                    0f
                );
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat(
                    "_Cull",
                    2f
                );
            }

            // ------------------------------------------------------------
            // GUARDAR
            // ------------------------------------------------------------

            if (changed)
            {
                EditorUtility.SetDirty(material);
                materialsChanged++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ------------------------------------------------------------
        // RESULTADO
        // ------------------------------------------------------------

        Debug.Log("============================================");
        Debug.Log(" LABORATORY → URP FINALIZADO");
        Debug.Log("============================================");

        Debug.Log(
            $"Materiales encontrados: {materialsFound}"
        );

        Debug.Log(
            $"Materiales modificados: {materialsChanged}"
        );

        Debug.Log(
            $"BaseMap encontrados: {baseFound}"
        );

        Debug.Log(
            $"MaskMap encontrados: {maskFound}"
        );

        Debug.Log(
            $"Normal encontrados: {normalFound}"
        );

        Debug.Log("============================================");

        EditorUtility.DisplayDialog(
            "Laboratory → URP",
            $"Conversión terminada.\n\n" +
            $"Materiales encontrados: {materialsFound}\n" +
            $"Materiales modificados: {materialsChanged}\n\n" +
            $"BaseMap encontrados: {baseFound}\n" +
            $"MaskMap encontrados: {maskFound}\n" +
            $"Normal encontrados: {normalFound}",
            "OK"
        );
    }

    // ================================================================
    // QUITAR DUPLICADOS
    // ================================================================

    private static string RemoveDuplicateSuffix(
        string materialName)
    {
        string result = materialName.Trim();

        // Ejemplo:
        //
        // Laboratory03_Floor_BaseMap 1
        //
        // se convierte en:
        //
        // Laboratory03_Floor_BaseMap

        while (result.Length > 0)
        {
            char lastChar =
                result[result.Length - 1];

            if (char.IsDigit(lastChar))
            {
                int spaceIndex =
                    result.LastIndexOf(' ');

                if (spaceIndex >= 0)
                {
                    result =
                        result.Substring(
                            0,
                            spaceIndex
                        ).Trim();
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        return result;
    }

    // ================================================================
    // OBTENER PREFIJO
    // ================================================================

    private static string GetTexturePrefix(
        string baseName)
    {
        const string suffix = "_BaseMap";

        if (baseName.EndsWith(
                suffix,
                StringComparison.OrdinalIgnoreCase))
        {
            return baseName.Substring(
                0,
                baseName.Length - suffix.Length
            );
        }

        return baseName;
    }

    // ================================================================
    // CONFIGURAR NORMAL MAP
    // ================================================================

    private static void ConfigureNormalTexture(
        Texture2D texture)
    {
        string path =
            AssetDatabase.GetAssetPath(texture);

        TextureImporter importer =
            AssetImporter.GetAtPath(path)
            as TextureImporter;

        if (importer == null)
            return;

        bool changed = false;

        if (importer.textureType !=
            TextureImporterType.NormalMap)
        {
            importer.textureType =
                TextureImporterType.NormalMap;

            changed = true;
        }

        if (importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    // ================================================================
    // CONFIGURAR MASK MAP
    // ================================================================

    private static void ConfigureMaskTexture(
        Texture2D texture)
    {
        string path =
            AssetDatabase.GetAssetPath(texture);

        TextureImporter importer =
            AssetImporter.GetAtPath(path)
            as TextureImporter;

        if (importer == null)
            return;

        if (importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }
    }
}