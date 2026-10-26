
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Collider))]
public class ActivadorMansionHistoria : MonoBehaviour
{
    private void Awake()
    {
        Collider zona = GetComponent<Collider>();
        zona.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (NetworkManager.Singleton != null &&
            !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        // Busca el objeto Player aunque el collider esté en un hijo.
        Transform jugador = other.transform;

        while (jugador != null && !jugador.CompareTag("Player"))
        {
            jugador = jugador.parent;
        }

        if (jugador == null)
            return;

        if (GestorMansionHistoria.Instance == null)
            return;

        GestorMansionHistoria.Instance.CompletarMision();
    }
}
