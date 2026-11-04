#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// Corrige MainSceneMultiPlayer para que los NetworkObject que viven dentro de
/// los escenarios sean objetos reales de escena y no NetworkObject anidados
/// dentro de una instancia de prefab.
///
/// Ejecutar UNA vez con MainSceneMultiPlayer abierta.
/// </summary>
public static class MultiplayerSceneNetworkFixer
{
    private const string GameplayScene = "MainSceneMultiPlayer";

    [MenuItem("Game/Dead Quarantine/Multiplayer/REPARAR NetworkObjects de MainSceneMultiPlayer")]
    private static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Salí de Play Mode",
                "La reparación de escena debe ejecutarse con el juego detenido.",
                "OK"
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || scene.name != GameplayScene)
        {
            EditorUtility.DisplayDialog(
                "Escena incorrecta",
                "Abrí MainSceneMultiPlayer y volvé a ejecutar la herramienta.",
                "OK"
            );
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Reparar NetworkObjects Multiplayer");
        int undoGroup = Undo.GetCurrentGroup();

        int escenariosDesempaquetados = 0;
        int networkObjectsSpawnQuitados = 0;
        int objetosDuplicadosBorrados = 0;

        // 1) Todos los escenarios que contengan ConfiguracionEscenarioJugable
        // deben quedar como contenido real de la escena. Esto permite que sus
        // puertas/Easter Eggs con NetworkObject sean In-Scene NetworkObjects validos.
        ConfiguracionEscenarioJugable[] configs =
            Object.FindObjectsByType<ConfiguracionEscenarioJugable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        HashSet<GameObject> yaProcesados = new HashSet<GameObject>();

        foreach (ConfiguracionEscenarioJugable config in configs)
        {
            if (config == null || config.gameObject.scene != scene)
                continue;

            GameObject prefabRoot =
                PrefabUtility.GetOutermostPrefabInstanceRoot(config.gameObject);

            if (prefabRoot == null || yaProcesados.Contains(prefabRoot))
                continue;

            yaProcesados.Add(prefabRoot);

            Undo.RegisterFullObjectHierarchyUndo(
                prefabRoot,
                "Desempaquetar escenario para Netcode"
            );

            PrefabUtility.UnpackPrefabInstance(
                prefabRoot,
                PrefabUnpackMode.Completely,
                InteractionMode.UserAction
            );

            prefabRoot.SetActive(true);
            escenariosDesempaquetados++;
        }

        // 2) El spawner que vive dentro del escenario NO necesita NetworkObject.
        // Su autoridad se comprueba con NetworkManager.Singleton.IsServer.
        MultiplayerPlayerSpawner[] spawners =
            Object.FindObjectsByType<MultiplayerPlayerSpawner>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (MultiplayerPlayerSpawner spawner in spawners)
        {
            if (spawner == null || spawner.gameObject.scene != scene)
                continue;

            // El spawner externo viejo ya no se usa.
            if (spawner.transform.parent == null &&
                spawner.gameObject.name == "MultiplayerPlayerSpawn")
            {
                Undo.DestroyObjectImmediate(spawner.gameObject);
                objetosDuplicadosBorrados++;
                continue;
            }

            NetworkObject netObj = spawner.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                Undo.DestroyObjectImmediate(netObj);
                networkObjectsSpawnQuitados++;
            }
        }

        // 3) Habia un NetworkObject de escena llamado "multiplayer" que llevaba
        // MultiplayerPlayerSpawnAssigner pero no era ningun Player real.
        MultiplayerPlayerSpawnAssigner[] assigners =
            Object.FindObjectsByType<MultiplayerPlayerSpawnAssigner>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (MultiplayerPlayerSpawnAssigner assigner in assigners)
        {
            if (assigner == null || assigner.gameObject.scene != scene)
                continue;

            if (assigner.transform.parent == null &&
                assigner.gameObject.name.Equals(
                    "multiplayer",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                Undo.DestroyObjectImmediate(assigner.gameObject);
                objetosDuplicadosBorrados++;
            }
        }

        // 4) Fuerza a Unity a volver a serializar los NetworkObject ya como
        // objetos de escena. Sus GlobalObjectIdHash se recalculan desde la escena.
        NetworkObject[] networkObjects =
            Object.FindObjectsByType<NetworkObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        int networkObjectsEscena = 0;

        foreach (NetworkObject networkObject in networkObjects)
        {
            if (networkObject == null || networkObject.gameObject.scene != scene)
                continue;

            EditorUtility.SetDirty(networkObject);
            networkObjectsEscena++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log(
            "[MultiplayerSceneNetworkFixer] Listo. " +
            "Escenarios desempaquetados=" + escenariosDesempaquetados +
            ", NetworkObject quitados de SpawnPlayer=" + networkObjectsSpawnQuitados +
            ", duplicados borrados=" + objetosDuplicadosBorrados +
            ", NetworkObject de escena restantes=" + networkObjectsEscena + "."
        );

        EditorUtility.DisplayDialog(
            "MainSceneMultiPlayer reparada",
            "Se desempaquetaron los escenarios para que sus NetworkObject sean de escena, " +
            "se quitó NetworkObject de SpawnPlayer y se eliminaron los spawners duplicados.\n\n" +
            "Guardá el proyecto y volvé a probar Host + Player 2.",
            "OK"
        );
    }
}
#endif
