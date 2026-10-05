#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

// Este script va en una carpeta llamada "Editor" en algún lugar de Assets
// (por ejemplo: Assets/Editor/SkeletonReassignTool.cs). Unity trata
// especialmente cualquier carpeta con ese nombre exacto: lo que esté
// adentro solo se usa en el Editor, nunca se compila en el build.
//
// Qué hace: repara clips de animación cuyas rutas de huesos quedaron
// apuntando a un esqueleto con otro nombre (ej: grabadas como
// "SKEL_Handgun_01/.../SOCKET_Eject" cuando tu objeto real en escena se
// llama "SKEL_Handgun_03"). Busca cada hueso por su NOMBRE (el último
// tramo de la ruta) dentro de la jerarquía real que arrastres, y genera
// una COPIA nueva del clip con las rutas corregidas.
//
// Nunca modifica el clip original — ni podría, si es un clip embebido en
// un FBX (esos Unity no los deja editar). Por eso el resultado siempre es
// un .anim nuevo, al lado, con "_Fixed" en el nombre.
public class SkeletonReassignTool : EditorWindow
{
    [MenuItem("Tools/Reasignar Esqueleto de Animación")]
    private static void Open() => GetWindow<SkeletonReassignTool>("Reasignar Esqueleto");

    private Transform rootCorrecto;
    private readonly List<AnimationClip> clips = new List<AnimationClip>();
    private Vector2 scroll;
    private string outputFolder = "Assets/_AnimacionesCorregidas";

    private struct Resultado
    {
        public AnimationClip original;
        public int resueltos;
        public int sinResolver;
        public List<string> sinResolverNombres;
    }

