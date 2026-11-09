using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DeadQuarantine.PvPvE.Networking
{
 // The mode's separate map scene reuses the three original scenario hierarchies.
 [DefaultExecutionOrder(-10000)]
 public sealed class PvPvEMapContext:MonoBehaviour {
  public static PvPvEMapContext Active {get;private set;}
  public ConfiguracionEscenarioJugable Scenario=>GestorEscenariosPartida.Instance?GestorEscenariosPartida.Instance.ObtenerEscenarioActivo():null;
  void Awake(){
   Active=this;var roster=PvPvETeamRoster.Active;var voting=roster?roster.GetComponent<PvPvEMapVoting>():null;
   var flow=roster?roster.GetComponent<PvPvEPreparationFlow>():null;
   var map=flow&&flow.Map.Value!=ConfiguracionPartidaSeleccionada.Escenario.Ninguno?flow.Map.Value:voting?.SelectedMap?.Id??ConfiguracionPartidaSeleccionada.Escenario.Ninguno;
   if(!ConfiguracionPartidaSeleccionada.Instancia)new GameObject("PvPvE scenario selection").AddComponent<ConfiguracionPartidaSeleccionada>();
   ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado=map;ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado=ConfiguracionPartidaSeleccionada.ModoJuego.Ninguno;
  }
  void Start(){SceneManager.SetActiveScene(gameObject.scene);}
  void OnDestroy(){if(Active==this)Active=null;}
  public static async Task UnloadAfterLeaveAsync(string sceneName){
   var scene=SceneManager.GetSceneByName(sceneName);if(!scene.IsValid()||!scene.isLoaded)return;
   var unload=SceneManager.UnloadSceneAsync(scene);if(unload==null)return;while(!unload.isDone)await Task.Yield();
  }
 }
}
