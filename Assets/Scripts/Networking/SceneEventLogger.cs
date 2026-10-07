using Unity.Netcode;
using UnityEngine;

// TEMPORAL: imprime los eventos de escena de Netcode para ver en que orden llegan.
public class SceneEventLogger : MonoBehaviour
{
    private NetworkSceneManager registrado;

    private void Update()
    {
        NetworkManager nm = NetworkManager.Singleton;

        if (nm == null || nm.SceneManager == null || registrado == nm.SceneManager)
            return;

        if (registrado != null)
            registrado.OnSceneEvent -= OnSceneEvent;

        registrado = nm.SceneManager;
        registrado.OnSceneEvent += OnSceneEvent;

        Debug.Log(
            "[SceneEvent] Escuchando eventos de escena. IsServer=" + nm.IsServer +
            " IsClient=" + nm.IsClient
        );
    }

    private void OnSceneEvent(SceneEvent e)
    {
        Debug.Log(
            "[SceneEvent] " + e.SceneEventType +
            " | escena=" + e.SceneName +
            " | clientId=" + e.ClientId +
            " | local=" + (NetworkManager.Singleton != null
                ? NetworkManager.Singleton.LocalClientId.ToString()
                : "?") +
            " | t=" + Time.realtimeSinceStartup.ToString("F2")
        );
    }

    private void OnDestroy()
    {
        if (registrado != null)
            registrado.OnSceneEvent -= OnSceneEvent;
    }
}