    private readonly List<Resultado> ultimoAnalisis = new List<Resultado>();

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1) Arrastrá acá el objeto que tiene la jerarquía REAL (ej: SK_Handgun_03).\n" +
            "2) Agregá los clips rotos.\n" +
            "3) Analizar primero (no toca nada) para ver qué se puede resolver.\n" +
            "4) Generar copias corregidas — crea archivos .anim nuevos, nunca pisa el original.",
            MessageType.Info
        );

        rootCorrecto = (Transform)EditorGUILayout.ObjectField(
            "Jerarquía real (raíz)", rootCorrecto, typeof(Transform), true
        );

        outputFolder = EditorGUILayout.TextField("Carpeta de salida", outputFolder);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Clips a reparar", EditorStyles.boldLabel);

        int toRemove = -1;
        for (int i = 0; i < clips.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            clips[i] = (AnimationClip)EditorGUILayout.ObjectField(clips[i], typeof(AnimationClip), false);
            if (GUILayout.Button("x", GUILayout.Width(22)))
                toRemove = i;
            EditorGUILayout.EndHorizontal();
        }
        if (toRemove >= 0)
            clips.RemoveAt(toRemove);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Agregar clip"))
            clips.Add(null);

        if (GUILayout.Button("Agregar seleccionados del Project"))
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is AnimationClip c && !clips.Contains(c))
                    clips.Add(c);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(rootCorrecto == null || clips.Count == 0))
        {
            if (GUILayout.Button("Analizar (no modifica nada)"))
                Analizar();
        }

        if (ultimoAnalisis.Count > 0)
        {
            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(220));

            foreach (Resultado r in ultimoAnalisis)
            {
                string nombre = r.original != null ? r.original.name : "(clip nulo)";
                EditorGUILayout.LabelField(nombre + ":  " + r.resueltos + " resueltos, " + r.sinResolver + " sin resolver");

                foreach (string n in r.sinResolverNombres)
                    EditorGUILayout.LabelField("   ⚠ " + n);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("Generar copias corregidas"))
                GenerarCopias();
        }
    }

    // Recorre rootCorrecto entero y arma: nombre del hueso -> ruta relativa real.
    // Si dos huesos distintos tienen el mismo nombre en ramas distintas, se
    // descarta esa entrada (ambigua) en vez de adivinar mal.
    private Dictionary<string, string> ConstruirMapaDeNombres()
    {
        var porNombre = new Dictionary<string, List<string>>();

        void Recorrer(Transform t, string rutaActual)
        {
            foreach (Transform hijo in t)
            {
                string ruta = string.IsNullOrEmpty(rutaActual) ? hijo.name : rutaActual + "/" + hijo.name;

                if (!porNombre.TryGetValue(hijo.name, out List<string> lista))
                {
                    lista = new List<string>();
                    porNombre[hijo.name] = lista;
                }

                lista.Add(ruta);
                Recorrer(hijo, ruta);
            }
        }

        Recorrer(rootCorrecto, "");

        var resultado = new Dictionary<string, string>();

        foreach (KeyValuePair<string, List<string>> kv in porNombre)
        {
            if (kv.Value.Count == 1) // único en toda la jerarquía — sin ambigüedad
                resultado[kv.Key] = kv.Value[0];
        }

        return resultado;
    }

    private static string NombreHueso(string pathOriginal)
    {
        int i = pathOriginal.LastIndexOf('/');
        return i >= 0 ? pathOriginal.Substring(i + 1) : pathOriginal;
    }

    private void Analizar()
    {
        ultimoAnalisis.Clear();
        Dictionary<string, string> mapa = ConstruirMapaDeNombres();

        foreach (AnimationClip clip in clips)
        {
            if (clip == null)
                continue;

            var r = new Resultado { original = clip, sinResolverNombres = new List<string>() };

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                Evaluar(binding.path, mapa, ref r);

            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                Evaluar(binding.path, mapa, ref r);

            ultimoAnalisis.Add(r);
        }
    }

    private void Evaluar(string pathOriginal, Dictionary<string, string> mapa, ref Resultado r)
    {
        string nombreHueso = NombreHueso(pathOriginal);

        if (mapa.ContainsKey(nombreHueso))
        {
            r.resueltos++;
        }
        else
        {
            r.sinResolver++;

            if (!r.sinResolverNombres.Contains(nombreHueso))
                r.sinResolverNombres.Add(nombreHueso);
        }
    }

    private void GenerarCopias()
    {
        Dictionary<string, string> mapa = ConstruirMapaDeNombres();

        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            string carpeta = outputFolder.TrimEnd('/');
            int slash = carpeta.LastIndexOf('/');
            string parent = slash >= 0 ? carpeta.Substring(0, slash) : "Assets";
            string nueva = slash >= 0 ? carpeta.Substring(slash + 1) : carpeta;

            if (AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder(parent, nueva);
            else
                Directory.CreateDirectory(outputFolder);
        }

        int generados = 0;

        foreach (AnimationClip clip in clips)
        {
            if (clip == null)
                continue;

            var copia = new AnimationClip { name = clip.name + "_Fixed" };
            AnimationUtility.SetAnimationClipSettings(copia, AnimationUtility.GetAnimationClipSettings(clip));
            copia.frameRate = clip.frameRate;

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationCurve curva = AnimationUtility.GetEditorCurve(clip, binding);
                EditorCurveBinding nuevoBinding = binding;
                nuevoBinding.path = ResolverRuta(binding.path, mapa);
                AnimationUtility.SetEditorCurve(copia, nuevoBinding, curva);
            }

            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                ObjectReferenceKeyframe[] curva = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                EditorCurveBinding nuevoBinding = binding;
                nuevoBinding.path = ResolverRuta(binding.path, mapa);
                AnimationUtility.SetObjectReferenceCurve(copia, nuevoBinding, curva);
            }

            string assetPath = outputFolder.TrimEnd('/') + "/" + copia.name + ".anim";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            AssetDatabase.CreateAsset(copia, assetPath);
            generados++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Listo",
            generados + " clip(s) corregido(s) en " + outputFolder + ".\n\n" +
            "Lo que no se pudo resolver quedó con la ruta vieja (seguirá en rojo) — " +
            "revisá la lista de \"sin resolver\" antes de generar si querés evitar sorpresas.",
            "OK"
        );
    }

    private string ResolverRuta(string pathOriginal, Dictionary<string, string> mapa)
    {
        string nombreHueso = NombreHueso(pathOriginal);
        return mapa.TryGetValue(nombreHueso, out string nuevaRuta) ? nuevaRuta : pathOriginal;
    }
}
#endif