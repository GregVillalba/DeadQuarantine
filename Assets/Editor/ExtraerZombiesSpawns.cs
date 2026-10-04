using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class ExtraerSpawnsDelRoundManager : EditorWindow
{
    private RoundManager roundManager;
    private Transform raizEscenario;

    [MenuItem("Tools/Zombies/Extraer Spawns del RoundManager")]
    public static void MostrarVentana()
    {
        GetWindow<ExtraerSpawnsDelRoundManager>(
            "Extraer Spawns"
        );
    }

    private void OnGUI()
    {
        GUILayout.Space(10);

        EditorGUILayout.LabelField(
            "Extraer Spawn Points",
            EditorStyles.boldLabel
        );

        GUILayout.Space(10);

        roundManager =
            (RoundManager)EditorGUILayout.ObjectField(
                "Round Manager",
                roundManager,
                typeof(RoundManager),
                true
            );

        raizEscenario =
            (Transform)EditorGUILayout.ObjectField(
                "Raíz del escenario",
                raizEscenario,
                typeof(Transform),
                true
            );

        GUILayout.Space(15);

        EditorGUILayout.HelpBox(
            "Copiará los Spawn Points actualmente asignados " +
            "al RoundManager y creará nuevos objetos dentro " +
            "de la raíz del escenario seleccionada.",
            MessageType.Info
        );

        GUILayout.Space(10);

        GUI.enabled =
            roundManager != null &&
            raizEscenario != null;

        if (GUILayout.Button(
            "EXTRAER SPAWN POINTS",
            GUILayout.Height(40)
        ))
        {
            ExtraerSpawns();
        }

        GUI.enabled = true;
    }

    private void ExtraerSpawns()
    {
        if (roundManager == null)
        {
            EditorUtility.DisplayDialog(
                "Error",
                "No se asignó el RoundManager.",
                "Aceptar"
            );

            return;
        }

        if (raizEscenario == null)
        {
            EditorUtility.DisplayDialog(
                "Error",
                "No se asignó la raíz del escenario.",
                "Aceptar"
            );

            return;
        }

        SerializedObject serializedRoundManager =
            new SerializedObject(roundManager);

        SerializedProperty spawnPointsProperty =
            serializedRoundManager.FindProperty(
                "spawnPoints"
            );

        if (spawnPointsProperty == null)
        {
            EditorUtility.DisplayDialog(
                "Error",
                "No se encontró el campo 'spawnPoints' en RoundManager.",
                "Aceptar"
            );

            return;
        }

        int cantidad =
            spawnPointsProperty.arraySize;

        if (cantidad == 0)
        {
            EditorUtility.DisplayDialog(
                "Sin Spawn Points",
                "El RoundManager no tiene Spawn Points asignados.",
                "Aceptar"
            );

            return;
        }

        int creados = 0;

        Undo.IncrementCurrentGroup();

        int undoGroup =
            Undo.GetCurrentGroup();

        for (int i = 0; i < cantidad; i++)
        {
            SerializedProperty elemento =
                spawnPointsProperty.GetArrayElementAtIndex(i);

            Transform spawnOriginal =
                elemento.objectReferenceValue as Transform;

            if (spawnOriginal == null)
            {
                Debug.LogWarning(
                    "[ExtraerSpawns] Spawn Point " +
                    i +
                    " es nulo. Se omite."
                );

                continue;
            }

            GameObject nuevoSpawn =
                new GameObject(
                    "ZombieSpawn_" +
                    (creados + 1).ToString("00")
                );

            Undo.RegisterCreatedObjectUndo(
                nuevoSpawn,
                "Crear Zombie Spawn"
            );

            Transform nuevoTransform =
                nuevoSpawn.transform;

            nuevoTransform.SetParent(
                raizEscenario,
                false
            );

            nuevoTransform.position =
                spawnOriginal.position;

            nuevoTransform.rotation =
                spawnOriginal.rotation;

            nuevoTransform.localScale =
                spawnOriginal.lossyScale;

            creados++;

            Debug.Log(
                "[ExtraerSpawns] Creado: " +
                nuevoSpawn.name +
                " | Posición: " +
                nuevoTransform.position
            );
        }

        Undo.CollapseUndoOperations(
            undoGroup
        );

        EditorSceneManager.MarkSceneDirty(
            raizEscenario.gameObject.scene
        );

        EditorUtility.DisplayDialog(
            "Spawns extraídos",
            "Se crearon " +
            creados +
            " Spawn Points dentro de:\n\n" +
            raizEscenario.name +
            "\n\nAhora podés asignarlos al " +
            "ConfiguracionEscenarioJugable.",
            "Aceptar"
        );
    }
}