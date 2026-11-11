using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// TEMPORAL: imprime los eventos de escena de Netcode para ver en que orden llegan.
public class SceneEventLogger : MonoBehaviour
{
    private NetworkSceneManager registrado;
    private readonly Dictionary<string, double> inicioCarga = new Dictionary<string, double>();
    private readonly Dictionary<string, double> inicioDescarga = new Dictionary<string, double>();

    private void Update()
    {
        NetworkManager nm = NetworkManager.Singleton;

        if (nm == null || nm.SceneManager == null || registrado == nm.SceneManager)
            return;

        if (registrado != null)
            registrado.OnSceneEvent -= OnSceneEvent;

        inicioCarga.Clear();
        inicioDescarga.Clear();
        registrado = nm.SceneManager;
        registrado.OnSceneEvent += OnSceneEvent;

        Debug.Log(
            "[SceneEvent] Escuchando eventos de escena. IsServer=" + nm.IsServer +
            " IsClient=" + nm.IsClient
        );
    }

    private void OnSceneEvent(SceneEvent e)
    {
        NetworkManager red = NetworkManager.Singleton;
        if (red == null)
            return;

        double ahora = Time.realtimeSinceStartupAsDouble;
        string medida = "";
        switch (e.SceneEventType)
        {
            case SceneEventType.Load:
                inicioCarga[e.SceneName] = ahora;
                break;
            case SceneEventType.Unload:
                inicioDescarga[e.SceneName] = ahora;
                break;
            case SceneEventType.LoadComplete:
                medida = Medir(inicioCarga, e, red, ahora);
                break;
            case SceneEventType.UnloadComplete:
                medida = Medir(inicioDescarga, e, red, ahora);
                break;
            case SceneEventType.LoadEventCompleted:
            case SceneEventType.UnloadEventCompleted:
                medida = " | completaron=" + string.Join(",", e.ClientsThatCompleted) +
                         " | timeout=" + string.Join(",", e.ClientsThatTimedOut);
                break;
        }

        Debug.Log(
            $"[SceneEvent] {e.SceneEventType} | escena={e.SceneName} | clientId={e.ClientId}" +
            $" | local={red.LocalClientId} | t={ahora:F2}{medida}"
        );
    }

    private static string Medir(
        Dictionary<string, double> inicios, SceneEvent e, NetworkManager red, double ahora)
    {
        bool local = e.ClientId == red.LocalClientId;
        string etiqueta = local ? "duracionLocal" : "desdeInicioHost";
        string medida = inicios.TryGetValue(e.SceneName, out double inicio)
            ? $" | {etiqueta}={ahora - inicio:F2}s" : "";

        NetworkTransport transporte = red.NetworkConfig.NetworkTransport;
        if (transporte != null && (red.IsServer ? !local : red.IsConnectedClient))
        {
            ulong extremo = red.IsServer ? e.ClientId : NetworkManager.ServerClientId;
            ulong rtt = transporte.GetCurrentRtt(extremo);
            if (rtt > 0)
                medida += $" | RTT={rtt}ms";
        }

        return medida;
    }

    private void OnDestroy()
    {
        if (registrado != null)
            registrado.OnSceneEvent -= OnSceneEvent;
    }
}
