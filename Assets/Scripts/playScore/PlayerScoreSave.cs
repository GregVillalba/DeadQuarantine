
using Unity.Netcode;
using UnityEngine;

public partial class PlayerScore
{
   
/// <summary>
/// Guarda los puntos y las estadísticas del jugador.
/// Debe ejecutarse desde el servidor.
/// </summary>
public void GuardarDatosPuntuacion(DatosJugadorSaveData datos)
{
    if (!IsServer || !IsSpawned || datos == null)
        return;

    datos.puntuacion = ScoreNetwork.Value;
    datos.zombiesEliminados = ZombiesEliminadosNetwork.Value;
    datos.disparosRealizados = DisparosRealizadosNetwork.Value;
    datos.disparosAcertados = DisparosAcertadosNetwork.Value;

    datos.reapariciones = ReaparicionesNetwork.Value;
    datos.caidas = CaidasNetwork.Value;
    datos.headshotsRealizados = HeadshotsRealizadosNetwork.Value;

    Debug.Log(
        "[PlayerScore] Puntuación guardada. " +
        "Puntos: " + datos.puntuacion +
        " | Zombies: " + datos.zombiesEliminados +
        " | Disparos: " + datos.disparosRealizados +
        " | Impactos: " + datos.disparosAcertados +
        " | Headshots: " + datos.headshotsRealizados
    );
}

/// <summary>
/// Restaura los puntos y las estadísticas desde el guardado.
/// Debe ejecutarse desde el servidor, después de OnNetworkSpawn.
/// </summary>
public void RestaurarDatosPuntuacion(DatosJugadorSaveData datos)
{
    if (!IsServer || !IsSpawned || datos == null)
        return;

    ScoreNetwork.Value = Mathf.Max(0, datos.puntuacion);
    ZombiesEliminadosNetwork.Value = Mathf.Max(0, datos.zombiesEliminados);
    DisparosRealizadosNetwork.Value = Mathf.Max(0, datos.disparosRealizados);
    DisparosAcertadosNetwork.Value = Mathf.Max(0, datos.disparosAcertados);

    ReaparicionesNetwork.Value = Mathf.Max(0, datos.reapariciones);
    CaidasNetwork.Value = Mathf.Max(0, datos.caidas);
    HeadshotsRealizadosNetwork.Value = Mathf.Max(0, datos.headshotsRealizados);

    Debug.Log(
        "[PlayerScore] Puntuación restaurada. " +
        "Puntos: " + ScoreNetwork.Value +
        " | Zombies: " + ZombiesEliminadosNetwork.Value +
        " | Disparos: " + DisparosRealizadosNetwork.Value +
        " | Impactos: " + DisparosAcertadosNetwork.Value +
        " | Headshots: " + HeadshotsRealizadosNetwork.Value
    );
}

}
