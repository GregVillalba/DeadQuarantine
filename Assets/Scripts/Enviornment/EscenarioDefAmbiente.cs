using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// La ciudad usa un Volume local y un pase de cámara; nunca modifica RenderSettings.
public sealed class EscenarioDefAmbiente : MonoBehaviour
{
 [Header("Niebla local de la ciudad")]
 [SerializeField] private Color colorNiebla=new(.095f,.12f,.17f,1);
 [SerializeField,Min(0)] private float inicioNiebla=30;
 [SerializeField,Range(0,.05f)] private float densidadNiebla=.012f;
 [SerializeField,Range(0,1)] private float opacidadNiebla=.66f;
 [SerializeField] private BoxCollider limites;
 static readonly HashSet<EscenarioDefAmbiente> activos=new();
 sealed class EstadoCamara {public Camera camara;public bool post;public LayerMask capas;public float alcance;}
 readonly Dictionary<UniversalAdditionalCameraData,EstadoCamara> camaras=new();
 float siguienteRevision;
 public Color ColorNiebla=>colorNiebla;
 public float InicioNiebla=>inicioNiebla;
 public float DensidadNiebla=>densidadNiebla;
 public float OpacidadNiebla=>opacidadNiebla;
 public BoxCollider Limites {get {if(!limites)limites=GetComponent<BoxCollider>();return limites;}}
 void OnEnable()=>AplicarAmbiente();
 void LateUpdate(){if(Time.unscaledTime<siguienteRevision)return;siguienteRevision=Time.unscaledTime+.2f;PrepararCamaras();}
 void OnDisable()=>RestaurarAmbiente();
 public float PesoEn(Vector3 posicion){var box=Limites;var volume=GetComponent<Volume>();if(!box||!box.enabled||!isActiveAndEnabled||!gameObject.activeInHierarchy||!volume||!volume.enabled||volume.isGlobal||volume.weight<=0)return 0;var q=box.transform.InverseTransformPoint(posicion)-box.center;var fuera=Vector3.Max(Vector3.zero,new Vector3(Mathf.Abs(q.x),Mathf.Abs(q.y),Mathf.Abs(q.z))-box.size*.5f);float distancia=box.transform.TransformVector(fuera).magnitude;return volume.weight*(distancia<=0?1:volume.blendDistance>0?Mathf.Clamp01(1-distancia/volume.blendDistance):0);}
 public static EscenarioDefAmbiente ParaCamara(Camera camara){if(!camara)return null;foreach(var ambiente in activos){if(!ambiente||!ambiente.isActiveAndEnabled)continue;if(camara.scene.IsValid()&&camara.scene!=ambiente.gameObject.scene)continue;if(ambiente.PesoEn(camara.transform.position)>0)return ambiente;}return null;}
 public void AplicarAmbiente(){activos.Add(this);PrepararCamaras();}
 public void PrepararCamaras(){foreach(var camara in Camera.allCameras){if(camara.cameraType!=CameraType.Game||!camara.TryGetComponent<UniversalAdditionalCameraData>(out var datos)||datos.renderType!=CameraRenderType.Base)continue;bool dentro=PesoEn(camara.transform.position)>0&&(!camara.scene.IsValid()||camara.scene==gameObject.scene);if(dentro){if(!camaras.ContainsKey(datos))camaras[datos]=new EstadoCamara{camara=camara,post=datos.renderPostProcessing,capas=datos.volumeLayerMask,alcance=camara.farClipPlane};datos.renderPostProcessing=true;datos.volumeLayerMask|=1<<gameObject.layer;camara.farClipPlane=Mathf.Max(camara.farClipPlane,1800);}else if(camaras.TryGetValue(datos,out var estado)){Restaurar(datos,estado);camaras.Remove(datos);}}}
 static void Restaurar(UniversalAdditionalCameraData datos,EstadoCamara estado){if(!datos)return;datos.renderPostProcessing=estado.post;datos.volumeLayerMask=estado.capas;if(estado.camara)estado.camara.farClipPlane=estado.alcance;}
 public void RestaurarAmbiente(){activos.Remove(this);foreach(var estado in camaras)Restaurar(estado.Key,estado.Value);camaras.Clear();}
}
