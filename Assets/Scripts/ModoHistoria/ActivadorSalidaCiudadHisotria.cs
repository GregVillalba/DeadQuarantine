
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class ActivadorSalidaCiudadHistoria : MonoBehaviour
{
    public enum TipoTriggerHistoria
    {
        SalidaCiudad = 0,
        PuertaLaboratorio = 1
    }

    [SerializeField] private TipoTriggerHistoria tipoTrigger;

    private readonly HashSet<Collider> collidersJugadorLocal =
        new HashSet<Collider>();

    private bool activado;

    public void ConfigurarComoTriggerLaboratorio()
    {
        tipoTrigger = TipoTriggerHistoria.PuertaLaboratorio;
    }

    private void OnTriggerEnter(Collider other)
    {
        GestorHistoriaPartida gestor =
            GestorHistoriaPartida.Instance;

        if (gestor == null)
        {
            Debug.LogError(
                "[Salida Ciudad] No se encontró GestorHistoriaPartida."
            );
            return;
        }

        if (EsMultiplayerAditivo())
        {
            NetworkObject jugador =
                other.GetComponentInParent<NetworkObject>();

            if (jugador == null || !jugador.IsPlayerObject ||
                !jugador.IsOwner || !collidersJugadorLocal.Add(other))
                return;

            if (collidersJugadorLocal.Count == 1)
            {
                gestor.NotificarPresenciaEnTriggerHistoria(
                    tipoTrigger == TipoTriggerHistoria.PuertaLaboratorio,
                    true
                );
            }

            return;
        }

        if (tipoTrigger != TipoTriggerHistoria.SalidaCiudad || activado)
            return;

        Transform jugadorSinglePlayer = other.transform.root;

        if (!jugadorSinglePlayer.CompareTag("Player"))
            return;

        if (!gestor.EsperandoSalidaCiudad)
            return;

        activado = true;

        Debug.Log(
            "[Salida Ciudad] Jugador llegó al camino de salida."
        );

        gestor.IntentarCompletarSalidaCiudad();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!EsMultiplayerAditivo() ||
            !collidersJugadorLocal.Remove(other) ||
            collidersJugadorLocal.Count > 0)
        {
            return;
        }

        GestorHistoriaPartida gestor = GestorHistoriaPartida.Instance;
        if (gestor != null && gestor.IsSpawned)
        {
            gestor.NotificarPresenciaEnTriggerHistoria(
                tipoTrigger == TipoTriggerHistoria.PuertaLaboratorio,
                false
            );
        }
    }

    private static bool EsMultiplayerAditivo()
    {
        GestorEscenariosPartida gestorEscenarios =
            GestorEscenariosPartida.Instance;

        return NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsListening &&
               gestorEscenarios != null &&
               gestorEscenarios.UsaEscenasAditivasMultiplayer;
    }
}
