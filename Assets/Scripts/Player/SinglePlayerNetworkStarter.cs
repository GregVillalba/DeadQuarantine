using UnityEngine;
using Unity.Netcode;

public class SinglePlayerNetworkStarter : MonoBehaviour
{
    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError(
                "[SinglePlayerNetworkStarter] No existe un NetworkManager en la escena."
            );

            return;
        }

        if (!NetworkManager.Singleton.IsListening)
        {
            Debug.Log(
                "[SinglePlayerNetworkStarter] Iniciando Host para Single Player."
            );

            NetworkManager.Singleton.StartHost();
        }
    }
}