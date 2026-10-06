using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Saca una "foto" de un modelo/prefab de arma con fondo transparente y la guarda como Sprite,
/// para usarla en WeaponOffer.imagenTarjeta (tarjetas de la tienda).
/// Tools > Tienda > Generar imagen de arma
/// </summary>
public class GeneradorImagenTienda : EditorWindow
{
    private GameObject modelo;
    private Vector3 rotacion = new Vector3(0f, 90f, 0f);
    private int ancho = 1024;
    private int alto = 512;
    private float margen = 1.1f;
    private float intensidadLuz = 1.3f;
    private string carpeta = "Assets/Art/Sprites/Tienda";

    private Texture2D vistaPrevia;

    [MenuItem("Tools/Tienda/Generar imagen de arma")]
    private static void Abrir()
    {
        GetWindow<GeneradorImagenTienda>("Imagen de arma");
    }

    private void OnDisable()
    {
        LimpiarVistaPrevia();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Arrastrá el prefab o modelo del arma, ajustá la rotación hasta que se vea de costado y guardá. " +
            "Después asigná el Sprite generado en 'Imagen Tarjeta' de la oferta del Mercader.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();

        modelo = (GameObject)EditorGUILayout.ObjectField("Modelo / prefab", modelo, typeof(GameObject), false);
        rotacion = EditorGUILayout.Vector3Field("Rotación", rotacion);
        ancho = EditorGUILayout.IntSlider("Ancho (px)", ancho, 128, 2048);
        alto = EditorGUILayout.IntSlider("Alto (px)", alto, 128, 2048);
        margen = EditorGUILayout.Slider("Margen", margen, 1f, 2f);
        intensidadLuz = EditorGUILayout.Slider("Intensidad de luz", intensidadLuz, 0.2f, 3f);

        bool cambio = EditorGUI.EndChangeCheck();

        carpeta = EditorGUILayout.TextField("Carpeta de salida", carpeta);

        using (new EditorGUI.DisabledScope(modelo == null))
        {
            if (cambio || GUILayout.Button("Actualizar vista previa"))
                ActualizarVistaPrevia();

            if (GUILayout.Button("Guardar imagen", GUILayout.Height(30)))
                Guardar();
        }

        if (vistaPrevia != null)
        {
            float anchoVista = position.width - 20f;
            float altoVista = anchoVista * alto / ancho;
            Rect rect = GUILayoutUtility.GetRect(anchoVista, altoVista);
            EditorGUI.DrawTextureTransparent(rect, vistaPrevia, ScaleMode.ScaleToFit);
        }
    }

    private void ActualizarVistaPrevia()
    {
        LimpiarVistaPrevia();

        if (modelo != null)
            vistaPrevia = Renderizar();
    }

    private void LimpiarVistaPrevia()
    {
        if (vistaPrevia != null)
            DestroyImmediate(vistaPrevia);

        vistaPrevia = null;
    }

    private Texture2D Renderizar()
    {
        Scene escena = EditorSceneManager.NewPreviewScene();

        GameObject instancia = Instantiate(modelo);
        SceneManager.MoveGameObjectToScene(instancia, escena);
        instancia.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(rotacion));

        // Solo se fotografían los renderers del arma (sin partículas, efectos ni scripts).
        foreach (MonoBehaviour script in instancia.GetComponentsInChildren<MonoBehaviour>(true))
            script.enabled = false;

        Renderer[] renderers = instancia.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(instancia.transform.position, Vector3.zero);
        bool hayBounds = false;

        foreach (Renderer r in renderers)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
            {
                r.enabled = false;
                continue;
            }

            if (!hayBounds)
            {
                bounds = r.bounds;
                hayBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (!hayBounds)
            bounds = new Bounds(Vector3.zero, Vector3.one);

        GameObject luzGO = new GameObject("Luz");
        SceneManager.MoveGameObjectToScene(luzGO, escena);
        Light luz = luzGO.AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.intensity = intensidadLuz;
        luzGO.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

        GameObject camGO = new GameObject("Camara");
        SceneManager.MoveGameObjectToScene(camGO, escena);
        Camera cam = camGO.AddComponent<Camera>();
        cam.scene = escena;
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.nearClipPlane = 0.01f;

        float aspecto = (float)ancho / alto;
        float distancia = bounds.extents.magnitude * 4f + 1f;
        cam.farClipPlane = distancia * 2f;
        cam.transform.position = bounds.center - Vector3.forward * distancia;
        cam.transform.rotation = Quaternion.identity;
        cam.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / aspecto) * margen;

        RenderTexture rt = RenderTexture.GetTemporary(ancho, alto, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture anterior = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D textura = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
        textura.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
        textura.Apply();

        RenderTexture.active = anterior;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);

        EditorSceneManager.ClosePreviewScene(escena);

        return textura;
    }

    private void Guardar()
    {
        Texture2D textura = Renderizar();

        if (!Directory.Exists(carpeta))
            Directory.CreateDirectory(carpeta);

        string ruta = Path.Combine(carpeta, "Tienda_" + modelo.name + ".png").Replace('\\', '/');
        File.WriteAllBytes(ruta, textura.EncodeToPNG());
        DestroyImmediate(textura);

        AssetDatabase.Refresh();

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(ruta);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        EditorGUIUtility.PingObject(sprite);
        Selection.activeObject = sprite;

        Debug.Log("[GeneradorImagenTienda] Imagen guardada en " + ruta);
    }
}
