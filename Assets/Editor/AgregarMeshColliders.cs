
using UnityEngine;
using UnityEditor;

public class AgregarMeshColliderATodos : EditorWindow
{
    [MenuItem("Herramientas/Agregar Mesh Colliders al escenario seleccionado")]
    private static void AgregarColliders()
    {
        GameObject seleccionado = Selection.activeGameObject;

        if (seleccionado == null)
        {
            EditorUtility.DisplayDialog(
                "Sin selección",
                "Seleccioná el objeto raíz del escenario en la jerarquía.",
                "Aceptar"
            );
            return;
        }

        int agregados = 0;

        MeshFilter[] objetos =
            seleccionado.GetComponentsInChildren<MeshFilter>(true);

        foreach (MeshFilter meshFilter in objetos)
        {
            GameObject objeto = meshFilter.gameObject;

            // Ignorar objetos inactivos
            if (!objeto.activeInHierarchy)
                continue;

            // Ignorar mallas vacías
            if (meshFilter.sharedMesh == null)
                continue;

            // No duplicar colliders existentes
            if (objeto.GetComponent<Collider>() != null)
                continue;

            MeshCollider collider =
                objeto.AddComponent<MeshCollider>();

            collider.sharedMesh = meshFilter.sharedMesh;

            agregados++;
        }

        Debug.Log(
            "Mesh Colliders agregados al escenario '" +
            seleccionado.name + "': " + agregados
        );

        EditorUtility.DisplayDialog(
            "Proceso terminado",
            "Escenario: " + seleccionado.name +
            "\nMesh Colliders agregados: " + agregados,
            "Aceptar"
        );
    }
}
