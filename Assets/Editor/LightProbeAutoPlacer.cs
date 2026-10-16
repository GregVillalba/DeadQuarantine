#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Rellena un Light Probe Group automáticamente dentro de una zona, en lugar de colocar los probes a mano.
///
/// Uso:
///  1) Creá un objeto vacío con un componente "Light Probe Group" (GameObject > Light > Light Probe Group).
///  2) Creá otro objeto vacío con un Box Collider (marcá Is Trigger) que cubra la sala o zona. La base de la caja
///     tiene que quedar a la altura del piso y el techo de la caja no importa mucho.
///  3) Abrí Tools > Iluminación > Rellenar Light Probes, asigná ambos y tocá "Generar probes".
///
/// Pone los probes en una grilla, en dos alturas (baja y alta). Descarta los puntos que caen dentro de objetos
/// sólidos (paredes, cajas, estanterías) y, si activás "Solo sobre NavMesh", también los que no están sobre una
/// zona caminable. Los probes solo se calculan al hacer Generate Lighting.
///
/// Este archivo tiene que estar dentro de una carpeta llamada "Editor" (por ejemplo Assets/Editor).
/// </summary>
public class LightProbeAutoPlacer : EditorWindow
{
    private LightProbeGroup targetGroup;
    private Collider areaCollider;

    private float spacing = 2.5f;
    private float lowHeight = 0.5f;
    private float highHeight = 1.7f;

    private bool onlyOverNavMesh = true;
    private float navMeshTolerance = 0.5f;

    private LayerMask solidMask = ~0;
    private float clearance = 0.25f;

    private bool replaceExisting = true;

    private const int MaxProbes = 20000;

    [MenuItem("Tools/Iluminación/Rellenar Light Probes")]
    private static void Open()
    {
        GetWindow<LightProbeAutoPlacer>("Light Probes automáticos");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1) Light Probe Group vacío.  2) Objeto con Box Collider (Is Trigger) que cubra la zona, con su base a la altura del piso.  " +
            "3) Generar probes.  Después hacé Generate Lighting.",
            MessageType.Info);

        targetGroup = (LightProbeGroup)EditorGUILayout.ObjectField("Light Probe Group", targetGroup, typeof(LightProbeGroup), true);
        areaCollider = (Collider)EditorGUILayout.ObjectField("Zona (Collider)", areaCollider, typeof(Collider), true);

        EditorGUILayout.Space();
        spacing = Mathf.Max(0.5f, EditorGUILayout.FloatField("Separación (m)", spacing));
        lowHeight = EditorGUILayout.FloatField("Altura baja (m)", lowHeight);
        highHeight = EditorGUILayout.FloatField("Altura alta (m)", highHeight);

        EditorGUILayout.Space();
        onlyOverNavMesh = EditorGUILayout.Toggle(new GUIContent("Solo sobre NavMesh", "Pone probes solo donde los zombies y jugadores pueden caminar (usa tu NavMesh horneado)."), onlyOverNavMesh);

        using (new EditorGUI.DisabledScope(!onlyOverNavMesh))
            navMeshTolerance = Mathf.Max(0.1f, EditorGUILayout.FloatField("Tolerancia NavMesh (m)", navMeshTolerance));

        solidMask = LayerMaskField("Capas sólidas", solidMask);
        clearance = Mathf.Max(0.05f, EditorGUILayout.FloatField("Margen con objetos (m)", clearance));

        replaceExisting = EditorGUILayout.Toggle("Reemplazar los existentes", replaceExisting);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(targetGroup == null || areaCollider == null))
        {
            if (GUILayout.Button("Generar probes", GUILayout.Height(28)))
                Generate();
        }
    }

    private void Generate()
    {
        Bounds bounds = areaCollider.bounds;
        List<Vector3> worldPositions = new List<Vector3>();

        // El collider de la zona no debe contar como "objeto sólido".
        bool wasEnabled = areaCollider.enabled;
        areaCollider.enabled = false;
        Physics.SyncTransforms();

        try
        {
            float verticalSearch = Mathf.Max(bounds.extents.y, 1f) + 0.5f;

            for (float x = bounds.min.x + spacing * 0.5f; x <= bounds.max.x; x += spacing)
            {
                for (float z = bounds.min.z + spacing * 0.5f; z <= bounds.max.z; z += spacing)
                {
                    float floorY = bounds.min.y;

                    if (onlyOverNavMesh)
                    {
                        NavMeshHit hit;
                        Vector3 probe = new Vector3(x, bounds.center.y, z);

                        if (!NavMesh.SamplePosition(probe, out hit, verticalSearch, NavMesh.AllAreas))
                            continue;

                        // Tiene que estar casi justo encima/debajo del punto de la grilla, no al costado.
                        if (Mathf.Abs(hit.position.x - x) > navMeshTolerance || Mathf.Abs(hit.position.z - z) > navMeshTolerance)
                            continue;

                        floorY = hit.position.y;
                    }

                    TryAdd(worldPositions, new Vector3(x, floorY + lowHeight, z));
                    TryAdd(worldPositions, new Vector3(x, floorY + highHeight, z));

                    if (worldPositions.Count > MaxProbes)
                    {
                        EditorUtility.DisplayDialog("Light Probes", "Demasiados probes (más de " + MaxProbes + "). Aumentá la separación o achicá la zona.", "OK");
                        return;
                    }
                }
            }
        }
        finally
        {
            areaCollider.enabled = wasEnabled;
        }

        List<Vector3> localPositions = new List<Vector3>();

        if (!replaceExisting && targetGroup.probePositions != null)
            localPositions.AddRange(targetGroup.probePositions);

        for (int i = 0; i < worldPositions.Count; i++)
            localPositions.Add(targetGroup.transform.InverseTransformPoint(worldPositions[i]));

        Undo.RecordObject(targetGroup, "Rellenar Light Probes");
        targetGroup.probePositions = localPositions.ToArray();
        EditorUtility.SetDirty(targetGroup);

        EditorUtility.DisplayDialog(
            "Light Probes",
            "Se colocaron " + worldPositions.Count + " probes (total en el grupo: " + localPositions.Count + ").\n" +
            "Revisalos en la escena y hacé Generate Lighting.",
            "OK");
    }

    // Agrega el punto si no cae dentro de un objeto sólido.
    private void TryAdd(List<Vector3> list, Vector3 position)
    {
        if (Physics.CheckSphere(position, clearance, solidMask, QueryTriggerInteraction.Ignore))
            return;

        list.Add(position);
    }

    // Selector de capas para un LayerMask (Unity no trae uno listo para ventanas de editor).
    private static LayerMask LayerMaskField(string label, LayerMask selected)
    {
        List<string> layers = new List<string>();
        List<int> layerNumbers = new List<int>();

        for (int i = 0; i < 32; i++)
        {
            string layerName = LayerMask.LayerToName(i);

            if (layerName != "")
            {
                layers.Add(layerName);
                layerNumbers.Add(i);
            }
        }

        int maskWithoutEmpty = 0;

        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if (((1 << layerNumbers[i]) & selected.value) != 0)
                maskWithoutEmpty |= 1 << i;
        }

        maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());

        int mask = 0;

        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) != 0)
                mask |= 1 << layerNumbers[i];
        }

        selected.value = mask;
        return selected;
    }
}
#endif