#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DeadQuarantine.PvPvE.UI;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class PvPvEMenuPhase2Preview
{
 public static void Render()
 {
  var scene=EditorSceneManager.OpenScene(PvPvEMenuPhase2Tools.ScenePath);
  var source=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PvPvEModeMenu>(true)).Single();
  string output=Environment.GetEnvironmentVariable("DQ_PVPVE_PHASE2_OUTPUT")??Path.GetFullPath("Temp/PvPvEPhase2");Directory.CreateDirectory(output);
  var camera=new GameObject("Phase2 Preview Camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-10000);camera.orthographic=true;camera.orthographicSize=5;camera.nearClipPlane=.1f;camera.farClipPlane=5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.025f,.025f);camera.cullingMask=1<<31;camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
  var canvas=new GameObject("Phase2 Preview Canvas",typeof(RectTransform)).AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
  var scaler=canvas.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
  var menu=UnityEngine.Object.Instantiate(source,canvas.transform,false);
  foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
  try {
   foreach(var size in new[]{new Vector2Int(1600,900),new Vector2Int(1280,720)})foreach(bool pvpve in new[]{false,true}) {
    menu.OpenModes();if(pvpve)menu.OpenPvPvE();
    var rt=new RenderTexture(size.x,size.y,24);var active=RenderTexture.active;
    try {
     camera.targetTexture=rt;Canvas.ForceUpdateCanvases();
     foreach(var text in menu.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();if(text.isTextOverflowing)throw new Exception("Text overflow: "+text.name+" at "+size);}
     camera.Render();RenderTexture.active=rt;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,(pvpve?"PvPvE":"Modalidades")+"-"+size.x+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
    }finally{camera.targetTexture=null;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
   }
  }finally{UnityEngine.Object.DestroyImmediate(canvas.gameObject);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
 }
}
#endif
