using UnityEngine;
using Unity.Netcode;

public class SinglePlayerNetworkStarter : MonoBehaviour
{
    [SerializeField] private NetworkManager sceneNetwork;

    private void Start()
    {
        if (SceneTransition.IsReturningToMenu)
            return;

        NetworkManager currentNetwork = NetworkManager.Singleton;
        if (currentNetwork != null && currentNetwork.IsListening)
            return;

        NetworkManager network = sceneNetwork != null ? sceneNetwork : currentNetwork;
        if (network == null)
        {
            Debug.LogError(
                "[SinglePlayerNetworkStarter] No existe un NetworkManager en la escena."
            );

            return;
        }

        if (network != currentNetwork)
            network.SetSingleton();

        Debug.Log("[SinglePlayerNetworkStarter] Iniciando Host para Single Player.");
        network.StartHost();
    }

    private void OnDestroy()
    {
        // Netcode hace persistente el manager. Su host local termina con esta escena.
        if (sceneNetwork != null)
            Destroy(sceneNetwork.gameObject);
    }
}
