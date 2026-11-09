using UnityEngine;
namespace DeadQuarantine.PvPvE.Networking {
 // Authored only in the separate PvPvE map scene. Revalidated at runtime.
 public sealed class PvPvETeamSpawnPoint:MonoBehaviour {
  public PvPvETeam Team;
  public ConfiguracionPartidaSeleccionada.Escenario Map;
  void OnDrawGizmosSelected(){Gizmos.color=Team==PvPvETeam.Alpha?Color.cyan:Color.red;Gizmos.DrawWireSphere(transform.position,.4f);Gizmos.DrawRay(transform.position+Vector3.up,transform.forward*2);}
 }
}
