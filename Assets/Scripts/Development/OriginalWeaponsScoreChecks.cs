#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.Linq;using System.Collections;using UnityEngine;using Unity.Netcode;
namespace DeadQuarantine.Development {public sealed partial class OriginalWeaponsSmokeDriver {
 public DeadQuarantine.PvPvE.Networking.PvPvENetworkSettings LoadingSettings;
 IEnumerator ReviewOriginalScoring(){
  Check(ZombiePrefab,"Original zombie prefab supplied for authoritative scoring check");
  if(role=="host")foreach(string owner in new[]{"host","client"})foreach(string kind in new[]{"body","head"}){
   var obj=Instantiate(ZombiePrefab,new Vector3(20+(owner=="host"?0:4),0,20+(kind=="body"?0:4)),Quaternion.identity);obj.name="Legacy score "+owner+" "+kind;var agent=obj.GetComponent<UnityEngine.AI.NavMeshAgent>();if(agent)agent.enabled=false;var ai=obj.GetComponent<ZombieAI>();if(ai)ai.enabled=false;obj.GetComponent<NetworkObject>().Spawn();obj.GetComponent<ZombieHealth>().InitializeHealth(1000);Write("score-zombie-"+owner+"-"+kind,obj.GetComponent<NetworkObject>().NetworkObjectId.ToString());
  }
  yield return Until(()=>new[]{"host-body","host-head","client-body","client-head"}.All(id=>System.IO.File.Exists(PathCombine("score-zombie-"+id))&&Network.SpawnManager.SpawnedObjects.ContainsKey(ulong.Parse(System.IO.File.ReadAllText(PathCombine("score-zombie-"+id))))),"Both peers receive original server-owned scoring zombies");
  var score=Local.GetComponent<PlayerScore>();int before=score.ScoreNetwork.Value;
  var body=Network.SpawnManager.SpawnedObjects[ulong.Parse(System.IO.File.ReadAllText(PathCombine("score-zombie-"+role+"-body")))].GetComponent<ZombieHealth>();
  body.TakeDamage(1,body.transform.position+Vector3.up,Vector3.up);yield return Until(()=>score.ScoreNetwork.Value==before+50,"Original confirmed hit grants exactly 50 on its owner");
  body.TakeDamage(99999,body.transform.position+Vector3.up,Vector3.up);yield return Until(()=>score.ScoreNetwork.Value==before+600,"Original body elimination is 50 hit plus 500 kill");
  var head=Network.SpawnManager.SpawnedObjects[ulong.Parse(System.IO.File.ReadAllText(PathCombine("score-zombie-"+role+"-head")))].GetComponent<ZombieHealth>();int health=head.CurrentHealth;
  head.TakeDamage(1,head.transform.position+Vector3.up*1.6f,Vector3.up,true,true);yield return Until(()=>score.ScoreNetwork.Value==before+700&&head.CurrentHealth==health-2,"Original head impact doubles damage and adds exactly 50 bonus");
  head.TakeDamage(99999,head.transform.position+Vector3.up*1.6f,Vector3.up,true,true);yield return Until(()=>score.ScoreNetwork.Value==before+1300,"Original head elimination grants exactly 600, with no longshot bonus");
  var feedback=score.GetComponent<DeadQuarantine.PvPvE.UI.PvPvEKillFeedback>();Check(feedback&&feedback.Panel&&feedback.Panel.activeInHierarchy,"Original modes reuse visible confirmed score feedback");Check(feedback.Message.Contains("ELIMINACIÓN"),"Original feedback reaches the correct local HUD");
  var marker=feedback.Panel.transform.Find("Headshot").GetComponent<UnityEngine.UI.Image>();Check(marker.gameObject.activeSelf&&marker.sprite==Resources.Load<Sprite>("PvPvE/Headshot"),"Original headkill uses supplied white headshot icon");
  Write(role+"-points-complete","ok");yield return Until(()=>System.IO.File.Exists(PathCombine("host-points-complete"))&&System.IO.File.Exists(PathCombine("client-points-complete")),"Both original scoring flows complete before UI checks");
  yield return ReviewOriginalLoading();
  Write(role+"-score-checked","ok");yield return Until(()=>System.IO.File.Exists(PathCombine("host-score-checked"))&&System.IO.File.Exists(PathCombine("client-score-checked")),"Both original scoring owners verified");
 }
 IEnumerator CaptureOriginalLoading(DeadQuarantine.PvPvE.PvPvELoadingScreen screen,string file){
  var canvas=screen.Panel.GetComponent<Canvas>();var previous=canvas.renderMode;var previousCamera=canvas.worldCamera;float previousPlane=canvas.planeDistance;
  var go=new GameObject("Original loading render check");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(60000,-60000,0);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.cullingMask=~0;go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing=false;
  var target=new RenderTexture(1280,720,24);var old=RenderTexture.active;Texture2D image=null;
  try{canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();int visible=image.GetPixels32().Count(p=>p.r>20||p.g>20||p.b>20);Check(visible>1280*720/10,"Actual loading pixels and text rendered: "+file+" pixels="+visible);System.IO.File.WriteAllBytes(PathCombine(file),image.EncodeToPNG());}
  finally{canvas.renderMode=previous;canvas.worldCamera=previousCamera;canvas.planeDistance=previousPlane;RenderTexture.active=old;if(image)Destroy(image);target.Release();Destroy(target);Destroy(go);}
 }
 IEnumerator ReviewOriginalLoading(){
  var cfg=ConfiguracionPartidaSeleccionada.Instancia;Check(cfg&&LoadingSettings,"Original loading reuses current configuration and map settings");var mode=cfg.modoSeleccionado;var map=cfg.escenarioSeleccionado;
  DeadQuarantine.PvPvE.PvPvELoadingScreen.ConfigureOriginal(LoadingSettings);
  foreach(var selectedMode in new[]{ConfiguracionPartidaSeleccionada.ModoJuego.Historia,ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5,ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia})foreach(var selectedMap in LoadingSettings.Maps){
   cfg.modoSeleccionado=selectedMode;cfg.escenarioSeleccionado=selectedMap.Id;DeadQuarantine.PvPvE.PvPvELoadingScreen.BeginOriginal("Loading visual validation");DeadQuarantine.PvPvE.PvPvELoadingScreen.OriginalProgress(.5f);yield return null;yield return new WaitForEndOfFrame();
   var screen=FindFirstObjectByType<DeadQuarantine.PvPvE.PvPvELoadingScreen>();Check(screen&&screen.IsShowing&&screen.MapText==selectedMap.Name&&screen.Preview&&screen.StatusText.Contains("50%"),"Original loading renders actual map/mode/progress: "+selectedMode+" / "+selectedMap.Name+" screen="+(screen?screen.IsShowing:false)+" title="+(screen?screen.MapText:"missing")+" image="+(screen&&screen.Preview)+" status="+(screen?screen.StatusText:"missing"));yield return CaptureOriginalLoading(screen,role+"-loading-"+selectedMode+"-"+selectedMap.Id+".png");DeadQuarantine.PvPvE.PvPvELoadingScreen.CancelOriginal();yield return null;
  }
  cfg.modoSeleccionado=mode;cfg.escenarioSeleccionado=map;
  var cameras=Local.GetComponentsInChildren<Camera>(true);var enabled=cameras.Select(c=>c.enabled).ToArray();foreach(var c in cameras)c.enabled=false;
  var intro=new GameObject("Original intro loading validation",typeof(Camera));intro.GetComponent<Camera>().cullingMask=0;
  DeadQuarantine.PvPvE.PvPvELoadingScreen.BeginOriginal(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);yield return null;yield return null;
  Check(!FindFirstObjectByType<DeadQuarantine.PvPvE.PvPvELoadingScreen>(),"Loading yields to initialized story/cinematic even with FP camera disabled");
  for(int i=0;i<cameras.Length;i++)cameras[i].enabled=enabled[i];Destroy(intro);

 }
}}
#endif


