using UnityEngine;
using Unity.Netcode;

// Trigger que cubre toda ZonaSegura1 (desde la puerta del laboratorio hasta el tanque).
// Mientras el jugador local está adentro: los zombies dejan de seguirlo, no aparecen
// zombies nuevos y se puede apretar ENTER para continuar a la Ciudad. Al volver al
// laboratorio todo eso se revierte.
[RequireComponent(typeof(BoxCollider))]
public class EntradaZonaSegura : MonoBehaviour
{
    // El jugador puede tener varios colliders: cuenta cuántos están adentro.
    private int collidersJugadorDentro;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnDisable()
    {
        // Al pasar a la Ciudad la zona se apaga: no hay salida que avisar.
        collidersJugadorDentro = 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        NetworkObject jugador = ObtenerJugadorLocal(other);

        if (jugador == null)
            return;

        collidersJugadorDentro++;

        if (collidersJugadorDentro == 1)
            CambiarEstado(jugador, true);
    }

    private void OnTriggerExit(Collider other)
    {
        NetworkObject jugador = ObtenerJugadorLocal(other);

        if (jugador == null || collidersJugadorDentro == 0)
            return;

        collidersJugadorDentro--;

        if (collidersJugadorDentro == 0)
            CambiarEstado(jugador, false);
    }

    private static NetworkObject ObtenerJugadorLocal(Collider other)
    {
        NetworkObject jugador = other.GetComponentInParent<NetworkObject>();

        if (jugador == null || !jugador.IsPlayerObject || !jugador.IsOwner)
            return null;

        return jugador;
    }

    private static void CambiarEstado(NetworkObject jugador, bool dentro)
    {
        GestorEscenariosPartida gestorEscenarios =
            GestorEscenariosPartida.Instance;

        if (gestorEscenarios == null)
        {
            Debug.LogError(
                "[ZonaSegura] No se encontró GestorEscenariosPartida."
            );
            return;
        }

        if (dentro)
            gestorEscenarios.EntrarSafeZone1(jugador.transform);
        else
            gestorEscenarios.SalirSafeZone1(jugador.transform);

        if (GestorHistoriaPartida.Instance != null)
            GestorHistoriaPartida.Instance.NotificarZonaSegura(dentro);

        Debug.Log(
            dentro
                ? "[ZonaSegura] El jugador entró a la zona segura."
                : "[ZonaSegura] El jugador volvió al laboratorio."
        );
    }

    private void OnDrawGizmos()
    {
        BoxCollider caja = GetComponent<BoxCollider>();

        if (caja == null)
            return;

        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(caja.center, caja.size);
    }
}
